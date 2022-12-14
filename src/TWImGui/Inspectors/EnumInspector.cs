using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public class EnumCacheEntry
{
    public Type EnumType;
    public Type UnderlyingValueType;
    public string[] Names;
    public int[] ValuesAsInt;
    public bool IsFlag;
    public Dictionary<int, string> CachedFlagNames = new();

    public EnumCacheEntry(Type enumType)
    {
        EnumType = enumType;
        Names = Enum.GetNames(enumType);
        var values = Enum.GetValues(enumType);
        UnderlyingValueType = Enum.GetUnderlyingType(enumType);
        ValuesAsInt = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            ValuesAsInt[i] = (int)(values.GetValue(i) ?? throw new Exception());
        }

        IsFlag = enumType.IsDefined(typeof(FlagsAttribute));
    }
}

public unsafe class EnumInspector : IInspectorWithTarget, IInspectorWithMemberInfo, IInspectorWithType
{
    private static StringBuilder _sb = new();
    private static Dictionary<Type, EnumCacheEntry> _enumCache = new();
    public string? InspectorOrder { get; set; }
    private static bool _isExpanded;
    private bool _isReadOnly;

    private Type? _type;
    private MemberInfo? _memberInfo;
    private object? _target;
    private string _name = "Enum";

    private Type? _enumType;
    
    private bool _isInitialized;

    public void SetType(Type type)
    {
        _type = type;
    }

    public void SetMemberInfo(MemberInfo memberInfo)
    {
        _memberInfo = memberInfo;
    }

    public void SetTarget(object target)
    {
        _target = target;
    }

    private static EnumCacheEntry GetOrCreateCacheEntry(Type type)
    {
        if (_enumCache.TryGetValue(type, out var cachedEntry))
        {
            return cachedEntry;
        }

        var entry = new EnumCacheEntry(type);
        _enumCache[type] = entry;
        return entry;
    }

    private void Initialize()
    {
        if (_memberInfo is FieldInfo field)
        {
            _isReadOnly = field.IsInitOnly || field.IsDefined(typeof(ReadOnlyAttribute));
            _name = field.Name;
            _enumType = field.FieldType;
        }
        else if (_memberInfo is PropertyInfo prop)
        {
            _isReadOnly = !prop.CanWrite || prop.IsDefined(typeof(ReadOnlyAttribute));
            _name = prop.Name;
            _enumType = prop.PropertyType;
        }
        else
            throw new Exception();

        _isInitialized = true;
    }

    private int GetValue()
    {
        if (_memberInfo is FieldInfo field)
            return (int)(field.GetValue(_target) ?? throw new Exception());

        if (_memberInfo is PropertyInfo prop)
            return (int)(prop.GetValue(_target) ?? throw new Exception());

        throw new Exception();
    }

    private void SetValue(int value)
    {
        if (_memberInfo is FieldInfo field)
            field.SetValue(_target, value);
        else if (_memberInfo is PropertyInfo prop)
            prop.SetValue(_target, value);
        else
            throw new Exception();
    }

    public void Draw()
    {
        if (!_isInitialized)
            Initialize();

        var entry = GetOrCreateCacheEntry(_enumType ?? throw new Exception($"{nameof(_enumType)} cannot be null"));
        var value = GetValue();

        ImGui.BeginDisabled(_isReadOnly);

        if (InspectEnum(_name, ref value, entry, false))
        {
            SetValue(value);
        }

        ImGui.EndDisabled();
    }

    private static bool InspectEnum(string label, ref int value, EnumCacheEntry entry, bool useButtons = true)
    {
        if (useButtons)
        {
            ImGuiExt.LabelPrefix(label); ImGui.NewLine();
            return ImGuiExt.EnumButtons(entry.Names, entry.ValuesAsInt, ref value, entry.IsFlag, ImGuiExt.Colors[0]);
        }
        
        if (entry.IsFlag)
        {
            var flagName = GetFlagName(value, entry);
            return DrawFlag(label, ref value, flagName, entry);
        }
        
        return DrawCombo(label, ref value, entry);
    }

    private static string GetFlagName(int value, EnumCacheEntry entry)
    {
        if (entry.CachedFlagNames.TryGetValue(value, out var flagName))
        {
            return flagName;
        }

        if (value == 0)
        {
            var index = Array.IndexOf(entry.ValuesAsInt, value);
            return index != -1 ? entry.Names[index] : "";
        }
        
        _sb.Clear();
        for (var i = 0; i < entry.ValuesAsInt.Length; i++)
        {
            var entryValue = entry.ValuesAsInt[i];
            if ((value & entryValue) == entryValue)
            {
                if (_sb.Length > 0)
                    _sb.Append(" | ");
                _sb.Append(entry.Names[i]);
            }
        }

        var name = _sb.ToString();
        entry.CachedFlagNames[value] = name;
        return name;
    }

    private static bool DrawCombo(string label, ref int value, EnumCacheEntry entry)
    {
        var result = false;
        var index = Array.IndexOf(entry.ValuesAsInt, value);
        if (ImGui.BeginCombo(ImGuiExt.LabelPrefix(label), entry.Names[index]))
        {
            for (var i = 0; i < entry.Names.Length; i++)
            {
                var isSelected = i == index;
                if (ImGui.Selectable(entry.Names[i], isSelected, ImGuiSelectableFlags.None, default))
                {
                    value = entry.ValuesAsInt[i];
                    result = true;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return result;
    }

    private static bool DrawFlag(string label, ref int value, string flagName, EnumCacheEntry entry)
    {
        ImGuiExt.LabelPrefix($"{FontAwesome6.FlagCheckered} {label}");

        var icon = _isExpanded ? FontAwesome6.AngleDown : FontAwesome6.AngleRight;
        if (ImGuiExt.TextButton($"{icon} {flagName}", "Click to expand"))
            _isExpanded = !_isExpanded;

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                    ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.RowBg;

        if (!_isExpanded)
            return false;

        var result = false;
        if (ImGui.BeginTable("#Matrix", 2, flags, default))
        {
            ImGui.TableSetupColumn(label);
            ImGui.TableSetupColumn(flagName);

            result |= DrawFlagRows(entry, ref value);

            ImGui.EndTable();
        }

        return result;
    }

    public static bool InspectEnum<T>(string label, ref T value, bool useButtons = false) where T : Enum
    {
        var entry = GetOrCreateCacheEntry(typeof(T));
        var tmp = (int)(object)value;
        if (InspectEnum(label, ref tmp, entry, useButtons))
        {
            value = (T)(object)tmp;
            return true;
        }

        return false;
    }

    private static bool DrawFlagRows(EnumCacheEntry entry, ref int flagValue)
    {
        var result = false;
        for (var i = 1; i < entry.Names.Length; i++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(entry.Names[i]);
            ImGui.TableNextColumn();

            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetStyle()->Colors[(int)ImGuiCol.CheckMark]);

            result |= ImGui.CheckboxFlags($"##{entry.Names[i]}", ImGuiExt.RefPtr(ref flagValue), entry.ValuesAsInt[i]);

            ImGui.PopStyleColor();

            var isHovered = ImGui.IsItemHovered();
            if (isHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(entry.Names[i]);
                ImGui.EndTooltip();
            }
        }

        return result;
    }
}
