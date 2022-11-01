using MyGame.Cameras;
using MyGame.Components;
using MyGame.Graphics;

namespace MyGame.Screens;

public class GameScreen
{
    private Sprite? _spriteRenderer;
    private Sprite? _backgroundSprite;
    private Camera _camera;
    private MyGameMain _game;
    private GraphicsDevice _device;
    private CameraController _cameraController;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadLDtk();
        LoadTextures();

        _camera = new Camera();
        _cameraController = new CameraController(_camera);
    }

    private void LoadTextures()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var asepritePath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
            var asepriteTexture = TextureUtils.LoadAseprite(_device, asepritePath);
            _spriteRenderer = new Sprite(asepriteTexture);

            var menu = TextureUtils.LoadPngTexture(_device, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
            _backgroundSprite = new Sprite(menu);
            Logger.LogInfo($"Loaded textures in {sw2.ElapsedMilliseconds} ms");
        });
    }

    private void LoadLDtk()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var ldtkPath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Ldtk.MapLdtk);
            var jsonString = File.ReadAllText(ldtkPath);
            var ldtkJson = LdtkJson.FromJson(jsonString);
            Logger.LogInfo($"Loaded LDtk in {sw2.ElapsedMilliseconds} ms");
        });
    }

    public void Unload()
    {
        _spriteRenderer?.Texture.Dispose();
        _backgroundSprite?.Texture.Dispose();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var input = _game.InputHandler;
        _cameraController.Update(deltaSeconds, input, allowMouseInput, allowKeyboardInput);
    }

    public void Draw(Renderer renderer)
    {
        if (_backgroundSprite != null)
        {
            renderer.DrawSprite(_backgroundSprite.Value, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(-200, -100),
                Color.White,
                200f);
            renderer.DrawSprite(_backgroundSprite.Value, Matrix3x2.CreateScale(1f, 1f) * Matrix3x2.CreateTranslation(-200, -100),
                Color.White,
                180f);
        }

        renderer.DrawText(FontType.RobotoMedium, "Hello!", Vector2.Zero, Color.White);
        renderer.DrawText("In default font", new Vector2(100, 100), 0, Color.White);
        renderer.DrawText(FontType.RobotoMedium, "Hello again!", new Vector2(150, 150), Color.White);

        renderer.DrawBMText("BMFONT TEST", new Vector2(200, 0), 0, Color.White);
        
        _camera.Size = _game.MainWindow.Size;
        
        renderer.DepthStencilAttachmentInfo.LoadOp = LoadOp.Clear;
        renderer.DepthStencilAttachmentInfo.StencilLoadOp = LoadOp.Clear;
        renderer.FlushBatches(renderer.SwapTexture, _camera.ViewProjectionMatrix, renderer.DefaultClearColor);
    }
}
