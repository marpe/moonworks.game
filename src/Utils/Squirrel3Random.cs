namespace MyGame.Utils;

public class Squirrel3Random
{
    public delegate float LerpFunc(float a0, float a1, float w);

    private const uint NOISE1 = 0xb5297a4d;
    private const uint NOISE2 = 0x68e31da4;
    private const uint NOISE3 = 0x1b56c4e9;
    private const uint CAP = uint.MaxValue;
    private int _n;

    private uint _seed;

    public Squirrel3Random()
    {
    }

    public Squirrel3Random(uint seed)
    {
        _seed = seed;
    }

    public void Seed(uint seed)
    {
        _seed = seed;
    }

    /// <summary>Generates a float between 0 (inclusive) and 1 (inclusive)</summary>
    /// <returns></returns>
    public float Float()
    {
        return Float(_n++, _seed);
    }

    /// <summary>Generates a signed integer within min (inclusive) and max (exclusive)</summary>
    public int Range(int min, int max)
    {
        return Range(min, max, _n++, _seed);
    }

    /// <summary>Generates a float between min (inclusive) and max (inclusive)</summary>
    public float Range(float min, float max)
    {
        return Range(min, max, _n++, _seed);
    }


    /// <summary>Generates a Vector2 with length 1 on the unit circle</summary>
    public Vector2 Vector2()
    {
        var f = Float(_n++, _seed) * MathHelper.TwoPi;
        return new Vector2(System.MathF.Cos(f), System.MathF.Sin(f));
    }

    public Vector2 Vector2(float minLength, float maxLength)
    {
        return Vector2() * Range(minLength, maxLength);
    }

    /// <summary>Generates a float between 0 (inclusive) and 1 (inclusive) based on a given (signed) integer input parameter `n` and optional `seed`</summary>
    public static float Float(int n, uint seed)
    {
        return UInt(n, seed) / (float)CAP;
    }

    /// <summary>Generates an unsigned integer based on a given (signed) integer input parameter `n` and optional `seed`</summary>
    private static uint UInt(int n, uint seed)
    {
        long r = n;

        unchecked
        {
            r *= NOISE1;
            r += seed;
            r ^= r >> 8;
            r += NOISE2;
            r ^= r << 8;
            r *= NOISE3;
            r ^= r >> 8;
        }

        return (uint)(r % CAP);
    }

    /// <summary>Generates a signed integer within min (inclusive) and max (exclusive) based on a given (signed) integer input parameter `n` and optional `seed`</summary>
    public static int Range(int min, int max, int n, uint seed)
    {
        var f = Float(n, seed);
        return min + (int)(f * (max - min));
    }

    /// <summary>Generates a float between min (inclusive) and max (inclusive) based on a given (signed) integer input parameter `n` and optional `seed`</summary>
    public static float Range(float min, float max, int n, uint seed)
    {
        var f = Float(n, seed);
        return min + f * (max - min);
    }

    public static float Float2(int x, int y, uint seed)
    {
        const int PRIME_NUMBER = 198491317;
        return Float(x + PRIME_NUMBER * y, seed);
    }

    /// <summary>Function to linearly interpolate between a0 and a1 Weight w should be in the range [0.0, 1.0]</summary>
    public static float Lerp(float a0, float a1, float w)
    {
        if (w < 0)
        {
            return a0;
        }

        if (w > 1)
        {
            return 1;
        }

        return a0 + (a1 - a0) * w;
    }

    /// <summary>Use this cubic interpolation [[Smoothstep]] instead, for a smooth appearance</summary>
    public static float SmoothstepLerp(float a0, float a1, float w)
    {
        if (w < 0)
        {
            return a0;
        }

        if (w > 1)
        {
            return 1;
        }

        // Cubic interpolation
        return a0 + (a1 - a0) * (3f - w * 2f) * w * w;
    }


    /// <summary>Use [[Smootherstep]] for an even smoother result with a second derivative equal to zero on boundaries</summary>
    public static float SmootherstepLerp(float a0, float a1, float w)
    {
        if (w < 0)
        {
            return a0;
        }

        if (w > 1)
        {
            return 1;
        }

        // Smootherstep
        return a0 + (a1 - a0) * ((w * (w * 6f - 15f) + 10f) * w * w * w);
    }

    public static float Float2(float x, float y, uint seed, LerpFunc lerpFunc)
    {
        var x0 = MathF.FloorToInt(x);
        var x1 = x0 + 1;
        var y0 = MathF.FloorToInt(y);
        var y1 = y0 + 1;

        var remainderX = x - x0;
        var remainderY = y - y0;

        var n0 = Float2(x0, y0, seed);
        var n1 = Float2(x1, y0, seed);
        var ix0 = lerpFunc(n0, n1, remainderX);

        var m0 = Float2(x0, y1, seed);
        var m1 = Float2(x1, y1, seed);
        var ix1 = lerpFunc(m0, m1, remainderX);

        return lerpFunc(ix0, ix1, remainderY);
    }
}
