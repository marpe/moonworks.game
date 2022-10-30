using MyGame.Components;
using MyGame.Graphics;

namespace MyGame;

public interface IGameScreen
{
    void Update(float deltaSeconds);
    void Draw(Renderer renderer);
}

public class GameScreen : IGameScreen
{
    private Sprite? _spriteRenderer;
    private Sprite? _backgroundSprite;
    private readonly Camera _camera;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    private MyGameMain _game;
    private readonly GraphicsDevice _device;

    public GameScreen(MyGameMain game)
    {
        _game = game;
        _device = _game.GraphicsDevice;

        LoadLDtk();
        LoadTextures();

        _camera = new Camera();
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
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

    public void Update(float deltaSeconds)
    {
        var input = _game.InputHandler;
        if (input.IsAnyModifierKeyDown())
            return;

        if (input.IsKeyPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (_camera.Use3D)
        {
            if (input.IsKeyPressed(KeyCode.Home))
            {
                _cameraRotation = new Vector2(0, MathHelper.Pi);
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
                _camera.Position3D = new Vector3(0, 0, -1000);
            }

            if (input.IsMouseButtonHeld(MouseButtonCode.Right))
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(input.MouseDelta.X, -input.MouseDelta.Y) * rotationSpeed * deltaSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
            }

            var camera3DSpeed = 750f;
            var moveDelta = camera3DSpeed * deltaSeconds;
            if (input.IsKeyDown(KeyCode.W))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.S))
            {
                _camera.Position3D -= Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.A))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Left, _camera.Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.D))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Right, _camera.Rotation3D) * moveDelta;
            }
        }
        else
        {
            var cameraSpeed = 500f;
            var moveDelta = cameraSpeed * deltaSeconds;

            if (input.IsKeyDown(KeyCode.W))
            {
                _camera.Position.Y -= moveDelta;
            }

            if (input.IsKeyDown(KeyCode.S))
            {
                _camera.Position.Y += moveDelta;
            }

            if (input.IsKeyDown(KeyCode.A))
            {
                _camera.Position.X -= moveDelta;
            }

            if (input.IsKeyDown(KeyCode.D))
            {
                _camera.Position.X += moveDelta;
            }
        }
    }

    public void Draw(Renderer renderer)
    {
        if (_backgroundSprite != null)
            renderer.DrawSprite(_backgroundSprite.Value, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(-200, -100),
                Color.White,
                200f);

        renderer.DrawText(FontType.Roboto, "Hello!", Vector2.Zero, Color.White);
        renderer.DrawText("In default font", new Vector2(100, 100), 0, Color.White);
        renderer.DrawText(FontType.Roboto, "Hello again!", new Vector2(150, 150), Color.White);

        _camera.Size = _game.MainWindow.Size;
        renderer.BeginRenderPass(_camera.ViewProjectionMatrix);
        // stuff
        renderer.EndRenderPass();
    }
}
