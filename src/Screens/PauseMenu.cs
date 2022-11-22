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
            () => { Shared.Game.GameScreen.SetWorld(null); },
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

    public override void Draw(Renderer renderer, double alpha)
    {
        var bgAlpha = State == MenuScreenState.Covered ? 1.0f : (1.0f - MathF.Abs(_spring.Position));
        renderer.DrawRect(new Rectangle(0, 0, (int)MyGameMain.DesignResolution.X, (int)MyGameMain.DesignResolution.Y), Color.Black * bgAlpha * 0.5f);
        base.Draw(renderer, alpha);
    }
}
