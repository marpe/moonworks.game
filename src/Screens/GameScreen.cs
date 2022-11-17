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
        Shared.Game.GameScreen.LoadWorld();
        Shared.LoadingScreen.QueueLoad(() => { Shared.Game.ConsoleScreen.IsHidden = true; });
    }

    public void LoadWorld()
    {
        Shared.LoadingScreen.QueueLoad(() => { World = new World(this, _game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk); });
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
            _game.MenuManager.QueuePushScreen(Menus.Pause);
            return;
        }

        CameraController.Update(deltaSeconds, input);
        World?.Update(deltaSeconds, input);
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (World == null || World.IsDisposed)
            return;

        Camera.Size = MyGameMain.DesignResolution;
        Camera.Zoom = 4f;
        World.Draw(renderer, Camera.Bounds, alpha);

        var sz = MyGameMain.DesignResolution;
        var viewProjection = CameraController.GetViewProjection(sz.X, sz.Y);

        renderer.End(commandBuffer, renderDestination, Color.Magenta, viewProjection);

        // render view bounds
        renderer.DrawRect(Vector2.Zero, sz, Color.LimeGreen, 10f);
        renderer.End(commandBuffer, renderDestination, null, null);
    }
}
