namespace MyGame.Screens;

public class PauseMenu : MenuScreen
{
    private readonly ConfirmScreen _confirmQuit;

    public PauseMenu(MyGameMain game) : base(game)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Pause") { IsEnabled = false },
            new TextMenuItem("Resume", OnCancelled),
            new TextMenuItem("Restart", OnRestart),
            new TextMenuItem("Options", () => { Shared.Menus.AddScreen(Shared.Menus.OptionsScreen); }),
            new TextMenuItem("Quit", ConfirmQuitToMain),
        });
        _confirmQuit = new ConfirmScreen(game, Quit, () => { });
    }

    private void OnRestart()
    {
        MyGameMain.Restart(false);
    }

    public override void OnCancelled()
    {
        Exit();
    }

    private void ConfirmQuitToMain()
    {
        Shared.Menus.AddScreen(_confirmQuit);
    }

    private void Quit()
    {
        Shared.LoadingScreen.LoadSync(() =>
        {
            Shared.Game.UnloadWorld();
            Shared.Menus.RemoveAll();
            Shared.Menus.AddScreen(Shared.Menus.MainMenuScreen);
        });
    }

    public override void Draw(Renderer renderer, double alpha)
    {
        var bgAlpha = State == MenuScreenState.Active ? 1.0f : _transitionPercentage;
        renderer.DrawRect(new Rectangle(0, 0, (int)_game.RenderTargets.CompositeRender.Width, (int)_game.RenderTargets.CompositeRender.Height), Color.Black * bgAlpha * 0.5f);
        base.Draw(renderer, alpha);
    }
}
