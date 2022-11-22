namespace MyGame.Screens;

public class ConfirmScreen : MenuScreen
{
    private Action _confirm;
    private Action _cancel;

    public ConfirmScreen(MyGameMain game, Action confirm, Action cancel) : base(game)
    {
        _cancel = cancel;
        _confirm = confirm;
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("Are you sure?") { IsEnabled = false },
            new TextMenuItem("Quit", OnConfirm),
            new TextMenuItem("Cancel", OnCancelled)
        });
    }

    private void OnConfirm()
    {
        Exit();
        _confirm.Invoke();
    }

    public override void OnCancelled()
    {
        Exit();
        _cancel.Invoke();
    }

    public override void OnScreenAdded()
    {
        base.OnScreenAdded();
        _selectedIndex = 2;
    }
}
