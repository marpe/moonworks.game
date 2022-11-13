namespace MyGame.Utils;

public static class ReflectionUtils
{
    public static T CreateInstance<T>(Type type) where T : notnull
    {
        return (T)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Received null value when creating an instance of type {type.Name}"));
    }

    public static MethodInfo? GetMethodInfo(object targetObject, string methodName)
    {
        return GetMethodInfo(targetObject.GetType(), methodName);
    }

    public static MethodInfo? GetMethodInfo(object targetObject, string methodName, Type[] parameters)
    {
        return GetMethodInfo(targetObject.GetType(), methodName, parameters);
    }

    public static MethodInfo? GetMethodInfo(Type type, string methodName, Type[]? parameters = null)
    {
        if (parameters == null)
        {
            return type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        return type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            Type.DefaultBinder,
            parameters, null);
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
