namespace MyGame;

public struct TexturePage
{
    public Texture Texture;
    public int Width;
    public int Height;

    public TexturePage(Texture texture)
    {
        Texture = texture;
        Width = (int)texture.Width;
        Height = (int)texture.Height;
    }

    public TexturePage(Texture texture, int width, int height)
    {
        Texture = texture;
        Width = width;
        Height = height;
    }
}
