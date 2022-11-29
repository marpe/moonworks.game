namespace MyGame.Utils;

public static class PointExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToVec2(this Point self)
    {
        return new Vector2(self.X, self.Y);
    }
}
