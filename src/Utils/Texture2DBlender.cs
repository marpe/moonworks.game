// See: http://wwwimages.adobe.com/www.adobe.com/content/dam/Adobe/en/devnet/pdf/pdfs/PDF32000_2008.pdf
// Page 333

namespace MyGame.Utils;

public static class Texture2DBlender
{
    private const float ToFloat = 1.0f / byte.MaxValue;

    private const int R_SHIFT = 0;
    private const int G_SHIFT = 8;
    private const int B_SHIFT = 16;
    private const int A_SHIFT = 24;
    private const int RGB_MASK = 0x00ffffff;
    private const byte ONE_HALF = 128;

    public static float Screen(float b, float s)
    {
        return b + s - b * s;
    }

    public static float Overlay(float b, float s)
    {
        return HardLight(s, b);
    }

    public static float Darken(float b, float s)
    {
        return Math.Min(b, s);
    }

    public static float Lighten(float b, float s)
    {
        return Math.Max(b, s);
    }

    // Color Dodge & Color Burn:  http://wwwimages.adobe.com/www.adobe.com/content/dam/Adobe/en/devnet/pdf/pdfs/adobe_supplement_iso32000_1.pdf
    public static float ColorDodge(float b, float s)
    {
        if (b == 0)
        {
            return 0;
        }

        if (b >= 1 - s)
        {
            return 1;
        }

        return b / (1 - s);
    }

    public static float ColorBurn(float b, float s)
    {
        if (MathF.Approx(b, 1))
        {
            return 1;
        }

        if (1 - b >= s)
        {
            return 0;
        }

        return 1 - (1 - b) / s;
    }

    public static float HardLight(float b, float s)
    {
        if (s <= 0.5)
        {
            return b * 2 * s;
        }

        return Screen(b, 2 * s - 1);
    }

    public static float SoftLight(float b, float s)
    {
        if (s <= 0.5)
        {
            return b - (1 - 2 * s) * b * (1 - b);
        }

        return b + (2 * s - 1) * (SoftLightD(b) - b);
    }

    private static float SoftLightD(float x)
    {
        if (x <= 0.25)
        {
            return ((16 * x - 12) * x + 4) * x;
        }

        return MathF.Sqrt(x);
    }

    public static float Difference(float b, float s)
    {
        return Math.Abs(b - s);
    }

    public static float Exclusion(float b, float s)
    {
        return b + s - 2 * b * s;
    }

    public static uint BlendMultiply(uint backdrop, uint src, byte opacity)
    {
        var r = Multiply(GetR(backdrop), GetR(src));
        var g = Multiply(GetG(backdrop), GetG(src));
        var b = Multiply(GetB(backdrop), GetB(src));
        src = PackRGBA(r, g, b, 0) | GetA(src);
        return BlendNormal(backdrop, src, opacity);
    }

    public static uint BlendNormal(uint backdrop, uint src, byte opacity)
    {
        if (GetA(backdrop) == 0)
        {
            var a = Multiply(GetA(src), opacity);
            var rgb = src & RGB_MASK;
            return rgb | (uint)(a << A_SHIFT);
        }

        if (GetA(src) == 0)
        {
            return backdrop;
        }

        var Br = GetR(backdrop);
        var Bg = GetG(backdrop);
        var Bb = GetB(backdrop);
        var Ba = GetA(backdrop);

        var Sr = GetR(src);
        var Sg = GetG(src);
        var Sb = GetB(src);
        var Sa = GetA(src);

        Sa = Multiply(Sa, opacity);

        // Ra = Sa + Ba*(1-Sa)
        //    = Sa + Ba - Ba*Sa
        var Ra = Sa + Ba - Multiply(Ba, Sa);

        // Ra = Sa + Ba*(1-Sa)
        // Ba = (Ra-Sa) / (1-Sa)
        // Rc = (Sc*Sa + Bc*Ba*(1-Sa)) / Ra                Replacing Ba with (Ra-Sa) / (1-Sa)...
        //    = (Sc*Sa + Bc*(Ra-Sa)/(1-Sa)*(1-Sa)) / Ra
        //    = (Sc*Sa + Bc*(Ra-Sa)) / Ra
        //    = Sc*Sa/Ra + Bc*Ra/Ra - Bc*Sa/Ra
        //    = Sc*Sa/Ra + Bc - Bc*Sa/Ra
        //    = Bc + (Sc-Bc)*Sa/Ra
        var Rr = Br + (Sr - Br) * Sa / Ra;
        var Rg = Bg + (Sg - Bg) * Sa / Ra;
        var Rb = Bb + (Sb - Bb) * Sa / Ra;

        return PackRGBA(Rr, Rg, Rb, Ra);
    }

    /// <summary>Multiplication method converted from https://github.com/aseprite/pixman/blob/eb0dfaa0c6eb54ca9f8a6d8bf63d346c0fc4f2b9/pixman/pixman-combine32.h#L67</summary>
    public static byte Multiply(byte a, byte b)
    {
        var t = (uint)(a * b + ONE_HALF);
        t = GetG(GetG(t) + t);
        return (byte)t;
    }

    public static void Normal(Span<uint> dstLayer, uint[] srcLayer, byte opacity)
    {
        for (var i = 0; i < dstLayer.Length; i++)
        {
            dstLayer[i] = BlendNormal(dstLayer[i], srcLayer[i], opacity);
        }
    }


    /// <summary>Converted from https://github.com/aseprite/aseprite/blob/a5c36d0b0f3663d36a8105497458e86a41da310e/src/doc/blend_funcs.cpp#L254</summary>
    public static void Multiply(Span<uint> dstLayer, uint[] srcLayer, byte opacity)
    {
        for (var i = 0; i < dstLayer.Length; i++)
        {
            dstLayer[i] = BlendMultiply(dstLayer[i], srcLayer[i], opacity);
        }
    }

    private static float BlendDivide(float b, float s)
    {
        if (b == 0)
        {
            return 0;
        }

        if (b >= s)
        {
            return 1f;
        }

        return b / s;
    }

    private static float Lum(Color c)
    {
        return (0.3f * c.R + 0.59f * c.G + 0.11f * c.B) * ToFloat;
    }

    private static Color ClipColor(Color c)
    {
        var l = Lum(c);
        float n = Math.Min(c.R, Math.Min(c.G, c.B));
        float x = Math.Max(c.R, Math.Max(c.G, c.B));

        if (n < 0)
        {
            c.R = (byte)(l + (c.R - l) * l / (l - n));
            c.G = (byte)(l + (c.G - l) * l / (l - n));
            c.B = (byte)(l + (c.B - l) * l / (l - n));
        }

        if (x > 1)
        {
            c.R = (byte)(l + (c.R - l) * (1 - l) / (x - l));
            c.G = (byte)(l + (c.G - l) * (1 - l) / (x - l));
            c.B = (byte)(l + (c.B - l) * (1 - l) / (x - l));
        }

        return c;
    }

    private static Color SetLum(Color c, float l)
    {
        var d = l - Lum(c);
        c.R = (byte)(c.R + d);
        c.G = (byte)(c.G + d);
        c.B = (byte)(c.B + d);

        return ClipColor(c);
    }

    private static double Sat(Color c)
    {
        return Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
    }

    private static double DMax(double x, double y)
    {
        return x > y ? x : y;
    }

    private static double DMin(double x, double y)
    {
        return x < y ? x : y;
    }

    private static Color SetSat(Color c, double s)
    {
        var cMin = GetMinComponent(c);
        var cMid = GetMidComponent(c);
        var cMax = GetMaxComponent(c);

        double min = GetComponent(c, cMin);
        double mid = GetComponent(c, cMid);
        double max = GetComponent(c, cMax);


        if (max > min)
        {
            mid = (mid - min) * s / (max - min);
            c = SetComponent(c, cMid, (float)mid);
            max = s;
            c = SetComponent(c, cMax, (float)max);
        }
        else
        {
            mid = max = 0;
            c = SetComponent(c, cMax, (float)max);
            c = SetComponent(c, cMid, (float)mid);
        }

        min = 0;
        c = SetComponent(c, cMin, (float)min);

        return c;
    }

    private static float GetComponent(Color c, char component)
    {
        switch (component)
        {
            case 'r': return c.R;
            case 'g': return c.G;
            case 'b': return c.B;
        }

        return 0f;
    }

    private static Color SetComponent(Color c, char component, float value)
    {
        switch (component)
        {
            case 'r':
                c.R = (byte)(value * byte.MaxValue);
                break;
            case 'g':
                c.G = (byte)(value * byte.MaxValue);
                break;
            case 'b':
                c.B = (byte)(value * byte.MaxValue);
                break;
        }

        return c;
    }

    private static char GetMinComponent(Color c)
    {
        var r = new KeyValuePair<char, float>('r', c.R);
        var g = new KeyValuePair<char, float>('g', c.G);
        var b = new KeyValuePair<char, float>('b', c.B);

        return MIN(r, MIN(g, b)).Key;
    }

    private static char GetMidComponent(Color c)
    {
        var r = new KeyValuePair<char, float>('r', c.R);
        var g = new KeyValuePair<char, float>('g', c.G);
        var b = new KeyValuePair<char, float>('b', c.B);

        return MID(r, g, b).Key;
    }

    private static char GetMaxComponent(Color c)
    {
        var r = new KeyValuePair<char, float>('r', c.R);
        var g = new KeyValuePair<char, float>('g', c.G);
        var b = new KeyValuePair<char, float>('b', c.B);

        return MAX(r, MAX(g, b)).Key;
    }

    private static KeyValuePair<char, float> MIN(KeyValuePair<char, float> x, KeyValuePair<char, float> y)
    {
        return x.Value < y.Value ? x : y;
    }

    private static KeyValuePair<char, float> MAX(KeyValuePair<char, float> x, KeyValuePair<char, float> y)
    {
        return x.Value > y.Value ? x : y;
    }

    private static KeyValuePair<char, float> MID(KeyValuePair<char, float> x, KeyValuePair<char, float> y, KeyValuePair<char, float> z)
    {
        var components = new List<KeyValuePair<char, float>>();
        components.Add(x);
        components.Add(z);
        components.Add(y);


        components.Sort((c1, c2) => { return c1.Value.CompareTo(c2.Value); });

        return components[1];
        //return MAX(x, MIN(y, z));
    }

    public static byte GetR(uint packedValue)
    {
        return unchecked((byte)(packedValue >> R_SHIFT));
    }

    public static byte GetG(uint packedValue)
    {
        return unchecked((byte)(packedValue >> G_SHIFT));
    }

    public static byte GetB(uint packedValue)
    {
        return unchecked((byte)(packedValue >> B_SHIFT));
    }

    public static byte GetA(uint packedValue)
    {
        return unchecked((byte)(packedValue >> A_SHIFT));
    }

    public static uint PackRGBA(int r, int g, int b, int a)
    {
        return (uint)((r << R_SHIFT) | (g << G_SHIFT) | (b << B_SHIFT) | (a << A_SHIFT));
    }
}
