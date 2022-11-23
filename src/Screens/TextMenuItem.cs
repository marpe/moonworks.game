namespace MyGame.Screens;

public abstract class MenuItem
{
    public int Height;
    public int Width;
    public Vector2 Position;
    public Vector2 PreviousPosition;
    public bool IsVisible = true;
    public bool IsEnabled = true;
    public float Alpha = 0f;
    public Spring NudgeSpring = new();
    
    public bool IsSelectable => IsVisible && IsEnabled;
    public abstract Rectangle Bounds { get; }

    public abstract void Draw(Vector2 position, Renderer renderer, Color color);
}

public class TextMenuItem : MenuItem
{
    public Action Callback;
    public string Text;

    public HorizontalAlignment AlignH = HorizontalAlignment.Center;
    public VerticalAlignment AlignV = VerticalAlignment.Top;
    public FontType FontType = FontType.PixellariLarge;

    public TextMenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
        var size = Shared.Game.Renderer.TextBatcher.GetFont(FontType).MeasureString(text);
        Width = (int)size.X;
        Height = (int)size.Y;
    }

    public override Rectangle Bounds
    {
        get
        {
            var alignV = AlignV switch
            {
                VerticalAlignment.Baseline => 0,
                VerticalAlignment.Top => 0,
                VerticalAlignment.Middle => 0.5f,
                _ => 1f
            };
            var alignH = AlignH switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Center => 0.5f,
                _ => 1f
            };

            var offset = new Vector2(Width, Height) * new Vector2(alignH, alignV);

            return new Rectangle(
                (int)(Position.X - offset.X),
                (int)(Position.Y - offset.Y),
                Width,
                Height
            );
        }
    }

    public override void Draw(Vector2 position, Renderer renderer, Color color)
    {
        if (!IsVisible)
            return;
        renderer.DrawText(FontType, Text, position + new Vector2(5, 5), 0, Color.Black * Alpha, AlignH, AlignV);
        renderer.DrawText(FontType, Text, position, 0, color * Alpha, AlignH, AlignV);
    }
}
