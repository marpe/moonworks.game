namespace MyGame.Screens;

public class PauseMenu : MenuScreen
{
    public PauseMenu(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new[]
        {
            new MenuItem("Resume", OnResume),
            new MenuItem("Options", () => { menuManager.Push(Menus.Options); }),
            new MenuItem("Quit To Main Menu", OnQuitToMain),
        });
    }

    private void OnResume()
    {
        _menuManager.Pop();
    }
    
    private void OnQuitToMain()
    {
        _menuManager.Game.LoadingScreen.StartLoad(() =>
        {
            _menuManager.Game.GameScreen.Unload();
            _menuManager.PopAllAndPush(Menus.Main);
        });
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
