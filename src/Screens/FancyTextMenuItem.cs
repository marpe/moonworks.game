namespace MyGame.Screens;

public class FancyTextMenuItem : MenuItem
{
    public FancyTextComponent TextComponent;
    public BMFontType FontType = BMFontType.PixellariHuge;

    public override Rectangle Bounds
    {
        get
        {
            var alignV = TextComponent.AlignV switch
            {
                AlignV.Baseline => 0,
                AlignV.Top => 0,
                AlignV.Middle => 0.5f,
                _ => 1f
            };
            var alignH = TextComponent.AlignH switch
            {
                AlignH.Left => 0,
                AlignH.Center => 0.5f,
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

    public FancyTextMenuItem(ReadOnlySpan<char> text)
    {
        TextComponent = new FancyTextComponent(text);
        var size = Shared.Game.Renderer.GetFont(FontType).MeasureString(TextComponent.StrippedText);
        Width = (int)size.X;
        Height = (int)size.Y;
    }

    public void Update(float deltaSeconds)
    {
        TextComponent.Update(deltaSeconds);
    }

    public override void Draw(Vector2 position, Renderer renderer, Color color)
    {
        if (!IsVisible)
            return;
        
        TextComponent.Render(FontType, renderer, position + new Vector2(5, 5), Color.Black * Alpha, 1.0f);
        TextComponent.Render(FontType, renderer, position, color * Alpha, 1.0f);
    }
}
