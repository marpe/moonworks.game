using System.Globalization;

namespace MyGame;

internal class Program
{
    private static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var windowCreateInfo = new WindowCreateInfo
        {
            WindowWidth = 1920,
            WindowHeight = 1080,
            WindowTitle = "ProjectName",
            ScreenMode = ScreenMode.Windowed,
            PresentMode = PresentMode.FIFO,
            SystemResizable = true,
        };
        var frameLimiterSettings = new FrameLimiterSettings
        {
            Mode = FrameLimiterMode.Uncapped,
            Cap = 120,
        };
        Logger.LogInfo = LogInfo;
        Logger.LogWarn = LogWarn;
        Logger.LogError = LogError;
        var gameMain = new MyGameMain(
            windowCreateInfo,
            frameLimiterSettings,
            MyGameMain.TARGET_TIMESTEP,
            true
        );
        gameMain.Run();
    }

    private static void LogInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void LogWarn(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void LogError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
