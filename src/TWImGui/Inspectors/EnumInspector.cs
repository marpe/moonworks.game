using ImGuiNET;

namespace MyGame.TWImGui.Inspectors;

public class EnumInspector : Inspector
{
    private Type _underlyingValueType = null!;
    private string[] _enumNames = Array.Empty<string>();
    private int[] _enumIntValues = Array.Empty<int>();
    private uint[] _enumUintValues = Array.Empty<uint>();
    private int _numValues = 0;
    private bool _isFlag;
    private Array _enumValues = Array.Empty<object>();
    private bool _isExpanded = false;

    public override void Initialize()
    {
        base.Initialize();
        if (_valueType == null) throw new InvalidOperationException();
        
        _isFlag = _valueType.GetCustomAttribute<FlagsAttribute>() != null;
        _enumNames = Enum.GetNames(_valueType);
        _underlyingValueType = Enum.GetUnderlyingType(_valueType);

        _enumValues = Enum.GetValues(_valueType);
        if (_underlyingValueType == typeof(int))
            _enumIntValues = _enumValues.Cast<int>().ToArray();
        else if (_underlyingValueType == typeof(uint))
            _enumUintValues = _enumValues.Cast<uint>().ToArray();
        else
            throw new InvalidOperationException("Unknown underlying enum value type");
        _numValues = _enumValues.Length;
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
        {
            DrawDebug();
        }

        if (IsReadOnly)
            ImGui.BeginDisabled();

        if (_isFlag)
        {
            var value = GetValue();

            var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                        ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.RowBg;

            if (ImGui.BeginTable("#Matrix", 2, flags))
            {
                ImGui.TableSetupColumn(Name);
                ImGui.TableSetupColumn(value.ToString());
                ImGui.TableHeadersRow();

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    _isExpanded = !_isExpanded;
                }

                ImGuiExt.ItemTooltip("Click to expand");

                if (_isExpanded)
                {
                    DrawRows(value);
                }

                ImGui.EndTable();
            }
        }
        else
        {
            var value = GetValue();
            var index = Array.IndexOf(_enumValues, value);

            if (ImGui.Combo(_name, ref index, _enumNames, _enumNames.Length))
            {
                SetValue(_enumValues.GetValue(index));
            }
        }

        if (IsReadOnly)
            ImGui.EndDisabled();
    }

    private void DrawRows(object value)
    {
        for (var i = 1; i < _numValues; i++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(_enumNames[i]);
            ImGui.TableNextColumn();

            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetStyle().Colors[(int)ImGuiCol.CheckMark]);

            if (_underlyingValueType == typeof(int))
            {
                var tmp = (int)value;
                if (ImGui.CheckboxFlags($"##{_enumNames[i]}", ref tmp, _enumIntValues[i]))
                {
                    SetValue(tmp);
                }
            }
            else if (_underlyingValueType == typeof(uint))
            {
                var tmp = (uint)value;
                if (ImGui.CheckboxFlags($"##{_enumNames[i]}", ref tmp, _enumUintValues[i]))
                {
                    SetValue(tmp);
                }
            }
            else
            {
                throw new InvalidOperationException("Unknown underlying enum value type");
            }

            ImGui.PopStyleColor();

            var isHovered = ImGui.IsItemHovered();
            if (isHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(_enumNames[i]);
                ImGui.EndTooltip();
            }
        }
    }
}