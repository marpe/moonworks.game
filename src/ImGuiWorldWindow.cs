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
        ImGui.Separator();
        ImGui.TextUnformatted("Player");
        ImGui.Separator();
        _playerInspector ??= InspectorExt.GetInspectorForTarget(world.Player);
        _playerInspector.Draw();
        
        var cell = world.GetGridCoords(world.Player);
        
        var (adjustX, adjustY) = (MathF.Approx(world.Player.Pivot.X, 1) ? -1 : 0, MathF.Approx(world.Player.Pivot.Y, 1) ? -1 : 0);
        var positionInCell = new Vector2(
            ((world.Player.Position.X + adjustX) % world.GridSize) / world.GridSize,
            ((world.Player.Position.Y + adjustY) % world.GridSize) / world.GridSize
        );
        
        ImGui.TextUnformatted($"Cell: {cell}");
        ImGui.TextUnformatted($"Rel: {positionInCell}");

        ImGui.InputFloat("Gravity", ref world.Gravity);
        
        ImGui.PopID();

        ImGui.PushID("CameraCon");
        ImGui.Separator();
        ImGui.TextUnformatted("CameraCon");
        ImGui.Separator();
        _cameraConInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.CameraController);
        _cameraConInspector.Draw();
        ImGui.PopID();

        ImGui.PushID("Camera");
        ImGui.Separator();
        ImGui.TextUnformatted("Camera");
        ImGui.Separator();
        _cameraInspector ??= InspectorExt.GetInspectorForTarget(Shared.Game.GameScreen.Camera);
        _cameraInspector.Draw();
        ImGui.PopID();
        
        ImGui.End();
    }
}
