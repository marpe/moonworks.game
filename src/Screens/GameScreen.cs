using System.Threading;
using MyGame.Cameras;

namespace MyGame.Screens;

public class GameScreen
{
    public Camera Camera { get; private set; }
    public World? World { get; private set; }

    private GraphicsDevice _device;

    private readonly MyGameMain _game;

    private readonly object worldLock = new();

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        Camera = new Camera(this)
        {
            ClampToLevelBounds = true,
        };
    }


    [ConsoleHandler("restart")]
    public static void Restart()
    {
        Shared.LoadingScreen.QueueLoad(() =>
            {
                Shared.Game.GameScreen.SetWorld(new World(Shared.Game.GameScreen, Shared.Game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk));
                Shared.Game.ConsoleScreen.IsHidden = true;
            },
            () =>
            {
                Shared.Menus.RemoveAll();
                while (Shared.Game.GameScreen.World == null)
                {
                    Thread.Sleep(1);
                }
            }
        );
    }

    public void SetWorld(World? world)
    {
        lock (worldLock)
        {
            World?.Dispose();
            World = world;
        }
    }

    public void Update(float deltaSeconds)
    {
        lock (worldLock)
        {
            if (World == null)
                return;

            if (_game.InputHandler.IsKeyPressed(KeyCode.Escape))
            {
                Shared.Menus.AddScreen(Shared.Menus.PauseScreen);
                return;
            }

            World.Update(deltaSeconds, _game.InputHandler);
            Camera.Update(deltaSeconds, _game.InputHandler);
        }
    }

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        lock (worldLock)
        {
            if (World == null)
            {
                renderer.Clear(commandBuffer, renderDestination, Color.Black);
                return;
            }

            World.Draw(renderer, Camera.Bounds, alpha);
            var viewProjection = Camera.GetViewProjection(MyGameMain.DesignResolution.X, MyGameMain.DesignResolution.Y);
            renderer.Flush(commandBuffer, renderDestination, Color.Black, viewProjection);
        }

        DrawViewBounds(renderer, commandBuffer, renderDestination);
    }

    private void DrawViewBounds(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination)
    {
        if (!World.Debug)
            return;

        renderer.DrawRect(Vector2.Zero, MyGameMain.DesignResolution, Color.LimeGreen, 10f);
        renderer.Flush(commandBuffer, renderDestination, null, null);
    }
}
