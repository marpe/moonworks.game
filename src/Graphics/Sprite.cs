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
        var position = new Vector2((float)srcRect.X / texture.Width, (float)srcRect.Y / texture.Height);
        var dimensions = new Vector2((float)srcRect.Width / texture.Width, (float)srcRect.Height / texture.Height);
        
        uv.Position = position;
        uv.Dimensions = dimensions;
        uv.TopLeft = uv.Position;
        uv.TopRight = uv.Position + new Vector2(uv.Dimensions.X, 0);
        uv.BottomLeft = uv.Position + new Vector2(0, uv.Dimensions.Y);
        uv.BottomRight = uv.Position + new Vector2(uv.Dimensions.X, uv.Dimensions.Y);
    }
}
