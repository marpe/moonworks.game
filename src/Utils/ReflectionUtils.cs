namespace MyGame.Utils;

public static class ReflectionUtils
{
    public static string GetDisplayName(Type? type)
    {
        if (type == null)
            return "Global";
        if (type.DeclaringType != null)
            return type.DeclaringType.Name + "." + type.Name;
        return type.Name;
    }

    public static T CreateInstance<T>(Type type) where T : notnull
    {
        return (T)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Received null value when creating an instance of type {type.Name}"));
    }

    public static MethodInfo? GetMethodInfo(Type type, string methodName, Type[]? parameters = null)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Static | BindingFlags.DeclaredOnly;

        if (parameters == null)
        {
            return type.GetMethod(methodName, flags);
        }

        return type.GetMethod(
            methodName,
            flags,
            Type.DefaultBinder,
            parameters,
            null
        );
    }

    public static List<Type> GetAllSubclassesOfType<T>()
    {
        var assembly = Assembly.GetEntryAssembly();
        var sceneTypes = new List<Type>();
        var types = assembly?.GetTypes();

        if (types == null)
        {
            return sceneTypes;
        }

        foreach (var type in types)
        {
            if (type.IsSubclassOf(typeof(T)))
            {
                sceneTypes.Add(type);
            }
        }

        return sceneTypes;
    }
}
