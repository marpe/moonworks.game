namespace MyGame.TWConsole;

public class ConsoleScreenBuffer
{
    private short[] _buffer;
    private int _wrappedY => (_cursorY % _height) * _width;

    private int _displayY = 0;

    public int DisplayY
    {
        get => _displayY;
        set => _displayY = value;
    }

    private int _cursorY = 0;
    private int _cursorX = 0;

    private readonly int _height;
    private readonly int _width;

    public int Height => _height;
    public int Width => _width;
    public int TotalLength => _width * _height;

    public int WrappedCursorY => _wrappedY;

    public int CursorY => _cursorY;

    public ConsoleScreenBuffer(int width, int height)
    {
        _height = height;
        _width = width;
        _buffer = new short[width * height];
    }

    public (char c, byte color) GetChar(int x, int y)
    {
        var i = (_height + y) % _height * _width + x;
        return Unpack(_buffer[i]);
    }

    public static short Pack(char c, byte color)
    {
        return (short)((color << 8) | c);
    }

    public static (char c, byte color) Unpack(short s)
    {
        var color = (byte)((s >> 8) & 0xff);
        var c = (char)(s & 0xff);
        return (c, color);
    }

    public void Clear()
    {
        for (var i = 0; i < _buffer.Length; i++)
            _buffer[i] = Pack(' ', 0);

        _cursorX = 0;
        _cursorY = 0;
        _displayY = 0;
    }

    private void Linefeed()
    {
        if (_displayY == _cursorY)
            _displayY++;

        _cursorY++;
        _cursorX = 0;
        ClearLine(_cursorY);
    }

    private void ClearLine(int y)
    {
        for (var i = 0; i < _width; i++)
            _buffer[_wrappedY + i] = Pack(' ', 0);
    }

    public void AddLine(ReadOnlySpan<char> line)
    {
        var lastY = 0;
        foreach (var run in line.SplitLines(true, _width))
        {
            while (run.Y - lastY > 0)
            {
                Linefeed();
                lastY++;
            }

            for (var i = 0; i < run.Text.Length; i++)
            {
                if (_cursorX >= _width)
                    Linefeed();
                _buffer[_wrappedY + _cursorX] = Pack(run.Text[i], run.Color);
                _cursorX++;
            }
        }

        Linefeed();
    }
}
