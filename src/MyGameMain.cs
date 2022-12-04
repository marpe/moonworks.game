using FreeTypeSharp;
using MyGame.Audio;
using MyGame.Debug;
using MyGame.Fonts;

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
    // public static readonly UPoint DesignResolution = new UPoint(1920, 1080);
    public const int TARGET_TIMESTEP = 120;

    public readonly InputHandler InputHandler;

    public ConsoleScreen ConsoleScreen;
    public GameScreen GameScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    public Texture CompositeRender;
    private readonly Texture _menuRender;
    public readonly Texture GameRender;

    public UPoint GameRenderSize => new(
        CompositeRender.Width / RenderScale,
        CompositeRender.Height / RenderScale
    );

    protected UPoint _swapSize;

    public static uint RenderScale = 1;

    [CVar("screen_mode", "Sets screen mode (Window, Fullscreen Window or Fullscreen)")]
    public static ScreenMode ScreenMode
    {
        get => Shared.Game.MainWindow.ScreenMode;
        set
        {
            if (value == ScreenMode.Fullscreen)
                SetWindowDisplayModeToMatchDesktop(Shared.Game.MainWindow.Handle);

            Shared.Game.MainWindow.SetScreenMode(value);
        }
    }

    private Stopwatch _renderStopwatch = new();
    private Stopwatch _renderGameStopwatch = new();
    private Stopwatch _updateStopwatch = new();
    private float _renderDurationMs;
    private float _renderGameDurationMs;
    private float _updateDurationMs;
    private float _peakUpdateDurationMs;
    private float _peakRenderDurationMs;
    private float _peakRenderGameDurationMs;


    /// <summary>
    /// Sets the window display mode to the same as the desktop
    /// </summary>
    private static void SetWindowDisplayModeToMatchDesktop(IntPtr windowHandle)
    {
        var windowDisplayIndex = SDL.SDL_GetWindowDisplayIndex(windowHandle);
        int result;
        result = SDL.SDL_GetDesktopDisplayMode(windowDisplayIndex, out var desktopDisplayMode);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_GetDesktopDisplayMode));
        result = SDL.SDL_SetWindowDisplayMode(windowHandle, ref desktopDisplayMode);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_SetWindowDisplayMode));
    }

    public static SDL.SDL_DisplayMode GetWindowDisplayMode(IntPtr windowHandle)
    {
        var result = SDL.SDL_GetWindowDisplayMode(windowHandle, out var displayMode);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_GetWindowDisplayMode));
        return displayMode;
    }

    public static ScreenMode GetScreenMode(IntPtr windowHandle)
    {
        var flags = SDL.SDL_GetWindowFlags(windowHandle);
        var isFullscreenWindow = (flags & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP) == (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;
        if (isFullscreenWindow)
            return ScreenMode.BorderlessFullscreen;

        var isFullscreen = (flags & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) == (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
        if (isFullscreen)
            return ScreenMode.Fullscreen;

        return ScreenMode.Windowed;
    }

    public static void LogWindowDisplayMode(IntPtr windowHandle)
    {
        var displayMode = GetWindowDisplayMode(windowHandle);
        SDL.SDL_GetWindowSize(windowHandle, out var width, out var height);
        Logs.LogInfo($"WindowSize: {width}x{height}, DisplayMode: {displayMode.w}x{displayMode.h} ({displayMode.refresh_rate} Hz)");
    }
    
    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        int targetTimestep,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();

        var setModeTimer = Stopwatch.StartNew();
        SetWindowDisplayModeToMatchDesktop(MainWindow.Handle);
        setModeTimer.StopAndLog("SetWindowDisplayModeToMatchDesktop");
        
        Time = new Time();

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        Binds.Initialize();
        InputHandler = new InputHandler(Inputs);

        // create render targets
        var createRtsTimer = Stopwatch.StartNew();
        var compositeRenderSize = new UPoint(1920, 1080);
        var gameRenderSize = RenderScale == 1 ? compositeRenderSize : compositeRenderSize / (int)RenderScale + UPoint.One;
        var flags = TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget;
        CompositeRender = Texture.CreateTexture2D(GraphicsDevice, compositeRenderSize.X, compositeRenderSize.Y, TextureFormat.B8G8R8A8, flags);
        GameRender = Texture.CreateTexture2D(GraphicsDevice, gameRenderSize.X, gameRenderSize.Y, TextureFormat.B8G8R8A8, flags);
        _menuRender = TextureUtils.CreateTexture(GraphicsDevice, CompositeRender);
        createRtsTimer.StopAndLog("RenderTargets");

        var createRendererTimer = Stopwatch.StartNew();
        Renderer = new Renderer(this);
        createRendererTimer.StopAndLog("Renderer");
        
        Shared.LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this);

        var audioTimer = Stopwatch.StartNew();
        Shared.AudioManager = new AudioManager(this);
        audioTimer.StopAndLog("AudioManager");

        var menuTimer = Stopwatch.StartNew();
        Shared.Menus = new MenuHandler(this);
        menuTimer.StopAndLog("MenuHandler");
        
        var freeTypeTimer = Stopwatch.StartNew();
        Shared.FreeTypeLibrary = new FreeTypeLibrary();
        var fontAtlas = new FontAtlas(GraphicsDevice);
        fontAtlas.AddFont(ContentPaths.fonts.Pixellari_ttf);
        freeTypeTimer.StopAndLog("FreeType");
        
        Shared.LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Loggers.Add(new TWConsoleLogger());
        });

        sw.StopAndLog("MyGameMain");
    }

    protected override void Update(TimeSpan dt)
    {
        _updateStopwatch.Restart();
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);
        SetInputViewport();

        UpdateScreens();

        Shared.AudioManager.Update((float)dt.TotalSeconds);

        InputHandler.EndFrame();
        _updateStopwatch.Stop();
        _updateDurationMs = _updateStopwatch.GetElapsedMilliseconds();
    }

    protected virtual void SetInputViewport()
    {
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(MainWindow.Size, CompositeRender.Size());
        InputHandler.SetViewportTransform(viewportTransform);
    }

    private void UpdateScreens()
    {
        GameScreen.ExecuteQueuedActions();

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
            MainWindow.Title = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}, SwapSize: {_swapSize.ToString()}";
            _nextWindowTitleUpdate += 1f;
        }
    }
    
    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        _renderStopwatch.Restart();
        {
            _renderGameStopwatch.Restart();
            RenderGame(alpha, CompositeRender);
            _renderGameStopwatch.Stop();
            _renderGameDurationMs = _renderGameStopwatch.GetElapsedMilliseconds();
        }

        {
            var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

            if (swapTexture == null)
            {
                Logs.LogError("Could not acquire swapchain texture");
                return;
            }

            _swapSize = swapTexture.Size();
            
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(swapTexture.Size(), CompositeRender.Size());
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(CompositeRender, viewportTransform, Color.White);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, view * projection);
            Renderer.Submit(ref commandBuffer);
        }
        _renderStopwatch.Stop();
        _renderDurationMs = _renderStopwatch.GetElapsedMilliseconds();
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        Time.UpdateDrawCount();

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        GameScreen.Draw(Renderer, ref commandBuffer, GameRender, alpha);

        Shared.Menus.Draw(Renderer, ref commandBuffer, _menuRender, alpha);

        if (RenderScale != 1)
        {
            var camera = GameScreen.Camera;
            var dstSize = CompositeRender.Size() / (int)RenderScale;
            // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
            var srcRect = new Bounds(camera.FloorRemainder.X, camera.FloorRemainder.Y, dstSize.X, dstSize.Y);
            var gameRenderSprite = new Sprite(GameRender, srcRect);
            var scale = Matrix3x2.CreateScale((int)RenderScale, (int)RenderScale).ToMatrix4x4();
            Renderer.DrawSprite(gameRenderSprite, scale, Color.White);
        }
        else
        {
            Renderer.DrawSprite(GameRender, Matrix4x4.Identity, Color.White);
        }

        Renderer.DrawSprite(_menuRender, Matrix4x4.Identity, Color.White);
        Renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null);

        DrawFPS(Renderer, commandBuffer, renderDestination);

        RenderConsole(Renderer, ref commandBuffer, renderDestination, alpha);

        Shared.LoadingScreen.Draw(Renderer, ref commandBuffer, renderDestination, GameRender, _menuRender, alpha);

        Renderer.Submit(ref commandBuffer);
    }

    private void DrawFPS(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination)
    {
        var position = new Vector2(renderDestination.Width, 0);

        _peakUpdateDurationMs = StopwatchExt.SmoothValue(_peakUpdateDurationMs, _updateDurationMs);
        _peakRenderDurationMs = StopwatchExt.SmoothValue(_peakRenderDurationMs, _renderDurationMs);
        _peakRenderGameDurationMs = StopwatchExt.SmoothValue(_peakRenderGameDurationMs, _renderGameDurationMs);
        
        var str = $"Update: {Time.UpdateFps:0.##} ({_peakUpdateDurationMs:0.00}), Draw: {Time.DrawFps:0.##} ({_peakRenderGameDurationMs:0.00}/{_peakRenderDurationMs:0.00})";
        var strSize = renderer.TextBatcher.GetFont(FontType.ConsolasMonoMedium).MeasureString(str);
        var bg = RectangleExt.FromFloats(position.X - strSize.X, 0, strSize.X, strSize.Y);
        renderer.DrawRect(bg, Color.Black * 0.66f);
        renderer.DrawText(FontType.ConsolasMonoMedium, str, new Vector2(position.X - strSize.X, 0), 0, Color.Yellow);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }

    private void RenderConsole(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        ConsoleToast.Draw(renderer, ref commandBuffer, renderDestination);
        ConsoleScreen.Draw(renderer, ref commandBuffer, renderDestination, alpha);
    }

    protected override void Destroy()
    {
        Logs.LogInfo("Shutting down");

        GameScreen.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        Shared.LoadingScreen.Unload();

        Shared.Console.SaveConfig();
    }
}
