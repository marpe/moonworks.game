﻿using Newtonsoft.Json;

namespace MyGame.Utils;

public static class ColorExt
{
    private const string HEX = "0123456789ABCDEF";
    private static byte HexToByte(char c) => (byte)HEX.IndexOf(char.ToUpper(c));
    
    public static Color FromHex(ReadOnlySpan<char> hex)
    {
        var r = (HexToByte(hex[0]) * 16 + HexToByte(hex[1])) / 255.0f;
        var g = (HexToByte(hex[2]) * 16 + HexToByte(hex[3])) / 255.0f;
        var b = (HexToByte(hex[4]) * 16 + HexToByte(hex[5])) / 255.0f;

        return new Color(r, g, b);
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
    
    /// <summary>
    /// Convert HSV floats to Color
    /// </summary>
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
            _ => new Color(v, p, q)
        };
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
            return new Color();
        
        if (strValue[0] == '#')
            strValue = strValue[1..];
        
        return ColorExt.FromHex(strValue);
    }
}