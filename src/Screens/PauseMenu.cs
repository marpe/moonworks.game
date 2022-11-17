namespace MyGame.Screens;

public class PauseMenu : MenuScreen
{
    public PauseMenu(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new[]
        {
            new FancyMenuItem("Pause", () => {}){ IsEnabled =  false },
            new MenuItem("Resume", OnResume),
            new MenuItem("Options", () => { menuManager.SetActiveMenu(Menus.Options); }),
            new MenuItem("Quit", OnQuitToMain),
        });
    }

    private void OnResume()
    {
        IsHidden = true;
        Logger.LogInfo("Resuming..");
    }

    public override void OnCancelled()
    {
        OnResume();
    }

    private void OnQuitToMain()
    {
        _menuManager.SetActiveMenu(Menus.Main);
        Shared.Game.GameScreen.Unload();
    }
}
