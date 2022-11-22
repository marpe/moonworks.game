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
            new FancyTextMenuItem("<~><#ff0000>Menu</#></~>")
            {
                IsEnabled = false
            },
            new TextMenuItem("New Game", OnPlay),
            new TextMenuItem("Options", () => { SetChild(Shared.Menus.OptionsScreen); }),
            new TextMenuItem("Quit", OnQuit),
        });

        _confirmScreen = new ConfirmScreen(game, () => Shared.Game.Quit(), () => { });

        _background = TextureUtils.LoadPngTexture(game.GraphicsDevice, ContentPaths.Textures.menu_background_png);
    }

    private void OnPlay()
    {
        Shared.LoadingScreen.QueueLoad(
            () => { Shared.Game.GameScreen.SetWorld(new World(Shared.Game.GameScreen, Shared.Game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk)); },
            () =>
            {
                Shared.Game.SetMenu(null);
                while (Shared.Game.GameScreen.World == null)
                {
                    Thread.Sleep(1);
                }
            }
        );
    }

    private void OnQuit()
    {
        SetChild(_confirmScreen);
    }
    
    public override void Draw(Renderer renderer, double alpha)
    {
        var scale = new Vector2(
            MyGameMain.DesignResolution.X / (float)_background.Width,
            MyGameMain.DesignResolution.Y / (float)_background.Height
        );

        renderer.DrawSprite(_background, Matrix4x4.CreateScale(scale.X, scale.Y, 1.0f), Color.White);

        base.Draw(renderer, alpha);
    }
}
