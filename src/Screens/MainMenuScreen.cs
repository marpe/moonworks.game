namespace MyGame.Screens;

public class MainMenuScreen : MenuScreen
{
    private readonly FancyTextComponent _title;

    public MainMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _title = new FancyTextComponent("<~><#ff0000>Menu</#></~>");
        _menuItems.AddRange(new[]
        {
            new MenuItem("New Game", OnPlay),
            new MenuItem("Options", () => { menuManager.Push(Menus.Options); }),
            new MenuItem("Quit", OnQuit),
        });
    }

    private void OnPlay()
    {
        _menuManager.Game.GameScreen.LoadWorld();
        _menuManager.Pop();
    }

    private void OnQuit()
    {
        _menuManager.Game.Quit();
    }
    
    public override void Update(float deltaSeconds)
    {
        _title.Position = Position + new Vector2(0, -60);
        _title.Update(deltaSeconds);
        base.Update(deltaSeconds);
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        base.Draw(renderer, commandBuffer, renderDestination, alpha);
        _title.Render(BMFontType.ConsolasMonoHuge, renderer, alpha);
    }
}
