namespace MyGame.Screens;

public class OptionsMenuScreen : MenuScreen
{
    public OptionsMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new []
        {
            new MenuItem("Back", () => ExitScreen())
        });
    }
}
