using JetBrains.Annotations;

namespace MyGame.TWConsole;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class CVarAttribute : Attribute
{
    public string Description;
    public string Name;

    public CVarAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ConsoleHandlerAttribute : Attribute
{
    public string[] Aliases;
    public string Command;
    public string Description;

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
