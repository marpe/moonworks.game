using System.Threading;
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

    public MenuManager MenuManager;
    public ConsoleScreen ConsoleScreen;
    public readonly LoadingScreen LoadingScreen;
    public GameScreen GameScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        int targetTimestep,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();
        
        Time = new Time();
        InputHandler = new InputHandler(Inputs);
        
        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        
        Renderer = new Renderer(this);
        LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this);
        MenuManager = new MenuManager(this);
        
        LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Thread.Sleep(3000);
            MenuManager.Push(Menus.Main);
        });

        Logger.LogInfo($"Game constructor loaded in {sw.ElapsedMilliseconds} ms");
    }


    protected override void Update(TimeSpan dt)
    {
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);

        LoadingScreen.Update(Time.ElapsedTime);

        if (!LoadingScreen.IsLoading)
        {
            ConsoleScreen.Update(Time.ElapsedTime);

            if (ConsoleScreen.IsHidden)
            {
                if(MenuManager.IsHidden)
                    GameScreen.Update(Time.ElapsedTime);
                else
                    MenuManager.Update(Time.ElapsedTime);
            }
        }

        InputHandler.EndFrame();
    }

    private void UpdateWindowTitle()
    {
        if (Time.TotalElapsedTime >= _nextWindowTitleUpdate)
        {
            SDL.SDL_SetWindowTitle(MainWindow.Handle, $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}");
            _nextWindowTitleUpdate += 1f;
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

        MenuManager.Draw(Renderer, renderDestination, alpha);

        ConsoleScreen.Draw(Renderer, renderDestination, alpha);

        LoadingScreen.Draw(Renderer, renderDestination, alpha);

        Renderer.FlushBatches(renderDestination);
    }

    protected override void Destroy()
    {
        Logger.LogInfo("Shutting down");

        GameScreen.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        LoadingScreen.Unload();

        Shared.Console.SaveCVars();
    }
}
