using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.TWImGui.Inspectors;

public unsafe class SimpleTypeInspector : Inspector
{
    public static Type[] SupportedTypes =
    {
        typeof(bool), typeof(Color), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(string),
        typeof(Point), typeof(UPoint), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(System.Numerics.Vector2), typeof(System.Numerics.Vector3),
        typeof(System.Numerics.Vector4),
        typeof(Rectangle),
    };

    [CVar("imgui.hide_readonly", "Toggle visibility of readonly values in inspector")]
    public static bool HideReadOnly = true;

    public static readonly RangeSettings DefaultRangeSettings = new(-(ImGuiExt.FLT_MAX / 2), ImGuiExt.FLT_MAX / 2, 1, true);
    public static readonly RangeSettings UnsignedDefaultRangeSettings = new(0, uint.MaxValue, 1, true);

    private Func<object> _getter = null!;
    private Action<object> _setter = null!;

    public override void Initialize()
    {
        base.Initialize();

        _getter = () => GetValue()!;
        _setter = SetValue;
    }

    public static void DrawFieldTooltip(string tooltip)
    {
        var textSize = ImGui.CalcTextSize(tooltip, true);
        if (textSize.X > 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled | ImGuiHoveredFlags.DelayNormal))
        {
            ImGui.BeginTooltip();
            var min = ImGui.GetCursorScreenPos();
            var max = min + textSize;
            ImGuiInternal.RenderTextClipped(min, max, tooltip, &textSize, new System.Numerics.Vector2(0, 0.5f));
            ImGui.Dummy(textSize);
            ImGui.EndTooltip();
        }
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
            return result;
        }

        if (isReadOnly)
        {
            ImGui.BeginDisabled();
        }

        if (type == typeof(int))
        {
            var tmpValue = (int)value;
            result |= InspectInt(name, ref tmpValue, rangeSettings.Value);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(uint))
        {
            var tmpValue = (uint)value;
            result |= InspectUInt(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(ulong))
        {
            var tmpValue = (ulong)value;
            result |= InspectULong(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(bool))
        {
            var tmpValue = (bool)value;
            result |= InspectBool(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(string))
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            var tmpValue = (string)(value ?? "");
            result |= InspectString(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(Point))
        {
            var tmpValue = (Point)value;
            result |= InspectPoint(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(UPoint))
        {
            var tmpValue = (UPoint)value;
            result |= InspectUPoint(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(System.Numerics.Vector2))
        {
            var tmpValue = (System.Numerics.Vector2)value;
            result |= InspectNumVector2(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(System.Numerics.Vector3))
        {
            var tmpValue = (System.Numerics.Vector3)value;
            result |= InspectNumVector3(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(System.Numerics.Vector4))
        {
            var tmpValue = (System.Numerics.Vector4)value;
            result |= InspectNumVector4(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(Vector2))
        {
            var tmpValue = (Vector2)value;
            result |= InspectVector2(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(Vector3))
        {
            var tmpValue = (Vector3)value;
            result |= InspectVector3(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(Vector4))
        {
            var tmpValue = (Vector4)value;
            result |= InspectVector4(name, ref tmpValue);
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(Color))
        {
            var tmpValue = (Color)value;
            result |= InspectColor(name, ref tmpValue, "");
            if (result)
            {
                value = tmpValue;
            }
        }
        else if (type == typeof(float))
        {
            var tmpValue = (float)value;
            result |= InspectFloat(name, ref tmpValue, rangeSettings.Value);
            if (result)
            {
                value = tmpValue;
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

    public static bool DrawSimpleInspector(Type type, string name, Func<object> getter, Action<object> setter, bool isReadOnly = false,
        RangeSettings? rangeSettings = null)
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


    #region Inspectors

    public static bool InspectULong(string name, ref ulong value)
    {
        var result = false;
        var valuePtr = (void*)ImGuiExt.RefPtr(ref value);
        if (ImGui.DragScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U64, valuePtr, default, default, default, default, ImGuiSliderFlags.None))
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

            var size = new Num.Vector2(value.Width, value.Height);
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

    public static bool InspectColor(string name, ref Color color, string tooltip = "", Color? refColor = null,
        ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
    {
        var result = ImGuiExt.ColorEdit(ImGuiExt.LabelPrefix(name), ref color, refColor, flags);
        if (tooltip != "" && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }

        return result;
    }


    public static bool InspectPercentage(string name, ref float value)
    {
        var tmpValue = value * 100f;

        var result = ImGui.SliderFloat(
            ImGuiExt.LabelPrefix(name),
            ImGuiExt.RefPtr(ref tmpValue),
            0,
            100,
            "%.1f%%",
            ImGuiSliderFlags.AlwaysClamp
        );

        if (result)
        {
            value = tmpValue / 100f;
        }

        return result;
    }

    public static bool InspectFloat(string name, ref float value, RangeSettings rangeSettings, string format = "%.4g",
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        var result = false;
        if (rangeSettings.UseDragVersion)
        {
            result |= ImGui.DragFloat(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.StepSize,
                rangeSettings.MinValue,
                rangeSettings.MaxValue,
                format,
                flags
            );
        }
        else
        {
            result |= ImGui.SliderFloat(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.MinValue,
                rangeSettings.MaxValue,
                format,
                flags
            );
        }

        return result;
    }

    public static bool InspectInputUint(string name, ref uint value, int* step = null, int* stepFast = null,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.CharsDecimal)
    {
        var data = (void*)ImGuiExt.RefPtr(ref value);
        var stepPtr = (void*)step;
        var stepFastPtr = (void*)stepFast;
        var result = ImGui.InputScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, data, stepPtr, stepFastPtr, "%u", flags);
        return result;
    }

    public static bool InspectInputInt(string name, ref int value, int step = 0, int stepFast = 0, ImGuiInputTextFlags flags = ImGuiInputTextFlags.CharsDecimal)
    {
        var result = ImGui.InputInt(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), step, stepFast, flags);
        return result;
    }

    public static bool InspectInt(string name, ref int value, RangeSettings rangeSettings)
    {
        var result = false;
        if (rangeSettings.UseDragVersion)
        {
            result |= ImGui.DragInt(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                rangeSettings.StepSize,
                (int)rangeSettings.MinValue,
                (int)rangeSettings.MaxValue,
                default,
                ImGuiSliderFlags.AlwaysClamp
            );
        }
        else
        {
            result |= ImGui.SliderInt(
                ImGuiExt.LabelPrefix(name),
                ImGuiExt.RefPtr(ref value),
                (int)rangeSettings.MinValue,
                (int)rangeSettings.MaxValue,
                default,
                ImGuiSliderFlags.AlwaysClamp
            );
        }

        return result;
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

        var result = false;
        var flags = ImGuiSliderFlags.AlwaysClamp;

        if (rangeSettings.Value.UseDragVersion)
        {
            result |= ImGui.DragScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, valuePtr, stepSize, minValuePtr, maxValuePtr, "%u", flags);
        }
        else
        {
            result |= ImGui.SliderScalar(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, valuePtr, minValuePtr, maxValuePtr, "%u", flags);
        }

        return result;
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

    public static bool InspectNumVector2(string name, ref System.Numerics.Vector2 value)
    {
        var result = ImGui.DragFloat2(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), 1.0f, 0, 0, "%.4g");
        return result;
    }


    public static bool InspectVector2(string name, ref Vector2 value)
    {
        var numValue = value.ToNumerics();
        var result = InspectNumVector2(name, ref numValue);
        if (result)
        {
            value = numValue.ToXNA();
        }

        return result;
    }

    public static bool InspectNumVector3(string name, ref System.Numerics.Vector3 value)
    {
        var result = ImGui.DragFloat3(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), 1f, default, default, "%.6g");
        return result;
    }

    public static bool InspectNumVector4(string name, ref System.Numerics.Vector4 value)
    {
        var result = ImGui.DragFloat4(ImGuiExt.LabelPrefix(name), ImGuiExt.RefPtr(ref value), 1f, default, default, "%.6g");
        return result;
    }

    public static bool InspectVector3(string name, ref Vector3 value)
    {
        var numValue = value.ToNumerics();
        var result = InspectNumVector3(name, ref numValue);
        if (result)
        {
            value = numValue.ToXNA();
        }

        return result;
    }

    public static bool InspectVector4(string name, ref Vector4 value)
    {
        var numValue = value.ToNumerics();
        var result = InspectNumVector4(name, ref numValue);
        if (result)
        {
            value = numValue.ToXNA();
        }

        return result;
    }

    public static bool InspectBool(string name, ref bool value, int labelWidth = -1)
    {
        var label = ImGuiExt.LabelPrefix(name, labelWidth);
        var result = false;

        if (ImGui.IsItemClicked())
        {
            result = true;
            value = !value;
            ImGuiInternal.NavMoveRequestCancel();
        }

        result |= ImGuiExt.DrawCheckbox(label, ref value);

        return result;
    }

    public static bool InspectPoint(string name, ref Point value, float speed = 1.0f, int minValue = 0, int maxValue = 0)
    {
        var result = false;
        var tuple = new ValueTuple<int, int>(value.X, value.Y);
        var xy = &tuple;

        if (ImGui.DragInt2(ImGuiExt.LabelPrefix(name), xy, speed, minValue, maxValue, "%d"))
        {
            value = new Point(xy->Item1, xy->Item2);
            result = true;
        }

        return result;
    }

    public static bool InspectUPoint(string name, ref UPoint value, float speed = 1.0f, int minValue = 0, int maxValue = 0)
    {
        var tuple = (value.X, value.Y);
        var xy = &tuple;
        var result = false;
        var minValuePtr = minValue < maxValue ? ImGuiExt.RefPtr(ref minValue) : null;
        var maxValuePtr = minValue < maxValue ? ImGuiExt.RefPtr(ref maxValue) : null;
        if (ImGui.DragScalarN(ImGuiExt.LabelPrefix(name), ImGuiDataType.U32, xy, 2, speed, minValuePtr, maxValuePtr, "%u", ImGuiSliderFlags.AlwaysClamp))
        {
            value = new UPoint(xy->Item1, xy->Item2);
            result = true;
        }

        return result;
    }

    #endregion
}
