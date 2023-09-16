namespace MyGame.Debug;

public static class ConsoleToast
{
    private static char[] _tmp = new char[1];
    private static int _lastCursorY;
    private static int _numLinesToDraw = 0;
    private static float _nextRemoveTime;
    private static float _lineDisplayDuration = 1.0f;
    private static float _elapsedTime;
    private static float _lineRemovePercentage = 1.0f;
    private static float _speed = 2.0f;
    private static Point _charSize = new Point(10, 18);

    public static void Update(float deltaSeconds)
    {
        if (_lastCursorY < Shared.Console.ScreenBuffer.CursorY)
        {
            var numNewLines = Shared.Console.ScreenBuffer.CursorY - _lastCursorY;
            var linesLeft = Shared.Console.ScreenBuffer.Height - _numLinesToDraw;
            var linesToAdd = Math.Min(linesLeft, numNewLines);
            if (_numLinesToDraw == 0)
                _nextRemoveTime = _elapsedTime + _lineDisplayDuration;
            _numLinesToDraw += linesToAdd;
            _speed = Math.Max(_numLinesToDraw / 5.0f, 1.0f);
        }

        _lastCursorY = Shared.Console.ScreenBuffer.CursorY;

        _elapsedTime += deltaSeconds * _speed;

        if (_numLinesToDraw > 0 && _elapsedTime >= _nextRemoveTime)
        {
            _numLinesToDraw--;
            _nextRemoveTime += _lineDisplayDuration;
        }

        _lineRemovePercentage = MathF.Clamp01((_nextRemoveTime - _elapsedTime) / _lineDisplayDuration);
    }

    public static void Draw(Renderer renderer, int maxHeight)
    {
        if (_numLinesToDraw == 0 || Shared.Console.ScreenBuffer.CursorY == 0)
            return;

        IterateLines(renderer, maxHeight, DrawContainer);
        IterateLines(renderer, maxHeight, DrawLine);
    }

    private static void DrawContainer(Renderer renderer, Rectangle lineRect, int lineIndex)
    {
        var lineAlpha = 1.0f; // i == numLinesToDraw - 1 ? _lineRemovePercentage : 1.0f;
        renderer.DrawRect(lineRect, Color.Black * 0.66f * lineAlpha);
    }

    private static void DrawLine(Renderer renderer, Rectangle lineRect, int lineIndex)
    {
        for (var j = 0; j < Shared.Console.ScreenBuffer.Width; j++)
        {
            Shared.Console.ScreenBuffer.GetChar(j, lineIndex, out var c, out var color);
            if (c == '\0')
                break;
            if (ConsoleScreen.CanSkipChar(c))
                continue;
            var charColor = ConsoleSettings.Colors[color];
            var position = new Vector2(lineRect.X + j * _charSize.X, lineRect.Y);
            _tmp[0] = c;
            renderer.DrawFTText(BMFontType.ConsolasMonoSmall, _tmp, position, charColor);
        }
    }

    private static void IterateLines(Renderer renderer, int maxHeight, Action<Renderer, Rectangle, int> drawCallback)
    {
        var displayPosition = new Vector2(0, 0);
        displayPosition.Y = _lineRemovePercentage < 0.2f
            ? _charSize.Y * (MathF.Map(_lineRemovePercentage, 0f, 0.2f, 0f, 1f) - 1.0f)
            : 0;

        for (var i = 0; i < _numLinesToDraw; i++)
        {
            var lineIndex = (Shared.Console.ScreenBuffer.CursorY - 1) - i;
            if (lineIndex < 0)
                break;

            var lineWidth = GetLineWidth(lineIndex);
            var lineRect = new Rectangle(
                0,
                (int)(displayPosition.Y + _charSize.Y * (_numLinesToDraw - 1 - i)),
                lineWidth * _charSize.X,
                _charSize.Y
            );

            // skip line if it's outside the render target
            if (lineRect.Y >= maxHeight)
                continue;

            drawCallback(renderer, lineRect, lineIndex);
        }
    }

    private static int GetLineWidth(int lineIndex)
    {
        var lineWidth = 0;
        for (var j = 0; j < Shared.Console.ScreenBuffer.Width; j++)
        {
            Shared.Console.ScreenBuffer.GetChar(j, lineIndex, out var c, out var color);
            if (c == '\0')
                break;
            lineWidth++;
        }

        return lineWidth;
    }
}
