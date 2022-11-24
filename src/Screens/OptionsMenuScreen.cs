using SDL2;

namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly DisplayMode[] _displayModes;
    private readonly TextMenuItem _resolution;
    private readonly TextMenuItem _volume;
    private readonly TextMenuItem _windowMode;

    public OptionsMenuScreen(MyGameMain game) : base(game)
    {
        _volume = new TextMenuItem("Volume", () => { });
        _resolution = new TextMenuItem("Resolution", () => { });
        _windowMode = new TextMenuItem("Window mode", ToggleFullscreen);
        
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

    private void UpdateWindowMode()
    {
        var screenMode = GetScreenMode();
        _windowMode.Text = $"Window mode: {screenMode.ToString()}";
    }

    private ScreenMode GetScreenMode()
    {
        var flags = SDL.SDL_GetWindowFlags(_game.MainWindow.Handle);
        var isFullscreen = (flags & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;
        if (isFullscreen)
            return ScreenMode.Fullscreen;
        var isFullscreenWindow = (flags & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP) != 0;
        if (isFullscreenWindow)
            return ScreenMode.BorderlessFullscreen;
        return ScreenMode.Windowed;
    }

    private void UpdateResolution()
    {
        var result = SDL.SDL_GetWindowDisplayMode(_game.MainWindow.Handle, out var displayMode);
        if (result != 0)
            Logger.LogError($"SDL_GetWindowDisplayMode failed: {SDL.SDL_GetError()}");

        _resolution.Text = $"Resolution: {displayMode.w}x{displayMode.h} ({displayMode.refresh_rate} Hz)";
    }

    private void ToggleFullscreen()
    {
        MyGameMain.IsFullscreen = !MyGameMain.IsFullscreen;
    }

    public override void OnCancelled()
    {
        Exit();
    }
}
