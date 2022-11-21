using MyGame.Cameras;

namespace MyGame.Screens;

public class GameScreen
{
    public Camera Camera { get; }

    public CameraController CameraController { get; }

    public World? World { get; internal set; }

    private GraphicsDevice _device;

    private readonly MyGameMain _game;
    private Action _pauseCallback;

    public GameScreen(MyGameMain game, Action pauseCallback)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        _pauseCallback = pauseCallback;

        Camera = new Camera();
        Camera.Size = MyGameMain.DesignResolution;
        Camera.Zoom = 4f;
        CameraController = new CameraController(this, Camera);
    }


    [ConsoleHandler("restart")]
    public static void Restart()
    {
        Shared.LoadingScreen.QueueLoad(() =>
        {
            Shared.Game.GameScreen.World = new World(Shared.Game.GameScreen, Shared.Game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);
            Shared.Game.ConsoleScreen.IsHidden = true;
        }, () => { Shared.Game.SetMenu(null); });
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
            _pauseCallback.Invoke();
            return;
        }

        CameraController.Update(deltaSeconds, input);
        World?.Update(deltaSeconds, input);
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        var sz = MyGameMain.DesignResolution;
        if (World == null)
        {
            renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black);
            // render view bounds
            if (World.Debug)
                renderer.DrawRect(Vector2.Zero, sz, Color.LimeGreen, 10f);
            renderer.Flush(commandBuffer, renderDestination, Color.Black, null);
            return;
        }
  
        World.Draw(renderer, Camera.Bounds, alpha);

        var viewProjection = CameraController.GetViewProjection(sz.X, sz.Y);

        renderer.Flush(commandBuffer, renderDestination, Color.Black, viewProjection);

        // render view bounds
        if (World.Debug)
        {
            renderer.DrawRect(Vector2.Zero, sz, Color.LimeGreen, 10f);
            renderer.Flush(commandBuffer, renderDestination, null, null);
        }
    }
}
