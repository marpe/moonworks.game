namespace MyGame.Utils;

public static class PointExt
{
    public static Vector2 ToVec2(this Point p)
    {
        return new Vector2(p.X, p.Y);
    }
}
