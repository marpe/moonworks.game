namespace MyGame.Graphics;

public struct Sprite
{
    public TextureSlice TextureSlice;
    public Bounds SrcRect;
    public UV UV;
    
    public Sprite(Texture texture) : this(texture, new Rectangle(0, 0, (int)texture.Width, (int)texture.Height))
    {
    }

    public Sprite(TextureSlice textureSlice) : this(textureSlice.Texture, textureSlice.Rectangle)
    {
    }

    public Sprite(Texture texture, Rectangle srcRect) : this(texture, (Bounds)srcRect)
    {
    }
    
    
    public Sprite(TextureSlice textureSlice, Rectangle srcRect) : this(textureSlice, (Bounds)srcRect)
    {
    }

    public Sprite(TextureSlice textureSlice, Bounds srcRect)
    {
        TextureSlice = textureSlice;
        SrcRect = srcRect;
        UV = new UV();
        GenerateUVs(ref UV, textureSlice, srcRect);
    }

    public static void GenerateUVs(ref UV uv, in TextureSlice texture, in Bounds srcRect)
    {
        var posX = srcRect.X / texture.Rectangle.W;
        var posY = srcRect.Y / texture.Rectangle.H;

        var dimX = srcRect.Width / texture.Rectangle.W;
        var dimY = srcRect.Height / texture.Rectangle.H;

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
