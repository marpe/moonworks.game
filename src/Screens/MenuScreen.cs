using MoonWorks.Graphics.Font;

namespace MyGame.Screens;

public class MenuItem
{
    public Action Callback;
    public string Text;
    public bool IsVisible = true;

    public MenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
    }
}

public class MenuScreen
{
    private readonly FancyTextComponent _title;

    private readonly MyGameMain _game;
    private readonly List<MenuItem> _menuItems;

    private int _selectedIndex;
    private Point Position;
    private readonly MenuItem _resumeItem;
    public bool IsHidden { get; private set; } = true;

    public MenuScreen(MyGameMain game)
    {
        _game = game;

        _title = new FancyTextComponent("<~>Menu</~>");
        _resumeItem = new MenuItem("Resume", () => { IsHidden = !IsHidden; });
        _menuItems = new List<MenuItem>(new[]
        {
            _resumeItem,
            new MenuItem("New Game", OnPlay),
            new MenuItem("Options", () => { }),
            new MenuItem("Quit", OnQuit),
        });

        _selectedIndex = 0;
    }

    private void OnPlay()
    {
        _game.GameScreen.LoadWorld();
        IsHidden = true;
    }

    private void OnQuit()
    {
        _game.Quit();
    }

    public void Update(bool isPaused, float deltaSeconds)
    {
        if (isPaused)
            return;

        var input = _game.InputHandler;

        if (input.IsKeyPressed(KeyCode.Escape))
            SetVisible();

        if (IsHidden)
            return;

        Position = _game.MainWindow.Size / 2;
        _title.Position = Position + new Vector2(0, -60);
        _title.Update(deltaSeconds);

        if (input.IsKeyPressed(KeyCode.Down) || input.IsKeyPressed(KeyCode.S))
        {
            NextItem();
        }
        else if (input.IsKeyPressed(KeyCode.Up) || input.IsKeyPressed(KeyCode.W))
        {
            PreviousItem();
        }

        if (input.IsKeyPressed(KeyCode.Return) || input.IsKeyPressed(KeyCode.Space))
        {
            if (_selectedIndex != -1)
            {
                _menuItems[_selectedIndex].Callback.Invoke();
            }
        }

        // disable input for the next screen
        input.MouseEnabled = input.KeyboardEnabled = false;
    }

    public void SetVisible()
    {
        IsHidden = !IsHidden;
        _resumeItem.IsVisible = _game.GameScreen.World != null;

        // select first item
        _selectedIndex = -1;
        NextItem();
    }

    private void NextItem()
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

    private void PreviousItem()
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

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        if (IsHidden)
            return;

        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black * 0.5f);

        var font = BMFontType.ConsolasMonoHuge;
        // var font = renderer.TextBatcher.GetFont(FontType.ConsolasMonoLarge);
        _title.Render(font, renderer, alpha);

        var position = Position;
        var lineHeight = 50;

        for (var i = 0; i < _menuItems.Count; i++)
        {
            if (!_menuItems[i].IsVisible)
                continue;
            
            var color = _selectedIndex == i ? Color.Red : Color.White;
            renderer.DrawText(FontType.RobotoLarge, _menuItems[i].Text, position, 0, color, HorizontalAlignment.Center,
                VerticalAlignment.Middle);
            position.Y += lineHeight;
        }

        // TODO (marpe): fix this ugliness
        // Flush text batcher to sprite batch here, otherwise if console screen isnt being redrawn,
        // these text draw calls will be submitted after prev console render has been drawn 
        renderer.TextBatcher.FlushToSpriteBatch(renderer.SpriteBatch);
    }
}
