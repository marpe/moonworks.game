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
        _game.LoadingScreen.StartLoad(() =>
        {
            World = new World(this, _game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);
            Logger.LogInfo("World loaded...");
        });
    }

    public void Unload()
    {
        Logger.LogInfo("Unloading world...");
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

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (World == null)
            return;

        if (World.IsDisposed)
        {
            // TODO (marpe): Not able to replicate this issue
            Logger.LogError("World is disposed");
            World = null;
            return;
        }

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
