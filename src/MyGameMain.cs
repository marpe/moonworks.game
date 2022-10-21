namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteRenderer _spriteRenderer;
    private GraphicsPipeline _spritePipeline;
    private readonly Sampler _sampler;
    private readonly SpriteRenderer _menuRenderer;
    private int _numberOfTimesPressed;
    private readonly Camera _camera;
    private readonly Texture _depthTexture;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();
        _sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _spritePipeline = CreateGraphicsPipeline();

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

    private GraphicsPipeline CreateGraphicsPipeline()
    {
        var spriteVertexShader = new ShaderModule(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Shaders.Sg2_spriteVertSpv));
        var spriteFragmentShader = new ShaderModule(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Shaders.SpriteFragSpv));

        var myVertexBindings = new VertexBinding[]
        {
            VertexBinding.Create<Position3DTextureColorVertex>()
        };

        var myVertexAttributes = new VertexAttribute[]
        {
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Position), 0),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Color), 2),
        };

        var myVertexInputState = new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes
        };

        var myDepthStencilState = new DepthStencilState
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            CompareOp = CompareOp.GreaterOrEqual,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false
        };

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                TextureFormat.D16,
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = myDepthStencilState,
            VertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0),
            FragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1),
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = myVertexInputState,
        };

        return new GraphicsPipeline(
            GraphicsDevice,
            myGraphicsPipelineCreateInfo
        );
    }

    protected override void Update(TimeSpan dt)
    {
        if (Inputs.Keyboard.IsPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (_camera.Use3D)
        {
            if (Inputs.Mouse.LeftButton.IsHeld)
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(Inputs.Mouse.DeltaX, -Inputs.Mouse.DeltaY) * rotationSpeed * (float)dt.TotalSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
            }

            var cameraSpeed = 500f;
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
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

        if (swapchainTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        _menuRenderer.Draw(commandBuffer, _spriteBatch, Matrix3x2.Identity, Color.White, 5f, _sampler);
        _spriteRenderer.Draw(commandBuffer, _spriteBatch, Matrix3x2.Identity, Color.White, 0, _sampler);

        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(_depthTexture, new DepthStencilValue(0, 0)),
            new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
        );

        commandBuffer.BindGraphicsPipeline(_spritePipeline);

        _camera.Width = (int)MainWindow.Width;
        _camera.Height = (int)MainWindow.Height;
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(_camera.ViewProjectionMatrix);

        _spriteBatch.Draw(commandBuffer, vertexParamOffset);

        commandBuffer.EndRenderPass();

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
