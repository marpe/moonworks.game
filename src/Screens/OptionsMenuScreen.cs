namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly DisplayMode[] _displayModes;

    public OptionsMenuScreen(MyGameMain game) : base(game)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Options") { IsEnabled = false },
            new TextMenuItem("Volume", () => { }),
            new TextMenuItem("Resolution", () => { }),
            new TextMenuItem("Back", OnCancelled)
        });

        _displayModes = DisplayModes.GetDisplayModes(Shared.Game.MainWindow.Handle);
    }

    public override void OnCancelled()
    {
        Exit();
    }
}
