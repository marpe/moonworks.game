namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly SDL.SDL_DisplayMode[] _displayModes;
    private readonly TextMenuItem _scale;
    private readonly TextMenuItem _volume;
    private readonly TextMenuItem _windowMode;

    public static Dictionary<ScreenMode, string> ScreenModeNames = new()
    {
        { ScreenMode.Fullscreen, "Fullscreen" },
        { ScreenMode.BorderlessFullscreen, "Fullscreen window" },
        { ScreenMode.Windowed, "Windowed" },
    };

    public OptionsMenuScreen(MyGameMain game) : base(game)
    {
        _volume = new TextMenuItem("Volume", () => { });
        _scale = new TextMenuItem("Scale", ChangeScale);
        _windowMode = new TextMenuItem("Window mode", CycleScreenMode);

        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Options") { IsEnabled = false },
            _volume,
            _scale,
            _windowMode,
            new TextMenuItem("Back", OnCancelled)
        });

        _displayModes = DisplayModes.GetDisplayModes(Shared.Game.MainWindow.Handle);
    }

    private void ChangeScale()
    {
        
    }

    private void CycleScreenMode()
    {
        MyGameMain.ScreenMode = (ScreenMode)(((int)MyGameMain.ScreenMode + 1) % 3);
        _windowMode.Text = $"Window mode: {ScreenModeNames[_game.MainWindow.ScreenMode]}";
    }

    public override void OnCancelled()
    {
        Exit();
    }
}
