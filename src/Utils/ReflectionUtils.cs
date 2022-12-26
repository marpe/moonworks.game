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

    public static IEnumerable<Type> GetAllSubclassesOfType<T>()
    {
        var assembly = Assembly.GetEntryAssembly();
        var types = assembly?.GetTypes();
        return types == null ? new List<Type>() : types.Where(t => t.IsSubclassOf(typeof(T)));
    }
}
