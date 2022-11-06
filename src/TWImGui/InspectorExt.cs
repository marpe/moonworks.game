using System.Diagnostics.CodeAnalysis;
using MyGame.TWImGui.Inspectors;
using MyGame.Utils;

namespace MyGame.TWImGui;

public static class InspectorExt
{
	private static bool GetCustomAttributeInspector(MemberInfo type, [NotNullWhen(true)] out Inspector? inspector)
	{
		var customInspectorForFieldAttr = type.GetCustomAttribute<CustomInspectorAttribute>(true);
		if (customInspectorForFieldAttr != null)
		{
			inspector = ReflectionUtils.CreateInstance<Inspector>(customInspectorForFieldAttr.InspectorType);
			return true;
		}

		inspector = null;
		return false;
	}

	private static Inspector? GetInspectorForType(Type type)
	{
		if (GetCustomAttributeInspector(type, out var inspector))
			return inspector;
		if (type.IsEnum)
			return new EnumInspector();
		if (type.IsArray && type.GetArrayRank() == 1)
			return new CollectionInspector();
		if (type.IsGenericType && typeof(IList).IsAssignableFrom(type) && type.GetInterface(nameof(IList)) != null)
			return new CollectionInspector();
		if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type) && type.GetInterface(nameof(IDictionary)) != null)
			return new CollectionInspector();
		if (type == typeof(Color[]))
			return new GradientInspector();
		/*if (type == typeof(Timeline))
			return new TimelineInspector();*/
		if (SimpleTypeInspector.SupportedTypes.Contains(type))
			return new SimpleTypeInspector();
		return null;
	}

	public static GroupInspector GetInspectorForTarget(object target)
	{
		var inspectors = GetInspectorsForTarget(target);
		var groupInspector = new GroupInspector(inspectors);
		groupInspector.SetTarget(target, target.GetType());
		groupInspector.Initialize();
		return groupInspector;
	}

	public static List<IInspector> GetInspectorsForTarget(object target)
	{
		var type = target.GetType();
		List<IInspector> inspectors = new();
		if (type.IsValueType)
		{
			Logger.LogError(
				$"Attempted to get inspectors for object \"{target}\" which is of value type \"{type.Name}\", but objects of value types are not supported");
			return inspectors;
			// throw new InvalidOperationException();
		}

		while (type != null && type != typeof(object))
		{
			var inspector = GetInspectorForTypeOrIntrospect(target, type);
			if (inspector != null)
				inspectors.Add(inspector);
			type = type.BaseType;
		}

		inspectors.Reverse();
		return inspectors;
	}

	private static Inspector? GetInspectorForMember(object? target, MemberInfo memberInfo)
	{
		var hideAttr = memberInfo.GetCustomAttribute<HideInInspectorAttribute>(false);
		if (hideAttr != null && hideAttr.Condition == null)
			return null;

		var valueType = memberInfo switch
		{
			FieldInfo fieldInfo => fieldInfo.FieldType,
			PropertyInfo propInfo => propInfo.PropertyType,
			_ => null
		};

		if (valueType == null)
			return null;

		return GetInspectorForType(valueType);
	}


	public static IInspector? GetInspectorForTypeOrIntrospect(object? target, Type type)
	{
		var typeInspector = GetInspectorForType(type);
		if (typeInspector != null)
		{
			typeInspector.SetTarget(target, type);
			typeInspector.Initialize();
			return typeInspector;
		}

		return GetInspectorForTargetAndType(target, type);
	}

	public static IInspector? GetInspectorForTargetAndType(object? target, Type type)
	{
		// no inspector found for the "entire" type, iterate through fields and properties to create inspectors per member
		var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
		if (target != null)
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
				continue;

			if (field.IsInitOnly && !field.IsDefined(typeof(InspectableAttribute)))
				continue;

			var inspector = GetInspectorForMember(target, field);
			if (inspector != null)
			{
				inspector.SetTarget(target, type, field);
				inspectors.Add(inspector);
			}
			else
			{
				Logger.LogWarn($"Could not find an inspector for field: {field}");
			}
		}

		foreach (var prop in properties)
		{
			if (!prop.CanRead)
				continue;

			if (prop.GetMethod != null && !prop.GetMethod.IsPublic && !prop.IsDefined(typeof(InspectableAttribute)))
				continue;

			var inspector = GetInspectorForMember(target, prop);
			if (inspector != null)
			{
				inspector.SetTarget(target, type, prop);
				inspectors.Add(inspector);
			}
			else
			{
				Logger.LogWarn($"Could not find an inspector for property: {prop}");
			}
		}

		foreach (var methodInfo in methods)
		{
			var inspector = GetInspectorForMember(target, methodInfo);
			if (inspector != null)
			{
				inspectors.Add(inspector);
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

		foreach (var inspector in inspectors)
			inspector.Initialize();

		if (inspectors.Count == 0)
			return null;

		if (inspectors.Count == 1)
			return inspectors[0];

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
		groupInspector.SetTarget(target, type);
		groupInspector.Initialize();
		return groupInspector;
	}
}
