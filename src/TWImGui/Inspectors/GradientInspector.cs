using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public unsafe class GradientInspector : Inspector
{
    public override void Draw()
    {
        var value = GetValue();
        if (value == null)
        {
            return;
        }

        var grad = (Color[])value;
        var draw = ImGui.GetWindowDrawList();
        var gradientSize = new Num.Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());

        var p0 = ImGui.GetCursorScreenPos();
        var numSegments = grad.Length - 1;
        var segmentLength = gradientSize.X / numSegments;
        for (var i = 0; i < numSegments; i++)
        {
            var p1 = new Num.Vector2(p0.X + segmentLength, p0.Y + gradientSize.Y);
            draw->AddRectFilledMultiColor(
                p0,
                p1,
                grad[i].PackedValue,
                grad[i + 1].PackedValue,
                grad[i + 1].PackedValue,
                grad[i].PackedValue
            );
            p0.X = p1.X;
        }

        ImGui.InvisibleButton("##gradient", gradientSize);

        for (var i = 0; i < grad.Length; i++)
        {
            ImGuiExt.ColorEdit(i.ToString(), ref grad[i]);
        }

        if (ImGuiExt.ColoredButton("+"))
        {
            Array.Resize(ref grad, grad.Length + 1);
            SetValue(grad);
        }

        if (grad.Length - 1 > 0)
        {
            ImGui.SameLine();
            if (ImGuiExt.ColoredButton("-", Color.Red))
            {
                Array.Resize(ref grad, grad.Length - 1);
                SetValue(grad);
            }
        }
    }
}
