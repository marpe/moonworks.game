namespace MyGame.TWImGui;

public static class InspectorExt
{
    private static IInspector? GetCustomInspectorForType(MemberInfo type, bool checkInheritedAttributes)
    {
        var attr = type.GetCustomAttribute<CustomInspectorAttribute>(checkInheritedAttributes);
        return attr != null ? ReflectionUtils.CreateInstance<IInspector>(attr.InspectorType) : null;
    }

    private static IInspector? GetCustomDrawInspectorForType(MemberInfo type, bool checkInheritedAttributes)
    {
        var attr = type.GetCustomAttribute<CustomDrawInspectorAttribute>(checkInheritedAttributes);
        return attr != null ? new CustomDrawInspector() : null;
    }

    private static IInspector? GetInvokeMethodInspectorForType(MemberInfo type, bool checkInheritedAttributes)
    {
        var attr = type.GetCustomAttribute<InspectorCallableAttribute>(checkInheritedAttributes);
        return attr != null ? new InvokeMethodInspector() : null;
    }

    private static IInspector? GetInspectorForType(Type type, bool checkInheritedAttributes)
    {
        if (type.IsDefined(typeof(HideInInspectorAttribute), checkInheritedAttributes))
            return null;

        var drawInspector = GetCustomDrawInspectorForType(type, checkInheritedAttributes);
        if (drawInspector != null)
        {
            return drawInspector;
        }

        var customInspector = GetCustomInspectorForType(type, checkInheritedAttributes);
        if (customInspector != null)
        {
            return customInspector;
        }

        if (type.IsEnum)
        {
            return new EnumInspector();
        }

        if (type == typeof(Color[]))
        {
            return new GradientInspector();
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
                if (inspectorForType is IInspectorWithTarget withTarget)
                {
                    withTarget.SetTarget(target);
                }

                if (inspectorForType is IInspectorWithType withType)
                {
                    withType.SetType(type);
                }

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
                grpInspector.SetShowHeaders(true, 2);
            }

            return first;
        }

        var groupInspector = new GroupInspector(inspectors);
        groupInspector.SetTarget(target);
        groupInspector.Initialize();
        groupInspector.SetShowHeaders(true, 2);
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
        var inspectors = new List<IInspector>();

        foreach (var field in fields)
        {
            if (!field.IsPublic && !field.IsDefined(typeof(InspectableAttribute), false))
                continue;

            if (field.IsInitOnly && !field.IsDefined(typeof(InspectableAttribute), false))
                continue;

            if (field.IsDefined(typeof(HideInInspectorAttribute), false))
                continue;

            var inspector = GetCustomDrawInspectorForType(field, false) ??
                            GetCustomInspectorForType(field, false) ??
                            GetInspectorForType(field.FieldType, false);

            if (inspector != null)
            {
                if (inspector is IInspectorWithTarget withTarget)
                    withTarget.SetTarget(target);

                if (inspector is IInspectorWithMemberInfo withMemberInfo)
                    withMemberInfo.SetMemberInfo(field);

                if (inspector is IInspectorWithType withType)
                    withType.SetType(type);

                inspectors.Add(inspector);
            }
            else
            {
                var msg = $"Could not find an inspector for field: \"{field.Name}\" ({ReflectionUtils.GetDisplayName(field.FieldType)}), " +
                          $"target: \"{target}\" ({ReflectionUtils.GetDisplayName(target.GetType())})";
                Logs.LogVerbose(msg);
            }
        }

        foreach (var prop in properties)
        {
            if (!prop.CanRead)
                continue;

            if (prop.GetMethod != null && !prop.GetMethod.IsPublic && !prop.IsDefined(typeof(InspectableAttribute), false))
                continue;

            if (prop.IsDefined(typeof(HideInInspectorAttribute), false))
                continue;

            var inspector = GetCustomDrawInspectorForType(prop, false) ??
                            GetCustomInspectorForType(prop, false) ??
                            GetInspectorForType(prop.PropertyType, false);

            if (inspector != null)
            {
                if (inspector is IInspectorWithTarget withTarget)
                    withTarget.SetTarget(target);

                if (inspector is IInspectorWithMemberInfo withMemberInfo)
                    withMemberInfo.SetMemberInfo(prop);

                if (inspector is IInspectorWithType withType)
                    withType.SetType(type);

                inspectors.Add(inspector);
            }
            else
            {
                var msg = $"Could not find an inspector for property: \"{prop.Name}\" ({ReflectionUtils.GetDisplayName(prop.PropertyType)}), " +
                          $"target: \"{target}\" ({ReflectionUtils.GetDisplayName(target.GetType())})";
                Logs.LogVerbose(msg);
            }
        }

        foreach (var methodInfo in methods)
        {
            if (methodInfo.IsDefined(typeof(HideInInspectorAttribute), false))
                continue;

            var inspector = GetCustomDrawInspectorForType(methodInfo, false) ??
                            GetInvokeMethodInspectorForType(methodInfo, false);

            if (inspector != null)
            {
                if (inspector is IInspectorWithTarget withTarget)
                    withTarget.SetTarget(target);

                if (inspector is IInspectorWithMemberInfo withMemberInfo)
                    withMemberInfo.SetMemberInfo(methodInfo);

                if (inspector is IInspectorWithType withType)
                    withType.SetType(type);

                inspectors.Add(inspector);
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

        var ordered = inspectors.OrderByNatural(i => i.InspectorOrder).ToList();
        var groupInspector = new GroupInspector(ordered);
        groupInspector.SetTarget(target);
        groupInspector.SetType(type);
        return groupInspector;
    }
}
