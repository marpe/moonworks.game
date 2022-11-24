namespace MyGame.Utils;

public static class Vector2Ext
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point ToPoint(this Vector2 self)
    {
        return new Point((int)self.X, (int)self.Y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToVec2Int(this Vector2 self)
    {
        return new Vector2((int)self.X, (int)self.Y);
    }
}
