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
                SetWindowDisplayMode(Shared.Game.MainWindow.Handle);

            Shared.Game.MainWindow.SetScreenMode(value);
        }
    }

    private static void SetWindowDisplayMode(IntPtr windowHandle)
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

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        int targetTimestep,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();

        var displayMode = GetWindowDisplayMode(MainWindow.Handle);
        SetWindowDisplayMode(MainWindow.Handle);
        Logs.LogInfo($"WindowSize: {MainWindow.Size.X}x{MainWindow.Size.Y}, DisplayMode: {displayMode.w}x{displayMode.h} ({displayMode.refresh_rate} Hz)");

        Time = new Time();

        Shared.Game = this;
        Shared.Console = new TWConsole.TWConsole();
        Binds.Initialize();
        InputHandler = new InputHandler(Inputs);

        var compositeRenderSize = new UPoint(1920, 1080);
        var gameRenderSize = RenderScale == 1 ? compositeRenderSize : compositeRenderSize / (int)RenderScale + UPoint.One;
        var flags = TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget;
        CompositeRender = Texture.CreateTexture2D(GraphicsDevice, compositeRenderSize.X, compositeRenderSize.Y, TextureFormat.B8G8R8A8, flags);
        GameRender = Texture.CreateTexture2D(GraphicsDevice, gameRenderSize.X, gameRenderSize.Y, TextureFormat.B8G8R8A8, flags);
        _menuRender = TextureUtils.CreateTexture(GraphicsDevice, CompositeRender);

        Renderer = new Renderer(this);
        Shared.LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        GameScreen = new GameScreen(this);

        Shared.AudioManager = new AudioManager(this);

        Shared.Menus = new MenuHandler(this);

        Shared.FreeTypeLibrary = new FreeTypeLibrary();

        var fontAtlas = new FontAtlas(GraphicsDevice);
        fontAtlas.AddFont(ContentPaths.fonts.Pixellari_ttf);

        Shared.LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Loggers.Add(new TWConsoleLogger());
        });

        Logs.LogInfo($"Game constructor loaded in {sw.ElapsedMilliseconds} ms");
    }

    protected override void Update(TimeSpan dt)
    {
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);
        SetInputViewport();

        UpdateScreens();

        Shared.AudioManager.Update((float)dt.TotalSeconds);

        InputHandler.EndFrame();
    }

    protected virtual void SetInputViewport()
    {
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(MainWindow.Size, CompositeRender.Size());
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
            MainWindow.Title = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}, SwapSize: {_swapSize.ToString()}";
            _nextWindowTitleUpdate += 1f;
        }
    }
    
    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        {
            RenderGame(alpha, CompositeRender);
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
        var str = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}";
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
