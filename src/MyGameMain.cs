using MyGame.Graphics;
using MyGame.Screens;
using SDL2;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";
    private const int TARGET_TIMESTEP = 120;

    private readonly MenuScreen _menuScreen;

    public readonly InputHandler InputHandler;
    public readonly LoadingScreen LoadingScreen;

    public readonly Renderer Renderer;
    private double _drawFps;

    private double _nextFPSUpdate = 1.0;
    private ulong _prevDrawCount;
    private ulong _prevUpdateCount;

    private readonly Stopwatch _stopwatch;
    private double _updateFps;
    
    public ulong UpdateCount { get; private set; }
    public ulong DrawCount { get; private set; }
    public float TotalElapsedTime { get; private set; }
    public float ElapsedTime { get; private set; }
    public ConsoleScreen ConsoleScreen { get; }
    public GameScreen GameScreen { get; }

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 120, debugMode)
    {
        _stopwatch = Stopwatch.StartNew();

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        Task.Run(() => { Shared.Console.Initialize(); });

        LoadingScreen = new LoadingScreen(this);

        InputHandler = new InputHandler(this);

        Renderer = new Renderer(this);
        Renderer.DefaultClearColor = Color.Black;

        GameScreen = new GameScreen(this);
        _menuScreen = new MenuScreen(this);
        ConsoleScreen = new ConsoleScreen(this);

        Logger.LogInfo($"Game Loaded in {_stopwatch.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        UpdateCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        UpdateWindowTitle();

        InputHandler.BeginFrame();

        LoadingScreen.Update(ElapsedTime);

        var doUpdate = LoadingScreen.State != TransitionState.TransitionOn &&
                       LoadingScreen.State != TransitionState.Active;

        if (doUpdate)
        {
            ConsoleScreen.Update(ElapsedTime);

            if (!ConsoleScreen.IsHidden)
                InputHandler.MouseEnabled = InputHandler.KeyboardEnabled = false;
            
            _menuScreen.Update(ElapsedTime);

            if (!_menuScreen.IsHidden)
                InputHandler.MouseEnabled = InputHandler.KeyboardEnabled = false;

            var isPaused = !_menuScreen.IsHidden;
            GameScreen.Update(isPaused, ElapsedTime);
        }


        InputHandler.EndFrame();
    }

    private void UpdateWindowTitle()
    {
        if (_stopwatch.Elapsed.TotalSeconds < _nextFPSUpdate)
            return;

        _updateFps = UpdateCount - _prevUpdateCount;
        _drawFps = DrawCount - _prevDrawCount;
        _prevUpdateCount = UpdateCount;
        _prevDrawCount = DrawCount;
        _nextFPSUpdate = _stopwatch.Elapsed.TotalSeconds + 1.0;
        SDL.SDL_SetWindowTitle(MainWindow.Handle, $"Update: {_updateFps:0.##}, Draw: {_drawFps:0.##}");
    }

    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        if (!Renderer.BeginFrame(out var swapTexture))
            return;
        
        RenderGame(alpha, swapTexture);
        
        Renderer.EndFrame();
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        DrawCount++;

        GameScreen.Draw(Renderer, renderDestination, alpha);

        _menuScreen.Draw(Renderer, renderDestination, alpha);

        ConsoleScreen.Draw(Renderer, renderDestination, alpha);

        LoadingScreen.Draw(Renderer, renderDestination, alpha);

        Renderer.FlushBatches(renderDestination);
    }

    protected override void Destroy()
    {
        GameScreen.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        Shared.Console.SaveCVars();
    }
}
