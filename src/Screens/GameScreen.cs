using System.Threading;
using MyGame.Cameras;
using MyGame.Graphics;
using MyGame.TWConsole;

namespace MyGame.Screens;

public class GameScreen
{
    private Camera _camera;
    public Camera Camera => _camera;

    private MyGameMain _game;
    private GraphicsDevice _device;
    private CameraController _cameraController;
    public CameraController CameraController => _cameraController;

    private World? _world;

    public World? World => _world;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadWorld();

        _camera = new Camera();
        _cameraController = new CameraController(this, _camera);
    }

    [ConsoleHandler("restart")]
    public static void Restart()
    {
        Shared.Game.GameScreen.LoadWorld();
    }

    public void LoadWorld()
    {
        _game.LoadingScreen.StartLoad(() =>
        {
            _world = new World(this, _game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);
            _camera.Zoom = 4.0f;
        });
    }

    public void Unload()
    {
        _world?.Dispose();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;
        _cameraController.Update(deltaSeconds, input, allowMouseInput, allowKeyboardInput);

        _world?.Update(deltaSeconds, input, allowKeyboardInput, allowMouseInput);
    }

    public void Draw(Renderer renderer, double alpha)
    {
        // not sure why but if i don't render anything here the first loading screen gets weird and renders at a small size
        var swapSize = new Point((int)renderer.SwapTexture.Width, (int)renderer.SwapTexture.Height);
        renderer.DrawRect(new Rectangle(0, 0, swapSize.X, swapSize.Y), Color.Black);
        
        _world?.Draw(renderer, _camera, alpha);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        renderer.FlushBatches(renderer.SwapTexture, _cameraController.GetViewProjection(alpha), renderer.DefaultClearColor);
    }
}
