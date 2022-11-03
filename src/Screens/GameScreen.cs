using MyGame.Cameras;
using MyGame.Generated;
using MyGame.Graphics;

namespace MyGame.Screens;

public class GameScreen
{
    private Camera _camera;
    private MyGameMain _game;
    private GraphicsDevice _device;
    private CameraController _cameraController;
    private World? _world;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadWorld();

        _camera = new Camera();
        _cameraController = new CameraController(_camera);
    }

    private void LoadWorld()
    {
        Task.Run(() =>
        {
            _world = new World(_game.GraphicsDevice, ContentPaths.ldtk.Example.World_ldtk);

            _camera.Position = ((Vector2)_world.WorldSize) * 0.5f;
            _camera.Zoom = 2.0f;
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

        _world?.Update(deltaSeconds);
    }

    public void Draw(Renderer renderer)
    {
        _world?.Draw(renderer);

        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;

        _camera.Size = _game.MainWindow.Size;
        renderer.FlushBatches(renderer.SwapTexture, _cameraController.ViewProjection, renderer.DefaultClearColor);
    }


}
