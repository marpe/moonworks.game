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
    private readonly Sprite _backgroundSprite;
    private readonly Sprite _blankSprite;

    private Action? _callback;

    private Texture? _copyRender;
    private readonly SceneTransition _diamondTransition;

    private readonly MyGameMain _game;
    private float _previousProgress;

    private float _progress = 0;
    private readonly SceneTransition _sceneTransition;
    private bool _shouldCopyRender;
    private readonly float _transitionSpeed = 2.0f;

    public bool IsLoading => State == TransitionState.TransitionOn || State == TransitionState.Active;

    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        /*var asepritePath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = TextureUtils.LoadAseprite(game.GraphicsDevice, asepritePath);
        var backgroundSprite = new Sprite(asepriteTexture);*/

        var backgroundTexture = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
        var blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _backgroundSprite = new Sprite(backgroundTexture);
        _blankSprite = new Sprite(blankTexture);
        _diamondTransition = new DiamondTransition(game.GraphicsDevice);
        _sceneTransition = _diamondTransition;
    }

    public TransitionState State { get; private set; } = TransitionState.Hidden;

    [ConsoleHandler("load", "Load a level")]
    public static void TestLoad()
    {
        Shared.Game.LoadingScreen.StartLoad(() => { Thread.Sleep(1000); });
    }

    public void StartLoad(Action loadMethod)
    {
        if (State != TransitionState.Hidden)
        {
            Logger.LogError("Loading is already in progress");
            return;
        }

        _shouldCopyRender = true;
        State = TransitionState.TransitionOn;
        _callback = loadMethod;
    }

    public void Update(float deltaSeconds)
    {
        _previousProgress = _progress;
        if (State == TransitionState.TransitionOn)
        {
            _progress += _transitionSpeed * deltaSeconds;

            if (_progress >= 1.0f)
            {
                _progress = 1.0f;
                State = TransitionState.Active;

                Task.Run(() =>
                {
                    _callback?.Invoke();
                    State = TransitionState.TransitionOff;
                });
            }
        }
        else if (State == TransitionState.TransitionOff)
        {
            _progress -= _transitionSpeed * deltaSeconds;
            if (_progress <= 0)
            {
                _progress = 0;
                State = TransitionState.Hidden;

                _copyRender = null;
                _callback = null;
            }
        }
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        if (State == TransitionState.Hidden)
            return;

        var viewProjection = SpriteBatch.GetViewProjection(0, 0, renderDestination.Width, renderDestination.Height);

        if (_shouldCopyRender)
        {
            Logger.LogInfo("Copying render...");
            renderer.FlushBatches(renderDestination);
            _copyRender?.Dispose();
            _copyRender = TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
            renderer.CommandBuffer.CopyTextureToTexture(renderDestination, _copyRender, Filter.Nearest);
            _shouldCopyRender = false;
        }

        if (_copyRender != null && State is TransitionState.TransitionOn or TransitionState.Active)
        {
            renderer.DrawSprite(new Sprite(_copyRender), Matrix3x2.Identity, Color.White, 0);
        }

        renderer.FlushBatches(renderDestination);
        _sceneTransition.Draw(renderer, renderDestination, _progress);

        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);
        var windowSize = _game.MainWindow.Size;

        var textSize = renderer.TextBatcher.GetFont(FontType.RobotoLarge).MeasureString(loadingStr);
        var position = new Vector2(windowSize.X, windowSize.Y) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, 0, Color.White * MathHelper.Lerp(_previousProgress, _progress, (float)alpha));
    }

    public void Unload()
    {
        _copyRender?.Dispose();
        _sceneTransition.Unload();
    }
}
