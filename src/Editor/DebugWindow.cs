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

    private static string LoadingIndicator(bool isLoading)
    {
        if (!isLoading)
            return "";
        var n = (int)(Shared.Game.Time.TotalElapsedTime * 4 % 4);
        return " " + n switch
        {
            0 => FontAwesome6.ArrowRight,
            1 => FontAwesome6.ArrowDown,
            2 => FontAwesome6.ArrowLeft,
            _ => FontAwesome6.ArrowUp,
        };
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
            ImGui.TextUnformatted($"MousePosRaw: {_editor.InputHandler.MousePositionRaw.ToString()}");
            ImGui.TextUnformatted($"MousePos: {_editor.InputHandler.MousePosition.ToString()}");
            ImGui.TextUnformatted($"MouseEnabled: {_editor.InputHandler.MouseEnabled.ToString()}");
            ImGui.TextUnformatted($"Nav: {(io->NavActive ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureMouse: {(io->WantCaptureMouse ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureKeyboard: {(io->WantCaptureKeyboard ? "Y" : "N")}");
            ImGui.TextUnformatted($"DrawFps: {_editor.Time.DrawFps}");
            ImGui.TextUnformatted($"UpdateFps: {_editor.Time.UpdateFps}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");

            ImGui.TextUnformatted($"NumDrawCalls: {_editor.Renderer.SpriteBatch.MaxDrawCalls}, AddedSprites: {_editor.Renderer.SpriteBatch.MaxNumAddedSprites}");

            ImGui.BeginDisabled(Shared.LoadingScreen.IsLoading);
            if (ImGui.Button("Reload World" + LoadingIndicator(Shared.LoadingScreen.IsLoading), default))
            {
                GameScreen.Restart();
            }

            ImGui.EndDisabled();

            ImGui.TextUnformatted("MouseWheel:" + ImGui.GetIO()->MouseWheel);

            ImGui.SliderFloat("ShakeSpeed", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeSpeed), 0, 500, default);
            ImGui.SliderFloat("ShakeAmount", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeAmount), 0, 500, default);
            ImGui.SliderInt("UpdateRate", ImGuiExt.RefPtr(ref _editor.UpdateRate), 1, 10, default);

            ImGui.Separator();

            if (Shared.Game.GameScreen.World != null)
            {
                var player = Shared.Game.GameScreen.World.Player;

                if (ImGuiExt.BeginCollapsingHeader("Ground", Color.LightBlue))
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

                if (ImGuiExt.BeginCollapsingHeader("Move", Color.LightBlue))
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
