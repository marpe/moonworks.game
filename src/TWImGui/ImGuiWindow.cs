namespace MyGame.TWImGui;

public abstract class ImGuiWindow
{
    public bool IsOpen;
    public readonly string Title;
    public string? KeyboardShortcut;

    public ImGuiWindow(string title)
    {
        Title = title;
    }

    public abstract void Draw();
}
