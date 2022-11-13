using System.Globalization;

namespace MyGame.TWConsole;

public interface IStringParser
{
    public object Parse(Type type, string value);
}

public class StringParser<T> : IStringParser where T : notnull
{
    private readonly Func<Type, string, T> _parser;

    public StringParser(Func<Type, string, T> parser)
    {
        _parser = parser;
    }

    public object Parse(Type type, string value)
    {
        return Parse(value);
    }

    public T Parse(string value)
    {
        return _parser(typeof(T), value);
    }
}

public static class ConsoleUtils
{
    private static readonly Dictionary<string, bool> _boolLookup = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "true", true },
        { "1", true },
        { "false", false },
        { "0", false },
    };

    private static readonly Dictionary<Type, string> _typeDisplayNames = new()
    {
        { typeof(int), "int" },
        { typeof(float), "float" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(string), "string" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(sbyte), "sbyte" },
        { typeof(uint), "uint" },
        { typeof(short), "short" },
        { typeof(ushort), "ushort" },
        { typeof(long), "decimal" },
        { typeof(ulong), "ulong" },
        { typeof(char), "char" },
        { typeof(object), "object" },
        { typeof(Color), "color" },
        { typeof(Point), "point" },
        { typeof(Vector2), "vector2" },
    };

    private static readonly Dictionary<Type, IStringParser> _parsers = new();

    static ConsoleUtils()
    {
        _parsers.Add(typeof(int), new StringParser<int>((_, str) => int.Parse(str)));
        _parsers.Add(typeof(float), new StringParser<float>((_, str) => float.Parse(str)));
        _parsers.Add(typeof(decimal), new StringParser<decimal>((_, str) => decimal.Parse(str)));
        _parsers.Add(typeof(double), new StringParser<double>((_, str) => double.Parse(str)));
        _parsers.Add(typeof(byte), new StringParser<byte>((_, str) => byte.Parse(str)));
        _parsers.Add(typeof(sbyte), new StringParser<sbyte>((_, str) => sbyte.Parse(str)));
        _parsers.Add(typeof(uint), new StringParser<uint>((_, str) => uint.Parse(str)));
        _parsers.Add(typeof(short), new StringParser<short>((_, str) => short.Parse(str)));
        _parsers.Add(typeof(ushort), new StringParser<ushort>((_, str) => ushort.Parse(str)));
        _parsers.Add(typeof(long), new StringParser<long>((_, str) => long.Parse(str)));
        _parsers.Add(typeof(ulong), new StringParser<ulong>((_, str) => ulong.Parse(str)));
        _parsers.Add(typeof(char), new StringParser<char>((_, str) => char.Parse(str)));
        _parsers.Add(typeof(string), new StringParser<string>((_, str) => str));
        _parsers.Add(typeof(bool), new StringParser<bool>((_, str) => ParseBool(str)));
        _parsers.Add(typeof(Color), new StringParser<Color>((_, str) => ParseColor(str)));
        _parsers.Add(typeof(Vector2), new StringParser<Vector2>((_, str) => ParseVector2(str)));
        _parsers.Add(typeof(Point), new StringParser<Point>((_, str) => ParsePoint(str)));
        _parsers.Add(typeof(Enum), new StringParser<Enum>((type, str) => ParseEnum(type, str)));
    }

    public static string GetDisplayName(Type type)
    {
        if (_typeDisplayNames.ContainsKey(type))
        {
            return _typeDisplayNames[type];
        }

        if (type.DeclaringType != null)
        {
            return type.DeclaringType.Name + "." + type.Name;
        }

        return type.Name;
    }

    public static bool CanParse(Type type)
    {
        return _parsers.ContainsKey(type);
    }

    public static bool ParseBool(string str)
    {
        return _boolLookup.ContainsKey(str) ? _boolLookup[str] : throw new InvalidOperationException($"Cannot parse '{str}' to a bool.");
    }

    private static Enum ParseEnum(Type type, string strValue)
    {
        return (Enum)Enum.Parse(type, strValue);
    }

    public static T Parse<T>(string strValue) where T : struct
    {
        var t = typeof(T);
        if (t.IsEnum)
        {
            return Enum.Parse<T>(strValue, true);
        }

        if (!_parsers.ContainsKey(t))
        {
            throw new InvalidOperationException($"Cannot parse {GetDisplayName(t)}.");
        }

        var parser = (StringParser<T>)_parsers[t];
        return parser.Parse(strValue);
    }

    public static object ParseArg(Type type, string strValue)
    {
        if (!_parsers.ContainsKey(type))
        {
            throw new InvalidOperationException($"Cannot parse {GetDisplayName(type)}.");
        }

        return _parsers[type].Parse(type, strValue);
    }

    private static Point ParsePoint(string strValue)
    {
        var splitBy = strValue.Contains(',') ? ',' : ' ';
        var xy = strValue.Split(splitBy);
        var parsed = new[] { 0, 0 };
        for (var i = 0; i < xy.Length && i < 2; i++)
        {
            parsed[i] = Parse<int>(xy[i]);
        }

        return new Point(parsed[0], parsed[1]);
    }

    private static Vector2 ParseVector2(string strValues)
    {
        var splitBy = strValues.Contains(',') ? ',' : ' ';
        var xy = strValues.Split(splitBy);
        var parsed = new[] { 0f, 0f };
        for (var i = 0; i < xy.Length && i < 2; i++)
        {
            parsed[i] = Parse<float>(xy[i]);
        }

        return new Vector2(parsed[0], parsed[1]);
    }

    public static Point ParsePoint(ReadOnlySpan<char> strValue)
    {
        var splitBy = strValue.Contains(',') ? ',' : ' ';
        var splitAt = strValue.IndexOf(splitBy);

        var xSpan = strValue.Slice(0, splitAt);
        var ySpan = strValue.Slice(splitAt + 1);

        var x = int.Parse(xSpan);
        var y = int.Parse(ySpan);

        return new Point(x, y);
    }

    private static Color ParseColor(string strValue)
    {
        var splitBy = strValue.Contains(',') ? ',' : ' ';
        var rgba = strValue.Split(splitBy);
        Span<float> parsed = stackalloc[] { 1.0f, 1.0f, 1.0f, 1.0f };
        for (var i = 0; i < rgba.Length && i < 4; i++)
        {
            parsed[i] = Parse<float>(rgba[i]);
        }

        return new Color(parsed[0], parsed[1], parsed[2], parsed[3]);
    }

    private static T ParsePrimitive<T>(string strValue)
    {
        return (T)Convert.ChangeType(strValue, typeof(T), CultureInfo.InvariantCulture);
    }

    private static object ParsePrimitive(Type type, string strValue)
    {
        return Convert.ChangeType(strValue, type, CultureInfo.InvariantCulture);
    }

    public static string[] SplitArgs(ReadOnlySpan<char> text)
    {
        var args = new List<string>();
        var inQuotes = false;
        var splitStart = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (text[i] == ' ')
            {
                if (!inQuotes && i > 0 && text[i - 1] == ' ')
                {
                    // ignore whitespace between values
                    splitStart = i + 1;
                }
                else if (!inQuotes)
                {
                    // we ended an argument
                    var length = i - splitStart;
                    var arg = text.Slice(splitStart, length).ToString();
                    args.Add(arg);
                    splitStart = i + 1;
                }
            }
        }

        args.Add(text[splitStart..].ToString());

        for (var i = 0; i < args.Count; i++)
        {
            args[i] = args[i].Trim('"');
        }

        return args.ToArray();
    }

    public static string ConvertToString<T>(T value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string strValue)
        {
            return strValue;
        }

        if (value is Color c)
        {
            var r = c.R / 255f;
            var g = c.G / 255f;
            var b = c.B / 255f;
            var a = c.A / 255f;
            return $"{r:0.##} {g:0.##} {b:0.##} {a:0.##}";
        }

        if (value is Point p)
        {
            return $"{p.X}, {p.Y}";
        }

        if (value is Vector2 v)
        {
            return $"{v.X}, {v.Y}";
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLower() ?? string.Empty;
    }
}
