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
    public Action? Callback;

    public bool IsSelectable => IsVisible && IsEnabled;
    public abstract Rectangle Bounds { get; }

    public abstract void Draw(Vector2 position, Renderer renderer, Color color);
    
    public static (float alignX, float alignY) GetAlignment(HorizontalAlignment alignH, VerticalAlignment alignV)
    {
        var alignX = alignH switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Center => 0.5f,
            _ => 1f
        };
        var alignY = alignV switch
        {
            VerticalAlignment.Baseline => 0,
            VerticalAlignment.Top => 0,
            VerticalAlignment.Middle => 0.5f,
            _ => 1f
        };
        return (alignX, alignY);
    }
}

public class TextMenuItem : MenuItem
{
    public static BMFont.DrawCall[] DrawBuffer = new BMFont.DrawCall[1024];

    static TextMenuItem()
    {
        for (var i = 0; i < DrawBuffer.Length; i++)
        {
            DrawBuffer[i] = new BMFont.DrawCall
            {
                Colors = new Color[4],
            };
        }
    }
    
    public static void SortAndFlushBuffer(Renderer renderer, int numDrawCalls)
    {
        DrawBuffer.AsSpan(0, numDrawCalls)
            .Sort((a, b) => a.Sprite.TextureSlice.Texture.Handle.CompareTo(b.Sprite.TextureSlice.Texture.Handle));
        
        for (var i = 0; i < numDrawCalls; i++)
        {
            ref var drawCall = ref DrawBuffer[i];
            renderer.DrawSprite(drawCall.Sprite, drawCall.Transform, drawCall.Colors, drawCall.Depth);
        }
    }
    
    private string _text;
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            UpdateSize();
        }
    }

    public HorizontalAlignment AlignH = HorizontalAlignment.Center;
    public VerticalAlignment AlignV = VerticalAlignment.Middle;
    public BMFontType FontType = BMFontType.PixellariLarge;

    public Vector2 ShadowOffset = Vector2.One * 5f;

    public override Rectangle Bounds
    {
        get
        {
            var (alignX, alignY) = GetAlignment(AlignH, AlignV);
            var offset = new Vector2(Width, Height) * new Vector2(alignX, alignY);

            return new Rectangle(
                (int)(Position.X - offset.X),
                (int)(Position.Y - offset.Y),
                Width,
                Height
            );
        }
    }
    
    public TextMenuItem(string text, Action callback)
    {
        _text = text;
        UpdateSize();
        Callback = callback;
    }

    private void UpdateSize()
    {
        var size = Shared.Game.Renderer.MeasureString(FontType, _text);
        Width = (int)size.X;
        Height = (int)size.Y;
    }

    public override void Draw(Vector2 position, Renderer renderer, Color color)
    {
        if (!IsVisible)
            return;
        
        if (Alpha < 0.01f)
            return;
        
        var (alignX, alignY) = GetAlignment(AlignH, AlignV);
        var offset = new Vector2(Width, Height) * new Vector2(alignX, alignY);
        
        var numDrawCalls = 0;
        renderer.DrawBMText(FontType, Text, position + ShadowOffset, offset, Vector2.One, 0, 0, Color.Black * Alpha, DrawBuffer, ref numDrawCalls);
        SortAndFlushBuffer(renderer, numDrawCalls);

        numDrawCalls = 0;
        renderer.DrawBMText(FontType, Text, position, offset, Vector2.One, 0, 0, color * Alpha, DrawBuffer, ref numDrawCalls);
        SortAndFlushBuffer(renderer, numDrawCalls);
    }
}
