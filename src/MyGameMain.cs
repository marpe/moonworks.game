using System.Collections.Concurrent;
using System.Threading;
using MyGame.Audio;
using MyGame.Cameras;
using MyGame.Debug;
using MyGame.WorldsRoot;

namespace MyGame;

public class MyGameMain : Game
{
    public const int TARGET_TIMESTEP = 120;

    public readonly InputHandler InputHandler;
    public Camera Camera { get; }
    private ConcurrentQueue<Action> _queuedActions = new();

    public static bool IsStepping;
    public static bool IsPaused;
    public World World { get; }

    public ConsoleScreen ConsoleScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    public RenderTargets RenderTargets;

    protected UPoint _swapSize;

    public readonly FPSDisplay _fpsDisplay;

    public List<RenderPass> _renderPasses = new();

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
        Shared.Content = new ContentManager(this);
        Shared.Console = new TWConsole.TWConsole();
        Binds.Initialize();
        InputHandler = new InputHandler(Inputs);
        RenderTargets = new RenderTargets(GraphicsDevice);
        Renderer = new Renderer(this);
        Shared.LoadingScreen = new LoadingScreen(this);
        ConsoleScreen = new ConsoleScreen(this);
        Shared.AudioManager = new AudioManager();
        Shared.Menus = new MenuHandler(this);

        World = new World();

        Camera = new Camera(RenderTargets.GameSize.X, RenderTargets.GameSize.Y);

        Shared.LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Loggers.Add(new TWConsoleLogger());
        });

        _renderPasses.Add(new WorldRenderPass());
        _renderPasses.Add(new MenuRenderPass());
        _renderPasses.Add(new DebugRenderPass());
        _renderPasses.Add(new LoadingScreenRenderPass());
        _renderPasses.Add(new ConsoleRenderPass());

        _fpsDisplay = new FPSDisplay();

        sw.StopAndLog("MyGameMain");
    }

    protected override void Update(TimeSpan dt)
    {
        _fpsDisplay.BeginUpdate();
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);
        SetInputViewport();

        Binds.HandleButtonBinds(InputHandler);

        if (InputHandler.IsKeyPressed(KeyCode.B))
        {
            GraphicsDevice.UnclaimWindow(MainWindow);
            GraphicsDevice.ClaimWindow(MainWindow, PresentMode.Mailbox);
        }

        ConsoleToast.Update((float)dt.TotalSeconds);

        UpdateScreens();

        Shared.AudioManager.Update((float)dt.TotalSeconds);

        InputHandler.EndFrame();
        _fpsDisplay.EndUpdate();
    }

    protected virtual void SetInputViewport()
    {
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(MainWindow.Size, RenderTargets.CompositeRender.Size);
        InputHandler.SetViewportTransform(viewportTransform);
    }

    protected void UpdateScreens()
    {
        ExecuteQueuedActions();

        UpdateLastPositions();

        Shared.LoadingScreen.Update(Time.ElapsedTime);
        if (Shared.LoadingScreen.IsLoading)
            return;

        ConsoleScreen.Update(Time.ElapsedTime);
        if (!ConsoleScreen.IsHidden)
            return;

        Shared.Menus.Update(Time.ElapsedTime);
        if (!Shared.Menus.IsHidden)
            return;

        if (InputHandler.IsKeyPressed(KeyCode.Escape))
        {
            Shared.Menus.AddScreen(Shared.Menus.PauseScreen);
            return;
        }

        UpdateWorld(Time.ElapsedTime);
    }

    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        _fpsDisplay.BeginRender();
        {
            var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

            if (swapTexture == null)
            {
                Logs.LogError("Could not acquire swapchain texture");
                return;
            }

            _swapSize = swapTexture.Size();

            RenderGame(ref commandBuffer, alpha, RenderTargets.CompositeRender);
            
            // TestFunctions.DrawPixelArtShaderTestSkull(Renderer, ref commandBuffer, RenderTargets.CompositeRender);

            var (viewportTransform, viewport) = Renderer.GetViewportTransform(swapTexture.Size(), RenderTargets.CompositeRender.Size);
            // TODO (marpe): Render at int scale ?
            
            Renderer.DrawSprite(RenderTargets.CompositeRender.Target, viewportTransform, Color.White, SpriteFlip.None);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, null, true, PipelineType.Sprite);
            Renderer.Submit(ref commandBuffer);
        }
        _fpsDisplay.EndRender();
    }

    protected void RenderGame(ref CommandBuffer commandBuffer, double alpha, Texture renderDestination)
    {
        _fpsDisplay.BeginRenderGame();
        Renderer.RenderPasses = 0;
        SpriteBatch.DrawCalls = 0;
        Time.UpdateDrawCount();

        for (var i = 0; i < _renderPasses.Count; i++)
        {
            _renderPasses[i].Draw(Renderer, ref commandBuffer, renderDestination, alpha);
        }

        _fpsDisplay.EndRenderGame();
    }

    private void LoadRoot(Func<RootJson> rootLoader, string filepath, Action? onCompleteCallback)
    {
        if (Shared.LoadingScreen.IsLoading)
        {
            Logs.LogError("Loading screen is active, ignoring load call");
            return;
        }

        QueueAction(() =>
        {
            Logs.LogInfo($"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Removing screens");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            UnloadWorld();
        });

        Shared.LoadingScreen.LoadAsync(() =>
        {
            var root = rootLoader();
            QueueSetRoot(root, filepath, onCompleteCallback);
        });
    }

    private void ExecuteQueuedActions()
    {
        while (_queuedActions.TryDequeue(out var action))
        {
            action();
        }
    }

    private void UpdateLastPositions()
    {
        if (!World.IsLoaded)
            return;
        World.UpdateLastPositions();
        Camera.UpdateLastPosition();
    }

    private void UpdateWorld(float deltaSeconds)
    {
        if (!World.IsLoaded)
            return;

        if (IsStepping || !IsPaused)
        {
            World.Update(deltaSeconds, Camera);
            Camera.Update(deltaSeconds * World.TimeScale, InputHandler);
        }
        else
        {
            if (Camera.NoClip)
                Camera.Update(deltaSeconds, InputHandler);
        }

        if (IsStepping)
            IsStepping = false;
    }

    public void UnloadWorld()
    {
        if (!World.IsLoaded)
            return;

        Logs.LogInfo($"Unloading world from thread: {Thread.CurrentThread.ManagedThreadId}");
        Camera.TrackEntity(null);
        Camera.LevelBounds = Rectangle.Empty;
        World.Unload();
    }

    protected void QueueAction(Action callback)
    {
        _queuedActions.Enqueue(callback);
    }

    protected void QueueSetRoot(RootJson root, string filepath, Action? onCompleteCallback)
    {
        QueueAction(() =>
        {
            Logs.LogInfo(
                $"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Removing screens and setting world from thread: {Thread.CurrentThread.ManagedThreadId}");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            UnloadWorld();
            World.SetRoot(root, filepath);
            onCompleteCallback?.Invoke();
        });
    }

    protected override void Destroy()
    {
        Logs.LogInfo("Shutting down");

        World.Unload();

        ConsoleScreen.Unload();

        Renderer.Unload();

        Shared.LoadingScreen.Unload();

        Shared.Console.SaveConfig();
    }

    #region ConsoleHandlers

    [ConsoleHandler("restart")]
    public static void Restart(bool immediate = true)
    {
        Logs.LogInfo($"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Starting load");
        var filepath = Shared.Game.World.Filepath;
        if (filepath == "")
            filepath = ContentPaths.worlds.worlds_json;
        var rootLoader = () => Shared.Content.Load<RootJson>(filepath, true);
        if (immediate)
            Shared.Game.QueueSetRoot(rootLoader(), filepath, World.NextLevel);
        else
            Shared.Game.LoadRoot(rootLoader, filepath, World.NextLevel);
    }

    [ConsoleHandler("step")]
    public static void Step()
    {
        IsPaused = true;
        IsStepping = true;
        var updateCount = Shared.Game.World.WorldUpdateCount;
        Logs.LogInfo($"[WU:{updateCount}] Stepping...");
    }

    [ConsoleHandler("pause")]
    public static void Pause()
    {
        IsPaused = !IsPaused;
        Logs.LogInfo(IsPaused ? "Game paused" : "Game resumed");
    }

    [ConsoleHandler("speed_up")]
    public static void IncreaseTimeScale()
    {
        World.TimeScale += 0.0625F;
        Logs.LogInfo($"TimeScale: {World.TimeScale}");
    }

    [ConsoleHandler("speed_reset")]
    public static void ResetTimeScale()
    {
        World.TimeScale = 1.0f;
        Logs.LogInfo($"TimeScale: {World.TimeScale}");
    }

    [ConsoleHandler("speed_down")]
    public static void DecreaseTimeScale()
    {
        World.TimeScale -= 0.0625F;
        if (World.TimeScale < 0)
            World.TimeScale = 0;
        Logs.LogInfo($"TimeScale: {World.TimeScale}");
    }

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

    #endregion

    #region SDL Helpers

    /// <summary>
    /// Sets the window display mode to the same as the desktop
    /// </summary>
    private static void SetWindowDisplayModeToMatchDesktop(IntPtr windowHandle)
    {
        var windowDisplayIndex = SDL.SDL_GetWindowDisplayIndex(windowHandle);
        var desktopDisplayMode = GetDesktopDisplayMode(windowDisplayIndex);
        var result = SDL.SDL_SetWindowDisplayMode(windowHandle, ref desktopDisplayMode);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_SetWindowDisplayMode));
    }

    public static SDL.SDL_DisplayMode GetDesktopDisplayMode(int windowDisplayIndex)
    {
        var result = SDL.SDL_GetDesktopDisplayMode(windowDisplayIndex, out var desktopDisplayMode);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_GetDesktopDisplayMode));
        return desktopDisplayMode;
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

    #endregion

    #region Helpers

    protected void UpdateWindowTitle()
    {
        if (Time.TotalElapsedTime >= _nextWindowTitleUpdate)
        {
            MainWindow.Title = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}, SwapSize: {_swapSize.ToString()}";
            _nextWindowTitleUpdate += 1f;
        }
    }

    #endregion
}
