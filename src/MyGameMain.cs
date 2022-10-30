using System.Threading.Tasks;
using MyGame.Components;
using MyGame.Graphics;
using MyGame.TWConsole;
using MyGame.TWImGui;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    public ulong UpdateCount { get; private set; }
    public ulong DrawCount { get; private set; }
    public float TotalElapsedTime { get; private set; }

    public float ElapsedTime { get; private set; }

    private Sprite? _spriteRenderer;
    private Sprite? _backgroundSprite;
    private readonly Camera _camera;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    private ImGuiScreen? _imGuiScreen;
    private bool _drawImGui = true;
    private KeyCode[] _modifierKeys;

    private bool _saveTexture;

    public readonly Renderer Renderer;
    private readonly ConsoleScreen _consoleScreen;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();

        Shared.Game = this;
        Shared.MainWindow = MainWindow;
        Shared.Console = new TWConsole.TWConsole();

        Renderer = new Renderer(this);

        _consoleScreen = new ConsoleScreen(this);
        Task.Run(() =>
        {
            Shared.Console.Initialize();
        });

        LoadLDtk();

        LoadTextures();

        _camera = new Camera();
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);

        Task.Run(() => { _imGuiScreen = new ImGuiScreen(this); });

        _modifierKeys = new KeyCode[]
        {
            KeyCode.LeftControl,
            KeyCode.RightControl,
            KeyCode.LeftShift,
            KeyCode.RightShift,
            KeyCode.LeftAlt,
            KeyCode.RightAlt,
            KeyCode.LeftMeta,
            KeyCode.RightMeta,
        };

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
    }

    private void LoadTextures()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
            var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
            _spriteRenderer = new Sprite(asepriteTexture);

            var menu = LoadPngTexture(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
            _backgroundSprite = new Sprite(menu);
            Logger.LogInfo($"Loaded textures in {sw2.ElapsedMilliseconds} ms");
        });
    }

    private void LoadLDtk()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var ldtkPath = Path.Combine(ContentRoot, ContentPaths.Ldtk.MapLdtk);
            var jsonString = File.ReadAllText(ldtkPath);
            var ldtkJson = LdtkJson.FromJson(jsonString);
            Logger.LogInfo($"Loaded LDtk in {sw2.ElapsedMilliseconds} ms");
        });
    }

    private static Texture LoadPngTexture(GraphicsDevice device, string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"File not found: {path}");
        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.LoadPNG(device, commandBuffer, path);
        device.Submit(commandBuffer);
        return texture;
    }

    private bool IsAnyModifierKeyDown()
    {
        foreach (var key in _modifierKeys)
        {
            if (Inputs.Keyboard.IsDown(key))
                return true;
        }

        return false;
    }

    protected override void Update(TimeSpan dt)
    {
        UpdateCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        if (_imGuiScreen != null)
            _imGuiScreen.Update();

        _consoleScreen.Update(ElapsedTime);

        if (IsAnyModifierKeyDown())
            return;

        if (Inputs.Keyboard.IsPressed(KeyCode.P))
        {
            _saveTexture = true;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F2))
        {
            _drawImGui = !_drawImGui;
        }

        /*if (Inputs.Keyboard.IsPressed(KeyCode.F3))
        {
            var numBlendStates = Enum.GetValues<BlendState>().Length;
            var nextMode = (numBlendStates + (int)SpriteBatch.BlendState + 1) % numBlendStates;
            SpriteBatch.BlendState = (BlendState)nextMode;
            Logger.LogInfo($"BlendState: {SpriteBatch.BlendState}");
            // MainWindow.ChangeScreenMode((ScreenMode)nextMode);
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F4))
        {
            var numBlendStates = Enum.GetValues<BlendState>().Length;
            var nextMode = (numBlendStates + (int)SpriteBatch.BlendState - 1) % numBlendStates;
            SpriteBatch.BlendState = (BlendState)nextMode;
            Logger.LogInfo($"BlendState: {SpriteBatch.BlendState}");
            // MainWindow.ChangeScreenMode((ScreenMode)nextMode);
        }*/

        if (_camera.Use3D)
        {
            if (Inputs.Keyboard.IsPressed(KeyCode.Home))
            {
                _cameraRotation = new Vector2(0, MathHelper.Pi);
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
                _camera.Position3D = new Vector3(0, 0, -1000);
            }

            if (Inputs.Mouse.RightButton.IsHeld)
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(Inputs.Mouse.DeltaX, -Inputs.Mouse.DeltaY) * rotationSpeed * (float)dt.TotalSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
            }

            var cameraSpeed = 750f;
            if (Inputs.Keyboard.IsDown(KeyCode.W))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.S))
            {
                _camera.Position3D -= Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.A))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Left, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.D))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Right, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }
        }
        else
        {
            var cameraSpeed = 500f;
            if (Inputs.Keyboard.IsDown(KeyCode.W))
            {
                _camera.Position.Y -= cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.S))
            {
                _camera.Position.Y += cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.A))
            {
                _camera.Position.X -= cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.D))
            {
                _camera.Position.X += cameraSpeed * (float)dt.TotalSeconds;
            }
        }
    }


    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        DrawCount++;

        if (!Renderer.BeginFrame())
            return;

        if (_backgroundSprite != null)
            Renderer.DrawSprite(_backgroundSprite.Value, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(-200, -100), Color.White,
                200f);

        Renderer.DrawText(FontType.Roboto, "Hello!", Vector2.Zero, Color.White);
        Renderer.DrawText("In default font", new Vector2(100, 100), 0, Color.White);
        Renderer.DrawText(FontType.Roboto, "Hello again!", new Vector2(150, 150), Color.White);

        _camera.Size = MainWindow.Size;
        Renderer.BeginRenderPass(_camera.ViewProjectionMatrix);
        // stuff
        Renderer.EndRenderPass();

        if (_imGuiScreen != null && _drawImGui)
            _imGuiScreen.Draw(Renderer);

        _consoleScreen.Draw(Renderer);

        Renderer.EndFrame();
    }

    private static Texture LoadAseprite(GraphicsDevice device, string path)
    {
        var aseprite = AsepriteFile.LoadAsepriteFile(path);
        var (data, rects) = AsepriteToTextureAtlasConverter.GetTextureData(aseprite);
        var texture = Texture.CreateTexture2D(
            device,
            aseprite.Header.Width * (uint)aseprite.Frames.Count,
            aseprite.Header.Height,
            TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler
        );
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetTextureData(texture, data);
        device.Submit(commandBuffer);
        return texture;
    }

    protected override void Destroy()
    {
        _imGuiScreen?.Destroy();
        Shared.Console.SaveCVars();
    }
}
