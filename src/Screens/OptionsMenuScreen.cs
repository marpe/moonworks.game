namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    private readonly DisplayMode[] _displayModes;

    public OptionsMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Options") { IsEnabled = false },
            new TextMenuItem("Volume", () => { }),
            new TextMenuItem("Resolution", () => { }),
            new TextMenuItem("Back", Back)
        });

        _displayModes = DisplayModes.GetDisplayModes(Shared.Game.MainWindow.Handle);
    }

    public override void OnCancelled()
    {
        Back();
    }

    private void Back()
    {
        _menuManager.SetActiveMenu((Menus)_menuManager.PrevFocused);
    }
}
