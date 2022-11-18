using Mochi.DearImGui;
using MyGame.Utils;

namespace MyGame.TWImGui.Inspectors;

public unsafe class SimpleTypeInspector : Inspector
{
    public static Type[] SupportedTypes =
    {
        typeof(bool), typeof(Color), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(string),
        typeof(Point), typeof(UPoint), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Num.Vector2), typeof(Num.Vector3), typeof(Num.Vector4),
        typeof(Rectangle),
    };

    private bool InspectFloat(ref float value)
    {
        if (_rangeAttribute != null)
        {
            if (_rangeAttribute.UseDragVersion)
            {
                return ImGui.DragFloat(_name, ImGuiExt.RefPtr(ref value), _rangeAttribute.StepSize, _rangeAttribute.MinValue,
                    _rangeAttribute.MaxValue, "%g");
            }

            return ImGui.SliderFloat(_name, ImGuiExt.RefPtr(ref value), _rangeAttribute.MinValue, _rangeAttribute.MaxValue, "%.4g");
        }

        var stepSize = _stepSizeAttribute?.StepSize ?? 1f;
        return ImGui.DragFloat(_name, ImGuiExt.RefPtr(ref value), stepSize, 0, 0, "%g");
    }

    private bool InspectInt(ref int value)
    {
        if (_rangeAttribute != null)
        {
            if (_rangeAttribute.UseDragVersion)
            {
                return ImGui.DragInt(_name, ImGuiExt.RefPtr(ref value), _rangeAttribute.StepSize, (int)_rangeAttribute.MinValue,
                    (int)_rangeAttribute.MaxValue, default);
            }

            return ImGui.SliderInt(_name, ImGuiExt.RefPtr(ref value), (int)_rangeAttribute.MinValue, (int)_rangeAttribute.MaxValue, default);
        }

        var stepSize = _stepSizeAttribute != null ? _stepSizeAttribute.StepSize : 1f;
        return ImGui.DragInt(_name, ImGuiExt.RefPtr(ref value), stepSize, default, default, default);
    }

    private bool InspectUInt(ref uint value)
    {
        var result = false;
        var valuePtr = (void*)ImGuiExt.RefPtr(ref value);
        var name = (DearImGuiInterpolatedStringHandler)_name;
        if (ImGui.DragScalar(name, ImGuiDataType.U32, valuePtr, default, default, default, default))
        {
            result = true;
        }

        return result;
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
        }

        var hideInInspector = HideInInspectorCondition?.Invoke(_target) ?? false;
        if (hideInInspector)
        {
            return;
        }

        if (IsReadOnly)
        {
            ImGui.BeginDisabled();
        }

        if (_valueType == typeof(int))
        {
            var value = GetValue<int>();

            if (InspectInt(ref value))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(uint))
        {
            var value = GetValue<uint>();

            if (InspectUInt(ref value))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(bool))
        {
            var value = GetValue<bool>();

            if (ImGuiExt.DrawCheckbox(_name, ref value))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(string))
        {
            var value = GetValue<string>() ?? string.Empty;

            var inputBuffer = new ImGuiInputBuffer(value, 100);

            if (ImGui.InputText(_name, inputBuffer.Data, inputBuffer.Length))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(Point))
        {
            var value = GetValue<Point>();
            var tuple = new ValueTuple<int, int>(value.X, value.Y);
            var xy = &tuple;
            if (ImGui.DragInt2(_name, xy, 1.0f, 0, 0, "%11d"))
            {
                SetValue(new Point(xy->Item1, xy->Item2));
            }
        }
        else if (_valueType == typeof(UPoint))
        {
            var value = GetValue<UPoint>();
            var tuple = new ValueTuple<int, int>((int)value.X, (int)value.Y);
            var xy = &tuple;
            if (ImGui.DragInt2(_name, xy, 1.0f, 0, 0, "%u"))
            {
                SetValue(new UPoint((uint)xy->Item1, (uint)xy->Item2));
            }
        }
        else if (_valueType == typeof(Vector2))
        {
            var value = GetValue<Vector2>().ToNumerics();
            if (ImGui.DragFloat2(_name, ImGuiExt.RefPtr(ref value), 1.0f, 0, 0, "%g"))
            {
                SetValue(value.ToXNA());
            }
        }
        else if (_valueType == typeof(Num.Vector2))
        {
            var value = GetValue<Num.Vector2>();
            if (ImGui.DragFloat2(_name, ImGuiExt.RefPtr(ref value), 1f, 0, 0, "%g"))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(Vector3))
        {
            var value = GetValue<Vector3>().ToNumerics();
            if (ImGui.DragFloat3(_name, ImGuiExt.RefPtr(ref value), default, default, default, default))
            {
                SetValue(value.ToXNA());
            }
        }
        else if (_valueType == typeof(Vector4))
        {
            var value = GetValue<Vector4>().ToNumerics();
            if (ImGui.DragFloat4(_name, ImGuiExt.RefPtr(ref value), default, default, default, default))
            {
                SetValue(value.ToXNA());
            }
        }
        else if (_valueType == typeof(Rectangle))
        {
            ImGui.PushID(2);

            if (IsReadOnly)
            {
                ImGui.EndDisabled();
            }

            if (ImGuiExt.BeginCollapsingHeader(_name, Color.Transparent))
            {
                if (IsReadOnly)
                {
                    ImGui.BeginDisabled();
                }

                var value = GetValue<Rectangle>();

                var position = value.Location.ToNumerics();
                if (ImGuiExt.DrawXy("Position", ref position, "X", "Y", 1f, 0f, 0f, "%.6g"))
                {
                    value.X = (int)position.X;
                    value.Y = (int)position.Y;
                    SetValue(value);
                }

                var size = new Num.Vector2(value.Width, value.Height);
                if (ImGuiExt.DrawXy("Size", ref size, "Width", "Height"))
                {
                    value.Width = (int)size.X;
                    value.Height = (int)size.Y;
                    SetValue(value);
                }

                if (IsReadOnly)
                {
                    ImGui.EndDisabled();
                }

                ImGuiExt.EndCollapsingHeader();
            }

            if (IsReadOnly)
            {
                ImGui.BeginDisabled();
            }

            ImGui.PopID();
        }
        else if (_valueType == typeof(Color))
        {
            var value = GetValue<Color>();
            if (ImGuiExt.ColorEdit(_name, ref value))
            {
                SetValue(value);
            }
        }
        else if (_valueType == typeof(float))
        {
            var value = GetValue<float>();

            if (InspectFloat(ref value))
            {
                SetValue(value);
            }
        }
        else
        {
            if (ImGuiExt.DebugInspectors)
            {
                ImGui.TextColored(Color.Red.ToNumerics(), $"No inspector defined for type: {_valueType?.Name}");
            }
        }

        if (IsReadOnly)
        {
            ImGui.EndDisabled();
        }
    }
}
