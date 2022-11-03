using MyGame.Cameras;
using MyGame.Components;
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
            var sw2 = Stopwatch.StartNew();
            var ldtkPath = ContentPaths.ldtk.Example.World_ldtk;
            var jsonString = File.ReadAllText(ldtkPath);
            var loadTime = sw2.ElapsedMilliseconds;
            sw2.Restart();
            var ldtkJson = LdtkJson.FromJson(jsonString);
            var parseTime = sw2.ElapsedMilliseconds;
            sw2.Restart();
            var textures = LoadTextures(_game.GraphicsDevice, ldtkPath, ldtkJson.Defs.Tilesets);
            var textureLoadTime = sw2.ElapsedMilliseconds;
            sw2.Restart();

            _world = new World(ldtkJson, textures);

            _world.Initialize();

            _camera.Position = ((Vector2)_world.WorldSize) * 0.5f;
            _camera.Zoom = 2.0f;

            var setupTime = sw2.ElapsedMilliseconds;
            sw2.Restart();
            Logger.LogInfo($"LDtk Load: {loadTime} ms, Parse: {parseTime} ms, Textures: {textureLoadTime} ms, Setup: {setupTime} ms");
        });
    }


    private static Dictionary<long, Texture> LoadTextures(GraphicsDevice device, string ldtkPath, TilesetDefinition[] tilesets)
    {
        var textures = new Dictionary<long, Texture>();

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var tilesetDef in tilesets)
        {
            if (string.IsNullOrWhiteSpace(tilesetDef.RelPath))
                continue;
            var tilesetPath = Path.Combine(Path.GetDirectoryName(ldtkPath) ?? "", tilesetDef.RelPath);
            if (tilesetPath.EndsWith(".aseprite"))
            {
                var asepriteTexture = TextureUtils.LoadAseprite(device, tilesetPath);
                textures.Add(tilesetDef.Uid, asepriteTexture);
            }
            else
            {
                var texture = Texture.LoadPNG(device, commandBuffer, tilesetPath);
                textures.Add(tilesetDef.Uid, texture);
            }
        }

        device.Submit(commandBuffer);

        return textures;
    }

    public void Unload()
    {
        _world?.Dispose();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;
        _cameraController.Update(deltaSeconds, input, allowMouseInput, allowKeyboardInput);
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
