using System.Xml;

namespace MyGame.Utils;

public static class XmlNodeExt
{
    public static bool ParseBool(this XmlNode node, string attribute, bool fallbackValue)
    {
        var strValue = node.Attributes?[attribute]?.Value;
        return strValue != null ? XmlConvert.ToBoolean(strValue) : fallbackValue;
    }
        
    public static string AttributeValue(this XmlNode node, string attribute)
    {
        if (node.Attributes == null)
            throw new InvalidOperationException();
        var attr = node.Attributes[attribute];
        if (attr == null)
            throw new InvalidOperationException();
        return attr.Value;
    }

    public static int ParseInt(this XmlNode node, string attribute, int fallback)
    {
        var value = node.Attributes?[attribute]?.Value;
        return value != null ? XmlConvert.ToInt32(value) : fallback;
    }

    public static int ParseInt(this XmlNode node, string attribute)
    {
        return XmlConvert.ToInt32(node.AttributeValue(attribute));
    }

    public static float ParseFloat(this XmlNode node, string attribute)
    {
        return XmlConvert.ToSingle(node.AttributeValue(attribute));
    }

    public static float ParseFloat(this XmlNode node, string attribute, float fallback)
    {
        var value = node.Attributes?[attribute]?.Value;
        return value != null ? XmlConvert.ToSingle(value) : fallback;
    }
}
