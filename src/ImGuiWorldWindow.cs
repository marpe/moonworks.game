using ImGuiNET;
using MyGame.TWImGui;

namespace MyGame;

public class ImGuiWorldWindow : ImGuiWindow
{
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

        ImGui.Separator();

        ImGui.InputFloat("JumpSpeed", ref world.Player.JumpSpeed);
        ImGui.InputFloat("Speed", ref world.Player.Speed);
        ImGui.InputFloat("Gravity", ref world.Gravity);
        var isGrounded = world.IsGrounded(world.Player, world.Player.Velocity);
        ImGui.Checkbox("IsGrounded", ref isGrounded);
        var cell = world.GetGridCoords(world.Player);
        var gridSize = world.GridSize;
        var positionInCell = new Vector2(
            (world.Player.Position.X % gridSize) / gridSize,
            (world.Player.Position.Y % gridSize) / gridSize
        );
        var cellNum = new Num.Vector2(cell.X, cell.Y);
        ImGui.InputFloat2("Cell", ref cellNum);
        var cellRel = new Num.Vector2(positionInCell.X, positionInCell.Y);
        var velocity = new Num.Vector2(world.Player.Velocity.X, world.Player.Velocity.Y);
        ImGui.InputFloat2("Velocity", ref velocity);
        ImGui.End();
    }
}
