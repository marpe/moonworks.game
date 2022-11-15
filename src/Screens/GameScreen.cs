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
        _game.LoadingScreen.StartLoad(() => { World = new World(this, _game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk); });
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
        var rect = new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height);
        renderer.DrawRect(rect, Color.CornflowerBlue);

        // draw world
        Camera.Size = new Point(1920, 1080);
        Camera.Zoom = 4f;
        World?.Draw(renderer, Camera.Bounds, alpha);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        var view = Camera.View.ToMatrix4x4(); // CameraController.GetViewProjection(alpha, renderDestination.Width, renderDestination.Height);
        view.M43 = -1000;
        
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(renderDestination.Width, renderDestination.Height);

        var projection = Camera.GetProjection(renderDestination.Width, renderDestination.Height, false);

        renderer.FlushBatches(renderDestination, (view * viewportTransform) * projection, renderer.DefaultClearColor);
        
        // draw letter and pillar boxes
        renderer.DrawRect(new Rectangle(0, 0, viewport.X, viewport.Height), Color.Black);
        renderer.DrawRect(new Rectangle(viewport.X + viewport.Width, 0, (int)renderDestination.Width - (viewport.X + viewport.Width), (int)renderDestination.Height), Color.Black);
        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, viewport.Y), Color.Black);
        renderer.DrawRect(new Rectangle(0, viewport.Y + viewport.Height, (int)renderDestination.Width, (int)renderDestination.Height - (viewport.Y + viewport.Height)), Color.Black);
    }
}
