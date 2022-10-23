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

    public static bool IsKeyboardShortcutPressed(ReadOnlySpan<char> keyboardShortcut)
    {
        var result = true;

        for (var j = 0; j < keyboardShortcut.Length; j++)
        {
            if (keyboardShortcut[j] == '^')
                result &= ImGui.IsKeyDown((int)KeyCode.LeftControl) ||
                          ImGui.IsKeyDown((int)KeyCode.RightControl);
            else if (keyboardShortcut[j] == '+')
                result &= ImGui.IsKeyDown((int)KeyCode.LeftShift) ||
                          ImGui.IsKeyDown((int)KeyCode.RightShift);
            else if (keyboardShortcut[j] == '!')
                result &= ImGui.IsKeyDown((int)KeyCode.LeftAlt) ||
                          ImGui.IsKeyDown((int)KeyCode.RightAlt);
            else
            {
                var keyStr = keyboardShortcut.Slice(j);
                var keyCode = Enum.Parse<KeyCode>(keyStr);
                return result && ImGui.IsKeyPressed((int)keyCode);
            }

            if (!result)
                return false;
        }

        return false;
    }
}
