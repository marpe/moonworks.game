namespace MyGame.TWImGui;

/// <summary>Attribute that is used to indicate that the field/property should be present in the inspector</summary>
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
    public HideInInspectorAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ReadOnlyAttribute : Attribute
{
}

public record struct RangeSettings(float MinValue, float MaxValue, float StepSize, bool UseDragVersion);

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class RangeAttribute : Attribute
{
    public RangeSettings Settings;

    public RangeAttribute(float minValue, float maxValue, float stepSize = .1f, bool useDragFloat = false)
    {
        Settings = new RangeSettings(minValue, maxValue, stepSize, useDragFloat);
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

public abstract class CustomInspectorAttribute : Attribute
{
    public Type InspectorType;

    protected CustomInspectorAttribute(Type inspectorType)
    {
        InspectorType = inspectorType;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
public class CustomInspectorAttribute<T> : CustomInspectorAttribute where T : IInspector
{
    public CustomInspectorAttribute() : base(typeof(T))
    {
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
