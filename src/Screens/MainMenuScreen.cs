namespace MyGame.Screens;

public class MainMenuScreen : MenuScreen
{

    public MainMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new[]
        {
            new FancyMenuItem("<~><#ff0000>Menu</#></~>", () => {})
            {
                IsEnabled = false
            },
            new MenuItem("New Game", OnPlay),
            new MenuItem("Options", () =>
            {
                menuManager.SetActiveMenu(Menus.Options);
            }),
            new MenuItem("Quit", OnQuit),
        });
    }

    private void OnPlay()
    {
        _menuManager.Game.GameScreen.LoadWorld();
        IsHidden = true;
    }

    private void OnQuit()
    {
        _menuManager.Game.Quit();
    }
}
