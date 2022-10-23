using ImGuiNET;

namespace MyGame.TWImGui;

public class ImGuiExt
{
    public static bool Begin(string name, ref bool isOpen)
    {
        var framePadding = ImGui.GetStyle().FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(framePadding.X, 8));
        var flags = ImGuiWindowFlags.NoCollapse; // | ImGuiWindowFlags.NoTitleBar;
        var result = ImGui.Begin(name, ref isOpen, flags);
        ImGui.PopStyleVar();

        return result;
    }
}
