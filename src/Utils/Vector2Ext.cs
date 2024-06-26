﻿namespace MyGame.Utils;

public static class Vector2Ext
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point ToPoint(this Vector2 self)
    {
        return new Point((int)self.X, (int)self.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UPoint ToUPoint(this Vector2 self)
    {
        return new UPoint((uint)self.X, (uint)self.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Floor(this Vector2 self)
    {
        return new Vector2(MathF.Floor(self.X), MathF.Floor(self.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Ceil(this Vector2 self)
    {
        return new Vector2(MathF.Ceil(self.X), MathF.Ceil(self.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Round(this Vector2 self)
    {
        return new Vector2(MathF.Round(self.X), MathF.Round(self.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToNormal(this Vector2 vector)
    {
        var lengthSquared = vector.X * vector.X + vector.Y * vector.Y;
        if (lengthSquared < MathF.Epsilon * MathF.Epsilon)
            return Vector2.Zero;
        return vector / MathF.Sqrt(lengthSquared);
    }

    /*public static void Transform(ref Vector2 position, ref Matrix3x2 matrix, out Vector2 result)
    {
        result.X = (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M31;
        result.Y = (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M32;
    }*/
}