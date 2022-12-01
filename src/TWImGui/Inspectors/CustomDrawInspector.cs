namespace MyGame.TWImGui.Inspectors;

public class CustomDrawInspector : IInspectorWithTarget, IInspectorWithType, IInspectorWithMemberInfo
{
    public string? InspectorOrder { get; set; }
    private Action? _callback;

    private object? _target;
    private Type? _targetType;
    private MemberInfo? _memberInfo;
    private bool _isInitialized;

    private void Initialize()
    {
        var attr = _memberInfo?.GetCustomAttribute<CustomDrawInspectorAttribute>() ??
                   _targetType?.GetCustomAttribute<CustomDrawInspectorAttribute>() ??
                   _target?.GetType().GetCustomAttribute<CustomDrawInspectorAttribute>() ??
                   throw new Exception("Attribute not found");

        MethodInfo? methodInfo;
        if (attr.MethodName == null)
        {
            methodInfo = _memberInfo as MethodInfo ?? throw new Exception("Expected type to be MethodInfo");
        }
        else if (_memberInfo is MethodInfo info)
        {
            methodInfo = info;
        }
        else
        {
            var type = _targetType ?? _target?.GetType() ??
                (_memberInfo as FieldInfo)?.FieldType ??
                (_memberInfo as PropertyInfo)?.PropertyType ??
                throw new Exception("Could not get type");
            methodInfo = ReflectionUtils.GetMethodInfo(type, attr.MethodName) ?? throw new Exception("Draw method not found");
        }

        _callback = () => methodInfo.Invoke(_target, null);

        _isInitialized = true;
    }

    public void SetMemberInfo(MemberInfo memberInfo)
    {
        _memberInfo = memberInfo;
    }

    public void SetType(Type type)
    {
        _targetType = type;
    }

    public void SetTarget(object target)
    {
        _target = target;
    }

    public void Draw()
    {
        if (!_isInitialized)
            Initialize();

        _callback!.Invoke();
    }
}
