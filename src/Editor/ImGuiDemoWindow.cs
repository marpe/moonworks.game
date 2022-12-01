using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class ImGuiDemoWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "ImGui Demo Window";
    
    public ImGuiDemoWindow() : base(WindowTitle)
    {
        IsOpen = false;
        KeyboardShortcut = "^F2";
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        fixed (bool* isOpen = &IsOpen)
        {
            ImGui.ShowDemoWindow(isOpen);
        }
    }
}
