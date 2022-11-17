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
}
