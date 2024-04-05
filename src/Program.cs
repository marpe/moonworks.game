using System.Globalization;

namespace MyGame;

internal class Program
{
    private static void Main(string[] args)
    {
        Logs.Initialize();

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        
        var windowCreateInfo = new WindowCreateInfo
        {
            WindowWidth = 1920,
            WindowHeight = 1080,
            WindowTitle = "ProjectName",
            ScreenMode = ScreenMode.Windowed,
            // PresentMode = PresentMode.FIFORelaxed,
            PresentMode = PresentMode.Immediate,
            SystemResizable = true,
        };
        var frameLimiterSettings = new FrameLimiterSettings
        {
            Mode = FrameLimiterMode.Uncapped,
            Cap = 120,
        };

        Game gameMain;
        if (args.Length > 0 && args[0] == "--editor")
        {
            gameMain = new MyEditorMain(
                windowCreateInfo,
                frameLimiterSettings,
                MyGameMain.TARGET_TIMESTEP,
                true
            );
        }
        else
        {
            /*gameMain = new TestGame(windowCreateInfo,
                frameLimiterSettings,
                MyGameMain.TARGET_TIMESTEP,
                true
            );*/
            gameMain = new MyGameMain(
                windowCreateInfo,
                frameLimiterSettings,
                MyGameMain.TARGET_TIMESTEP,
                true
            );
        }

        // gameMain.MainWindow.IsMaximized = true;

        gameMain.Run();
    }

}
