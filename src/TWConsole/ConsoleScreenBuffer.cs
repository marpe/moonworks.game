namespace MyGame.TWConsole;

public class ConsoleScreenBuffer
{
    private short[] _buffer;
    private int _wrappedY => (_cursorY % _height) * _width;

    private int _displayY = 0;

    public int DisplayY
    {
        get => _displayY;
        set
        {
            var minValue = Math.Max(0, _cursorY - _height + 1); // show at least one line
            _displayY = MathF.Clamp(value, minValue, _cursorY);
        }
    }

    private int _cursorY = 0;
    private int _cursorX = 0;

    private int _height;
    private int _width;

    public int Height => _height;
    public int Width => _width;
    public int CursorY => _cursorY;

    public ConsoleScreenBuffer(int width, int height)
    {
        _height = height;
        _width = width;
        _buffer = new short[width * height];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChar(int x, int y, out char c, out byte color)
    {
        var line = (_height + y) % _height;
        var i = line * _width + x;
        Unpack(_buffer[i], out c, out color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short Pack(char c, byte color)
    {
        return (short)((color << 8) | c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Unpack(short s, out char c, out byte color)
    {
        color = (byte)((s >> 8) & 0xff);
        c = (char)(s & 0xff);
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
        var shouldScrollDisplay = _displayY == _cursorY;
        _cursorY++;
        if (shouldScrollDisplay)
            _displayY = _cursorY;
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

    public void Resize(int width, int height)
    {
        var (oldWidth, oldHeight) = (_width, _height);
        _width = width;
        _height = height;

        var newBuffer = new short[width * height];

        var numLines = _height < oldHeight ? _height : oldHeight;
        var numChars = _width < oldWidth ? _width : oldWidth;
        for (var y = 0; y < numLines; y++)
        {
            for (var x = 0; x < numChars ; x++)
            {
                newBuffer[(height - 1 - y) * width + x] = _buffer[(_cursorY - y + oldHeight) % oldHeight * oldWidth + x];
            }
        }

        _buffer = newBuffer;
        _cursorY = _height - 1;
        _displayY = _cursorY;
    }
}
