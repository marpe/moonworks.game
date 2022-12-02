using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public unsafe class SimpleTypeInspector : Inspector
{
    public static Type[] SupportedTypes =
    {
        typeof(bool), typeof(Color), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(string),
        typeof(Point), typeof(UPoint), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Num.Vector2), typeof(Num.Vector3), typeof(Num.Vector4),
        typeof(Rectangle),
    };

    [CVar("imgui.hide_readonly", "Toggle visibility of readonly values in inspector")]
    public static bool HideReadOnly = true;

    public static readonly RangeSettings DefaultRangeSettings = new RangeSettings(-(ImGuiExt.FLT_MAX / 2), ImGuiExt.FLT_MAX / 2, 1, true);

    private Func<object> _getter = null!;
    private Action<object> _setter = null!;

    public override void Initialize()
    {
        base.Initialize();

        _getter = () => GetValue()!;
        _setter = SetValue;
    }

    public static bool InspectFloat(string name, ref float value, RangeSettings rangeSettings)
    {
        if (rangeSettings.UseDragVersion)
        {
            return ImGui.DragFloat(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.StepSize,
                rangeSettings.MinValue,
                rangeSettings.MaxValue,
                "%g"
            );
        }

        return ImGui.SliderFloat(
            ImGuiExt.LabelPrefix(name),
            ImGuiExt.RefPtr(ref value),
            rangeSettings.MinValue,
            rangeSettings.MaxValue,
            "%.4g"
        );
    }


    public static bool InspectInt(string name, ref int value, RangeSettings rangeSettings)
    {
        if (rangeSettings.UseDragVersion)
        {
            return ImGui.DragInt(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.StepSize,
                (int)rangeSettings.MinValue,
                (int)rangeSettings.MaxValue,
                default
            );
        }

        return ImGui.SliderInt(
            ImGuiExt.LabelPrefix(name),
            ImGuiExt.RefPtr(ref value),
            (int)rangeSettings.MinValue,
            (int)rangeSettings.MaxValue,
            default
        );
    }

    public static bool InspectUInt(string name, ref uint value)
    {
        var result = false;
        var valuePtr = (void*)ImGuiExt.RefPtr(ref value);
        if (ImGui.DragScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, valuePtr, default, default, default, default))
        {
            result = true;
        }

        return result;
    }

    public static bool InspectString(string name, ref string value)
    {
        var result = false;
        var inputBuffer = new ImGuiInputBuffer(value, 100);
        fixed (byte* data = inputBuffer.Bytes)
        {
            if (ImGui.InputText(ImGuiExt.LabelPrefix(name), data, (nuint)inputBuffer.MaxLength))
            {
                value = ImGuiExt.StringFromPtr(data);
                result = true;
            }
        }
        inputBuffer.Dispose();
        return result;
    } 
    
    public static bool InspectULong(string name, ref ulong value)
    {
        var result = false;
        var valuePtr = (void*)ImGuiExt.RefPtr(ref value);
        if (ImGui.DragScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U64, valuePtr, default, default, default, default))
        {
            result = true;
        }

        return result;
    }

    public static bool InspectRectangle(string name, ref Rectangle value, bool isReadOnly)
    {
        var result = false;

        if (ImGuiExt.BeginCollapsingHeader(name, ImGuiExt.Colors[0], ImGuiTreeNodeFlags.None, ImGuiFont.Tiny))
        {
            if (isReadOnly)
            {
                ImGui.BeginDisabled();
            }

            var position = value.Location.ToNumerics();
            if (ImGuiExt.DrawXy("Position", ref position, "X", "Y", 1f, 0f, 0f, "%.6g"))
            {
                value.X = (int)position.X;
                value.Y = (int)position.Y;
                result = true;
            }

            var size = value.Size.ToNumerics();
            if (ImGuiExt.DrawXy("Size", ref size, "W", "H"))
            {
                value.Width = (int)size.X;
                value.Height = (int)size.Y;
                result = true;
            }

            if (isReadOnly)
            {
                ImGui.EndDisabled();
            }

            ImGuiExt.EndCollapsingHeader();
        }

        return result;
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
            DrawDebug();

        if (IsHidden)
            return;

        if (HideReadOnly && IsReadOnly)
            return;

        DrawSimpleInspector(
            _valueType ?? throw new InvalidOperationException(),
            _name,
            _getter,
            _setter,
            IsReadOnly,
            _rangeAttribute?.Settings
        );
    }

    public static bool DrawSimpleInspector(Type type, string name, Func<object> getter, Action<object> setter, bool isReadOnly,
        RangeSettings? rangeSettings = null)
    {
        rangeSettings ??= DefaultRangeSettings;
        var result = false;

        if (type == typeof(Rectangle))
        {
            var value = (Rectangle)getter();

            if (InspectRectangle(name, ref value, isReadOnly))
            {
                setter(value);
                result = true;
            }

            return result;
        }

        if (isReadOnly)
        {
            ImGui.BeginDisabled();
        }

        if (type == typeof(int))
        {
            var value = (int)getter();

            if (InspectInt(name, ref value, rangeSettings.Value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(uint))
        {
            var value = (uint)getter();

            if (InspectUInt(name, ref value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(ulong))
        {
            var value = (ulong)getter();

            if (InspectULong(name, ref value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(bool))
        {
            var value = (bool)getter();

            if (ImGuiExt.DrawCheckbox(ImGuiExt.LabelPrefix(name), ref value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(string))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            var value = (string)(getter() ?? "");
            if (InspectString(name, ref value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(Point))
        {
            var value = (Point)getter();
            var tuple = new ValueTuple<int, int>(value.X, value.Y);
            var xy = &tuple;
            if (ImGui.DragInt2(ImGuiExt.LabelPrefix(name), xy, 1.0f, 0, 0, "%11d"))
            {
                setter(new Point(xy->Item1, xy->Item2));
                result = true;
            }
        }
        else if (type == typeof(UPoint))
        {
            var value = (UPoint)getter();
            var tuple = new ValueTuple<int, int>((int)value.X, (int)value.Y);
            var xy = &tuple;
            if (ImGui.DragInt2(ImGuiExt.LabelPrefix(name), xy, 1.0f, 0, 0, "%u"))
            {
                setter(new UPoint((uint)xy->Item1, (uint)xy->Item2));
                result = true;
            }
        }
        else if (type == typeof(Vector2))
        {
            var value = ((Vector2)getter()).ToNumerics();
            if (ImGui.DragFloat2(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), 1.0f, 0, 0, "%g"))
            {
                setter(value.ToXNA());
                result = true;
            }
        }
        else if (type == typeof(Num.Vector2))
        {
            var value = (Num.Vector2)getter();
            if (ImGui.DragFloat2(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), 1f, 0, 0, "%g"))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(Vector3))
        {
            var value = ((Vector3)getter()).ToNumerics();
            if (ImGui.DragFloat3(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), default, default, default, default))
            {
                setter(value.ToXNA());
                result = true;
            }
        }
        else if (type == typeof(Vector4))
        {
            var value = ((Vector4)getter()).ToNumerics();
            if (ImGui.DragFloat4(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), default, default, default, default))
            {
                setter(value.ToXNA());
                result = true;
            }
        }
        else if (type == typeof(Color))
        {
            var value = (Color)getter();
            if (ImGuiExt.ColorEdit(ImGuiExt.LabelPrefix(name, true), ref value))
            {
                setter(value);
                result = true;
            }
        }
        else if (type == typeof(float))
        {
            var value = (float)getter();

            if (InspectFloat(name, ref value, rangeSettings.Value))
            {
                setter(value);
                result = true;
            }
        }
        else if (ImGuiExt.DebugInspectors)
        {
            ImGui.TextColored(Color.Red.ToNumerics(), $"No inspector defined for type: {type.Name}");
        }

        if (isReadOnly)
        {
            ImGui.EndDisabled();
        }

        return result;
    }
}
