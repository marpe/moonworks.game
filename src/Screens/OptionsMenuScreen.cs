using MyGame.Audio;

namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly SDL.SDL_DisplayMode[] _displayModes;
    private readonly TextMenuItem _scale;
    private readonly TextMenuItem _volume;
    private readonly TextMenuItem _windowMode;
    private readonly TextMenuItem _presentMode;

    public static Dictionary<ScreenMode, string> ScreenModeNames = new()
    {
        { ScreenMode.Fullscreen, "Fullscreen" },
        { ScreenMode.BorderlessFullscreen, "Fullscreen window" },
        { ScreenMode.Windowed, "Windowed" },
    };

    public static Dictionary<PresentMode, string> PresentModeNames = new()
    {
        { PresentMode.Immediate, "Immediate" },
        { PresentMode.Mailbox, "Mailbox" },
        { PresentMode.FIFO, "FIFO" },
        { PresentMode.FIFORelaxed, "FIFO Relaxed" },
    };

    private readonly int _maxScale;

    public OptionsMenuScreen(MyGameMain game) : base(game)
    {
        _volume = new TextMenuItem($"Volume: {AudioManager.Volume.ToString("P0")}", () => { });


        var windowDisplayIndex = SDL.SDL_GetWindowDisplayIndex(_game.MainWindow.Handle);
        var desktopDisplayMode = MyGameMain.GetDesktopDisplayMode(windowDisplayIndex);
        var gameResolution = _game.RenderTargets.GameSize;
        _maxScale = Math.Max(
            1,
            Math.Min(
                (int)(desktopDisplayMode.w / (float)gameResolution.X),
                (int)(desktopDisplayMode.h / (float)gameResolution.Y)
            )
        );
        _scale = new TextMenuItem($"Scale: {_maxScale}", ChangeScale);
        _windowMode = new TextMenuItem($"Window mode: {ScreenModeNames[_game.MainWindow.ScreenMode]}", CycleScreenMode);
        _presentMode = new TextMenuItem($"Present mode: {PresentModeNames[_game.MainWindow.PresentMode]}", CyclePresentMode);

        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("<!>Options</!>") { IsEnabled = false },
            _volume,
            _scale,
            _windowMode,
            _presentMode,
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

    private void CyclePresentMode()
    {
        Shared.Game.MainWindow.SetPresentMode((PresentMode)(((int)Shared.Game.MainWindow.PresentMode + 1) % 4));
        _presentMode.Text = $"Present mode: {PresentModeNames[Shared.Game.MainWindow.PresentMode]}";
    }

    public override void OnCancelled()
    {
        Exit();
    }
}
