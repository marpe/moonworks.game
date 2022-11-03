namespace MyGame.TWImGui;

/// <summary>
/// Attribute that is used to indicate that the field/property should be present in the inspector
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InspectableAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class InspectorCallableAttribute : Attribute
{
	public readonly string? Label;

	public InspectorCallableAttribute()
	{
	}

	public InspectorCallableAttribute(string label)
	{
		Label = label;
	}
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class HideInInspectorAttribute : Attribute
{
	public readonly Predicate<object?>? Condition;

	public HideInInspectorAttribute()
	{
	}

	public HideInInspectorAttribute(Predicate<object?> condition)
	{
		Condition = condition;
	}
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class RangeAttribute : Attribute
{
    public float MinValue;
    public float MaxValue;
    public float StepSize;
    public bool UseDragVersion;

    public RangeAttribute(float minValue, float maxValue, float stepSize = .1f, bool useDragFloat = false)
    {
        MinValue = minValue;
        MaxValue = maxValue;
        StepSize = stepSize;
        UseDragVersion = useDragFloat;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class StepSizeAttribute : Attribute
{
    public float StepSize;
    public StepSizeAttribute(float stepSize)
    {
        StepSize = stepSize;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
public class CustomInspectorAttribute : Attribute
{
	public Type InspectorType;

	public CustomInspectorAttribute(Type inspectorType)
	{
		InspectorType = inspectorType;
	}
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public class CustomDrawInspectorAttribute : Attribute
{
	public string? MethodName;

	public CustomDrawInspectorAttribute()
	{
	}

	public CustomDrawInspectorAttribute(string methodName)
	{
		MethodName = methodName;
	}
}

public class MenuItemAttribute : Attribute
{
	public string MenuPath;

	public MenuItemAttribute(string menuPath)
	{
		MenuPath = menuPath;
	}
}
