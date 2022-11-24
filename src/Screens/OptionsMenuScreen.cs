namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly SDL.SDL_DisplayMode[] _displayModes;
    private readonly TextMenuItem _resolution;
    private readonly TextMenuItem _volume;
    private readonly TextMenuItem _windowMode;

    private Dictionary<ScreenMode, string> _screenModeNames = new()
    {
        { ScreenMode.Fullscreen, "Fullscreen" },
        { ScreenMode.BorderlessFullscreen, "Fullscreen window" },
        { ScreenMode.Windowed, "Windowed" },
    };

    public OptionsMenuScreen(MyGameMain game) : base(game)
    {
        _volume = new TextMenuItem("Volume", () => { });
        _resolution = new TextMenuItem("Resolution", ChangeResolution);
        _windowMode = new TextMenuItem("Window mode", CycleScreenMode);

        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Options") { IsEnabled = false },
            _volume,
            _resolution,
            _windowMode,
            new TextMenuItem("Back", OnCancelled)
        });

        _displayModes = DisplayModes.GetDisplayModes(Shared.Game.MainWindow.Handle);
    }

    private void ChangeResolution()
    {
        var screenMode = GetScreenMode(_game.MainWindow.Handle);
        Logger.LogInfo($"Actual screen mode: {_screenModeNames[screenMode]}");
    }

    public override void OnScreenAdded()
    {
        base.OnScreenAdded();
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        UpdateWindowMode();
        UpdateResolution();
    }

    private static void GetRendererOutputSize(IntPtr windowHandle)
    {
        var renderer = SDL.SDL_GetRenderer(windowHandle);
        if (renderer == IntPtr.Zero)
            throw new SDLException(nameof(SDL.SDL_GetRenderer));
        var result = SDL.SDL_GetRendererOutputSize(renderer, out var w, out var h);
        if (result != 0)
            throw new SDLException(nameof(SDL.SDL_GetRendererOutputSize));
        Logger.LogInfo($"RendererOutputSize: {w}, {h}");
    }

    private void UpdateWindowMode()
    {
        _windowMode.Text = $"Window mode: {_screenModeNames[_game.MainWindow.ScreenMode]}";
    }

    private void UpdateResolution()
    {
        SDL.SDL_Vulkan_GetDrawableSize(_game.MainWindow.Handle, out var w, out var h);
        Logger.LogInfo($"SDL_Vulkan_GetDrawableSize: {w}, {h}");
        _resolution.Text = $"Resolution: {w}x{h}";
    }

    private void CycleScreenMode()
    {
        MyGameMain.ScreenMode = (ScreenMode)(((int)MyGameMain.ScreenMode + 1) % 3);
        UpdateLabels();
    }

    private static ScreenMode GetScreenMode(IntPtr windowHandle)
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

    public override void OnCancelled()
    {
        Exit();
    }
}
