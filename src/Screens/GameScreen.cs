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
        World = null;
    }

    public void Update(float deltaSeconds)
    {
        var input = _game.InputHandler;

        if (input.IsKeyPressed(KeyCode.Escape))
        {
            _game.MenuManager.Push(Menus.Pause);
            return;
        }

        CameraController.Update(deltaSeconds, input);
        World?.Update(deltaSeconds, input);
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        if (World == null)
        {
            renderer.FlushBatches(renderDestination, Matrix4x4.Identity, renderer.DefaultClearColor);
            return;
        }

        Camera.Size = MyGameMain.DesignResolution;
        Camera.Zoom = 4f;
        World.Draw(renderer, Camera.Bounds, alpha);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        var sz = MyGameMain.DesignResolution;
        var viewProjection = CameraController.GetViewProjection(sz.X, sz.Y);

        renderer.FlushBatches(renderDestination, viewProjection, renderer.DefaultClearColor);
        
        // render view bounds
        var view = Matrix4x4.CreateTranslation(0, 0, -1000);
        var projection = Matrix4x4.CreateOrthographicOffCenter(0, sz.X, sz.Y, 0, 0.0001f, 10000f);
        renderer.DrawRect(Vector2.Zero, sz, Color.LimeGreen, 4f);
        renderer.FlushBatches(renderDestination, view * projection);
    }
}
