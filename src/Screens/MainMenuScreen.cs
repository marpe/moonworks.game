namespace MyGame.Screens;

public class MainMenuScreen : MenuScreen
{
    private readonly Texture _background;

    public MainMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new[]
        {
            new FancyMenuItem("<~><#ff0000>Menu</#></~>", () => { })
            {
                IsEnabled = false
            },
            new MenuItem("New Game", OnPlay),
            new MenuItem("Options", () => { menuManager.SetActiveMenu(Menus.Options); }),
            new MenuItem("Quit", OnQuit),
        });

        _background = TextureUtils.LoadPngTexture(menuManager.Game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var scale = new Vector2(
            renderDestination.Width / (float)_background.Width,
            renderDestination.Height / (float)_background.Height
        );

        renderer.DrawSprite(_background, Matrix4x4.CreateScale(scale.X, scale.Y, 1.0f), Color.White, 0);

        base.Draw(renderer, commandBuffer, renderDestination, alpha);
    }

    private void OnPlay()
    {
        _menuManager.Game.GameScreen.LoadWorld();
        IsHidden = true;
    }

    private void OnQuit()
    {
        _menuManager.Game.Quit();
    }
}
