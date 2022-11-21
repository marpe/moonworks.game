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

    public MenuScreen? MenuScreen { get; private set; }
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
        InputHandler = new InputHandler(Inputs);

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();

        _compositeRender = Texture.CreateTexture2D(GraphicsDevice, DesignResolution.X, DesignResolution.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        _gameRender = TextureUtils.CreateTexture(GraphicsDevice, _compositeRender);
        _menuRender = TextureUtils.CreateTexture(GraphicsDevice, _compositeRender);

        Renderer = new Renderer(this);
        Shared.LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this, () => SetMenu(Shared.Menus.PauseScreen));

        Shared.Menus = new MenuHandler(this);
        SetMenu(Shared.Menus.MainMenuScreen);

        Shared.LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Logs.Loggers.Add(new TWConsoleLogger());
        });

        Logger.LogInfo($"Game constructor loaded in {sw.ElapsedMilliseconds} ms");
    }


    public void SetMenu(MenuScreen? menu)
    {
        var prevMenu = MenuScreen;
        MenuScreen = menu;
        if (menu != null)
        {
            menu.SetState(MenuScreenState.Active);
            if (prevMenu != MenuScreen)
                menu.OnBecameVisible();
        }
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
        Shared.LoadingScreen.Update(Time.ElapsedTime);

        if (Shared.LoadingScreen.IsLoading)
            return;

        ConsoleScreen.Update(Time.ElapsedTime);
        if (!ConsoleScreen.IsHidden)
            return;

        if (MenuScreen != null)
        {
            MenuScreen.Update(Time.ElapsedTime);

            if (MenuScreen.State == MenuScreenState.Exited)
                SetMenu(null);
            else if (MenuScreen.State != MenuScreenState.Exiting)
                return;
        }

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

            Renderer.DrawSprite(_compositeRender, viewportTransform, Color.White, 0, SpriteFlip.None);
            Renderer.Flush(commandBuffer, swapTexture, Color.Black, view * projection);
            Renderer.Submit(commandBuffer);
        }
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        Time.UpdateDrawCount();

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        GameScreen.Draw(Renderer, commandBuffer, _gameRender, alpha);

        Renderer.DrawPoint(renderDestination.Size() / 2, Color.Transparent, 10f, 0);
        MenuScreen?.Draw(Renderer, commandBuffer, _menuRender, alpha);
        Renderer.Flush(commandBuffer, _menuRender, Color.Transparent, null);

        Renderer.DrawSprite(_gameRender, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None);
        Renderer.DrawSprite(_menuRender, Matrix4x4.Identity, Color.White, 0, SpriteFlip.None);
        Renderer.Flush(commandBuffer, renderDestination, Color.Cyan, null);

        ConsoleScreen.Draw(Renderer, commandBuffer, renderDestination, alpha);

        Shared.LoadingScreen.Draw(Renderer, commandBuffer, renderDestination, _gameRender, _menuRender, alpha);

        Renderer.Submit(commandBuffer);
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
