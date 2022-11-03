using ImGuiNET;
using MyGame.TWImGui;
using MyGame.TWImGui.Inspectors;

namespace MyGame;

public class ImGuiWorldWindow : ImGuiWindow
{
    private GroupInspector? _playerInspector;
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

        if (_playerInspector == null)
        {
            _playerInspector = InspectorExt.GetInspectorForTarget(world.Player);
        }
        _playerInspector.Draw();

        ImGui.End();
    }
}
