using ImGuiNET;
using MyGame.TWImGui;
using MyGame.TWImGui.Inspectors;

namespace MyGame;

public class ImGuiWorldWindow : ImGuiWindow
{
    private GroupInspector? _inspector;
    private World? _prevWorld;
    private GroupInspector? _cameraControllerInspector;
    private GroupInspector? _cameraInspector;
    public const string WindowTitle = "World";

    public ImGuiWorldWindow() : base(WindowTitle)
    {
        KeyboardShortcut = "^W";
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.Begin(Title, ref IsOpen);

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
                if (_prevWorld != world || _inspector == null)
                    _inspector ??= InspectorExt.GetInspectorForTarget(world);
                _prevWorld = world;
                _inspector.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Camera"))
            {
                if (ImGuiExt.BeginCollapsingHeader("Controller", Color.Blue))
                {
                    _cameraControllerInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.CameraController);
                    _cameraControllerInspector.Draw();
                    ImGuiExt.EndCollapsingHeader();
                }

                if (ImGuiExt.BeginCollapsingHeader("Camera", Color.Blue))
                {
                    _cameraInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.Camera);
                    _cameraInspector.Draw();
                    ImGuiExt.EndCollapsingHeader();
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
