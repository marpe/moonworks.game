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
        AddScreen(MainMenuScreen);

        PauseScreen = new PauseMenu(game);
        OptionsScreen = new OptionsMenuScreen(game);
    }

    public void AddScreen(MenuScreen screen)
    {
        if (Menus.Contains(screen))
        {
            Menus.Remove(screen);
            Logger.LogError("Screen already added, removing and readding");
        }

        Menus.Add(screen);
        screen.OnScreenAdded();
    }

    public void RemoveScreen(MenuScreen screen)
    {
        Menus.Remove(screen);
    }

    public void RemoveAll()
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
            _menusToUpdate[i].Update(deltaSeconds, isCoveredByOtherScreen);

            if (!isCoveredByOtherScreen)
                isCoveredByOtherScreen = _menusToUpdate[i].State == MenuScreenState.Active || _menusToUpdate[i].State == MenuScreenState.TransitionOn;
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
