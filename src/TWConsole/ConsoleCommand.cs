﻿namespace MyGame.TWConsole;

public struct ConsoleCommandArg
{
    public string? Name;
    public object? DefaultValue;
    public bool HasDefaultValue;
    public Type Type;

    public ConsoleCommandArg(string? name, bool hasDefaultValue, object? defaultValue, Type type)
    {
        Name = name;
        HasDefaultValue = hasDefaultValue;
        DefaultValue = defaultValue;
        Type = type;
    }
}

public class ConsoleCommand : IComparable<ConsoleCommand>
{
    public delegate void ConsoleCommandHandler(TWConsole console, ConsoleCommand cmd, string[] args);

    public ConsoleCommandHandler Handler;
    public string Description;
    public string Key;
    public string[] Aliases;
    public ConsoleCommandArg[] Arguments;
    public bool IsCVar;

    public ConsoleCommand(string key, string description, ConsoleCommandHandler handler, ConsoleCommandArg[] args, string[] aliases, bool isCVar)
    {
        Key = key;
        Handler = handler;
        Description = description;
        Arguments = args;
        Aliases = aliases;
        IsCVar = isCVar;
    }

    public int CompareTo(ConsoleCommand? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return string.Compare(Key, other.Key, StringComparison.Ordinal);
    }
}