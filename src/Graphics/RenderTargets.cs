namespace MyGame.Graphics;

public class RenderTargets
{
    public UPoint GameSize => new(480, 270);
    public UPoint CompositeSize => new(1920, 1080);
    public int RenderScale => (int)(CompositeSize / GameSize).X;

    public RenderTarget CompositeRender;
    public RenderTarget MenuRender;
    public RenderTarget LightBase;
    public RenderTarget LevelBase;
    public RenderTarget NormalLights;
    public RenderTarget RimLights;
    public RenderTarget GameRender;
    public RenderTarget ConsoleRender;

    public RenderTargets(GraphicsDevice device)
    {
        var createRtsTimer = Stopwatch.StartNew();
        var compositeRenderSize = CompositeSize;
        var textureFlags = TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget;
        CompositeRender = new RenderTarget(Texture.CreateTexture2D(device, compositeRenderSize.X, compositeRenderSize.Y, TextureFormat.B8G8R8A8, textureFlags));

        var highRes = true;
        var gameRenderSize = highRes ? CompositeRender.Size : GameSize;

        // increase game render target size with 1 pixel to enable smooth camera panning by offsetting the upscaled render
        gameRenderSize += UPoint.One * (uint)RenderScale;
        
        GameRender = new RenderTarget(Texture.CreateTexture2D(device, gameRenderSize.X, gameRenderSize.Y, TextureFormat.B8G8R8A8, textureFlags));
        NormalLights = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        LightBase = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        LevelBase = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        RimLights = new RenderTarget(TextureUtils.CreateTexture(device, GameRender));
        //
        MenuRender = new RenderTarget(TextureUtils.CreateTexture(device, CompositeRender));
        ConsoleRender = new RenderTarget(TextureUtils.CreateTexture(device, CompositeRender));
        createRtsTimer.StopAndLog("RenderTargets");
    }
}
