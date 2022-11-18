namespace MyGame.Screens;

public class MainMenuScreen : MenuScreen
{
    private readonly Texture _background;

    public MainMenuScreen(MenuManager menuManager) : base(menuManager)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("<~><#ff0000>Menu</#></~>")
            {
                IsEnabled = false
            },
            new TextMenuItem("New Game", OnPlay),
            new TextMenuItem("Options", () => { menuManager.SetActiveMenu(Menus.Options); }),
            new TextMenuItem("Quit", OnQuit),
        });

        _background = TextureUtils.LoadPngTexture(menuManager.Game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (_menuManager.Game.GameScreen.World == null)
        {
            var scale = new Vector2(
                renderDestination.Width / (float)_background.Width,
                renderDestination.Height / (float)_background.Height
            );

            renderer.DrawSprite(_background, Matrix4x4.CreateScale(scale.X, scale.Y, 1.0f), Color.White, 0);
        }

        base.Draw(renderer, commandBuffer, renderDestination, alpha);
    }

    private void OnPlay()
    {
        Shared.LoadingScreen.QueueLoad(() =>
        {
            Shared.Game.GameScreen.World = new World(Shared.Game.GameScreen, Shared.Game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);
            IsHidden = true;
        });
    }

    private void OnQuit()
    {
        _menuManager.Game.Quit();
    }
}
