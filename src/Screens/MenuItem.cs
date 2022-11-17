namespace MyGame.Screens;

public class MenuItem
{
    public Action Callback;
    public string Text;
    public bool IsVisible = true;
    public bool IsEnabled = true;
    public bool IsSelectable => IsVisible && IsEnabled;

    public MenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
    }
}
