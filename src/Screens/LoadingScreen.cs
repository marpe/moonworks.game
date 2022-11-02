using System.Threading;
using MyGame.Graphics;
using MyGame.TWConsole;

namespace MyGame.Screens;

public enum TransitionState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden,
}

public class DiamondTransition
{
    public struct Uniforms
    {
        public float Progress;
        public float DiamondPixelSize;
    }
    
    public readonly GraphicsPipeline Pipeline;
    public Uniforms Uniform = new() { Progress = 0, DiamondPixelSize = 36 };

    public DiamondTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.DiamondTransition.Diamond_transitionVertSpv));
        var fragmentShader = new ShaderModule(device,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.DiamondTransition.Diamond_transitionFragSpv));

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 1);

        var myDepthStencilState = new DepthStencilState
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            CompareOp = CompareOp.GreaterOrEqual,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false
        };

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                TextureFormat.D16,
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = myDepthStencilState,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = Renderer.GetVertexInputState(),
        };

        Pipeline = new GraphicsPipeline(
            device,
            myGraphicsPipelineCreateInfo
        );
    }

    public void Draw(Renderer renderer, float progress)
    {
        var commandBuffer = renderer.CommandBuffer;
        var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.DrawRect(new Rectangle(0, 0, (int)swap.Width, (int)swap.Height), Color.Black, 1f);
        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(renderer.DepthStencilAttachmentInfo.Texture, LoadOp.Load),
            new ColorAttachmentInfo(swap, LoadOp.Load));
        commandBuffer.BindGraphicsPipeline(Pipeline);
        Uniform.Progress = progress;
        commandBuffer.PushFragmentShaderUniforms(Uniform);
        renderer.SpriteBatch.Flush(commandBuffer, viewProjection);
        commandBuffer.EndRenderPass();
    }
}

public class LoadingScreen
{
    private TransitionState _state = TransitionState.Hidden;
    public TransitionState State => _state;

    private readonly Sprite _backgroundSprite;
    private readonly Sprite _blankSprite;

    private MyGameMain _game;

    private Texture? _copyRender;
    private bool _shouldCopyRender;

    private Action? _callback;

    private float _progress = 0;
    private readonly DiamondTransition _diamondTransition;
    private float _transitionSpeed = 2.0f;

    [ConsoleHandler("load", "Load a level")]
    public static void TestLoad()
    {
        Shared.Game.LoadingScreen.StartLoad(() => { Thread.Sleep(1000); });
    }

    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        /*var asepritePath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = TextureUtils.LoadAseprite(game.GraphicsDevice, asepritePath);
        var backgroundSprite = new Sprite(asepriteTexture);*/

        var backgroundTexture = TextureUtils.LoadPngTexture(game.GraphicsDevice,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
        var blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _backgroundSprite = new Sprite(backgroundTexture);
        _blankSprite = new Sprite(blankTexture);
        _diamondTransition = new DiamondTransition(game.GraphicsDevice);
    }

    public void StartLoad(Action loadMethod)
    {
        if (_state != TransitionState.Hidden)
        {
            Logger.LogError("Loading is already in progress");
            return;
        }

        _shouldCopyRender = true;
        _state = TransitionState.TransitionOn;
        _callback = loadMethod;
    }

    public void Update(float deltaSeconds)
    {
        if (_state == TransitionState.TransitionOn)
        {
            _progress += _transitionSpeed * deltaSeconds;

            if (_progress >= 1.0f)
            {
                _progress = 1.0f;
                _state = TransitionState.Active;

                Task.Run(() =>
                {
                    _callback?.Invoke();
                    _state = TransitionState.TransitionOff;
                });
            }
        }
        else if (_state == TransitionState.TransitionOff)
        {
            _progress -= _transitionSpeed * deltaSeconds;
            if (_progress <= 0)
            {
                _progress = 0;
                _state = TransitionState.Hidden;

                _copyRender = null;
                _callback = null;
            }
        }
    }

    public void Draw(Renderer renderer)
    {
        // if (_state == TransitionState.Hidden)
        // return;

        var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        
        if (_shouldCopyRender)
        {
            Logger.LogInfo($"Copying render...");
            renderer.FlushBatches();
            _copyRender = TextureUtils.CreateTexture(_game.GraphicsDevice, renderer.SwapTexture);
            renderer.CommandBuffer.CopyTextureToTexture(renderer.SwapTexture, _copyRender, Filter.Nearest);
            _shouldCopyRender = false;
        }

        if (_copyRender != null && (_state is TransitionState.TransitionOn or TransitionState.Active))
        {
            renderer.DrawSprite(new Sprite(_copyRender), Matrix3x2.Identity, Color.White, 0);
        }
        
        renderer.FlushBatches();
        _diamondTransition.Draw(renderer, _progress);
        
        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);
        var windowSize = _game.MainWindow.Size;

        var textSize = renderer.TextBatcher.MeasureString(FontType.RobotoMedium, loadingStr);
        var position = new Vector2(windowSize.X, windowSize.Y) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, Color.White * _progress);
    }
}
