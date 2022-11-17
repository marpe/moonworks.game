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

        PushImmediate(Menus.Main);
    }

    private void PushImmediate(Menus menu)
    {
        _menuStack.Push(_allMenus[menu]);
        _allMenus[menu].OnScreenShown();
    }

    public void QueuePushScreen(Menus menu)
    {
        Game.LoadingScreen.QueueLoad(() => { }, () => { PushImmediate(menu); });
    }

    public void PopImmediate()
    {
        var menuScreen = _menuStack.Pop();
    }

    public void QueuePopScreen()
    {
        Game.LoadingScreen.QueueLoad(() => { }, PopImmediate);
    }

    public void QueuePopAllAndPush(Menus menu)
    {
        Game.LoadingScreen.QueueLoad(() => { }, () =>
        {
            while (_menuStack.Count > 0)
                _menuStack.Pop();

            Game.GameScreen.Unload();

            PushImmediate(menu);
        });
    }

    public void Update(float deltaSeconds)
    {
        if (_menuStack.Count == 0)
            return;

        var topMenu = _menuStack.Peek();
        topMenu.Update(deltaSeconds);
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (_menuStack.Count == 0)
        {
            renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Transparent);
            renderer.End(commandBuffer, renderDestination, Color.Transparent, null);
            return;
        }

        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Transparent);
            
        var topMenu = _menuStack.Peek();
        topMenu.Draw(renderer, commandBuffer, renderDestination, alpha);

        renderer.End(commandBuffer, renderDestination, Color.Transparent, null);
    }
}
