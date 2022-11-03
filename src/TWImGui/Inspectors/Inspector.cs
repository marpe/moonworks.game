using ImGuiNET;

namespace MyGame.TWImGui.Inspectors;

public abstract class Inspector : IInspector
{
	protected string _name = string.Empty;

	public string Name
	{
		get => _name;
		set => _name = value;
	}

	public string? InspectorOrder { get; set; }
	public MemberInfo? MemberInfo => _memberInfo;

	private object? _cachedValue;
	protected Type? _valueType;
	protected MemberInfo? _memberInfo;
	protected bool IsReadOnly;

	protected object? _target;
	protected Type? _targetType;

	/// <summary>
	/// Will hide this inspector if the predicate evaluates to true
	/// </summary>
	public Predicate<object?>? HideInInspectorCondition;

	protected RangeAttribute? _rangeAttribute;
	protected StepSizeAttribute? _stepSizeAttribute;
	protected CustomDrawInspectorAttribute? _customDrawAttr;
	protected InspectorCallableAttribute? _callableAttr;

	public void SetTarget(object? target, Type targetType, MemberInfo? memberInfo = null)
	{
		_target = target;
		_targetType = targetType;
		_name = targetType.Name;
		if (memberInfo != null)
			SetTarget(memberInfo);
	}

	private void SetTarget(MemberInfo memberInfo)
	{
		_memberInfo = memberInfo;
		_name = memberInfo.Name;
		_rangeAttribute = memberInfo.GetCustomAttribute<RangeAttribute>();
		_stepSizeAttribute = memberInfo.GetCustomAttribute<StepSizeAttribute>();
		_customDrawAttr = memberInfo.GetCustomAttribute<CustomDrawInspectorAttribute>();
		_callableAttr = memberInfo.GetCustomAttribute<InspectorCallableAttribute>();
		HideInInspectorCondition = memberInfo.GetCustomAttribute<HideInInspectorAttribute>()?.Condition;

		if (memberInfo is FieldInfo field)
		{
			_valueType = field.FieldType;
			IsReadOnly = field.IsInitOnly;
		}
		else if (memberInfo is PropertyInfo prop)
		{
			_valueType = prop.PropertyType;
			var hasPrivateSet = prop.SetMethod?.IsPrivate ?? false;
			IsReadOnly = hasPrivateSet && !prop.IsDefined(typeof(InspectableAttribute)) || !prop.CanWrite;
		}
		else if (memberInfo is Type type)
		{
			_valueType = type;
			IsReadOnly = true;
		}
	}

	public virtual void Initialize()
	{
	}

	public abstract void Draw();

	protected virtual void DrawDebug()
	{
		var label = $"[{GetType().Name}] {Name}";

		if (ImGuiExt.Fold(label))
		{
			if (ImGuiExt.BeginPropTable("Props"))
			{
				ImGuiExt.PropRow("Target", _target?.GetType().Name ?? "NULL");
				ImGuiExt.PropRow("TargetType", _targetType?.Name ?? "NULL");
				ImGuiExt.PropRow("IsReadOnly", IsReadOnly.ToString());

				var memberType = _memberInfo switch
				{
					FieldInfo => "Field",
					PropertyInfo => "Prop",
					MethodInfo => "Method",
					_ => "NULL"
				};
				ImGuiExt.PropRow("MemberInfo", memberType);
				ImGuiExt.PropRow("ValueType", _valueType?.Name ?? "NULL");
				ImGuiExt.PropRow("InspectorOrder", InspectorOrder ?? "NULL");
				ImGui.EndTable();
			}

			ImGui.Separator();
		}
	}

	public object? GetValue()
	{
		var value = _memberInfo switch
		{
			FieldInfo fieldInfo => fieldInfo.GetValue(_target),
			PropertyInfo propInfo => propInfo.GetValue(_target),
			_ => _target
		};
		_cachedValue = value;
		return value;
	}

	public T? GetValue<T>()
	{
		var value = GetValue();
		return value == null ? default : (T)value;
	}

	public void SetValue<T>(T value)
	{
		if (IsReadOnly) return;

		if (_memberInfo is FieldInfo fieldInfo)
		{
			fieldInfo.SetValue(_target, value);
		}
		else if (_memberInfo is PropertyInfo propInfo)
		{
			propInfo.SetValue(_target, value);
		}
		else
		{
			Logger.LogError($"Value cannot be set since MemberInfo ({_memberInfo?.GetType().Name ?? "null" }) is neither FieldInfo nor PropertyInfo");
		}
	}

	public object? GetTarget()
	{
		return _target;
	}
}
