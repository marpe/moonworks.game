namespace MyGame.TWImGui;

public abstract class ImGuiEditorWindow
{
    public readonly string Title;
    public bool IsOpen;
    public string? KeyboardShortcut;

    public ImGuiEditorWindow(string title)
    {
        Title = title;
    }

    public abstract void Draw();
}
