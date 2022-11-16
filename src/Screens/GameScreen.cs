using MyGame.Cameras;

namespace MyGame.Screens;

public class GameScreen
{
    public Camera Camera { get; }

    public CameraController CameraController { get; }

    public World? World { get; private set; }

    private GraphicsDevice _device;

    private readonly MyGameMain _game;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        Camera = new Camera();
        CameraController = new CameraController(this, Camera);
    }


    [ConsoleHandler("restart")]
    public static void Restart()
    {
        Shared.Game.ConsoleScreen.IsHidden = true;
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
        if (World == null)
        {
            renderer.FlushBatches(renderDestination, Matrix4x4.Identity, renderer.DefaultClearColor);
            return;
        }

        Camera.Size = new Point(1920, 1080);
        Camera.Zoom = 4f;
        World.Draw(renderer, Camera.Bounds, alpha);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        var cameraView = Camera.View.ToMatrix4x4(); // CameraController.GetViewProjection(alpha, renderDestination.Width, renderDestination.Height);
        cameraView.M43 = -1000;

        var screenResolution = new Point((int)renderDestination.Width, (int)renderDestination.Height);
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(
            screenResolution,
            new Point(1920, 1080)
        );

        var projection = Camera.GetProjection(renderDestination.Width, renderDestination.Height, false);
        var view = cameraView * viewportTransform;

        renderer.FlushBatches(renderDestination, view * projection, renderer.DefaultClearColor);

        DrawLetterAndPillarBoxes(renderer, screenResolution, viewport, Color.Black);
    }

    private static void DrawLetterAndPillarBoxes(Renderer renderer, Point screenSize, Rectangle viewport, Color color)
    {
        if (screenSize.X != viewport.Width)
        {
            var left = new Rectangle(0, 0, viewport.X, viewport.Height);
            var right = new Rectangle(viewport.X + viewport.Width, 0, screenSize.X - (viewport.X + viewport.Width), screenSize.Y);
            renderer.DrawRect(left, color);
            renderer.DrawRect(right, color);
        }

        if (screenSize.Y != viewport.Height)
        {
            var top = new Rectangle(0, 0, screenSize.X, viewport.Y);
            var bottom = new Rectangle(0, viewport.Y + viewport.Height, screenSize.X, screenSize.Y - (viewport.Y + viewport.Height));
            renderer.DrawRect(top, color);
            renderer.DrawRect(bottom, color);
        }
    }
}
