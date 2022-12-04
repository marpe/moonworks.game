using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public abstract class Inspector : IInspectorWithTarget, IInspectorWithMemberInfo, IInspectorWithType
{
    protected MemberInfo? _memberInfo;
    protected string _name = string.Empty;

    protected RangeAttribute? _rangeAttribute;
    protected StepSizeAttribute? _stepSizeAttribute;

    protected object? _target;
    protected Type? _targetType;
    protected Type? _valueType;

    protected bool IsReadOnly;

    protected bool IsHidden;

    public bool IsInitialized { get; private set; }

    public string? InspectorOrder { get; set; }

    public abstract void Draw();

    public void SetType(Type type)
    {
        _targetType = type;
        if (_name == string.Empty)
            _name = type.Name;
    }

    public void SetTarget(object target)
    {
        _target = target;
        if (_name == string.Empty)
            _name = target.GetType().Name;
    }

    public void SetMemberInfo(MemberInfo memberInfo)
    {
        _memberInfo = memberInfo;
        _name = memberInfo.Name;
        _rangeAttribute = memberInfo.GetCustomAttribute<RangeAttribute>();
        _stepSizeAttribute = memberInfo.GetCustomAttribute<StepSizeAttribute>();
        IsReadOnly = memberInfo.GetCustomAttribute<ReadOnlyAttribute>() != null;
        IsHidden = memberInfo.GetCustomAttribute<HideInInspectorAttribute>() != null;

        if (memberInfo is FieldInfo field)
        {
            _valueType = field.FieldType;
            IsReadOnly |= field.IsInitOnly || field.IsLiteral;
        }
        else if (memberInfo is PropertyInfo prop)
        {
            _valueType = prop.PropertyType;
            var hasPrivateSet = prop.SetMethod?.IsPrivate ?? false;
            IsReadOnly |= (hasPrivateSet && !prop.IsDefined(typeof(InspectableAttribute))) || !prop.CanWrite;
        }
        else
        {
            var displayName = ReflectionUtils.GetDisplayName(memberInfo.GetType());
            throw new InvalidOperationException($"Unexpected memberInfo type: {displayName}");
        }
    }

    public virtual void Initialize()
    {
        if (IsInitialized)
            throw new InvalidOperationException("Inspector has already been initialized");
        IsInitialized = true;
    }

    protected virtual void DrawDebug()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Inspector has not been initialized");

        var label = $"[{GetType().Name}] {_name}";

        if (ImGuiExt.Fold(label))
        {
            if (ImGuiExt.BeginPropTable("Props"))
            {
                ImGuiExt.PropRow("Target", _target?.GetType().Name ?? "NULL");
                ImGuiExt.PropRow("TargetType", _targetType?.Name ?? "NULL");
                ImGuiExt.PropRow("IsReadOnly", IsReadOnly.ToString());
                ImGuiExt.PropRow("IsHidden", IsHidden.ToString());

                var memberType = _memberInfo switch
                {
                    FieldInfo => "Field",
                    PropertyInfo => "Prop",
                    MethodInfo => "Method",
                    _ => "NULL",
                };
                ImGuiExt.PropRow("MemberInfo", memberType);
                ImGuiExt.PropRow("ValueType", _valueType?.Name ?? "NULL");
                ImGuiExt.PropRow("InspectorOrder", InspectorOrder ?? "NULL");
                ImGui.EndTable();
            }

            ImGui.Separator();
        }
    }

    protected object? GetValue()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Inspector has not been initialized");
        var value = _memberInfo switch
        {
            FieldInfo fieldInfo => fieldInfo.GetValue(_target),
            PropertyInfo propInfo => propInfo.GetValue(_target),
            _ => _target,
        };
        return value;
    }

    protected void SetValue(object? value)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Inspector has not been initialized");
        if (IsReadOnly)
            return;

        if (_memberInfo is FieldInfo fieldInfo)
            fieldInfo.SetValue(_target, value);
        else if (_memberInfo is PropertyInfo propInfo)
            propInfo.SetValue(_target, value);
        else if (_memberInfo == null)
            throw new InvalidOperationException("Value cannot be set when MemberInfo is null");
        else
            throw new InvalidOperationException(
                "Value cannot be set if MemberInfo isn't of type FieldInfo or PropertyInfo"
            );
    }

    protected T? GetValue<T>()
    {
        var value = GetValue();
        return value == null ? default : (T)value;
    }

    protected void SetValue<T>(T value)
    {
        SetValue((object?)value);
    }
}
