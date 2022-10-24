namespace MyGame.TWConsole;

public class CVar
{
	public readonly string Key;
	private readonly MemberInfo _memberInfo;
	public readonly object? DefaultValue;

	public CVar(string key, FieldInfo field)
	{
		Key = key;
		_memberInfo = field;
		DefaultValue = field.GetValue(null);
	}

	public CVar(string key, PropertyInfo prop)
	{
		Key = key;
		_memberInfo = prop;
		DefaultValue = prop.GetValue(null);
	}

	public Type VarType
	{
		get
		{
			var type = _memberInfo switch
			{
				FieldInfo fieldInfo => fieldInfo.FieldType,
				PropertyInfo propertyInfo => propertyInfo.PropertyType,
				_ => throw new InvalidOperationException("Invalid cvar member info")
			};
			return type;
		}
	}

	public string GetValue()
	{
		var value = GetValueRaw();
		return ConsoleUtils.ConvertToString(value);
	}

	public T GetValue<T>()
	{
		return (T)GetValueRaw()!;
	}

	public object? GetValueRaw()
	{
		var value = _memberInfo switch
		{
			FieldInfo field => field.GetValue(null),
			PropertyInfo prop => prop.GetValue(null),
			_ => throw new InvalidOperationException("Invalid cvar member info")
		};
		return value;
	}

	public void SetValue(object? value)
	{
		switch (_memberInfo)
		{
			case FieldInfo field:
			{
				field.SetValue(null, value);
				break;
			}
			case PropertyInfo prop:
			{
				prop.SetValue(null, value);
				break;
			}
			default:
				throw new InvalidOperationException("Invalid cvar member info");
		}
	}

	public void SetValue(string strValue)
	{
		var value = ConsoleUtils.ParseArg(VarType, strValue);
		SetValue(value);
	}
}
