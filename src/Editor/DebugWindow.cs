using Mochi.DearImGui;

namespace MyGame.Editor;

public static unsafe class CollisionDebug
{
    private static void DrawCollision(CollisionResult collision)
    {
        ImGuiExt.PropRow("Direction", collision.Direction.ToString());
        ImGuiExt.PropRow("PreviousPosition", collision.PreviousPosition.ToString());
        ImGuiExt.PropRow("Position", collision.Position.ToString());
        ImGuiExt.PropRow("Intersection", collision.Intersection.ToString());
    }

    public static void DrawCollisionDebug()
    {
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
    }
}

public static unsafe class MiscDebug
{
    public static void FancyTextDebug()
    {
        if (ImGuiExt.BeginCollapsingHeader("FancyText", ImGuiExt.Colors[0]))
        {
            ImGui.SliderFloat("ShakeSpeed", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeSpeed), 0, 500, default);
            ImGui.SliderFloat("ShakeAmount", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeAmount), 0, 10, default);
            ImGui.SliderFloat("WaveAmplitudeScale", ImGuiExt.RefPtr(ref FancyTextComponent.WaveAmplitudeScale), 0, 10, default);
            ImGuiExt.EndCollapsingHeader();
        }
    }
}

public unsafe class DebugWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    private float _peakImGuiRenderDurationMs;
    private float _peakRenderDurationMs;
    private float _peakUpdateDurationMs;
    private float _peakNumAddedSprites;
    public const string WindowTitle = "Debug";

    public DebugWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }

    private void UpdateMetrics()
    {
        float SmoothValue(float prev, float current)
        {
            return prev > current ? MathF.Lerp(prev, current, 0.05f) : current;
        }

        _peakImGuiRenderDurationMs = SmoothValue(_peakImGuiRenderDurationMs, _editor._imGuiRenderDurationMs);
        _peakRenderDurationMs = SmoothValue(_peakRenderDurationMs, _editor._renderDurationMs);
        _peakUpdateDurationMs = SmoothValue(_peakUpdateDurationMs, _editor._updateDurationMs);
        _peakNumAddedSprites = SmoothValue(_peakNumAddedSprites, _editor.Renderer.SpriteBatch.LastNumAddedSprites);
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        if (ImGuiExt.Begin(WindowTitle, ref IsOpen))
        {
            var io = ImGui.GetIO();

            if (ImGui.BeginChild("PerformanceMetrics", new Num.Vector2(0, 300)))
            {
                UpdateMetrics();
                ImGui.TextUnformatted($"DrawFps: {_editor.Time.DrawFps}");
                ImGui.TextUnformatted($"UpdateFps: {_editor.Time.UpdateFps}");
                ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");
                ImGui.TextUnformatted($"ImGuiRenderDuration: {_peakImGuiRenderDurationMs:0.0} ms");
                ImGui.TextUnformatted($"RenderDuration: {_peakRenderDurationMs:0.0} ms");
                ImGui.TextUnformatted($"UpdateDuration: {_peakUpdateDurationMs:0.0} ms");
                ImGui.TextUnformatted($"NumDrawCalls: {_editor.Renderer.SpriteBatch.MaxDrawCalls}");
                ImGui.TextUnformatted($"AddedSprites: {_peakNumAddedSprites:0}");
                SimpleTypeInspector.InspectInt("UpdateRate", ref _editor.UpdateRate, new RangeSettings() { MinValue = 1, MaxValue = 10 });
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }
}
