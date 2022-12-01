namespace MyGame.TWImGui;

public class ImGuiMenu
{
    public Action? Callback;
    public List<ImGuiMenu> Children = new();
    public bool? IsEnabled;
    public string? Shortcut;
    public string Text;
    public Func<bool>? IsSelectedCallback;

    public ImGuiMenu(string text, string? shortcut = null, Action? callback = null, bool? isEnabled = null, Func<bool>? isSelectedCallback = null)
    {
        Text = text;
        Shortcut = shortcut;
        IsEnabled = isEnabled;
        Callback = callback;
        IsSelectedCallback = isSelectedCallback;
    }

    public ImGuiMenu AddChild(params ImGuiMenu[] children)
    {
        Children.AddRange(children);
        return this;
    }
}
