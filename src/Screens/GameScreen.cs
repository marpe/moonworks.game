using System.Collections.Concurrent;
using System.Threading;
using MyGame.Cameras;

namespace MyGame.Screens;

public class GameScreen
{
    public Camera Camera { get; private set; }
    public World? World { get; private set; }

    private GraphicsDevice _device;

    private readonly MyGameMain _game;

    private ConcurrentQueue<Action> _queuedActions = new();

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
        Shared.Game.GameScreen.LoadWorld(() => new World(Shared.Game.GameScreen, Shared.Game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk));
    }

    public void LoadWorld(Func<World> worldLoader)
    {
        if (Shared.LoadingScreen.IsLoading)
        {
            Logger.LogError("Loading screen is active, ignoring load call");
            return;
        }

        _queuedActions.Enqueue(() =>
        {
            Logger.LogInfo($"Removing screens from: {Thread.CurrentThread.ManagedThreadId} {Shared.Game.Time.UpdateCount} - {Shared.Game.Time.DrawCount}");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            Unload();
        });

        Shared.LoadingScreen.LoadAsync(() =>
        {
            var world = worldLoader();
            _queuedActions.Enqueue(() =>
            {
                Logger.LogInfo($"Settings world from: {Thread.CurrentThread.ManagedThreadId} {Shared.Game.Time.UpdateCount} - {Shared.Game.Time.DrawCount}");
                Shared.Menus.RemoveAll();
                Shared.Game.ConsoleScreen.IsHidden = true;
                World = world;
            });
        });
    }

    public void Unload()
    {
        Logger.LogInfo($"Unloading world from: {Thread.CurrentThread.ManagedThreadId}, {Shared.Game.Time.UpdateCount} - {Shared.Game.Time.DrawCount}");
        World?.Dispose();
        World = null;
    }

    public void UpdateQueued()
    {
        while (_queuedActions.TryDequeue(out var action))
        {
            action();
        }
    }

    public void Update(float deltaSeconds)
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

    public void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (World == null)
        {
            renderer.Clear(commandBuffer, renderDestination, Color.Black);
            return;
        }

        World.Draw(renderer, Camera.Bounds, alpha);
        var viewProjection = Camera.GetViewProjection(MyGameMain.DesignResolution.X, MyGameMain.DesignResolution.Y);
        renderer.Flush(commandBuffer, renderDestination, Color.Black, viewProjection);

        // TODO (marpe): Rneder post processing

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
