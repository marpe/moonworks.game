using Mochi.DearImGui;
using MyGame.Entities;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public static class CollisionDebug
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
        if (!Shared.Game.World.IsLoaded)
        {
            var player = Shared.Game.World.Entities.First<Player>();

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
    // private float _peakNumAddedSprites;
    private float _peakRenderDurationMs;
    public const string WindowTitle = "Debug";

    public DebugWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }

    private void UpdateMetrics()
    {
        _peakImGuiRenderDurationMs = StopwatchExt.SmoothValue(_peakImGuiRenderDurationMs, _editor._imGuiRenderDurationMs);
        _peakRenderDurationMs = StopwatchExt.SmoothValue(_peakRenderDurationMs, _editor._renderDurationMs);
        // _peakNumAddedSprites = StopwatchExt.SmoothValue(_peakNumAddedSprites, _editor.Renderer.SpriteBatch.LastNumAddedSprites);
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(200, 200), new Vector2(800, 850));
        var labelWidth = (int)(ImGui.GetContentRegionAvail().X * 0.4f);
        ImGuiExt.PushLabelWidth(labelWidth);
        if (ImGuiExt.Begin(WindowTitle, ref IsOpen))
        {
            var io = ImGui.GetIO();

            EnumInspector.InspectEnum("ActiveInput", ref MyEditorMain.PrevActiveInput);
            
            if (ImGui.BeginChild("PerformanceMetrics", new Vector2(0, 200)))
            {
                UpdateMetrics();
                ImGui.TextDisabled($"DrawFps"); ImGui.SameLine(labelWidth); ImGui.Text($"{_editor.Time.DrawFps}");
                ImGui.TextDisabled($"UpdateFps");ImGui.SameLine(labelWidth); ImGui.Text($"{_editor.Time.UpdateFps}");
                ImGui.TextDisabled($"Framerate");ImGui.SameLine(labelWidth); ImGui.Text($"{(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");
                ImGui.TextDisabled($"ImGuiRenderDuration");ImGui.SameLine(labelWidth); ImGui.Text($"{_peakImGuiRenderDurationMs:0.0} ms");
                ImGui.TextDisabled($"RenderDuration");ImGui.SameLine(labelWidth); ImGui.Text($"{_peakRenderDurationMs:0.0} ms");
                // ImGui.TextDisabled($"NumDrawCalls");ImGui.SameLine(labelWidth); ImGui.Text($"{_editor.Renderer.SpriteBatch.MaxDrawCalls:0.0}");
                // ImGui.TextDisabled($"AddedSprites");ImGui.SameLine(labelWidth); ImGui.Text($"{_peakNumAddedSprites:0}");
            }

            ImGui.EndChild();

            /*var rangeSettings = SimpleTypeInspector.UnsignedDefaultRangeSettings;
            rangeSettings.MinValue = 10;
            rangeSettings.MaxValue = 1000;
            rangeSettings.UseDragVersion = false;*/
        }

        ImGui.End();
        ImGuiExt.PopLabelWidth();
    }
}
