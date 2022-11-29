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

    private Texture _copyRender;

    public GameScreen(MyGameMain game)
    {
        _game = game;

        Content = new ContentManager(game.GraphicsDevice);

        _copyRender = TextureUtils.CreateTexture(game.GraphicsDevice, _game.GameRender);

        Camera = new Camera(this)
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
            Logger.LogInfo($"Setting world from thread: {Thread.CurrentThread.ManagedThreadId}");
            Shared.Menus.RemoveAll();
            Shared.Game.ConsoleScreen.IsHidden = true;
            Unload();
            World = world;
            Logger.LogInfo("World set!");
        });
    }

    [ConsoleHandler("step")]
    public static void Step()
    {
        IsPaused = true;
        IsStepping = true;
        Logger.LogInfo("Stepping...");
    }

    [ConsoleHandler("pause")]
    public static void Pause()
    {
        IsPaused = !IsPaused;
        Logger.LogInfo(IsPaused ? "Game paused" : "Game resumed");
    }

    [ConsoleHandler("speed_up")]
    public static void IncreaseUpdateRate()
    {
        GameUpdateRate -= 5;
        if (GameUpdateRate < 1)
            GameUpdateRate = 1;
        Logger.LogInfo($"UpdateRate: {GameUpdateRate}");
    }

    [ConsoleHandler("speed_down")]
    public static void DecreaseUpdateRate()
    {
        GameUpdateRate += 5;
        Logger.LogInfo($"UpdateRate: {GameUpdateRate}");
    }

    public void LoadWorld(Func<World> worldLoader)
    {
        if (Shared.LoadingScreen.IsLoading)
        {
            Logger.LogError("Loading screen is active, ignoring load call");
            return;
        }

        QueueAction(() =>
        {
            Logger.LogInfo($"Removing screens from thread: {Thread.CurrentThread.ManagedThreadId}");
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
        Logger.LogInfo($"Unloading world from thread: {Thread.CurrentThread.ManagedThreadId}");
        World.Dispose();
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

    public Vector2 GetWorldPositionInScreen(Vector2 worldPosition)
    {
        var viewProjection = Camera.GetViewProjection(_game.GameRender.Width, _game.GameRender.Height);
        return Vector2.Transform(worldPosition, viewProjection);
    }

    public void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, double alpha)
    {
        if (World == null)
        {
            renderer.Clear(ref commandBuffer, renderDestination, Color.Black);
            return;
        }

        {
            World.Draw(renderer, Camera.Bounds, alpha);
            var viewProjection = Camera.GetViewProjection(renderDestination.Width, renderDestination.Height);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);
        }

        {
            World.DrawEntities(renderer, alpha);
            var viewProjection = Camera.GetViewProjection(renderDestination.Width, renderDestination.Height);
            renderer.RunRenderPass(ref commandBuffer, _copyRender, Color.Transparent, viewProjection);
            /*TextureUtils.EnsureTextureSize(ref _copyRender, _game.GraphicsDevice, renderDestination.Size());
            commandBuffer.CopyTextureToTexture(renderDestination, _copyRender, Filter.Nearest);*/

            var lights = new List<(Vector2, Vector3)>();

            for (var i = 0; i < 10; i++)
            {
                var a = MathHelper.TwoPi / i;
                lights.Add((World.Player.Position.Current + MathF.AngleToVector(a, 16),
                    new Vector3(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle())));
            }

            foreach (var (position, color) in lights)
            {
                renderer.DrawRect(Vector2.Zero, (Vector2.One * renderDestination.Size()), Color.Black);
                renderer.UpdateBuffers(ref commandBuffer);
                renderer.BeginRenderPass(ref commandBuffer, renderDestination, null, PipelineType.RimLight);
                
                
                var vertUniform = Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height);
                var fragmentBindings = new[] { new TextureSamplerBinding(), new TextureSamplerBinding(_copyRender, Renderer.PointClamp) };
                var fragUniform = new Pipelines.RimLightUniforms()
                {
                    LightColor = new Vector4(color.X, color.Y, color.Z, 1.0f),
                    LightIntensity = 1f,
                    LightRadius = 50f,
                    TexelSize = new Vector4(
                        1.0f / renderDestination.Width,
                        1.0f / renderDestination.Height,
                        renderDestination.Width,
                        renderDestination.Height
                    ),
                    ScreenSpaceLightPos = GetWorldPositionInScreen(position),
                };
                
                
                renderer.DrawIndexedSprites(ref commandBuffer, vertUniform, fragUniform, fragmentBindings);
                renderer.EndRenderPass(ref commandBuffer);
            }
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
