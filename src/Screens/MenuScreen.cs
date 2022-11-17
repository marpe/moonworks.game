using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public abstract class MenuScreen
{
    protected readonly List<MenuItem> _menuItems = new();
    protected Point Position = Point.Zero;
    protected int _selectedIndex = 0;
    protected readonly MenuManager _menuManager;

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
            if (_menuItems[j].IsVisible)
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
            if (_menuItems[j].IsVisible)
            {
                _selectedIndex = j;
                return;
            }
        }
    }

    public virtual void OnPush()
    {
        // select first item
        _selectedIndex = 0;
        if (!_menuItems[_selectedIndex].IsVisible)
        {
            NextItem();
        }
    }

    public virtual void OnPop()
    {
    }

    protected void ExitScreen()
    {
        _menuManager.Pop();
    }

    public virtual void Update(float deltaSeconds)
    {
        Position = _menuManager.Game.MainWindow.Size / 2;

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
            if (_selectedIndex != -1)
            {
                _menuItems[_selectedIndex].Callback.Invoke();
            }
        }

        // disable input for the next screen
        _menuManager.Game.InputHandler.MouseEnabled = _menuManager.Game.InputHandler.KeyboardEnabled = false;
    }

    public virtual void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black * 0.5f);

        var position = Position;
        var lineHeight = 50;

        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (!_menuItems[i].IsVisible)
                continue;

            var color = _selectedIndex == i ? Color.Red : Color.White;
            renderer.DrawText(FontType.RobotoLarge, _menuItems[i].Text, position, 0, color, HorizontalAlignment.Center, VerticalAlignment.Middle);
            position.Y += lineHeight;
        }

        // TODO (marpe): fix this ugliness
        // Flush text batcher to sprite batch here, otherwise if console screen isnt being redrawn,
        // these text draw calls will be submitted after prev console render has been drawn 
        renderer.TextBatcher.FlushToSpriteBatch(renderer.SpriteBatch);
    }
}
