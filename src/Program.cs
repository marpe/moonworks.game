using System.Runtime.InteropServices;

namespace MyGame;

class Program
{
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool SetDllDirectory(string lpPathName);

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
