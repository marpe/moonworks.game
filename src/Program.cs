namespace MyGame;

class Program
{
	static void Main(string[] args)
	{
		var windowCreateInfo = new WindowCreateInfo
		{
			WindowWidth = 1920,
			WindowHeight = 1080,
			WindowTitle = "ProjectName",
			ScreenMode = ScreenMode.Windowed,
			SystemResizable = true
		};
		var frameLimiterSettings = new FrameLimiterSettings
		{
			Mode = FrameLimiterMode.Capped,
			Cap = 60
		};
		Logger.LogInfo = LogInfo;
		Logger.LogWarn = LogWarn;
		Logger.LogError = LogError;
		var gameMain = new MyGameMain(
			windowCreateInfo,
			frameLimiterSettings,
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
