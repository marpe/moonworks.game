namespace MyGame.Utils;

public static class TextureExt
{
    public static Point Size(this Texture t)
    {
        return new Point((int)t.Width, (int)t.Height);
    }
}
