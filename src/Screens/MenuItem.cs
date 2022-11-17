using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public class MenuItem
{
    public Action Callback;
    public string Text;
    public bool IsVisible = true;
    public bool IsEnabled = true;
    public bool IsSelectable => IsVisible && IsEnabled;

    public MenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
    }

    public virtual void Draw(Renderer renderer, Vector2 position,  HorizontalAlignment alignH, VerticalAlignment alignV, Color color)
    {
        if (!IsVisible)
            return;
        renderer.DrawText(FontType.RobotoLarge, Text, position, 0, color, alignH, alignV);
    }
}
