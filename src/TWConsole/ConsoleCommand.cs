namespace MyGame.TWConsole;

public class ConsoleCommand : IComparable<ConsoleCommand>
{
    public delegate void ConsoleCommandHandler(TWConsole console, ConsoleCommand cmd, string[] args);

    public ConsoleCommandHandler Handler;
    public string Description;
    public string Key;
    public List<string> Arguments = new();

    public ConsoleCommand(string key, string description, ConsoleCommandHandler handler)
    {
        Key = key;
        Handler = handler;
        Description = description;
    }

    public ConsoleCommand Arg(string argumentDescription)
    {
        Arguments.Add(argumentDescription);
        return this;
    }

    public int CompareTo(ConsoleCommand? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return string.Compare(Key, other.Key, StringComparison.Ordinal);
    }
}
