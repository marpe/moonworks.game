using MoonWorks.Graphics.Font;
using MyGame.Utils;

namespace MyGame.Graphics;

public enum BlendState
{
    Additive,
    AlphaBlend,
    NonPremultiplied,
    Opaque,
    None,
    Disable,
    Custom
}

public class Renderer
{
    public static Sampler PointClamp = null!;

    public readonly SpriteBatch SpriteBatch;
    public readonly TextBatcher TextBatcher;

    public BlendState BlendState = BlendState.AlphaBlend;
    
    public DepthStencilAttachmentInfo DepthStencilAttachmentInfo;
    public ColorAttachmentInfo ColorAttachmentInfo;
    public Texture DepthTexture;
    
    private readonly MyGameMain _game;
    private readonly GraphicsDevice _device;
    private readonly Texture _blankTexture;
    private readonly Sprite _blankSprite;
    private readonly GraphicsPipeline[] _pipelines;

    private CommandBuffer? _commandBuffer;

    public CommandBuffer CommandBuffer =>
        _commandBuffer ?? throw new InvalidOperationException("CommandBuffer is null, did you forget to call BeginFrame?");

    private Texture? _swapTexture;

    public Texture SwapTexture =>
        _swapTexture ?? throw new InvalidOperationException("SwapTexture is null, did you forget to call BeginFrame?");

    public ColorAttachmentBlendState CustomBlendState = new ColorAttachmentBlendState
    {
        BlendEnable = true,
        AlphaBlendOp = BlendOp.Add,
        ColorBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RGBA,
        SourceColorBlendFactor = BlendFactor.One,
        SourceAlphaBlendFactor = BlendFactor.SourceAlpha,
        DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
        DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha
    };

    private readonly BMFont _bmFont;

    public Renderer(MyGameMain game)
    {
        _game = game;
        _device = game.GraphicsDevice;
        PointClamp = new Sampler(_device, SamplerCreateInfo.PointClamp);
        SpriteBatch = new SpriteBatch(_device);
        TextBatcher = new TextBatcher(_device);

        _blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _blankSprite = new Sprite(_blankTexture);

        var blendStates = Enum.GetValues<BlendState>();
        _pipelines = new GraphicsPipeline[blendStates.Length];
        for (var i = 0; i < blendStates.Length; i++)
        {
            var blendState = blendStates[i] switch
            {
                BlendState.Additive => ColorAttachmentBlendState.Additive,
                BlendState.AlphaBlend => ColorAttachmentBlendState.AlphaBlend,
                BlendState.NonPremultiplied => ColorAttachmentBlendState.NonPremultiplied,
                BlendState.Opaque => ColorAttachmentBlendState.Opaque,
                BlendState.None => ColorAttachmentBlendState.None,
                BlendState.Disable => ColorAttachmentBlendState.Disable,
                BlendState.Custom => CustomBlendState,
                _ => throw new ArgumentOutOfRangeException()
            };
            _pipelines[i] = CreateGraphicsPipeline(_device, blendState);
        }

        _bmFont = new BMFont(game.GraphicsDevice, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Bmfonts.ConsolasFnt));
        
        DepthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        DepthStencilAttachmentInfo = new DepthStencilAttachmentInfo()
        {
            DepthStencilClearValue = new DepthStencilValue(0, 0),
            Texture = DepthTexture,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store
        };
        ColorAttachmentInfo = new ColorAttachmentInfo()
        {
            ClearColor = Color.CornflowerBlue,
            LoadOp = LoadOp.Clear,
        };
    }

    public void UpdateCustomBlendPipeline()
    {
        _pipelines[(int)BlendState.Custom] = CreateGraphicsPipeline(_device, CustomBlendState);
    }

    public bool BeginFrame()
    {
        _commandBuffer = _device.AcquireCommandBuffer();
        _swapTexture = _commandBuffer?.AcquireSwapchainTexture(_game.MainWindow);
        if (_swapTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return false;
        }

        var windowSize = _game.MainWindow.Size;
        TextureUtils.EnsureTextureSize(ref DepthTexture, _device, (uint)windowSize.X, (uint)windowSize.Y);
        DepthStencilAttachmentInfo.Texture = DepthTexture;

        return true;
    }

    public void DrawRect(Rectangle rect, Color color, float depth = 0)
    {
        var scale = Matrix3x2.CreateScale(rect.Width, rect.Height) * Matrix3x2.CreateTranslation(rect.X, rect.Y);
        SpriteBatch.Draw(_blankSprite, color, depth, scale, PointClamp);
    }

    public void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var offset = from - to;
        var length = offset.Length();
        var scale = Matrix3x2.CreateScale(length, 1f, new Vector2(0, 0.5f));
        var rotationRad = MathF.AngleBetweenVectors(from, to);
        var rotation = Matrix3x2.CreateRotation(rotationRad, new Vector2(0, 0.5f));
        var translation = Matrix3x2.CreateTranslation(from);
        SpriteBatch.Draw(_blankSprite, color, 0, scale * rotation * translation, PointClamp);
    }

    public void DrawLine(Point from, Point to, Color color)
    {
        DrawLine(from.ToVec2(), to.ToVec2(), color);
    }

    public void DrawSprite(Sprite sprite, Matrix3x2 transform, Color color, float depth)
    {
        SpriteBatch.Draw(sprite, color, depth, transform, PointClamp);
    }

    public void DrawText(FontType fontType, ReadOnlySpan<char> text, float x, float y, float depth, Color color,
        HorizontalAlignment alignH = HorizontalAlignment.Left, VerticalAlignment alignV = VerticalAlignment.Top)
    {
        TextBatcher.Add(fontType, text, x, y, depth, color, alignH, alignV);
    }

    public void DrawText(ReadOnlySpan<char> text, Vector2 pos, float depth, Color color)
    {
        DrawText(FontType.ConsolasMono, text, pos.X, pos.Y, depth, color);
    }

    public void DrawText(ReadOnlySpan<char> text, Vector2 pos, Color color)
    {
        DrawText(text, pos, 0, color);
    }

    public void DrawText(FontType fontType, ReadOnlySpan<char> text, Vector2 pos, Color color)
    {
        DrawText(fontType, text, pos.X, pos.Y, 0, color);
    }

    public void DrawBMText(ReadOnlySpan<char> text, Vector2 position, float depth, Color color)
    {
        BMFont.DrawInto(this, _bmFont, text, position, color, 0, Vector2.Zero, Vector2.One, depth);
    }

    public void FlushBatches(Matrix4x4 viewProjection, Color clearColor, bool clear = true)
    {
        FlushBatches(SwapTexture, viewProjection, clearColor, clear);
    }

    public void FlushBatches(Texture renderTarget, Matrix4x4 viewProjection, Color clearColor, bool clear = true)
    {
        var commandBuffer = CommandBuffer;

        ColorAttachmentInfo.Texture = renderTarget;
        ColorAttachmentInfo.ClearColor = clearColor;
        ColorAttachmentInfo.LoadOp = clear ? LoadOp.Clear : LoadOp.Load;

        /*TextBatcher.FlushToSpriteBatch(SpriteBatch);*/

        SpriteBatch.UpdateBuffers(commandBuffer);
        TextBatcher.UpdateBuffers(commandBuffer);

        commandBuffer.BeginRenderPass(DepthStencilAttachmentInfo, ColorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(_pipelines[(int)BlendState]);
        SpriteBatch.Flush(commandBuffer, viewProjection);
        TextBatcher.Flush(commandBuffer, viewProjection);
        commandBuffer.EndRenderPass();
    }

    public void EndFrame()
    {
        _device.Submit(CommandBuffer);
        _commandBuffer = null;
        _swapTexture?.Dispose();
        _swapTexture = null;
    }

    public void Unload()
    {
        _bmFont.Dispose();

        for (var i = 0; i < _pipelines.Length; i++)
        {
            _pipelines[i].Dispose();
        }

        _blankTexture.Dispose();
        TextBatcher.Unload();
        SpriteBatch.Unload();
        PointClamp.Dispose();
        
        DepthTexture.Dispose();
        DepthStencilAttachmentInfo.Texture.Dispose();
        ColorAttachmentInfo.Texture.Dispose();
    }

    public static GraphicsPipeline CreateGraphicsPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var spriteVertexShader = new ShaderModule(device, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.Sg2_spriteVertSpv));
        var spriteFragmentShader = new ShaderModule(device, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.SpriteFragSpv));

        var myVertexBindings = new VertexBinding[]
        {
            VertexBinding.Create<Position3DTextureColorVertex>()
        };

        var myVertexAttributes = new VertexAttribute[]
        {
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Position), 0),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Color), 2),
        };

        var myVertexInputState = new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes
        };

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
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
            ),
            DepthStencilState = myDepthStencilState,
            VertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0),
            FragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1),
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = myVertexInputState,
        };

        return new GraphicsPipeline(
            device,
            myGraphicsPipelineCreateInfo
        );
    }
}
