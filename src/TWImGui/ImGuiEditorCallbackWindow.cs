namespace MyGame.TWImGui;

public class ImGuiEditorCallbackWindow : ImGuiEditorWindow
{
    private readonly Action<ImGuiEditorWindow> _callback;

    public ImGuiEditorCallbackWindow(string title, Action<ImGuiEditorWindow> callback) : base(title)
    {
        _callback = callback;
    }

    public override void Draw()
    {
        _callback.Invoke(this);
    }
}
