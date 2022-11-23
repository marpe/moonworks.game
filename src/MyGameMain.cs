using FreeTypeSharp;
using MyGame.Debug;
using MyGame.Fonts;
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

    public ConsoleScreen ConsoleScreen;
    public GameScreen GameScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    private Texture _compositeRender;
    private readonly Texture _menuRender;
    private readonly Texture _gameRender;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        int targetTimestep,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();

        Time = new Time();

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        InputHandler = new InputHandler(Inputs);

        _compositeRender = Texture.CreateTexture2D(GraphicsDevice, DesignResolution.X, DesignResolution.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        _gameRender = TextureUtils.CreateTexture(GraphicsDevice, _compositeRender);
        _menuRender = TextureUtils.CreateTexture(GraphicsDevice, _compositeRender);

        Renderer = new Renderer(this);
        Shared.LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this);

        Shared.Menus = new MenuHandler(this);

        Shared.FreeTypeLibrary = new FreeTypeLibrary();

        var fontAtlas = new FontAtlas(GraphicsDevice);
        fontAtlas.AddFont(ContentPaths.fonts.Pixellari_ttf);

        Shared.LoadingScreen.LoadImmediate(() =>
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
        SetInputViewport();

        UpdateScreens();

        InputHandler.EndFrame();
    }

    protected virtual void SetInputViewport()
    {
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(MainWindow.Size, DesignResolution);
        InputHandler.SetViewportTransform(viewportTransform);
    }

    private void UpdateScreens()
    {
        GameScreen.UpdateQueued();

        Shared.LoadingScreen.Update(Time.ElapsedTime);
        if (Shared.LoadingScreen.IsLoading)
            return;

        ConsoleScreen.Update(Time.ElapsedTime);
        if (!ConsoleScreen.IsHidden)
            return;

        Shared.Menus.Update(Time.ElapsedTime);
        if (!Shared.Menus.IsHidden)
            return;

        GameScreen.Update(Time.ElapsedTime);
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
            RenderGame(alpha, _compositeRender);
        }

        {
            var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

            if (swapTexture == null)
            {
                Logger.LogError("Could not acquire swapchain texture");
                return;
            }

            var (viewportTransform, viewport) = Renderer.GetViewportTransform(
                swapTexture.Size(),
                DesignResolution
            );
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(_compositeRender, viewportTransform, Color.White);
            Renderer.Flush(commandBuffer, swapTexture, Color.Black, view * projection);
            Renderer.Submit(commandBuffer);
        }
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        Time.UpdateDrawCount();

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        GameScreen.Draw(Renderer, commandBuffer, _gameRender, alpha);

        Shared.Menus.Draw(Renderer, commandBuffer, _menuRender, alpha);

        Renderer.DrawSprite(_gameRender, Matrix4x4.Identity, Color.White);
        Renderer.DrawSprite(_menuRender, Matrix4x4.Identity, Color.White);
        Renderer.Flush(commandBuffer, renderDestination, Color.Black, null);

        RenderConsole(Renderer, commandBuffer, renderDestination, alpha);

        Shared.LoadingScreen.Draw(Renderer, commandBuffer, renderDestination, _gameRender, _menuRender, alpha);

        Renderer.Submit(commandBuffer);
    }

    private void RenderConsole(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        ConsoleToast.Draw(renderer, commandBuffer, renderDestination);
        ConsoleScreen.Draw(renderer, commandBuffer, renderDestination, alpha);
    }

    protected override void Destroy()
    {
        Logger.LogInfo("Shutting down");

        GameScreen.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        Shared.LoadingScreen.Unload();

        Shared.Console.SaveCVars();
    }
}
