namespace MyGame;

public class TWConsoleLogger : ILogger
{
    public void LogVerbose(string str)
    {
        try
        {
            Shared.Console.Print($"^8{str}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void LogInfo(string text)
    {
        try
        {
            Shared.Console.Print($"^3{text}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void LogWarn(string text)
    {
        try
        {
            Shared.Console.Print($"^1{text}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void LogError(string text)
    {
        try
        {
            Shared.Console.Print($"^4{text}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
