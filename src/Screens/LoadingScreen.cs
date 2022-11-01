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

public class LoadingScreen
{
    private TransitionState _state = TransitionState.Hidden;
    private TransitionState _prevState = TransitionState.Hidden;
    private readonly Sprite _backgroundSprite;
    private MyGameMain _game;

    private Texture? _copyRender;
    private bool _shouldCopyRender;
    
    private Action? _callback;

    private float _progress = 0;

    [ConsoleHandler("load", "Load a level")]
    public static void TestLoad()
    {
        Shared.Game.LoadingScreen.StartLoad(() => { Thread.Sleep(5000); });
    }

    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        /*var asepritePath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = TextureUtils.LoadAseprite(game.GraphicsDevice, asepritePath);
        var backgroundSprite = new Sprite(asepriteTexture);*/

        var backgroundTexture = TextureUtils.LoadPngTexture(game.GraphicsDevice,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
        _backgroundSprite = new Sprite(backgroundTexture);
    }

    private void StartLoad(Action loadMethod)
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
        if (_state != _prevState)
        {
            Logger.LogInfo($"State: {_state}");
        }
        _prevState = _state;
        
        if (_state == TransitionState.TransitionOn)
        {
            _progress += 1.0f; // 0.5f * deltaSeconds;

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
            _progress -= 0.5f * deltaSeconds;
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
        if (_state == TransitionState.Hidden)
            return;

        if (_shouldCopyRender)
        {
            var swapTexture = renderer.SwapTexture;
            renderer.EndFrame();
            _game.GraphicsDevice.Wait();

            _copyRender = TextureUtils.CreateTexture(_game.GraphicsDevice, swapTexture);
            var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
            commandBuffer.CopyTextureToTexture(swapTexture, _copyRender, Filter.Nearest);
            _game.GraphicsDevice.Submit(commandBuffer);
            _game.GraphicsDevice.Wait();

            renderer.BeginFrame();
            _shouldCopyRender = false;
        }
        
        if (_copyRender != null)
            renderer.DrawSprite(new Sprite(_copyRender), Matrix3x2.Identity, Color.White * _progress, 0);

        // renderer.DrawSprite(_backgroundSprite, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(0, 0), Color.White, 0);
        var windowSize = _game.MainWindow.Size;
        renderer.DrawText(FontType.Roboto, "Loading...", new Vector2(windowSize.X * 0.5f, windowSize.Y * 0.5f), Color.White * _progress);

        var viewProjection = SpriteBatch.GetViewProjection(0, 0, (uint)windowSize.X, (uint)windowSize.Y);
        renderer.FlushBatches(renderer.SwapTexture, viewProjection);
    }
}
