using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public class FancyMenuItem : MenuItem
{
    private FancyTextComponent _textComponent;
    
    public FancyMenuItem(string text, Action callback) : base(text, callback)
    {
        _textComponent = new FancyTextComponent(text);
    }

    public void Update(float deltaSeconds)
    {
        _textComponent.Update(deltaSeconds);
    }
    
    public override void Draw(Renderer renderer, Vector2 position, HorizontalAlignment alignH, VerticalAlignment alignV, Color color)
    {
        _textComponent.Position = position;
        _textComponent.Alpha = color.A / 255f;
        _textComponent.Render(BMFontType.ConsolasMonoHuge, renderer, 1.0f);
    }
}
