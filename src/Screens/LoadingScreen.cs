using System.Collections.Concurrent;
using System.Threading;
using MyGame.Screens.Transitions;

namespace MyGame.Screens;

public enum TransitionState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden,
}

public enum TransitionType
{
    Diamonds,
    Pixelize,
    FadeToBlack,
    CircleCrop
}

public class LoadingScreen
{
    public static readonly Dictionary<TransitionType, SceneTransition> SceneTransitions = new();
    public TransitionState State { get; private set; } = TransitionState.Hidden;
    public bool IsLoading => State == TransitionState.TransitionOn || State == TransitionState.Active;

    private Texture? _compositeOldCopy;
    private Texture? _compositeNewCopy;
    private Texture? _gameOldCopy;
    private Texture? _menuOldCopy;

    private readonly MyGameMain _game;

    private float _progress;
    private bool _shouldCopyRender;

    public static TransitionType TransitionType = TransitionType.Pixelize;

    [CVar("load_transition_speed", "Toggle transition speed")]
    public static float TransitionSpeed = 2.0f;

    private ConcurrentQueue<Action> _queue = new();

    private Action? _loadSyncCallback;

    public static bool Debug;

    public static float DebugProgress;
    
    public static TransitionState DebugState
    {
        get
        {
            return DebugProgress switch
            {
                > 0 and < 0.4f => TransitionState.TransitionOn,
                >= 0.4f and < 0.6f => TransitionState.Active,
                >= 0.6f => TransitionState.TransitionOff,
                _ => TransitionState.Hidden,
            };
        }
    }

    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        SceneTransitions.Add(TransitionType.Diamonds, new DiamondTransition());
        SceneTransitions.Add(TransitionType.FadeToBlack, new FadeToBlack());
        SceneTransitions.Add(TransitionType.Pixelize, new PixelizeTransition());
        SceneTransitions.Add(TransitionType.CircleCrop, new CircleCropTransition());
    }

    [ConsoleHandler("test_load", "Test loading screen")]
    public static void TestLoad()
    {
        Shared.LoadingScreen.LoadAsync(() => Thread.Sleep(1000));
    }

    public void LoadSync(Action loadSyncCallback)
    {
        if (IsLoading || _loadSyncCallback != null)
        {
            Logs.LogError("Already loading sync");
        }

        _loadSyncCallback = loadSyncCallback;
        TransitionOn();
    }

    public void LoadAsync(Action loadCallback)
    {
        if (IsLoading || !_queue.IsEmpty)
        {
            Logs.LogError("Already loading async");
        }

        _queue.Enqueue(loadCallback);
        TransitionOn();
    }

    public void LoadImmediate(Action loadCallback)
    {
        LoadAsync(loadCallback);
        SetActive();
    }

    private void SetActive()
    {
        _progress = 1.0f;
        State = TransitionState.Active;

        Task.Run(() =>
        {
            while (_queue.TryDequeue(out var callback))
            {
                callback();
            }
        });
    }

    private void TransitionOn()
    {
        if (IsLoading)
        {
            Logs.LogError("LoadingScreen is already active, ignoring TransitionOn()");
            return;
        }

        State = TransitionState.TransitionOn;
        _shouldCopyRender = true;
    }

    public void Update(float deltaSeconds)
    {
        if (Debug)
            return;

        if (State == TransitionState.Hidden)
        {
            if (!_queue.IsEmpty || _loadSyncCallback != null)
                TransitionOn();
        }
        else if (State == TransitionState.TransitionOn)
        {
            _progress += TransitionSpeed * deltaSeconds;

            if (_progress >= 1.0f)
            {
                SetActive();
            }
        }
        else if (State == TransitionState.Active)
        {
            if (_loadSyncCallback != null)
            {
                _loadSyncCallback();
                _loadSyncCallback = null;
            }

            if (_queue.IsEmpty)
            {
                State = TransitionState.TransitionOff;
            }
        }
        else if (State == TransitionState.TransitionOff)
        {
            _progress -= TransitionSpeed * deltaSeconds;
            if (_progress <= 0)
            {
                _progress = 0;
                State = TransitionState.Hidden;
            }
        }
    }

    public void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, Texture gameRender, Texture menuRender, double alpha)
    {
        if (_shouldCopyRender)
        {
            _gameOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, gameRender);
            _menuOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, menuRender);
            _compositeOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
            commandBuffer.CopyTextureToTexture(gameRender, _gameOldCopy, Filter.Nearest);
            commandBuffer.CopyTextureToTexture(menuRender, _menuOldCopy, Filter.Nearest);
            commandBuffer.CopyTextureToTexture(renderDestination, _compositeOldCopy, Filter.Nearest);
            _shouldCopyRender = false;
        }

        // TODO (marpe): This seems a bit exhaustive
        _compositeNewCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
        commandBuffer.CopyTextureToTexture(renderDestination, _compositeNewCopy, Filter.Nearest);

        if (Debug)
        {
            SceneTransitions[TransitionType].Draw(renderer, ref commandBuffer, renderDestination, DebugProgress, DebugState, _gameOldCopy, _menuOldCopy,
                _compositeOldCopy, gameRender, menuRender, _compositeNewCopy);
            DrawLoadingText(renderer, ref commandBuffer, renderDestination);
        }
        else
        {
            SceneTransitions[TransitionType].Draw(renderer, ref commandBuffer, renderDestination, _progress, State, _gameOldCopy, _menuOldCopy,
                _compositeOldCopy,
                gameRender, menuRender, _compositeNewCopy);
            DrawLoadingText(renderer, ref commandBuffer, renderDestination);
        }
    }

    private void DrawLoadingText(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.Time.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);

        var textSize = renderer.TextBatcher.GetFont(FontType.RobotoLarge).MeasureString(loadingStr);
        var position = new Vector2(renderDestination.Width, renderDestination.Height) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, 0, Color.White * _progress);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }

    public void Unload()
    {
        _compositeOldCopy?.Dispose();
        _compositeNewCopy?.Dispose();
        _gameOldCopy?.Dispose();
        _menuOldCopy?.Dispose();
        
        foreach (var (_, value) in SceneTransitions)
        {
            value.Unload();
        }
        SceneTransitions.Clear();

        _queue.Clear();
        _loadSyncCallback = null;
    }
}
