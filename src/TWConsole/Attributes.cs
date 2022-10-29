namespace MyGame.TWConsole;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class CVarAttribute : Attribute
{
	public string Name;
	public string Description;

	public CVarAttribute(string name, string description)
	{
		Name = name;
		Description = description;
	}
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ConsoleHandlerAttribute : Attribute
{
	public string Command;
	public string Description;
	public string[] Aliases;

	public ConsoleHandlerAttribute(string command) : this(command, string.Empty, Array.Empty<string>())
	{
	}
	
	public ConsoleHandlerAttribute(string command, string description) : this(command, description, Array.Empty<string>())
	{
	}
	
	public ConsoleHandlerAttribute(string command, string description, string[] aliases)
	{
		Description = description;
		Command = command;
		Aliases = aliases;
	}
}
