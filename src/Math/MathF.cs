namespace MyGame;

public static class MathF
{
    public const float Epsilon = 0.00001f;
    public const float Deg2Rad = 0.017453292519943295769236907684886f;
    public const float Rad2Deg = 57.295779513082320876798154814105f;

    /// <summary>Takes an angle measured in radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float value)
    {
        return (float)Math.Cos(value);
    }

    /// <summary>Returns an angle measured in radians</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Acos(float value)
    {
        return (float)Math.Acos(value);
    }

    /// <summary>Takes an angle measured in radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float value)
    {
        return (float)Math.Sin(value);
    }

    /// <summary>Returns an angle measured in radians</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan2(float y, float x)
    {
        return (float)Math.Atan2(y, x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan(float d)
    {
        return (float)Math.Atan(d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float d)
    {
        return (float)Math.Tan(d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MinOf(float a, float b, float c)
    {
        return Math.Min(a, Math.Min(b, c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MinOf(float a, float b, float c, float d)
    {
        return Math.Min(a, Math.Min(b, Math.Min(c, d)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaxOf(float a, float b, float c)
    {
        return Math.Max(a, Math.Max(b, c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaxOf(float a, float b, float c, float d)
    {
        return Math.Max(a, Math.Max(b, Math.Max(c, d)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEven(int value)
    {
        return value % 2 == 0;
    }

    /// <summary>maps value (which is in the range leftMin - leftMax) to a value in the range rightMin - rightMax</summary>
    /// <param name="value">Value.</param>
    /// <param name="leftMin">Left minimum.</param>
    /// <param name="leftMax">Left max.</param>
    /// <param name="rightMin">Right minimum.</param>
    /// <param name="rightMax">Right max.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }

    /// <summary>Maps a value from some arbitrary range to the 0 to 1 range</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Map01(float value, float min, float max)
    {
        return (value - min) * 1f / (max - min);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AngleBetweenVectors(Vector2 from, Vector2 to)
    {
        return Atan2(to.Y - from.Y, to.X - from.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        return AngleBetweenVectors(from, to) * Math.Sign(from.X * to.Y - from.Y * to.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
    {
        return Math.Clamp(value, min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float value)
    {
        return Clamp(value, 0, 1);
    }

    /// <summary>floors the float to the nearest int value below x. note that this only works for values in the range of short (-32,768 to 32,767)</summary>
    /// <returns>The floor to int.</returns>
    /// <param name="x">The x coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FastFloorToInt(float x)
    {
        // we shift to guaranteed positive before casting then shift back after
        return (int)(x + 32768f) - 32768;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 AngleToVector(float angleRadians, float length = 1f)
    {
        return new Vector2(Cos(angleRadians) * length, Sin(angleRadians) * length);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 AngleToVectorDeg(float angleDegrees, float length = 1f)
    {
        return AngleToVector(angleDegrees * Deg2Rad, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Perpendicular(this Vector2 self)
    {
        return new Vector2(-self.Y, self.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Round(float f)
    {
        return (float)Math.Round(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sqrt(float val)
    {
        return (float)Math.Sqrt(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (maxDelta < 0)
        {
            throw new InvalidOperationException();
        }

        return Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DeltaMoveTowards(float current, float target, float maxDelta)
    {
        if (maxDelta < 0)
        {
            throw new InvalidOperationException();
        }

        var offset = Math.Abs(target - current);
        var sign = Math.Sign(target - current);
        if (offset < maxDelta)
        {
            return offset * sign;
        }

        return maxDelta * sign;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MoveTowardsAngleRadians(float current, float target, float maxDelta)
    {
        if (maxDelta < 0)
        {
            throw new InvalidOperationException();
        }

        var num = DeltaAngleRadians(current, target);
        if (-maxDelta < num && num < maxDelta)
        {
            return target;
        }

        target = current + num;
        return MoveTowards(current, target, maxDelta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        if (maxDelta < 0)
        {
            throw new InvalidOperationException();
        }

        var deltaAngle = DeltaAngleDegrees(current, target);
        if (-maxDelta < deltaAngle && deltaAngle < maxDelta)
        {
            return target;
        }

        target = current + deltaAngle;
        return MoveTowards(current, target, maxDelta);
    }

    /// <summary>Calculates the shortest difference between two given angles in degrees</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DeltaAngleDegrees(float current, float target)
    {
        var delta = Loop(target - current, 360f);
        if (delta > 180f)
        {
            delta -= 360f;
        }

        return delta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DeltaAngleRadians(float current, float target)
    {
        var num = Loop(target - current, MathHelper.TwoPi);
        if (num > MathHelper.Pi)
        {
            num -= MathHelper.TwoPi;
        }

        return num;
    }

    /// <summary>Loops t from 0 if t is greater than length, e.g Loop(4, 3) => 1</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Loop(float t, float length)
    {
        var numLoops = Floor(t / length);
        return t - numLoops * length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float f)
    {
        return (float)Math.Floor(f);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorToInt(float f)
    {
        return (int)Math.Floor(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundToInt(float f)
    {
        return (int)Math.Round(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceil(float f)
    {
        return (float)Math.Ceiling(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CeilToInt(float f)
    {
        return (int)Math.Ceiling(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approx(float a, float b, float tolerance = Epsilon)
    {
        return Math.Abs(a - b) <= tolerance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NotApprox(float a, float b, float tolerance = Epsilon)
    {
        return Math.Abs(a - b) > tolerance;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNearZero(float value, float tolerance = Epsilon)
    {
        return Approx(value, 0, tolerance);
    }
    
    /// <summary>Lerp between two angles measured in radians</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LerpAngle(float a, float b, float t)
    {
        var t1 = Loop(b - a, MathHelper.TwoPi);
        if (t1 > MathHelper.Pi)
        {
            t1 -= MathHelper.TwoPi;
        }

        return a + t1 * Clamp01(t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow(float x, float y)
    {
        return (float)Math.Pow(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * Clamp01(t);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LerpUnclamped(float from, float to, float t)
    {
        return from + (to - from) * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Exp(float value)
    {
        return (float)Math.Exp(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Abs(float value)
    {
        return Math.Abs(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Frac(float value)
    {
        return (float)Math.Abs(value - Math.Truncate(value));
    }
}
