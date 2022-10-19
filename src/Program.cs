using System.Runtime.InteropServices;
using MoonWorks;

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
			WindowWidth = 1280,
			WindowHeight = 720,
			WindowTitle = "ProjectName",
			ScreenMode = ScreenMode.Windowed
		};
		var frameLimiterSettings = new FrameLimiterSettings
		{
			Mode = FrameLimiterMode.Uncapped,
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
