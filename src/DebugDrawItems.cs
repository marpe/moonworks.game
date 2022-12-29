namespace MyGame;

public enum DebugDrawType
{
    Rect,
    Text,
}

public class DebugDraw
{
    public Color Color;
    public Color OutlineColor;
    public DebugDrawType DrawType = DebugDrawType.Text;
    public Rectangle Rectangle;
    public string Text = "";
    public ulong UpdateCountAtDraw;
}

public class DebugDrawItems
{
    private readonly List<DebugDraw> _debugDrawCalls = new();

    public void AddText(ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        _debugDrawCalls.Add(new DebugDraw()
        {
            Color = color,
            Text = text.ToString(),
            Rectangle = new Rectangle((int)position.X, (int)position.Y, 0, 0),
            UpdateCountAtDraw = Shared.Game.Time.UpdateCount,
            DrawType = DebugDrawType.Text,
        });
    }
    
    public void AddRect(Rectangle rect, Color fill, Color outline)
    {
        _debugDrawCalls.Add(new DebugDraw()
        {
            Color = fill,
            OutlineColor = outline,
            Rectangle = rect,
            UpdateCountAtDraw = Shared.Game.Time.UpdateCount,
            DrawType = DebugDrawType.Rect,
        });
    }

    public void Render(Renderer renderer)
    {
        for (var i = _debugDrawCalls.Count - 1; i >= 0; i--)
        {
            if (_debugDrawCalls[i].UpdateCountAtDraw < Shared.Game.Time.UpdateCount)
            {
                _debugDrawCalls.RemoveAt(i);
            }
        }

        foreach (var debugDrawCall in _debugDrawCalls)
        {
            if (debugDrawCall.DrawType == DebugDrawType.Text)
            {
                renderer.DrawFTText(BMFontType.ConsolasMonoSmall, debugDrawCall.Text, debugDrawCall.Rectangle.Min(), debugDrawCall.Color);
            }
            else
            {
                renderer.DrawRectWithOutline(debugDrawCall.Rectangle, debugDrawCall.Color, debugDrawCall.OutlineColor);
            }
        }
    }
}
