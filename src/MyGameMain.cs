using MyGame.TWImGui;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    public ulong FrameCount { get; private set; }
    public ulong RenderCount { get; private set; }
    public float TotalElapsedTime { get; private set; }

    public float ElapsedTime { get; private set; }

    public readonly SpriteBatch SpriteBatch;
    private readonly SpriteRenderer _spriteRenderer;
    private readonly SpriteRenderer _menuRenderer;
    private readonly Camera _camera;
    private Texture _depthTexture;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    private ImGuiScreen _imGuiScreen;
    private bool _drawImGui;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        var ldtkPath = Path.Combine(ContentRoot, ContentPaths.Ldtk.MapLdtk);
        var jsonString = File.ReadAllText(ldtkPath);
        var ldtkJson = LdtkJson.FromJson(jsonString);

        var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
        _spriteRenderer = new SpriteRenderer(asepriteTexture);

        var menu = LoadPngTexture(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
        _menuRenderer = new SpriteRenderer(menu);

        _camera = new Camera();
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);

        _depthTexture = Texture.CreateTexture2D(GraphicsDevice, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);

        _imGuiScreen = new ImGuiScreen(this);

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
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

    protected override void Update(TimeSpan dt)
    {
        FrameCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        _imGuiScreen.Update();

        if (Inputs.Keyboard.IsPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F2))
        {
            _drawImGui = !_drawImGui;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F3))
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
        }

        if (_camera.Use3D)
        {
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

        RenderCount++;
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

        if (swapchainTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        
        var windowSize = MainWindow.Size;
        if (windowSize.X != _depthTexture.Width || windowSize.Y != _depthTexture.Height)
        {
            _depthTexture.Dispose();
            _depthTexture = Texture.CreateTexture2D(GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y,
                TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        }

        _menuRenderer.Draw(commandBuffer, SpriteBatch, Matrix3x2.Identity, Color.White, 5f);
        _spriteRenderer.Draw(commandBuffer, SpriteBatch, Matrix3x2.Identity, Color.White, 0);

        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(_depthTexture, new DepthStencilValue(0, 0)),
            new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
        );

        _camera.Size = windowSize;
        SpriteBatch.Draw(commandBuffer, _camera.ViewProjectionMatrix);

        commandBuffer.EndRenderPass();

        if (_drawImGui)
        {
            _imGuiScreen.Draw(_depthTexture, commandBuffer, swapchainTexture);
        }

        GraphicsDevice.Submit(commandBuffer);
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
    }
}
