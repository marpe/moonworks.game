using MyGame.Input;
using SDL2;

namespace MyGame;

public class TestGame : Game
{
    public TestGame(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep = 60, bool debugMode = false) : base(
        windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
    }

    protected override void Update(TimeSpan delta)
    {
    }

    protected override void Draw(double alpha)
    {
    }
}

public class MyGameMain : Game
{
    public const int TARGET_TIMESTEP = 120;

    public readonly InputHandler InputHandler;

    private readonly MenuScreen _menuScreen;
    private readonly ConsoleScreen _consoleScreen;
    public readonly LoadingScreen LoadingScreen;
    public GameScreen GameScreen { get; }
    public Time Time { get; }

    public readonly Renderer Renderer;

    private float _nextTitleUpdate;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        int targetTimestep,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();
        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        Task.Run(() => { Shared.Console.Initialize(); });

        LoadingScreen = new LoadingScreen(this);

        InputHandler = new InputHandler(Inputs);

        Renderer = new Renderer(this);

        GameScreen = new GameScreen(this);
        _menuScreen = new MenuScreen(this);
        _consoleScreen = new ConsoleScreen(this);
        Time = new Time();

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);

        LoadingScreen.Update(Time.ElapsedTime);

        if (!LoadingScreen.IsLoading)
        {
            _consoleScreen.Update(Time.ElapsedTime);

            var isPaused = !_consoleScreen.IsHidden;
            _menuScreen.Update(isPaused, Time.ElapsedTime);

            isPaused |= !_menuScreen.IsHidden;
            GameScreen.Update(isPaused, Time.ElapsedTime);
        }

        InputHandler.EndFrame();
    }

    private void UpdateWindowTitle()
    {
        if (Time.TotalElapsedTime >= _nextTitleUpdate)
        {
            SDL.SDL_SetWindowTitle(MainWindow.Handle, $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}");
            _nextTitleUpdate += 1f;
        }
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
        Time.UpdateDrawCount();

        GameScreen.Draw(Renderer, renderDestination, alpha);

        _menuScreen.Draw(Renderer, renderDestination, alpha);

        _consoleScreen.Draw(Renderer, renderDestination, alpha);

        LoadingScreen.Draw(Renderer, renderDestination, alpha);

        Renderer.FlushBatches(renderDestination);
    }

    protected override void Destroy()
    {
        Logger.LogInfo("Shutting down");

        GameScreen.Unload();

        _consoleScreen.Unload();

        Renderer.Unload();

        LoadingScreen.Unload();

        Shared.Console.SaveCVars();
    }
}
