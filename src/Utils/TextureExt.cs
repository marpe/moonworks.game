namespace MyGame.Utils;

public static class TextureExt
{
    public static UPoint Size(this Texture t)
    {
        return new UPoint(t.Width, t.Height);
    }
    
    public static Rectangle Bounds(this Texture t)
    {
        return new Rectangle(0,  0, (int)t.Width, (int)t.Height);
    }

    public static Texture CreateTexture(GraphicsDevice graphicsDevice, uint width, uint height)
    {
        return Texture.CreateTexture2D(graphicsDevice, width, height, TextureFormat.B8G8R8A8, TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
    }
}
