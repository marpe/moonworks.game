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
    public TransitionState State => _state;
    private TransitionState _prevState = TransitionState.Hidden;

    private readonly Sprite _backgroundSprite;
    private readonly Sprite _blankSprite;

    private MyGameMain _game;

    private Texture? _copyRender;
    private bool _shouldCopyRender;

    private Action? _callback;

    private float _progress = 0;

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
        if (_state != _prevState)
        {
            // Logger.LogInfo($"State: {_state}");
        }

        _prevState = _state;

        if (_state == TransitionState.TransitionOn)
        {
            _progress += 5f * deltaSeconds;

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
            _progress -= 5f * deltaSeconds;
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
            _copyRender = TextureUtils.CreateTexture(_game.GraphicsDevice, renderer.SwapTexture);
            renderer.CommandBuffer.CopyTextureToTexture(renderer.SwapTexture, _copyRender, Filter.Nearest);
            _shouldCopyRender = false;
        }

        if (_copyRender != null)
        {
            Color color;
            if (_state == TransitionState.TransitionOn || _state == TransitionState.Active)
                color = Color.Lerp(Color.White, Color.Black, _progress);
            else
                color = Color.Lerp(Color.Transparent, Color.Black, _progress);
            renderer.DrawSprite(new Sprite(_copyRender), Matrix3x2.Identity, color, 0);
        }

        // renderer.DrawSprite(_backgroundSprite, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(0, 0), Color.White, 0);
        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);
        var windowSize = _game.MainWindow.Size;
        // var center = new Vector2(windowSize.X * 0.5f, windowSize.Y * 0.5f);

        var textSize = renderer.TextBatcher.MeasureString(FontType.RobotoMedium, loadingStr);
        var position = new Vector2(windowSize.X, windowSize.Y) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, Color.White * _progress);

        var viewProjection = SpriteBatch.GetViewProjection(0, 0, (uint)windowSize.X, (uint)windowSize.Y);
        renderer.FlushBatches(renderer.SwapTexture, viewProjection);
    }
}
