namespace MyGame;

public class ColorConsole : ILogger
{
    public void LogVerbose(string text)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(text);
        Console.ResetColor();
    }
    
    public void LogInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public void LogWarn(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public void LogError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
