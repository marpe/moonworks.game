using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public abstract class MenuScreen
{
    public static Color BackgroundColor = Color.CornflowerBlue * 0.5f;
    public static Color HighlightColor = Color.Yellow;
    public static Color DisabledColor = Color.Black * 0.66f;
    public static Color NormalColor = Color.White;

    protected Point Position = MyGameMain.DesignResolution / 2;

    protected readonly List<MenuItem> _menuItems = new();
    protected readonly MenuManager _menuManager;

    protected int _selectedIndex = 0;

    public MenuScreen(MenuManager menuManager)
    {
        _menuManager = menuManager;
    }

    protected void NextItem()
    {
        var startIndex = _selectedIndex;
        var numItems = _menuItems.Count;
        for (var i = 1; i < numItems; i++)
        {
            var j = (_menuItems.Count + startIndex + i) % _menuItems.Count;
            if (_menuItems[j].IsSelectable)
            {
                _selectedIndex = j;
                return;
            }
        }
    }

    protected void PreviousItem()
    {
        var startIndex = _selectedIndex;
        var numItems = _menuItems.Count;
        for (var i = 1; i < numItems; i++)
        {
            var j = (_menuItems.Count + startIndex - i) % _menuItems.Count;
            if (_menuItems[j].IsSelectable)
            {
                _selectedIndex = j;
                return;
            }
        }
    }

    public virtual void OnScreenShown()
    {
        // select first item
        _selectedIndex = 0;
        if (!_menuItems[_selectedIndex].IsSelectable)
        {
            NextItem();
        }
    }

    public virtual void OnCancelled()
    {
    }

    public virtual void Update(float deltaSeconds)
    {
        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Down) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.S))
        {
            NextItem();
        }
        else if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Up) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.W))
        {
            PreviousItem();
        }

        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Return) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Space))
        {
            _menuItems[_selectedIndex].Callback.Invoke();
        }

        if (_menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Escape) || _menuManager.Game.InputHandler.IsKeyPressed(KeyCode.Backspace))
        {
            OnCancelled();
        }

        // disable input for the next screen
        _menuManager.Game.InputHandler.MouseEnabled = _menuManager.Game.InputHandler.KeyboardEnabled = false;
    }

    public virtual void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var position = Position;
        var lineHeight = 50;

        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (!_menuItems[i].IsVisible)
                continue;
            var color = _selectedIndex == i ? HighlightColor : _menuItems[i].IsEnabled ? NormalColor : DisabledColor;
            renderer.DrawText(FontType.RobotoLarge, _menuItems[i].Text, position, 0, color, HorizontalAlignment.Center, VerticalAlignment.Middle);
            position.Y += lineHeight;
        }
    }
}
