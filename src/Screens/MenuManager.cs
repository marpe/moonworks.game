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
    private readonly Stack<MenuScreen> _menuStack = new();
    private readonly Dictionary<Menus, MenuScreen> _allMenus = new();
    public bool IsHidden => _menuStack.Count == 0;

    public MenuManager(MyGameMain game)
    {
        Game = game;

        _allMenus.Add(Menus.Main, new MainMenuScreen(this));
        _allMenus.Add(Menus.Options, new OptionsMenuScreen(this));
        _allMenus.Add(Menus.Pause, new PauseMenu(this));
    }

    public void Pop()
    {
        var menuScreen = _menuStack.Pop();
        menuScreen.OnPop();
    }
    
    public void PopAllAndPush(Menus menu)
    {
        while (_menuStack.Count > 0)
            _menuStack.Pop();
        Push(menu);
    }

    public void Push(Menus menu)
    {
        var menuScreen = _allMenus[menu];
        menuScreen.OnPush();
        _menuStack.Push(menuScreen);
    }

    public void Update(float deltaSeconds)
    {
        if (_menuStack.Count == 0)
            return;
        
        var topMenu = _menuStack.Peek();
        topMenu.Update(deltaSeconds);
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        if (_menuStack.Count == 0)
            return;

        var topMenu = _menuStack.Peek();
        topMenu.Draw(renderer, renderDestination, alpha);
    }
}
