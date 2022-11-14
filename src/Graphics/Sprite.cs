namespace MyGame.Graphics;

public struct Sprite
{
    public Texture Texture;
    public Rectangle SrcRect;
    public UV UV;

    public Sprite(Texture texture) : this(texture, new Rectangle(0, 0, (int)texture.Width, (int)texture.Height))
    {
    }

    public Sprite(Texture texture, Rectangle srcRect)
    {
        Texture = texture;
        SrcRect = srcRect;
        UV = new UV();
        GenerateUVs(ref UV, texture, srcRect);
    }

    public static void GenerateUVs(ref UV uv, in Texture texture, in Rectangle srcRect)
    {
        var posX = (float)srcRect.X / texture.Width;
        var posY = (float)srcRect.Y / texture.Height;

        var dimX = (float)srcRect.Width / texture.Width;
        var dimY = (float)srcRect.Height / texture.Height;

        uv.Position.X = posX;
        uv.Position.Y = posY;

        uv.Dimensions.X = dimX;
        uv.Dimensions.Y = dimY;

        uv.TopLeft.X = uv.TopRight.X = uv.BottomLeft.X = uv.BottomRight.X = uv.Position.X;
        uv.TopLeft.Y = uv.TopRight.Y = uv.BottomLeft.Y = uv.BottomRight.Y = uv.Position.Y;

        uv.TopRight.X += uv.Dimensions.X;
        uv.BottomLeft.Y += uv.Dimensions.Y;
        uv.BottomRight.X += uv.Dimensions.X;
        uv.BottomRight.Y += uv.Dimensions.Y;
    }

    public static implicit operator Sprite(Texture texture) => new(texture);
}
