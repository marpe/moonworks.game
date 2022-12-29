namespace MyGame.Screens;

public class MainMenuScreen : MenuScreen
{
    private readonly Texture _background;
    private readonly ConfirmScreen _confirmScreen;

    public MainMenuScreen(MyGameMain game) : base(game)
    {
        _menuItems.AddRange(new MenuItem[]
        {
            new FancyTextMenuItem("<~><#ff0000><!>Menu</!></#></~>")
            {
                IsEnabled = false
            },
            new FancyTextMenuItem("New Game", OnPlay)
            {
                FontType = BMFontType.PixellariLarge,
            },
            new FancyTextMenuItem("Options", () => { Shared.Menus.AddScreen(Shared.Menus.OptionsScreen); })
            {
                FontType = BMFontType.PixellariLarge,
            },
            new FancyTextMenuItem("Quit", OnQuit)
            {
                FontType = BMFontType.PixellariLarge,
            },
        });

        _confirmScreen = new ConfirmScreen(game, () => Shared.Game.Quit(), () => { });

        _background = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
    }

    private void OnPlay()
    {
        MyGameMain.Restart(false);
    }

    private void OnQuit()
    {
        Shared.Menus.AddScreen(_confirmScreen);
    }

    public override void Draw(Renderer renderer, double alpha)
    {
        var scale = new Vector2(
            _game.RenderTargets.CompositeRender.Width / (float)_background.Width,
            _game.RenderTargets.CompositeRender.Height / (float)_background.Height
        );

        renderer.DrawSprite(_background, Matrix4x4.CreateScale(scale.X, scale.Y, 1.0f), Color.White);

        base.Draw(renderer, alpha);
    }
}
