using System.Threading;
using MyGame.Logs;

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
    public static readonly UPoint DesignResolution = new UPoint(1920, 1080);
    public const int TARGET_TIMESTEP = 120;

    public readonly InputHandler InputHandler;

    public MenuManager MenuManager;
    public ConsoleScreen ConsoleScreen;
    public readonly LoadingScreen LoadingScreen;
    public GameScreen GameScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    private Texture _gameRender;

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

        _gameRender = Texture.CreateTexture2D(GraphicsDevice, DesignResolution.X, DesignResolution.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

        Renderer = new Renderer(this);
        Shared.LoadingScreen = LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this);
        MenuManager = new MenuManager(this);

        LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Logs.Loggers.Add(new TWConsoleLogger());
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
            // TODO (marpe): eewww
            if (ConsoleScreen.IsHidden)
            {
                if (MenuManager.IsHidden)
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
            MainWindow.Title = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}";
            _nextWindowTitleUpdate += 1f;
        }
    }

    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        {
            RenderGame(alpha, _gameRender);
        }

        {
            var windowSize = MainWindow.Size;
            var (commandBuffer, swapTexture) = Renderer.Begin(windowSize);

            var (viewportTransform, viewport) = Renderer.GetViewportTransform(
                swapTexture.Size(),
                DesignResolution
            );
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(_gameRender, viewportTransform, Color.White, 0, SpriteFlip.None);
            Renderer.End(commandBuffer, swapTexture, Color.Black, view * projection);
            Renderer.Submit(commandBuffer);
        }
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        Time.UpdateDrawCount();

        var commandBuffer = Renderer.Begin();

        GameScreen.Draw(Renderer, commandBuffer, renderDestination, alpha);

        MenuManager.Draw(Renderer, commandBuffer, renderDestination, alpha);

        ConsoleScreen.Draw(Renderer, commandBuffer, renderDestination, alpha);

        LoadingScreen.Draw(Renderer, commandBuffer, renderDestination, alpha);

        Renderer.Submit(commandBuffer);
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
