namespace MyGame.Utils;

public static class ColorExt
{
    private const string HEX = "0123456789ABCDEF";

    private static byte HexToByte(char c)
    {
        return (byte)HEX.IndexOf(char.ToUpper(c));
    }

    public static Color FromHex(ReadOnlySpan<char> hex)
    {
        var r = (HexToByte(hex[0]) * 16 + HexToByte(hex[1])) / 255.0f;
        var g = (HexToByte(hex[2]) * 16 + HexToByte(hex[3])) / 255.0f;
        var b = (HexToByte(hex[4]) * 16 + HexToByte(hex[5])) / 255.0f;

        return new Color(r, g, b);
    }

    public static string ToHex(in Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static (float, float, float) RgbToHsv(Color color)
    {
        var k = 0f;
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;

        if (g < b)
        {
            (g, b) = (b, g);
            k = -1f;
        }

        if (r < g)
        {
            (r, g) = (g, r);
            k = -2f / 6f - k;
        }

        var chroma = r - (g < b ? g : b);
        var h = Math.Abs(k + (g - b) / (6f * chroma + 1e-20f));
        var s = chroma / (r + 1e-20f);
        var v = r;

        return (h, s, v);
    }

    /// <summary>Convert HSV floats to Color</summary>
    /// <param name="h">Hue in range [0-1]</param>
    /// <param name="s">Saturation in range [0-1]</param>
    /// <param name="v">Value in range [0-1]</param>
    /// <returns></returns>
    public static Color HsvToRgb(float h, float s, float v)
    {
        if (s == 0)
        {
            return new Color(v, v, v);
        }

        float Remainder(float x, float y)
        {
            var n = (int)(x / y);
            return x - n * y;
        }

        h = Remainder(h, 1.0f) / (60f / 360f);
        var i = (int)h;
        var f = h - i;
        var p = v * (1f - s);
        var q = v * (1f - s * f);
        var t = v * (1f - s * (1f - f));

        return i switch
        {
            0 => new Color(v, t, p),
            1 => new Color(q, v, p),
            2 => new Color(p, v, t),
            3 => new Color(p, q, v),
            4 => new Color(t, p, v),
            _ => new Color(v, p, q),
        };
    }

    /// <summary>
    /// linearly interpolates Color from - to
    /// </summary>
    public static Color Lerp(in Color from, in Color to, float t)
    {
        var t255 = (int)(t * 255);
        return new Color(
            from.R + (to.R - from.R) * t255 / 255,
            from.G + (to.G - from.G) * t255 / 255,
            from.B + (to.B - from.B) * t255 / 255,
            from.A + (to.A - from.A) * t255 / 255
        );
    }

    public static Color PulseColor(in Color from, in Color to, float timer)
    {
        var t = Math.Abs(MathF.Sin(timer));
        return Lerp(from, to, t);
    }

    public static Color Add(in Color a, in Color b)
    {
        return new Color(
            a.R + b.R,
            a.G + b.G,
            a.B + b.B,
            a.A + b.A
        );
    }

    public static Color Multiply(in Color self, in Color second)
    {
        return new Color
        {
            R = (byte)(self.R * second.R / 255),
            G = (byte)(self.G * second.G / 255),
            B = (byte)(self.B * second.B / 255),
            A = (byte)(self.A * second.A / 255)
        };
    }

    public static void MultiplyColors(Span<Color> colors, in Color tint)
    {
        for (var i = 0; i < colors.Length; i++)
            colors[i] = Multiply(colors[i], tint);
    }

    public static Color MultiplyAlpha(in Color color, float alpha)
    {
        return color.MultiplyAlpha(alpha);
    }

    public static Color MultiplyRGB(this Color self, float value)
    {
        return new Color
        {
            R = (byte)(self.R * value),
            G = (byte)(self.G * value),
            B = (byte)(self.B * value),
            A = self.A
        };
    }

    public static Color MultiplyRGB(this Color self, Color other)
    {
        return new Color
        {
            R = (byte)(self.R * other.R / 255),
            G = (byte)(self.G * other.G / 255),
            B = (byte)(self.B * other.B / 255),
            A = self.A
        };
    }

    public static Color MultiplyAlpha(this Color color, float alpha)
    {
        return new Color(
            color.R,
            color.G,
            color.B,
            (int)(color.A * alpha)
        );
    }
    
    public static Color FromPacked(uint packedValue)
    {
        return new Color(
            Texture2DBlender.GetR(packedValue),
            Texture2DBlender.GetG(packedValue),
            Texture2DBlender.GetB(packedValue),
            Texture2DBlender.GetA(packedValue)
        );
    }

    public static Color Subtract(in Color a, in Color b)
    {
        return new Color(
            a.R - b.R,
            a.G - b.G,
            a.B - b.B,
            a.A - b.A
        );
    }
}

public class ColorConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
    {
    }

    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        var strValue = reader.Value as string;

        if (string.IsNullOrWhiteSpace(strValue))
        {
            return new Color();
        }

        if (strValue[0] == '#')
        {
            strValue = strValue[1..];
        }

        return ColorExt.FromHex(strValue);
    }
}
