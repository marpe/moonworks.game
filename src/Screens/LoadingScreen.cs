using System.Threading;

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
    public TransitionState State { get; private set; } = TransitionState.Hidden;
    public bool IsLoading => State == TransitionState.TransitionOn || State == TransitionState.Active;

    private readonly Sprite _backgroundSprite;
    private readonly Sprite _blankSprite;

    private Action? _loadMethod;

    private Texture? _copyRender;
    private readonly SceneTransition _diamondTransition;

    private readonly MyGameMain _game;

    private float _alpha = 0;
    private readonly SceneTransition _sceneTransition;
    private bool _shouldCopyRender;
    private readonly float _transitionSpeed = 2.0f;

    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        var backgroundTexture = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
        var blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _backgroundSprite = new Sprite(backgroundTexture);
        _blankSprite = new Sprite(blankTexture);
        _diamondTransition = new DiamondTransition(game.GraphicsDevice);
        _sceneTransition = _diamondTransition;
    }


    [ConsoleHandler("test_load", "Test loading screen")]
    public static void TestLoad()
    {
        Shared.Game.LoadingScreen.StartLoad(() => { Thread.Sleep(1000); });
    }

    public void StartLoad(Action loadMethod)
    {
        if (IsLoading)
        {
            Logger.LogError("Loading is already in progress");
            return;
        }

        _shouldCopyRender = true;
        State = TransitionState.TransitionOn;
        _loadMethod = loadMethod;
    }

    public void LoadImmediate(Action loadMethod)
    {
        if (IsLoading)
        {
            Logger.LogError("Loading is already in progress");
            return;
        }

        SetActive(loadMethod);
    }

    private void SetActive(Action? loadMethod)
    {
        _alpha = 1.0f;
        State = TransitionState.Active;
        Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            loadMethod?.Invoke();
            State = TransitionState.TransitionOff;
            Logger.LogInfo($"Loading finished in {sw.ElapsedMilliseconds} ms");
        });
    }
    
    public void Update(float deltaSeconds)
    {
        if (State == TransitionState.TransitionOn)
        {
            _alpha += _transitionSpeed * deltaSeconds;

            if (_alpha >= 1.0f)
            {
                SetActive(_loadMethod);
            }
        }
        else if (State == TransitionState.TransitionOff)
        {
            _alpha -= _transitionSpeed * deltaSeconds;
            if (_alpha <= 0)
            {
                _alpha = 0;
                State = TransitionState.Hidden;
                _loadMethod = null;
            }
        }
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        if (State == TransitionState.Hidden)
            return;

        if (_shouldCopyRender)
        {
            Logger.LogInfo("Copying render...");
            renderer.FlushBatches(renderDestination);
            if (_copyRender == null)
                _copyRender = TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
            else
                TextureUtils.EnsureTextureSize(ref _copyRender, _game.GraphicsDevice, renderDestination.Width, renderDestination.Height);
            renderer.CommandBuffer.CopyTextureToTexture(renderDestination, _copyRender, Filter.Nearest);
            _shouldCopyRender = false;
        }

        if (_copyRender != null && IsLoading)
        {
            renderer.DrawSprite(new Sprite(_copyRender), Matrix3x2.Identity, Color.White, 0);
        }

        renderer.FlushBatches(renderDestination);
        _sceneTransition.Draw(renderer, renderDestination, _alpha);

        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.Time.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);

        var textSize = renderer.TextBatcher.GetFont(FontType.RobotoLarge).MeasureString(loadingStr);
        var position = new Vector2(renderDestination.Width, renderDestination.Height) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, 0, Color.White * _alpha);
    }

    public void Unload()
    {
        _copyRender?.Dispose();
        _sceneTransition.Unload();
    }
}
