using System.Collections.Concurrent;
using System.Threading;
using FreeTypeSharp;
using MyGame.Audio;
using MyGame.Cameras;
using MyGame.Debug;
using MyGame.Fonts;
using MyGame.Screens.Transitions;
using MyGame.WorldsRoot;

namespace MyGame;

public class MyGameMain : Game
{
    public const int TARGET_TIMESTEP = 120;

    public readonly InputHandler InputHandler;
    public Camera Camera { get; }
    private ConcurrentQueue<Action> _queuedActions = new();

    public static int GameUpdateRate = 1;
    public static bool IsStepping;
    public static bool IsPaused;
    public static bool DebugViewBounds = false;
    public World World { get; }

    public ConsoleScreen ConsoleScreen;
    public readonly Time Time;

    public readonly Renderer Renderer;

    private float _nextWindowTitleUpdate;

    public RenderTargets RenderTargets;

    protected UPoint _swapSize;

    protected readonly FPSDisplay _fpsDisplay;
    private bool _hasRenderedConsole;
    public DebugRenderTarget DebugRenderTarget = DebugRenderTarget.None;

    [CVar("lights_enabled", "Toggle lights")]
    public static bool LightsEnabled = true;

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

        var freeTypeTimer = Stopwatch.StartNew();
        Shared.FreeTypeLibrary = new FreeTypeLibrary();
        var fontAtlas = new FontAtlas(GraphicsDevice);
        fontAtlas.AddFont(ContentPaths.fonts.Pixellari_ttf);
        freeTypeTimer.StopAndLog("FreeType");

        World = new World();

        Camera = new Camera(RenderTargets.GameRenderSize.X, RenderTargets.GameRenderSize.Y)
        {
            ClampToLevelBounds = true,
        };

        Shared.LoadingScreen.LoadImmediate(() =>
        {
            Shared.Console.Initialize();
            Logs.Loggers.Add(new TWConsoleLogger());
        });

        _fpsDisplay = new FPSDisplay();

        sw.StopAndLog("MyGameMain");
    }

    protected override void Update(TimeSpan dt)
    {
        _fpsDisplay.BeginUpdate();
        Time.Update(dt);

        UpdateWindowTitle();

        InputHandler.BeginFrame(Time.ElapsedTime);

        Binds.UpdateButtonStates(InputState.Create(InputHandler));
        Binds.ExecuteTriggeredBinds();

        SetInputViewport();

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
            RenderGame(alpha, RenderTargets.CompositeRender);
        }

        {
            var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

            if (swapTexture == null)
            {
                Logs.LogError("Could not acquire swapchain texture");
                return;
            }

            _swapSize = swapTexture.Size();

            var finalRenderTarget = DebugRenderTarget switch
            {
                DebugRenderTarget.GameRender => RenderTargets.GameRender,
                DebugRenderTarget.LightSource => RenderTargets.LightSource,
                DebugRenderTarget.LightTarget => RenderTargets.LightTarget,
                DebugRenderTarget.Console => RenderTargets.ConsoleRender,
                DebugRenderTarget.Menu => RenderTargets.MenuRender,
                _ => RenderTargets.CompositeRender
            };

            var (viewportTransform, viewport) = Renderer.GetViewportTransform(swapTexture.Size(), finalRenderTarget.Size);
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(finalRenderTarget.Target, viewportTransform, Color.White);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, view * projection);
            Renderer.Submit(ref commandBuffer);
        }
        _fpsDisplay.EndRender();
    }

    protected void RenderGame(double alpha, Texture renderDestination)
    {
        _fpsDisplay.BeginRenderGame();
        Time.UpdateDrawCount();

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        Renderer.Clear(ref commandBuffer, RenderTargets.LightSource, Color.Transparent);
        Renderer.Clear(ref commandBuffer, RenderTargets.LightTarget, Color.Transparent);
        DrawWorld(Renderer, ref commandBuffer, RenderTargets.GameRender, alpha);

        Shared.Menus.Draw(Renderer, ref commandBuffer, RenderTargets.MenuRender, alpha);

        if (RenderTargets.RenderScale != 1)
        {
            var camera = Camera;
            var dstSize = RenderTargets.CompositeRender.Size / (int)RenderTargets.RenderScale;
            // offset the uvs with whatever fraction the camera was at so that camera panning looks smooth
            var srcRect = new Bounds(camera.FloorRemainder.X, camera.FloorRemainder.Y, dstSize.X, dstSize.Y);
            var gameRenderSprite = new Sprite(RenderTargets.GameRender, srcRect);
            var scale = Matrix3x2.CreateScale((int)RenderTargets.RenderScale, (int)RenderTargets.RenderScale).ToMatrix4x4();
            Renderer.DrawSprite(gameRenderSprite, scale, Color.White);
        }
        else
        {
            Renderer.DrawSprite(RenderTargets.GameRender.Target, Matrix4x4.Identity, Color.White);
        }

        Renderer.DrawSprite(RenderTargets.MenuRender.Target, Matrix4x4.Identity, Color.White);
        Renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, null);

        Shared.LoadingScreen.Draw(Renderer, ref commandBuffer, renderDestination, RenderTargets.GameRender, RenderTargets.MenuRender, alpha);

        RenderConsole(Renderer, ref commandBuffer, renderDestination, alpha);
        _fpsDisplay.DrawFPS(Renderer, commandBuffer, renderDestination);

        Renderer.Submit(ref commandBuffer);

        _fpsDisplay.EndRenderGame();
    }

    private void RenderConsole(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if ((int)Time.UpdateCount % ConsoleSettings.RenderRate == 0)
        {
            _hasRenderedConsole = true;
            renderer.Clear(ref commandBuffer, RenderTargets.ConsoleRender, Color.Transparent);

            ConsoleToast.Draw(renderer, ref commandBuffer, RenderTargets.ConsoleRender);

            if (!ConsoleScreen.IsHidden)
            {
                ConsoleScreen.Draw(renderer, ref commandBuffer, RenderTargets.ConsoleRender, alpha);
            }
        }

        if (_hasRenderedConsole)
        {
            renderer.DrawSprite(RenderTargets.ConsoleRender.Target, Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
        }
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
    }

    /// Call before loading starts
    private void SetCircleCropPosition(Vector2 position)
    {
        var viewProjection = Camera.GetViewProjection(RenderTargets.GameRender.Width, RenderTargets.GameRender.Height);
        var positionInScreen = Vector2.Transform(position, viewProjection);
        positionInScreen = Vector2.Half + positionInScreen * 0.5f;
        var circleLoad = (CircleCropTransition)LoadingScreen.SceneTransitions[TransitionType.CircleCrop];
        circleLoad.CenterX = positionInScreen.X;
        circleLoad.CenterY = positionInScreen.Y;
    }

    private void DrawViewBounds(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        if (!World.Debug) return;
        if (!DebugViewBounds) return;

        renderer.DrawRectOutline(Vector2.Zero, RenderTargets.CompositeRender.Size, Color.LimeGreen, 10f);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }

    private void DrawWorld(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (!World.IsLoaded)
        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            return;
        }

        renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
        World.Draw(renderer, Camera, alpha);

        World.DrawDebug(renderer, Camera, alpha);
        // draw ambient background color
        // renderer.DrawRect(Camera.Position - Camera.ZoomedSize * 0.5f, (Camera.Position + Camera.ZoomedSize * 0.5f).Ceil(), World.AmbientColor);

        var viewProjection = Camera.GetViewProjection(renderDestination.Width, renderDestination.Height);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);


        if (LightsEnabled)
            World.DrawLights(renderer, ref commandBuffer, renderDestination, Camera, RenderTargets.LightSource, RenderTargets.LightTarget, alpha);


        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }

    private void UpdateWorld(float deltaSeconds)
    {
        if (!World.IsLoaded)
            return;

        var doUpdate = IsStepping ||
                       (int)Shared.Game.Time.UpdateCount % GameUpdateRate == 0 && !IsPaused;
        if (doUpdate)
        {
            World.Update(deltaSeconds, InputHandler, Camera);
            Camera.Update(deltaSeconds, InputHandler);
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
        var rootLoader = () => Shared.Content.LoadRoot(filepath, true);
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
    public static void IncreaseUpdateRate()
    {
        GameUpdateRate -= 5;
        if (GameUpdateRate < 1)
            GameUpdateRate = 1;
        Logs.LogInfo($"UpdateRate: {GameUpdateRate}");
    }

    [ConsoleHandler("speed_down")]
    public static void DecreaseUpdateRate()
    {
        GameUpdateRate += 5;
        Logs.LogInfo($"UpdateRate: {GameUpdateRate}");
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

    protected void UpdateWindowTitle()
    {
        if (Time.TotalElapsedTime >= _nextWindowTitleUpdate)
        {
            MainWindow.Title = $"Update: {Time.UpdateFps:0.##}, Draw: {Time.DrawFps:0.##}, SwapSize: {_swapSize.ToString()}";
            _nextWindowTitleUpdate += 1f;
        }
    }
}
