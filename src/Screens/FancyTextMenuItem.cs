namespace MyGame.Screens;

public class FancyTextMenuItem : MenuItem
{
    public FancyTextComponent TextComponent;
    public BMFontType FontType = BMFontType.PixellariHuge;

    public override Rectangle Bounds
    {
        get
        {
            var align = FancyTextComponent.GetAlignVector(TextComponent.AlignH, TextComponent.AlignV);
            var offset = new Vector2(Width, Height) * align;

            return new Rectangle(
                (int)(Position.X - offset.X),
                (int)(Position.Y - offset.Y),
                Width,
                Height
            );
        }
    }
    
    public Vector2 ShadowOffset = Vector2.One * 5f;

    public FancyTextMenuItem(ReadOnlySpan<char> text, Action? callback = null)
    {
        TextComponent = new FancyTextComponent(text);
        var size = Shared.Game.Renderer.MeasureString(FontType, TextComponent.StrippedText);
        Width = (int)size.X;
        Height = (int)(size.Y * TextComponent.LineHeightScaling);
        Callback = callback;
    }

    public void Update(float deltaSeconds)
    {
        TextComponent.Update(deltaSeconds);
        Width = (int)TextComponent.LastRenderSize.X;
        Height = (int)TextComponent.LastRenderSize.Y;
    }

    public override void Draw(Vector2 position, Renderer renderer, Color color)
    {
        if (!IsVisible)
            return;

        TextComponent.Render(FontType, renderer, position + ShadowOffset , Color.Black * Alpha, 1.0f);
        TextComponent.Render(FontType, renderer, position, color * Alpha, 1.0f);
    }
}
