using MoonWorks;
using MoonWorks.Graphics;

namespace MyGame;

public class MyGameMain : Game
{
	public MyGameMain(
		WindowCreateInfo windowCreateInfo,
		FrameLimiterSettings frameLimiterSettings,
		bool debugMode
	) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
	{
		// Insert your game initialization logic here.
	}

	protected override void Update(System.TimeSpan dt)
	{
		// Insert your game update logic here.
	}

	protected override void Draw(double alpha)
	{
		// Replace this with your own drawing code.

		var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
		var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

		commandBuffer.BeginRenderPass(
			new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
		);

		commandBuffer.EndRenderPass();

		GraphicsDevice.Submit(commandBuffer);
	}

	protected override void Destroy()
	{

	}
}
