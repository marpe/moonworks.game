using System.Threading;

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
            new TextMenuItem("New Game", OnPlay),
            new TextMenuItem("Options", () => { Shared.Menus.AddScreen(Shared.Menus.OptionsScreen); }),
            new TextMenuItem("Quit", OnQuit),
        });

        _confirmScreen = new ConfirmScreen(game, () => Shared.Game.Quit(), () => { });

        _background = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
    }

    private void OnPlay()
    {
        GameScreen.Restart(false);
    }

    private void OnQuit()
    {
        Shared.Menus.AddScreen(_confirmScreen);
    }
    
    public override void Draw(Renderer renderer, double alpha)
    {
        var scale = new Vector2(
            _game.CompositeRender.Width / (float)_background.Width,
            _game.CompositeRender.Height / (float)_background.Height
        );

        renderer.DrawSprite(_background, Matrix4x4.CreateScale(scale.X, scale.Y, 1.0f), Color.White);

        base.Draw(renderer, alpha);
    }
}
