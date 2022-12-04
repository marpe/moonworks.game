using System.Collections.Concurrent;
using System.Threading;
using MyGame.Cameras;
using MyGame.Screens.Transitions;

namespace MyGame.Screens;

public class GameScreen
{
    public ContentManager Content;
    public Camera Camera { get; private set; }
    public World? World { get; private set; }

    private readonly MyGameMain _game;
    public MyGameMain Game => _game;

    private ConcurrentQueue<Action> _queuedActions = new();

    public static int GameUpdateRate = 1;
    public static bool IsStepping = false;
    public static bool IsPaused = false;
    public static bool DebugViewBounds = false;

    public GameScreen(MyGameMain game)
    {
        _game = game;

        Content = new ContentManager(game.GraphicsDevice);
        
        Camera = new Camera(game.GameRenderSize.X, game.GameRenderSize.Y)
        {
            ClampToLevelBounds = true,
        };
    }

    [ConsoleHandler("restart")]
    public static void Restart(bool immediate = true)
    {
        var worldLoader = () => new World(Shared.Game.GameScreen, ContentPaths.ldtk.Example.World_ldtk);
        if (immediate)
        {
            Shared.Game.GameScreen.QueueSetWorld(worldLoader());
        }
        else
        {
            Shared.Game.GameScreen.LoadWorld(worldLoader);
        }
    }

    public void QueueSetWorld(World world)
    {
        QueueAction(() =>
        {
            Logs.LogInfo($"Setting world from thread: {Thread.CurrentThread.ManagedThreadId}");
            Shared.Menus.RemoveAll();
            Shared.Game.ConsoleScreen.IsHidden = true;
            Unload();
            World = world;
            Logs.LogInfo("World set!");
        });
    }

    [ConsoleHandler("step")]
    public static void Step()
    {
        IsPaused = true;
        IsStepping = true;
        Logs.LogInfo("Stepping...");
    }

    [ConsoleHandler("pause")]
    public static void Pause()
    {
        IsPaused = !IsPaused;
        Logs.LogInfo(IsPaused ? "Game paused" : "Game resumed");
    }

    [ConsoleHandler("speed_up")]
    public static void IncreaseUpdateRate()
    {
        GameUpdateRate -= 5;
        if (GameUpdateRate < 1)
            GameUpdateRate = 1;
        Logs.LogInfo($"UpdateRate: {GameUpdateRate}");
    }

    [ConsoleHandler("speed_down")]
    public static void DecreaseUpdateRate()
    {
        GameUpdateRate += 5;
        Logs.LogInfo($"UpdateRate: {GameUpdateRate}");
    }

    public void LoadWorld(Func<World> worldLoader)
    {
        if (Shared.LoadingScreen.IsLoading)
        {
            Logs.LogError("Loading screen is active, ignoring load call");
            return;
        }

        QueueAction(() =>
        {
            Logs.LogInfo($"Removing screens from thread: {Thread.CurrentThread.ManagedThreadId}");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            Unload();
        });

        Shared.LoadingScreen.LoadAsync(() =>
        {
            var world = worldLoader();
            QueueSetWorld(world);
        });
    }

    public void QueueAction(Action callback)
    {
        _queuedActions.Enqueue(callback);
    }

    public void Unload()
    {
        if (World == null)
            return;
        Logs.LogInfo($"Unloading world from thread: {Thread.CurrentThread.ManagedThreadId}");
        World.Dispose();
        World = null;
    }

    public void ExecuteQueuedActions()
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

        World.UpdateLastPositions();

        if (_game.InputHandler.IsKeyPressed(KeyCode.Escape))
        {
            Shared.Menus.AddScreen(Shared.Menus.PauseScreen);
            return;
        }

        var doUpdate = IsStepping ||
                       (int)Shared.Game.Time.UpdateCount % GameUpdateRate == 0 && !IsPaused;

        if (doUpdate)
        {
            World.Update(deltaSeconds, _game.InputHandler);
        }
        else
        {
            if (Camera.NoClip)
                Camera.Update(deltaSeconds, _game.InputHandler);
        }

        SetCircleCropPosition();

        if (IsStepping)
        {
            IsStepping = false;
            return;
        }
    }

    private void SetCircleCropPosition()
    {
        if (World == null)
            return;

        var entity = World.Player;
        var transform = entity.LastTransform;
        var viewProjection = Camera.GetViewProjection(_game.GameRender.Width, _game.GameRender.Height);
        var halfSize = entity.Size.ToVec2() * 0.5f;
        var playerInScreen = Vector2.Transform(halfSize, transform * viewProjection);
        playerInScreen = Vector2.Half + playerInScreen * 0.5f;
        var circleLoad = (CircleCropTransition)LoadingScreen.SceneTransitions[TransitionType.CircleCrop];
        circleLoad.CenterX = playerInScreen.X;
        circleLoad.CenterY = playerInScreen.Y;
    }

    public void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (World == null)
        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            return;
        }

        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            World.Draw(renderer, Camera, alpha);

            // draw ambient background color
            // renderer.DrawRect(Camera.Position - Camera.ZoomedSize * 0.5f, (Camera.Position + Camera.ZoomedSize * 0.5f).Ceil(), Color.Black * 0.75f);

            var viewProjection = Camera.GetViewProjection(renderDestination.Width, renderDestination.Height);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);
        }

        {
            World.DrawLights(renderer, ref commandBuffer, renderDestination, alpha);
        }


        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }

    private void DrawViewBounds(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        if (!World.Debug) return;
        if (!DebugViewBounds) return;

        renderer.DrawRectOutline(Vector2.Zero, _game.CompositeRender.Size(), Color.LimeGreen, 10f);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }
}
