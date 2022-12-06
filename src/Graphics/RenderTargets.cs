namespace MyGame.Graphics;

public class RenderTargets
{
    private readonly GraphicsDevice _device;
    public static uint RenderScale = 1;

    public UPoint GameRenderSize => new(
        CompositeRender.Width / RenderScale,
        CompositeRender.Height / RenderScale
    );

    public RenderTarget CompositeRender;
    public RenderTarget MenuRender;
    public RenderTarget LightSource;
    public RenderTarget LightTarget;
    public RenderTarget GameRender;
    public RenderTarget ConsoleRender;

    public RenderTargets(GraphicsDevice device)
    {
        var createRtsTimer = Stopwatch.StartNew();
        _device = device;
        var compositeRenderSize = new UPoint(1920, 1080);
        // increase game render target size with 1 pixel if render at < 1920x1080 to enable smooth camera panning by offsetting the upscaled render
        var gameRenderSize = RenderScale == 1 ? compositeRenderSize : compositeRenderSize / (int)RenderScale + UPoint.One;
        var textureFlags = TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget;

        CompositeRender = new RenderTarget(Texture.CreateTexture2D(device, compositeRenderSize.X, compositeRenderSize.Y, TextureFormat.B8G8R8A8, textureFlags));
        GameRender = new RenderTarget(Texture.CreateTexture2D(device, gameRenderSize.X, gameRenderSize.Y, TextureFormat.B8G8R8A8, textureFlags));
        LightSource = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        LightTarget = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        MenuRender = new RenderTarget(TextureUtils.CreateTexture(device, CompositeRender));
        ConsoleRender = new RenderTarget(TextureUtils.CreateTexture(device, CompositeRender));
        createRtsTimer.StopAndLog("RenderTargets");
    }
}
