namespace MyGame.Debug;

public static class ConsoleToast
{
    private static char[] _tmp = new char[1];
    private static int _lastCursorY;
    private static int numLinesToDraw = 0;
    private static float _nextRemoveTime;
    private static float _lineDisplayDuration = 1.0f;
    private static float _elapsedTime;
    private static float _lineRemovePercentage = 1.0f;
    private static float _speed = 1.0f;

    private static void Update()
    {
        if (_lastCursorY < Shared.Console.ScreenBuffer.CursorY)
        {
            var numNewLines = Shared.Console.ScreenBuffer.CursorY - _lastCursorY;
            var linesLeft = Shared.Console.ScreenBuffer.Height - numLinesToDraw;
            var linesToAdd = Math.Min(linesLeft, numNewLines);
            if (numLinesToDraw == 0)
                _nextRemoveTime = _elapsedTime + linesToAdd * _lineDisplayDuration;
            numLinesToDraw += linesToAdd;
            if (numLinesToDraw < 0)
            {
                Logs.LogInfo("numLinesToDrawBug");
            }
        }

        _lastCursorY = Shared.Console.ScreenBuffer.CursorY;

        _speed = Math.Max(numLinesToDraw / 5.0f, 1.0f);
        _elapsedTime += Shared.Game.Time.ElapsedTime * _speed;

        if (numLinesToDraw > 0 && _elapsedTime >= _nextRemoveTime)
        {
            numLinesToDraw--;
            _nextRemoveTime += _lineDisplayDuration;
        }

        _lineRemovePercentage = MathF.Clamp01((_nextRemoveTime - _elapsedTime) / _lineDisplayDuration);
    }

    public static void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        Update();

        if (numLinesToDraw == 0 || Shared.Console.ScreenBuffer.CursorY == 0)
            return;

        var charSize = new Point(10, 18);
        var displayPosition = new Vector2(0, 0);
        displayPosition.Y = _lineRemovePercentage < 0.2f ? charSize.Y * (MathF.Map(_lineRemovePercentage, 0f, 0.2f, 0f, 1f) - 1.0f) : 0;

        for (var i = 0; i < numLinesToDraw; i++)
        {
            var lineIndex = (Shared.Console.ScreenBuffer.CursorY - 1) - i;
            if (lineIndex < 0)
                break;

            var lineWidth = GetLineWidth(lineIndex);

            var lineRect = new Rectangle(0, (int)(displayPosition.Y + charSize.Y * (numLinesToDraw - 1 - i)), lineWidth * charSize.X, charSize.Y);
            
            // skip line if it's outside the render target
            if (lineRect.Y >= renderDestination.Height)
                continue;
            
            var lineAlpha = 1.0f; // i == numLinesToDraw - 1 ? _lineRemovePercentage : 1.0f;

            renderer.DrawRect(lineRect, Color.Black * 0.66f * lineAlpha);

            for (var j = 0; j < Shared.Console.ScreenBuffer.Width; j++)
            {
                Shared.Console.ScreenBuffer.GetChar(j, lineIndex, out var c, out var color);
                if (c == '\0')
                    break;
                if (ConsoleScreen.CanSkipChar(c))
                    continue;
                var charColor = ConsoleSettings.Colors[color] * lineAlpha;
                var position = new Vector2(lineRect.X + j * charSize.X, lineRect.Y);
                _tmp[0] = c;
                // renderer.DrawText(FontType.ConsolasMonoMedium, _tmp, position, 0, charColor);
                renderer.DrawFTText(_tmp, position, charColor);
            }
        }

        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
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
