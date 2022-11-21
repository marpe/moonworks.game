using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class WorldWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "World";
    private GroupInspector? _cameraInspector;
    private GroupInspector? _worldInspector;
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
            ImGui.TextUnformatted("World is null");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("Tabs"))
        {
            if (ImGui.BeginTabItem("World"))
            {
                if (_prevWorld != world || _worldInspector == null)
                {
                    _worldInspector = InspectorExt.GetInspectorForTarget(world);
                }

                _prevWorld = world;
                _worldInspector.Draw();
                
                ImGui.Separator();
                
                var (cell, cellRel) = Entity.GetGridCoords(world.Player);
                ImGui.TextUnformatted($"Cell {cell.ToString()}");
                ImGui.TextUnformatted($"CellRel {cellRel.ToString()}");
                
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Camera"))
            {
                _cameraInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.Camera);
                _cameraInspector.Draw();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
