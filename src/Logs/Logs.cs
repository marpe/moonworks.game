using System.Collections.Concurrent;

namespace MyGame;

public static class Logs
{
    public static ConcurrentBag<ILogger> Loggers = new();

    [CVar("log_level", "Set log level (3 = everything, 2 = info, warnings and errors, 1 = warnings and errors, 0 = only errors", false)]
    public static int LogLevel = 3;

    public static void Initialize()
    {
        Loggers.Add(new ColorConsole());

        Logger.LogInfo = LogInfo;
        Logger.LogWarn = LogWarn;
        Logger.LogError = LogError;
    }

    public static void LogVerbose(string str)
    {
        if (LogLevel < 3)
            return;
        foreach (var logger in Loggers)
        {
            logger.LogVerbose(str);
        }
    }
    
    public static void LogInfo(string str)
    {
        if (LogLevel < 2)
            return;
        foreach (var logger in Loggers)
        {
            logger.LogInfo(str);
        }
    }
    
    public static void LogWarn(string str)
    {
        if (LogLevel < 1)
            return;
        foreach (var logger in Loggers)
        {
            logger.LogWarn(str);
        }
    }
    
    public static void LogError(string str)
    {
        foreach (var logger in Loggers)
        {
            logger.LogError(str);
        }
    }
}
