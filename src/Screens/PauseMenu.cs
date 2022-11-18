namespace MyGame.Screens;

public class PauseMenu : MenuScreen
{
    public PauseMenu(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Pause") { IsEnabled = false },
            new TextMenuItem("Resume", OnResume),
            new TextMenuItem("Options", () => { menuManager.SetActiveMenu(Menus.Options); }),
            new TextMenuItem("Quit", OnQuitToMain),
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
        Shared.LoadingScreen.QueueLoad(() => { Shared.Game.GameScreen.Unload(); }, () => { _menuManager.SetActiveMenu(Menus.Main); });
    }
}
