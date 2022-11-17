namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    public OptionsMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new []
        {
            new MenuItem("Options", () => {})
            {
                IsEnabled = false,
            },
            new MenuItem("Volume", () => {}),
            new MenuItem("Resolution", () => {}),
            new MenuItem("Back", Back)
        });
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
