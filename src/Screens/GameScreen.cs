using System.Collections.Concurrent;
using System.Threading;
using MyGame.Cameras;
using MyGame.Screens.Transitions;

namespace MyGame.Screens;

public class GameScreen
{
    public Camera Camera { get; private set; }

    private readonly MyGameMain _game;
    public MyGameMain Game => _game;

    private ConcurrentQueue<Action> _queuedActions = new();

    public static int GameUpdateRate = 1;
    public static bool IsStepping = false;
    public static bool IsPaused = false;
    public static bool DebugViewBounds = false;
    public World World { get; }

    public GameScreen(MyGameMain game)
    {
        _game = game;

        World = new();
        
        Camera = new Camera(game.RenderTargets.GameRenderSize.X, game.RenderTargets.GameRenderSize.Y)
        {
            ClampToLevelBounds = true,
        };
    }

    [ConsoleHandler("restart")]
    public static void Restart(bool immediate = true)
    {
        Logs.LogInfo($"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Starting load");
        var ldtkLoader = () => Shared.Content.LoadAndAddLDtkWithTextures(ContentPaths.ldtk.Example.World_ldtk);
        if (immediate)
        {
            Shared.Game.GameScreen.QueueSetLdtk(ldtkLoader(), World.NextLevel);
        }
        else
        {
            Shared.Game.GameScreen.LoadLdtk(ldtkLoader, World.NextLevel);
        }
    }

    public void QueueSetLdtk(LDtkAsset ldtk, Action? onCompleteCallback)
    {
        QueueAction(() =>
        {
            Logs.LogInfo($"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Removing screens and setting world from thread: {Thread.CurrentThread.ManagedThreadId}");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            Unload();
            World.SetLDtk(ldtk);
            onCompleteCallback?.Invoke();
        });
    }

    [ConsoleHandler("step")]
    public static void Step()
    {
        IsPaused = true;
        IsStepping = true;
        var updateCount = Shared.Game.GameScreen.World?.WorldUpdateCount ?? 0;
        Logs.LogInfo($"[WU:{updateCount}] Stepping...");
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

    public void LoadLdtk(Func<LDtkAsset> ldtkLoader, Action? onCompleteCallback)
    {
        if (Shared.LoadingScreen.IsLoading)
        {
            Logs.LogError("Loading screen is active, ignoring load call");
            return;
        }

        QueueAction(() =>
        {
            Logs.LogInfo($"[U:{Shared.Game.Time.UpdateCount}, D:{Shared.Game.Time.DrawCount}] Removing screens");
            Shared.Game.ConsoleScreen.IsHidden = true;
            Shared.Menus.RemoveAll();
            Unload();
        });

        Shared.LoadingScreen.LoadAsync(() =>
        {
            var ldtk = ldtkLoader();
            QueueSetLdtk(ldtk, onCompleteCallback);
        });
    }

    public void QueueAction(Action callback)
    {
        _queuedActions.Enqueue(callback);
    }

    public void Unload()
    {
        if (!World.IsLoaded)
            return;
        Logs.LogInfo($"Unloading world from thread: {Thread.CurrentThread.ManagedThreadId}");
        Camera.TrackEntity(null);
        Camera.LevelBounds = Rectangle.Empty;
        World.Unload();
    }

    public void ExecuteQueuedActions()
    {
        while (_queuedActions.TryDequeue(out var action))
        {
            action();
        }
    }

    public void UpdateLastPositions()
    {
        World?.UpdateLastPositions();
    }

    public void Update(float deltaSeconds)
    {
        if (!World.IsLoaded)
            return;

        var doUpdate = IsStepping ||
                       (int)Shared.Game.Time.UpdateCount % GameUpdateRate == 0 && !IsPaused;

        if (doUpdate)
        {
            // BindHandler.HandleBoundKeys();
            World.Update(deltaSeconds, _game.InputHandler, Camera);
        }
        else
        {
            if (Camera.NoClip)
                Camera.Update(deltaSeconds, _game.InputHandler);
        }

        if (IsStepping)
            IsStepping = false;
    }

    /// Call before loading starts
    private void SetCircleCropPosition(Vector2 position)
    {
        var viewProjection = Camera.GetViewProjection(_game.RenderTargets.GameRender.Width, _game.RenderTargets.GameRender.Height);
        var positionInScreen = Vector2.Transform(position, viewProjection);
        positionInScreen = Vector2.Half + positionInScreen * 0.5f;
        var circleLoad = (CircleCropTransition)LoadingScreen.SceneTransitions[TransitionType.CircleCrop];
        circleLoad.CenterX = positionInScreen.X;
        circleLoad.CenterY = positionInScreen.Y;
    }

    public void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (!World.IsLoaded)
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
            World.DrawLights(renderer, ref commandBuffer, renderDestination, Camera, _game.RenderTargets.LightSource, _game.RenderTargets.LightTarget, alpha);
        }


        DrawViewBounds(renderer, ref commandBuffer, renderDestination);
    }

    private void DrawViewBounds(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination)
    {
        if (!World.Debug) return;
        if (!DebugViewBounds) return;

        renderer.DrawRectOutline(Vector2.Zero, _game.RenderTargets.CompositeRender.Size, Color.LimeGreen, 10f);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null);
    }
}
