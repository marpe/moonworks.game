using MyGame.Cameras;
using MyGame.Graphics;
using MyGame.TWConsole;

namespace MyGame.Screens;

public class GameScreen
{
    private GraphicsDevice _device;

    private readonly MyGameMain _game;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadWorld();

        Camera = new Camera();
        CameraController = new CameraController(this, Camera);
    }

    public Camera Camera { get; }

    public CameraController CameraController { get; }

    public World? World { get; private set; }

    [ConsoleHandler("restart")]
    public static void Restart()
    {
        Shared.Game.GameScreen.LoadWorld();
    }

    public void LoadWorld()
    {
        _game.LoadingScreen.StartLoad(() =>
        {
            World = new World(this, _game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);
            Camera.Zoom = 4.0f;
        });
    }

    public void Unload()
    {
        World?.Dispose();
    }

    public void Update(bool isPaused, float deltaSeconds)
    {
        var input = _game.InputHandler;
        CameraController.Update(isPaused, deltaSeconds, input);
        World?.Update(isPaused, deltaSeconds, input);
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        // not sure why but if i don't render anything here the first loading screen gets weird and renders at a small size
        // so just draw a black rectangle ¯\_(ツ)_/¯
        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black);

        World?.Draw(renderer, Camera, alpha);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        renderer.FlushBatches(renderDestination, CameraController.GetViewProjection(alpha), renderer.DefaultClearColor);
    }
}
