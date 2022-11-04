using MyGame.Graphics;
using MyGame.Utils;

namespace MyGame;

public enum DebugDrawType
{
    Rect,
    Text
}

public class DebugDraw
{
    public Rectangle Rectangle;
    public Color Color;
    public ulong UpdateCountAtDraw;
    public string Text = "";
    public DebugDrawType DrawType = DebugDrawType.Text;
}

public class DebugDrawItems
{
    private List<DebugDraw> _debugDrawCalls = new();

    public void AddText(ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        _debugDrawCalls.Add(new DebugDraw()
        {
            Color = color,
            Text = text.ToString(),
            Rectangle = new Rectangle((int)position.X, (int)position.Y, 0, 0),
            UpdateCountAtDraw = Shared.Game.UpdateCount,
            DrawType = DebugDrawType.Text,
        });
    }

    public void Render(Renderer renderer)
    {
        for (var i = _debugDrawCalls.Count - 1; i >= 0; i--)
        {
            if (_debugDrawCalls[i].UpdateCountAtDraw < Shared.Game.UpdateCount)
                _debugDrawCalls.RemoveAt(i);
        }

        foreach (var debugDrawCall in _debugDrawCalls)
        {
            if (debugDrawCall.Text != null)
            {
                renderer.DrawText(FontType.ConsolasMonoMedium, debugDrawCall.Text, debugDrawCall.Rectangle.Min() + Vector2.One,
                    Color.Black);
                renderer.DrawText(FontType.ConsolasMonoMedium, debugDrawCall.Text, debugDrawCall.Rectangle.Min(), debugDrawCall.Color);
            }
            else
            {
                renderer.DrawRect(debugDrawCall.Rectangle, debugDrawCall.Color);
            }
        }
    }
}
