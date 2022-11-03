using System.Text;

namespace MyGame.CodeGen;

public static class TextGenerator
{
    private static readonly StringBuilder _output = new();
    private static int _indent;
    private const int IndentWidth = 4;

    public static void Clear()
    {
        _output.Clear();
        _indent = 0;
    }

    public static void WriteLine(string line = "")
    {
        if (line != string.Empty)
            _output.Append(new string(' ', _indent * IndentWidth));
        _output.AppendLine(line);
    }

    public static void StartBlock()
    {
        WriteLine("{");
        _indent++;
    }

    public static void EndBlock(string endStr = "}")
    {
        _indent--;
        WriteLine(endStr);
    }

    public static string AsString()
    {
        return _output.ToString();
    }
}
