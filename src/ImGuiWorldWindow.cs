using ImGuiNET;
using MyGame.TWImGui;
using MyGame.TWImGui.Inspectors;

namespace MyGame;

public class ImGuiWorldWindow : ImGuiWindow
{
    private GroupInspector? _playerInspector;
    private GroupInspector? _cameraConInspector;
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

        ImGuiExt.DrawCheckbox("Debug", ref World.Debug);

        ImGui.Separator();

        ImGui.PushID("Player");
        _playerInspector ??= InspectorExt.GetInspectorForTarget(world.Player);
        _playerInspector.Draw();
        ImGui.PopID();

        ImGui.PushID("CameraCon");
        _cameraConInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.CameraController);
        _cameraConInspector.Draw();
        ImGui.PopID();

        ImGui.PushID("Camera");
        _cameraInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.Camera);
        _cameraInspector.Draw();
        ImGui.PopID();
        
        ImGui.End();
    }
}
