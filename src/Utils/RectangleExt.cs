namespace MyGame.Utils;

public static class RectangleExt
{
    public static Point Min(this Rectangle rect)
    {
        return new Point(rect.X, rect.Y);
    }

    public static Vector2 MinVec(this Rectangle rect)
    {
        return new Vector2(rect.X, rect.Y);
    }

    public static Point BottomLeft(this Rectangle rect)
    {
        return new Point(rect.X, rect.Y + rect.Height);
    }

    public static Vector2 BottomLeftVec(this Rectangle rect)
    {
        return new Vector2(rect.X, rect.Y + rect.Height);
    }

    public static Point TopRight(this Rectangle rect)
    {
        return new Point(rect.X + rect.Width, rect.Y);
    }
    
    public static Vector2 TopRightVec(this Rectangle rect)
    {
        return new Vector2(rect.X + rect.Width, rect.Y);
    }

    public static Point Max(this Rectangle rect)
    {
        return new Point(rect.X + rect.Width, rect.Y + rect.Height);
    }

    public static Vector2 MaxVec(this Rectangle rect)
    {
        return new Vector2(rect.X + rect.Width, rect.Y + rect.Height);
    }

    public static Rectangle FromTexture(Texture texture)
    {
        return new Rectangle(0, 0, (int)texture.Width, (int)texture.Height);
    }

    public static Rectangle FromPositionAndSize(Vector2 position, Vector2 size)
    {
        return FromPositionAndSize(position, size, Vector2.Half);
    }

    public static Rectangle FromPositionAndSize(Vector2 position, Vector2 size, Vector2 origin)
    {
        var offset = size * origin;
        return new Rectangle((int)(position.X - offset.X), (int)(position.Y - offset.Y), (int)size.X, (int)size.Y);
    }

    public static Rectangle FromFloats(float x, float y, float width, float height)
    {
        return new Rectangle((int)x, (int)y, (int)width, (int)height);
    }

    public static Rectangle FromMinMax(Vector2 min, Vector2 max)
    {
        return new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));
    }
}
