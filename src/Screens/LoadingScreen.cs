using System.Collections.Concurrent;
using System.Threading;

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
    FadeToBlack
}

public class LoadingScreen
{
    [CVar("load_in_task", "Toggle using tasks for loading")]
    public static bool RunInTask;

    public TransitionState State { get; private set; } = TransitionState.Hidden;
    public bool IsLoading => State == TransitionState.TransitionOn || State == TransitionState.Active;

    private readonly Sprite _backgroundSprite;
    private readonly Sprite _blankSprite;

    private ConcurrentQueue<Action> _taskWork = new();
    private Queue<Action> _work = new();

    private Texture? _compositeOldCopy;
    private Texture? _compositeNewCopy;
    private Texture? _gameOldCopy;
    private Texture? _menuOldCopy;

    private readonly MyGameMain _game;

    private float _progress = 0;
    private bool _shouldCopyRender;

    private Dictionary<TransitionType, SceneTransition> _sceneTransitions = new();

    public static TransitionType Type = TransitionType.FadeToBlack;
    
    [CVar("load_transition_speed", "Toggle transition speed")]
    public static float TransitionSpeed = 5.0f;


    public LoadingScreen(MyGameMain game)
    {
        _game = game;

        var backgroundTexture = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
        var blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _backgroundSprite = new Sprite(backgroundTexture);
        _blankSprite = new Sprite(blankTexture);

        _sceneTransitions.Add(TransitionType.Diamonds, new DiamondTransition(game.GraphicsDevice));
        _sceneTransitions.Add(TransitionType.FadeToBlack, new FadeToBlack());
    }

    [ConsoleHandler("test_load", "Test loading screen")]
    public static void TestLoad()
    {
        Shared.Game.LoadingScreen.QueueLoad(() => { Thread.Sleep(1000); });
    }

    public void QueueLoad(Action runInTask, Action? otherWork = null)
    {
        _taskWork.Enqueue(runInTask);
        if (otherWork != null)
            _work.Enqueue(otherWork);
    }

    public void LoadImmediate(Action runInTask, Action? otherWork = null)
    {
        QueueLoad(runInTask, otherWork);
        SetActive();
    }

    private void SetActive()
    {
        _progress = 1.0f;
        State = TransitionState.Active;

        void Load()
        {
            var sw = Stopwatch.StartNew();
            var taskWorkCount = _taskWork.Count;
            while (_taskWork.TryDequeue(out var work))
            {
                work.Invoke();
            }

            Logger.LogInfo($"*** Loading in task finished ({taskWorkCount} items in {sw.ElapsedMilliseconds} ms)");
        }

        if (RunInTask)
            Task.Run(Load);
        else
            Load();
    }

    public void Update(float deltaSeconds)
    {
        if (State == TransitionState.Hidden)
        {
            if (!_taskWork.IsEmpty || _work.Count > 0)
            {
                State = TransitionState.TransitionOn;
                _shouldCopyRender = true;
            }
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
            var workCount = _work.Count;
            var sw = Stopwatch.StartNew();
            while (_work.TryDequeue(out var work))
            {
                work.Invoke();
            }

            Logger.LogInfo($"--- Loading finished, ({workCount} items in {sw.ElapsedMilliseconds} ms)");

            if (_taskWork.IsEmpty)
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
    
    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, Texture gameRender, Texture menuRender, double alpha)
    {
        if (_shouldCopyRender)
        {
            _gameOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, gameRender);
            _menuOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, menuRender);
            _compositeOldCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
            commandBuffer.CopyTextureToTexture(gameRender, _gameOldCopy, Filter.Nearest);
            commandBuffer.CopyTextureToTexture(menuRender, _menuOldCopy, Filter.Nearest);
            commandBuffer.CopyTextureToTexture(renderDestination, _compositeOldCopy, Filter.Nearest);
            // _game.GraphicsDevice.Submit(commandBuffer);
            // _game.GraphicsDevice.Wait();
            _shouldCopyRender = false;
        }
        
        _compositeNewCopy ??= TextureUtils.CreateTexture(_game.GraphicsDevice, renderDestination);
        commandBuffer.CopyTextureToTexture(renderDestination, _compositeNewCopy, Filter.Nearest);

        _sceneTransitions[Type].Draw(renderer, commandBuffer, renderDestination, _progress, State, _gameOldCopy, _menuOldCopy, _compositeOldCopy, gameRender, menuRender, _compositeNewCopy);

        DrawLoadingText(renderer, commandBuffer, renderDestination);
    }

    private void DrawLoadingText(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination)
    {
        ReadOnlySpan<char> loadingStr = "Loading...";
        var offset = 3 - (int)(_game.Time.TotalElapsedTime / 0.2f) % 4;
        var loadingSpan = loadingStr.Slice(0, loadingStr.Length - offset);

        var textSize = renderer.TextBatcher.GetFont(FontType.RobotoLarge).MeasureString(loadingStr);
        var position = new Vector2(renderDestination.Width, renderDestination.Height) - textSize;
        renderer.DrawText(FontType.RobotoMedium, loadingSpan, position, 0, Color.White * _progress);
        renderer.End(commandBuffer, renderDestination, null, null);
    }

    public void Unload()
    {
        _gameOldCopy?.Dispose();

        foreach (var (key, value) in _sceneTransitions)
        {
            value.Unload();
        }

        _taskWork.Clear();
        _work.Clear();
        _sceneTransitions.Clear();
    }


}
