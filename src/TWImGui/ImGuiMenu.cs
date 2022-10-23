namespace MyGame.TWImGui;

public class ImGuiMenu
{
    public List<ImGuiMenu> Children = new();
    public string Text;
    public bool? IsEnabled;
    public string? Shortcut;
    public Action? Callback;

    public ImGuiMenu(string text, string? shortcut = null, Action? callback = null, bool? isEnabled = null)
    {
        Text = text;
        Shortcut = shortcut;
        IsEnabled = isEnabled;
        Callback = callback;
    }

    public ImGuiMenu AddChild(params ImGuiMenu[] children)
    {
        Children.AddRange(children);
        return this;
    }
}
