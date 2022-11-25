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
                    _worldInspector = InspectorExt.GetGroupInspectorForTarget(world);
                }

                _prevWorld = world;
                _worldInspector.Draw();
                
                ImGui.Separator();
                
                var (cell, cellRel) = Entity.GetGridCoords(world.Player);
                ImGui.TextUnformatted($"Cell {cell.ToString()}");
                ImGui.TextUnformatted($"CellRel {cellRel.ToString()}");
                ImGui.TextUnformatted($"Pos {world.Player.Position.Current.ToString()}");
                
                var mousePosition = Shared.Game.InputHandler.MousePosition;
                var view = Shared.Game.GameScreen.Camera.GetView();
                Matrix3x2.Invert(view, out var invertedView);
                var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
                ImGui.TextUnformatted($"MousePos {mouseInWorld.ToString()}");
                
                var (mouseCell, mouseCellPos) = Entity.GetGridCoords(mouseInWorld);
                ImGui.TextUnformatted($"MouseCel {mouseCell.ToString()}");
                ImGui.TextUnformatted($"MouseCelPos {mouseCellPos.ToString()}");

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
