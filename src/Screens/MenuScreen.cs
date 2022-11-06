using MoonWorks.Graphics.Font;
using MyGame.Graphics;

namespace MyGame.Screens;

public class MenuItem
{
    public string Text;
    public Action Callback;

    public MenuItem(string text, Action callback)
    {
        Text = text;
        Callback = callback;
    }
}

public class MenuScreen
{
    public bool IsHidden { get; private set; } = true;

    private MyGameMain _game;

    private int _selectedIndex = -1;
    private List<MenuItem> _menuItems;
    private readonly FancyTextComponent _title;
    private Point Position;

    public MenuScreen(MyGameMain game)
    {
        _game = game;

        _title = new FancyTextComponent("<~>Menu</~>");
        _menuItems = new(new[]
        {
            new MenuItem("Resume", () => { IsHidden = !IsHidden; }),
            new MenuItem("New Game", OnPlay),
            new MenuItem("Options", () => { }),
            new MenuItem("Quit", OnQuit),
        });

        _selectedIndex = 0;
    }


    private void OnPlay()
    {
    }

    private void OnQuit()
    {
        _game.Quit();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;

        if (allowKeyboardInput && input.IsKeyPressed(KeyCode.Escape))
            IsHidden = !IsHidden;

        if (IsHidden)
            return;

        Position = _game.MainWindow.Size / 2;
        _title.Position = Position + new Vector2(0, -60);
        _title.Update(deltaSeconds);

        if (allowKeyboardInput)
        {
            if (input.IsKeyPressed(KeyCode.Down) || input.IsKeyPressed(KeyCode.S))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuItems.Count;
            }
            else if (input.IsKeyPressed(KeyCode.Up) || input.IsKeyPressed(KeyCode.W))
            {
                _selectedIndex = (_menuItems.Count + _selectedIndex - 1) % _menuItems.Count;
            }

            if (input.IsKeyPressed(KeyCode.Return) || input.IsKeyPressed(KeyCode.Space))
            {
                if (_selectedIndex != -1)
                {
                    _menuItems[_selectedIndex].Callback.Invoke();
                }
            }
        }
    }

    public void Draw(Renderer renderer, double alpha)
    {
        if (!IsHidden)
        {
            renderer.DrawRect(renderer.RenderRect, Color.Black * 0.5f, 0f);

            var font = BMFontType.ConsolasMonoHuge;
            // var font = renderer.TextBatcher.GetFont(FontType.ConsolasMonoLarge);
            _title.Render(font, renderer, alpha);

            var position = Position;
            var lineHeight = 50;

            for (var i = 0; i < _menuItems.Count; i++)
            {
                var color = _selectedIndex == i ? Color.Red : Color.White;
                renderer.DrawText(FontType.RobotoLarge, _menuItems[i].Text, position, 0, color, HorizontalAlignment.Center,
                    VerticalAlignment.Middle);
                position.Y += lineHeight;
            }

            // TODO (marpe): Flush text batcher to sprite batch here, otherwise if console screen isnt being redrawn,
            // these text draw calls will be submitted after prev console render has been drawn 
            renderer.TextBatcher.FlushToSpriteBatch(renderer.SpriteBatch);
        }

        /*var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.FlushBatches(swap, viewProjection);*/
    }
}
