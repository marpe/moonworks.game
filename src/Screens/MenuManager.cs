namespace MyGame.Screens;

public enum Menus
{
    Main,
    Pause,
    Options
}

public class MenuManager
{
    public MyGameMain Game { get; }
    public MenuScreen[] _menuScreens;

    public int FocusedMenu = 0;
    public int PrevFocused;

    public bool IsHidden
    {
        get
        {
            for (var i = _menuScreens.Length - 1; i >= 0; i--)
            {
                if (!_menuScreens[i].IsHidden)
                    return false;
            }

            return true;
        }
    }

    public MenuManager(MyGameMain game)
    {
        Game = game;

        _menuScreens = new MenuScreen[]
        {
            new MainMenuScreen(this)
            {
                IsHidden = false
            },
            new PauseMenu(this),
            new OptionsMenuScreen(this)
        };
    }


    private void HideAll()
    {
        for (var i = 0; i < _menuScreens.Length; i++)
        {
            _menuScreens[i].IsHidden = true;
        }
    }

    public void Update(float deltaSeconds)
    {
        for (var i = 0; i < _menuScreens.Length; i++)
        {
            _menuScreens[i].Update(deltaSeconds);
        }
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (IsHidden)
        {
            renderer.DrawRect(RectangleExt.FromTexture(renderDestination), Color.Transparent);
            renderer.End(commandBuffer, renderDestination, Color.Transparent, null);
            return;
        }

        renderer.DrawRect(RectangleExt.FromTexture(renderDestination), Color.Black * 0.5f);
        renderer.End(commandBuffer, renderDestination, Color.Transparent, null);

        for (var i = 0; i < _menuScreens.Length; i++)
        {
            _menuScreens[i].Draw(renderer, commandBuffer, renderDestination, alpha);
        }

        renderer.End(commandBuffer, renderDestination, null, null);
    }

    public void SetActiveMenu(Menus menu)
    {
        PrevFocused = FocusedMenu;
        FocusedMenu = (int)menu;

        _menuScreens[PrevFocused].IsHidden = true;
        _menuScreens[FocusedMenu].IsHidden = false;
    }
}
