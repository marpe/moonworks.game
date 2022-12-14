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
    public static readonly RangeSettings UnsignedDefaultRangeSettings = new RangeSettings(0, uint.MaxValue, 1, true);

    private Func<object> _getter = null!;
    private Action<object> _setter = null!;

    public override void Initialize()
    {
        base.Initialize();

        _getter = () => GetValue()!;
        _setter = SetValue;
    }

    public static bool InspectFloat(string name, ref float value, RangeSettings rangeSettings, string format = "%.4g",
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        if (rangeSettings.UseDragVersion)
        {
            return ImGui.DragFloat(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.StepSize,
                rangeSettings.MinValue,
                rangeSettings.MaxValue,
                format,
                flags
            );
        }

        return ImGui.SliderFloat(
            ImGuiExt.LabelPrefix(name),
            ImGuiExt.RefPtr(ref value),
            rangeSettings.MinValue,
            rangeSettings.MaxValue,
            format,
            flags
        );
    }

    public static bool InspectInputUint(string name, ref uint value)
    {
        var data = (void*)ImGuiExt.RefPtr(ref value);
        var step = 1u;
        var stepFast = 10u;
        var stepPtr = (void*)ImGuiExt.RefPtr(ref step);
        var stepFastPtr = (void*)ImGuiExt.RefPtr(ref stepFast);
        return ImGui.InputScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, data, stepPtr, stepFastPtr, "%u", ImGuiInputTextFlags.None);
    }

    public static bool InspectInputInt(string name, ref int value)
    {
        return ImGui.InputInt(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value));
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

    public static bool InspectUInt(string name, ref uint value, RangeSettings? rangeSettings = null)
    {
        rangeSettings ??= UnsignedDefaultRangeSettings;

        var valuePtr = (void*)ImGuiExt.RefPtr(ref value);
        var stepSize = (uint)rangeSettings.Value.StepSize;
        var minValue = (uint)rangeSettings.Value.MinValue;
        var minValuePtr = (void*)ImGuiExt.RefPtr(ref minValue);
        var maxValue = (uint)rangeSettings.Value.MaxValue;
        var maxValuePtr = (void*)ImGuiExt.RefPtr(ref maxValue);

        if (rangeSettings.Value.UseDragVersion)
            return ImGui.DragScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, valuePtr, stepSize, minValuePtr, maxValuePtr, "%u");

        return ImGui.SliderScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, valuePtr, minValuePtr, maxValuePtr, "%u");
    }

    public static bool InspectString(string name, ref string value)
    {
        var result = false;
        var inputBuffer = new ImGuiInputBuffer(value, 100);
        fixed (byte* data = inputBuffer.Bytes)
        {
            if (ImGui.InputText(ImGuiExt.LabelPrefix(name), data, (nuint)inputBuffer.MaxLength, ImGuiInputTextFlags.AutoSelectAll))
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

        if (ImGuiExt.BeginCollapsingHeader(name, ImGuiExt.Colors[5], ImGuiTreeNodeFlags.None, ImGuiFont.Tiny, typeof(Rectangle).Name))
        {
            if (isReadOnly)
            {
                ImGui.BeginDisabled();
            }

            var position = value.Location.ToNumerics();
            if (ImGuiExt.InspectNumVector2("##Position", ref position, "X", "", "Y", "", 1f, 0f, 0f, "%u"))
            {
                value.X = (int)position.X;
                value.Y = (int)position.Y;
                result = true;
            }

            var size = value.Size.ToNumerics();
            if (ImGuiExt.InspectNumVector2("##Size", ref size, "W", "Width", "H", "Height"))
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

    public static bool InspectColor(string label, ref Color color, Color? refColor = null, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
    {
        return ImGuiExt.ColorEdit(ImGuiExt.LabelPrefix(label, true), ref color, refColor, flags);
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

    public static bool DrawSimpleInspector(Type type, string name, ref object value, bool isReadOnly = false, RangeSettings? rangeSettings = null)
    {
        rangeSettings ??= DefaultRangeSettings;
        var result = false;

        if (type == typeof(Rectangle))
        {
            var rectValue = (Rectangle)value;
            result |= InspectRectangle(name, ref rectValue, isReadOnly);
        }

        if (isReadOnly)
        {
            ImGui.BeginDisabled();
        }

        if (type == typeof(int))
        {
            var tmpValue = (int)value;
            result |= InspectInt(name, ref tmpValue, rangeSettings.Value);
            if (result) value = tmpValue;
        }
        else if (type == typeof(uint))
        {
            var tmpValue = (uint)value;
            result |= InspectUInt(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(ulong))
        {
            var tmpValue = (ulong)value;
            result |= InspectULong(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(bool))
        {
            var tmpValue = (bool)value;
            result |= InspectBool(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(string))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            var tmpValue = (string)(value ?? "");
            result |= InspectString(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(Point))
        {
            var tmpValue = (Point)value;
            result |= InspectPoint(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(UPoint))
        {
            var tmpValue = (UPoint)value;
            var tuple = new ValueTuple<int, int>((int)tmpValue.X, (int)tmpValue.Y);
            var xy = &tuple;
            if (ImGui.DragInt2(ImGuiExt.LabelPrefix(name), xy, 1.0f, 0, 0, "%u"))
            {
                value = new UPoint((uint)xy->Item1, (uint)xy->Item2);
                result = true;
            }
        }
        else if (type == typeof(Vector2))
        {
            var tmpValue = (Vector2)value;
            result |= InspectVector2(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(Num.Vector2))
        {
            var tmpValue = (Num.Vector2)value;
            if (ImGui.DragFloat2(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref tmpValue), 1f, 0, 0, "%.4g"))
            {
                value = tmpValue;
                result = true;
            }
        }
        else if (type == typeof(Vector3))
        {
            var tmpValue = ((Vector3)value).ToNumerics();
            if (ImGui.DragFloat3(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref tmpValue), 1f, default, default, "%.6g"))
            {
                value = tmpValue.ToXNA();
                result = true;
            }
        }
        else if (type == typeof(Vector4))
        {
            var tmpValue = ((Vector4)value).ToNumerics();
            if (ImGui.DragFloat4(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref tmpValue), 1f, default, default, "%.6g"))
            {
                value = tmpValue.ToXNA();
                result = true;
            }
        }
        else if (type == typeof(Color))
        {
            var tmpValue = (Color)value;
            result |= InspectColor(name, ref tmpValue);
            if (result) value = tmpValue;
        }
        else if (type == typeof(float))
        {
            var tmpValue = (float)value;
            result |= InspectFloat(name, ref tmpValue, rangeSettings.Value);
            if (result) value = tmpValue;
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

    public static bool DrawSimpleInspector(Type type, string name, Func<object> getter, Action<object> setter, bool isReadOnly = false, RangeSettings? rangeSettings = null)
    {
        var value = getter();
        var result = false;
        if (DrawSimpleInspector(type, name, ref value, isReadOnly, rangeSettings))
        {
            setter(value);
            result = true;
        }

        return result;
    }

    public static bool InspectVector2(string name, ref Vector2 value)
    {
        var numValue = value.ToNumerics();
        var result = false;
        if (ImGui.DragFloat2(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref numValue), 1.0f, 0, 0, "%.4g"))
        {
            value = numValue.ToXNA();
            result = true;
        }

        return result;
    }

    public static bool InspectBool(string name, ref bool value)
    {
        return ImGuiExt.DrawCheckbox(ImGuiExt.LabelPrefix(name), ref value);
    }

    public static bool InspectPoint(string name, ref Point value)
    {
        var result = false;
        var tuple = new ValueTuple<int, int>(value.X, value.Y);
        var xy = &tuple;

        if (ImGui.DragInt2(ImGuiExt.LabelPrefix(name), xy, 1.0f, 0, 0, "%d"))
        {
            value = new Point(xy->Item1, xy->Item2);
            result = true;
        }

        return result;
    }
}
