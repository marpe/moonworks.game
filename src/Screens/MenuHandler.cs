namespace MyGame.Screens;

public class MenuHandler
{
    public List<MenuScreen> Menus = new();

    public MainMenuScreen MainMenuScreen;
    public PauseMenu PauseScreen;
    public OptionsMenuScreen OptionsScreen;

    public bool IsHidden => Menus.Count == 0;

    private List<MenuScreen> _menusToUpdate = new();

    public MenuHandler(MyGameMain game)
    {
        MainMenuScreen = new MainMenuScreen(game);
        PauseScreen = new PauseMenu(game);
        OptionsScreen = new OptionsMenuScreen(game);
    }

    public void PushMenu(MenuScreen menu)
    {
        Menus.Add(menu);
        menu.OnBecameVisible();
    }

    public void PopAll()
    {
        Menus.Clear();
    }

    public void Update(float deltaSeconds)
    {
        _menusToUpdate.Clear();
        _menusToUpdate.AddRange(Menus);

        var isCoveredByOtherScreen = false;
        for (var i = _menusToUpdate.Count - 1; i >= 0; i--)
        {
            if (isCoveredByOtherScreen)
                _menusToUpdate[i].SetState(MenuScreenState.Covered);

            _menusToUpdate[i].Update(deltaSeconds);

            if (_menusToUpdate[i].State == MenuScreenState.Exited)
            {
                Menus.RemoveAt(i);
                if (i > 0)
                    Menus[i - 1].SetState(MenuScreenState.Active);
                continue;
            }

            if (!isCoveredByOtherScreen)
                isCoveredByOtherScreen = (_menusToUpdate[i].State == MenuScreenState.Active);
        }
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (Menus.Count > 0)
        {
            foreach (var menu in Menus)
            {
                menu.Draw(renderer, alpha);
            }

            renderer.Flush(commandBuffer, renderDestination, Color.Transparent, null);
            return;
        }

        renderer.Clear(commandBuffer, renderDestination, Color.Transparent);
    }
}
