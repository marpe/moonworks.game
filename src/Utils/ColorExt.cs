using Newtonsoft.Json;

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
