namespace MyGame.Screens;

public class PauseMenu : MenuScreen
{
    public PauseMenu(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new[]
        {
            new MenuItem("Resume", OnResume),
            new MenuItem("Options", () => { menuManager.QueuePushScreen(Menus.Options); }),
            new MenuItem("Quit To Main Menu", OnQuitToMain),
        });
    }

    private void OnResume()
    {
        _menuManager.QueuePopScreen();
    }

    public override void OnCancelled()
    {
        OnResume();
    }

    private void OnQuitToMain()
    {
        _menuManager.QueuePopAllAndPush(Menus.Main);
    }

    public override void Update(float deltaSeconds)
    {
        var input = _menuManager.Game.InputHandler;
        if (input.IsKeyPressed(KeyCode.Escape))
        {
            OnResume();
            return;
        }

        base.Update(deltaSeconds);
    }
}
