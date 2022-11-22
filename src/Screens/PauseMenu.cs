using System.Threading;

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
            new TextMenuItem("Options", () => { SetChild(Shared.Menus.OptionsScreen); }),
            new TextMenuItem("Quit", ConfirmQuitToMain),
        });
        _confirmQuit = new ConfirmScreen(game, Quit, () => { });
    }

    public override void OnCancelled()
    {
        Exit();
    }

    private void ConfirmQuitToMain()
    {
        SetChild(_confirmQuit);
    }
    
    private void Quit()
    {
        Shared.LoadingScreen.QueueLoad(
            () =>
            {
                Shared.Game.GameScreen.SetWorld(null);
            },
            () =>
            {
                _game.SetMenu(Shared.Menus.MainMenuScreen);
                while (Shared.Game.GameScreen.World != null)
                {
                    Thread.Sleep(1);
                }
            }
        );
    }
}
