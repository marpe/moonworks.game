using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class WorldWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "World";
    private IInspector? _cameraInspector;
    private IInspector? _worldInspector;
    private World? _prevWorld;

    public WorldWindow() : base(WindowTitle)
    {
        KeyboardShortcut = "^W";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        ImGui.Begin(Title, ImGuiExt.RefPtr(ref IsOpen));

        var world = Shared.Game.GameScreen.World;
        if (world == null)
        {
            ImGui.TextDisabled("NULL");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("Tabs"))
        {
            if (ImGui.BeginTabItem("World"))
            {
                if (_prevWorld != world || _worldInspector == null)
                {
                    _worldInspector = InspectorExt.GetGroupInspectorForTarget(world);
                }

                _prevWorld = world;
                _worldInspector.Draw();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Camera"))
            {
                _cameraInspector ??= InspectorExt.GetGroupInspectorForTarget(Shared.Game.GameScreen.Camera);
                _cameraInspector.Draw();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
