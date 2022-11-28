using System.Diagnostics.CodeAnalysis;

namespace MyGame.TWImGui;

public static class InspectorExt
{
    private static bool GetCustomAttributeInspector(MemberInfo type, bool checkInheritedCustomInspectorAttributes, [NotNullWhen(true)] out Inspector? inspector)
    {
        var customInspectorForFieldAttr = type.GetCustomAttribute<CustomInspectorAttribute>(checkInheritedCustomInspectorAttributes);
        if (customInspectorForFieldAttr != null)
        {
            inspector = ReflectionUtils.CreateInstance<Inspector>(customInspectorForFieldAttr.InspectorType);
            return true;
        }

        inspector = null;
        return false;
    }

    private static Inspector? GetInspectorForType(Type type, bool checkInheritedCustomInspectorAttributes)
    {
        if (GetCustomAttributeInspector(type, checkInheritedCustomInspectorAttributes, out var inspector))
        {
            return inspector;
        }

        if (type.IsEnum)
        {
            return new EnumInspector();
        }

        if (type.IsArray && type.GetArrayRank() == 1)
        {
            return new CollectionInspector();
        }

        if (type.IsGenericType && typeof(IList).IsAssignableFrom(type) && type.GetInterface(nameof(IList)) != null)
        {
            return new CollectionInspector();
        }

        if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type) && type.GetInterface(nameof(IDictionary)) != null)
        {
            return new CollectionInspector();
        }

        if (type == typeof(Color[]))
        {
            return new GradientInspector();
        }

        /*if (type == typeof(Timeline))
            return new TimelineInspector();*/
        if (SimpleTypeInspector.SupportedTypes.Contains(type))
        {
            return new SimpleTypeInspector();
        }

        return null;
    }

    public static IInspector GetGroupInspectorForTarget(object target)
    {
        var type = target.GetType();
        List<IInspector> inspectors = new();
        if (type.IsValueType)
        {
            var message = $"Attempted to get inspectors for object \"{target}\" which is of value type \"{type.Name}\", " +
                          "but objects of value types are not supported";
            Logger.LogError(message);
            return new PlaceholderInspector(message);
        }

        while (type != null && type != typeof(object))
        {
            // check if there's a custom inspector for this type
            var inspectorForType = GetInspectorForType(type, false);
            if (inspectorForType != null)
            {
                inspectorForType.SetTarget(target, type, null);
                inspectors.Add(inspectorForType);
                type = type.BaseType;
                continue;
            }

            // if no custom inspector was found iterate through fields and properties to create inspectors per member
            var inspector = GetInspectorForTargetAndType(target, type);
            if (inspector != null)
                inspectors.Add(inspector);

            type = type.BaseType;
        }

        inspectors.Reverse();

        if (inspectors.Count == 0)
        {
            return new PlaceholderInspector($"Could not find any inspectors for {target.ToString()} ({ReflectionUtils.GetDisplayName(target.GetType())})");
        }

        if (inspectors.Count == 1)
        {
            var first = inspectors[0];
            if (first is GroupInspector grpInspector)
            {
                grpInspector.Initialize();
            }
            return first;
        }

        var groupInspector = new GroupInspector(inspectors);
        groupInspector.SetTarget(target, target.GetType(), null);
        groupInspector.Initialize();
        return groupInspector;
    }

    /// <summary>
    /// type specifies at which subclass level of target to inspect. E. g if target is of type Car which inherits from Vehicle,
    /// then supplying typeof(Car) will only check for members declared at that level.  
    /// </summary>
    public static IInspector? GetInspectorForTargetAndType(object target, Type type)
    {
        // no inspector found for the "entire" type, iterate through fields and properties to create inspectors per member
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        // if (target != null)
        {
            bindingFlags |= BindingFlags.Instance;
        }

        var fields = type.GetFields(bindingFlags);
        var properties = type.GetProperties(bindingFlags);
        var methods = type.GetMethods(bindingFlags);
        var inspectors = new List<Inspector>();

        foreach (var field in fields)
        {
            if (!field.IsPublic && !field.IsDefined(typeof(InspectableAttribute)))
            {
                continue;
            }

            if (field.IsInitOnly && !field.IsDefined(typeof(InspectableAttribute)))
            {
                continue;
            }

            if (field.IsDefined(typeof(HideInInspectorAttribute)))
            {
                continue;
            }

            var inspector = GetInspectorForType(field.FieldType, false);
            if (inspector != null)
            {
                inspector.SetTarget(target, type, field);
                inspectors.Add(inspector);
            }
            else
            {
                var msg = $"Could not find an inspector for field: \"{field.Name}\" ({ReflectionUtils.GetDisplayName(field.FieldType)}), " +
                          $"target: \"{target}\" ({ReflectionUtils.GetDisplayName(target.GetType())})";
                Logger.LogWarn(msg);
            }
        }

        foreach (var prop in properties)
        {
            if (!prop.CanRead)
            {
                continue;
            }

            if (prop.GetMethod != null && !prop.GetMethod.IsPublic && !prop.IsDefined(typeof(InspectableAttribute)))
            {
                continue;
            }

            if (prop.IsDefined(typeof(HideInInspectorAttribute)))
            {
                continue;
            }

            var inspector = GetInspectorForType(prop.PropertyType, false);
            if (inspector != null)
            {
                inspector.SetTarget(target, type, prop);
                inspectors.Add(inspector);
            }
            else
            {
                var msg = $"Could not find an inspector for property: \"{prop.Name}\" ({ReflectionUtils.GetDisplayName(prop.PropertyType)}), " +
                          $"target: \"{target}\" ({ReflectionUtils.GetDisplayName(target.GetType())})";
                Logger.LogWarn(msg);
            }
        }

        foreach (var methodInfo in methods)
        {
            var hideAttr = methodInfo.GetCustomAttribute<HideInInspectorAttribute>(false);
            if (hideAttr != null)
            {
                continue;
            }

            var customDraw = methodInfo.GetCustomAttribute<CustomDrawInspectorAttribute>(false);
            if (customDraw != null)
            {
                var customDrawInspector = new CustomDrawInspector();
                customDrawInspector.SetTarget(target, type, methodInfo);
                inspectors.Add(customDrawInspector);
                continue;
            }

            var callable = methodInfo.GetCustomAttribute<InspectorCallableAttribute>(false);
            if (callable != null)
            {
                var methodInspector = new InspectorCallableInspector();
                methodInspector.SetTarget(target, type, methodInfo);
                inspectors.Add(methodInspector);
            }
        }

        if (inspectors.Count == 0)
        {
            return null;
        }

        if (inspectors.Count == 1)
        {
            return inspectors[0];
        }

        for (var i = 0; i < inspectors.Count; i++)
        {
            var inspector = inspectors[i];
            if (string.IsNullOrEmpty(inspector.InspectorOrder))
            {
                inspector.InspectorOrder = i.ToString();
            }
        }

        var ordered = inspectors.OrderByNatural(i => i.InspectorOrder).Cast<IInspector>().ToList();
        var groupInspector = new GroupInspector(ordered);
        groupInspector.SetTarget(target, type, null);
        return groupInspector;
    }
}
