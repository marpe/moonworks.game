namespace MyGame.TWImGui;

public abstract class ImGuiWindow
{
    public readonly string Title;
    public bool IsOpen;
    public string? KeyboardShortcut;

    public ImGuiWindow(string title)
    {
        Title = title;
    }

    public abstract void Draw();
}
