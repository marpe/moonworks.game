namespace MyGame.Graphics;

public struct Sprite
{
    public TextureSlice TextureSlice;
    public Bounds SrcRect;
    public UV UV;
    
    public Sprite(in TextureSlice textureSlice, in Bounds? srcRect = null)
    {
        TextureSlice = textureSlice;
        SrcRect = srcRect ?? textureSlice.Rectangle;
        GenerateUVs(ref UV, TextureSlice, SrcRect);
    }

    public static void GenerateUVs(ref UV uv, in Texture texture, in Bounds srcRect)
    {
        GenerateUVs(ref uv, texture.Width, texture.Height, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height);
    }

    public static void GenerateUVs(ref UV uv, in TextureSlice texture, in Bounds srcRect)
    {
        GenerateUVs(ref uv, texture.Texture.Width, texture.Texture.Height, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height);
    }
    
    public static void GenerateUVs(ref UV uv, in TextureSlice texture, in Rectangle srcRect)
    {
        GenerateUVs(ref uv, texture.Texture.Width, texture.Texture.Height, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height);
    }
    
    public static void GenerateUVs(ref UV uv, float textureWidth, float textureHeight, float srcX, float srcY,  float srcWidth, float srcHeight)
    {
        var posX = srcX / textureWidth;
        var posY = srcY / textureHeight;

        var dimX = srcWidth / textureWidth;
        var dimY = srcHeight / textureHeight;

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
