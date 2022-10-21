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
		var gameMain = new MyGameMain(
			windowCreateInfo,
			frameLimiterSettings,
			true
		);
		gameMain.Run();
	}
}
