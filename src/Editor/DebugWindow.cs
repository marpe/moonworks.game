using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class DebugWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    public const string WindowTitle = "Debug";

    public DebugWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }
    
    private static void DrawCollision(CollisionResult collision)
    {
        ImGuiExt.PropRow("Direction", collision.Direction.ToString());
        ImGuiExt.PropRow("PreviousPosition", collision.PreviousPosition.ToString());
        ImGuiExt.PropRow("Position", collision.Position.ToString());
        ImGuiExt.PropRow("Intersection", collision.Intersection.ToString());
    }
    
    public override void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        if (ImGuiExt.Begin(WindowTitle, ref IsOpen))
        {
            var io = ImGui.GetIO();
            ImGui.TextUnformatted($"DrawFps: {_editor.Time.DrawFps}");
            ImGui.TextUnformatted($"UpdateFps: {_editor.Time.UpdateFps}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");
            ImGui.TextUnformatted($"NumDrawCalls: {_editor.Renderer.SpriteBatch.MaxDrawCalls}, AddedSprites: {_editor.Renderer.SpriteBatch.MaxNumAddedSprites}");
            ImGui.SliderInt("UpdateRate", ImGuiExt.RefPtr(ref _editor.UpdateRate), 1, 10, default);

            if (ImGuiExt.BeginCollapsingHeader("FancyText", ImGuiExt.Colors[0]))
            {
                ImGui.SliderFloat("ShakeSpeed", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeSpeed), 0, 500, default);
                ImGui.SliderFloat("ShakeAmount", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeAmount), 0, 10, default);    
                ImGui.SliderFloat("WaveAmplitudeScale", ImGuiExt.RefPtr(ref FancyTextComponent.WaveAmplitudeScale), 0, 10, default);    
                ImGuiExt.EndCollapsingHeader();
            }

            ImGui.Separator();

            if (Shared.Game.GameScreen.World != null)
            {
                var player = Shared.Game.GameScreen.World.Player;

                if (ImGuiExt.BeginCollapsingHeader("Ground", ImGuiExt.Colors[0]))
                {
                    ImGui.Text("GroundCollisions");

                    foreach (var collision in player.Mover.GroundCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("GroundCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGui.Text("ContinuedGroundCollisions");

                    foreach (var collision in player.Mover.ContinuedGroundCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("ContinuedGroundCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGuiExt.EndCollapsingHeader();
                }

                if (ImGuiExt.BeginCollapsingHeader("Move", ImGuiExt.Colors[0]))
                {
                    ImGui.Text("MoveCollisions");

                    foreach (var collision in player.Mover.MoveCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("MoveCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGui.Text("ContinuedMoveCollisions");
                    for (var i = 0; i < player.Mover.ContinuedMoveCollisions.Count; i++)
                    {
                        var collision = player.Mover.ContinuedMoveCollisions[i];
                        if (ImGuiExt.BeginPropTable("ContinuedMoveCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGuiExt.EndCollapsingHeader();
                }
            }

            /*_mainMenuInspector ??= InspectorExt.GetInspectorForTarget(Shared.Menus.MainMenuScreen);
            _mainMenuInspector.Draw();
            ImGui.SliderFloat("GoalPosition", ImGuiExt.RefPtr(ref Shared.Menus.MainMenuScreen.Spring.EquilibriumPosition), -1, 1, default);
            public Spring Spring = new();
            public Vector2 Position;
            public Vector2 Scale = Vector2.One;
            public float MoveOffset = 500;
            public Vector2 Size = new Vector2(50, 25);
            public float ScaleFactor = 2f;
            public Vector2 InitialPosition = new Vector2(960, 100);*/
        }

        ImGui.End();
    }
}
