using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public class PlaceholderInspector : IInspector
{
    public string? InspectorOrder { get; set; }
    public string? Name { get; } = "Placeholder";
    public Color TextColor = Color.Red;
    public string Message;
    public bool IsHiddenWithoutDebug;

    public PlaceholderInspector(string message, bool isHiddenWithoutDebug = false)
    {
        Message = message;
        IsHiddenWithoutDebug = isHiddenWithoutDebug;
    }

    public void Draw()
    {
        if (IsHiddenWithoutDebug && !ImGuiExt.DebugInspectors)
            return;
        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + avail.X);
        ImGui.TextColored(TextColor.ToNumerics(), Message);
        ImGui.PopTextWrapPos();
    }
}
