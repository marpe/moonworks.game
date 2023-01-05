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

    public static void GenerateUVs(ref UV uv, in TextureSlice texture, in Bounds srcRect)
    {
        var textureRect = texture.Rectangle;
        
        var posX = srcRect.X / textureRect.W;
        var posY = srcRect.Y / textureRect.H;

        var dimX = srcRect.Width / textureRect.W;
        var dimY = srcRect.Height / textureRect.H;

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
