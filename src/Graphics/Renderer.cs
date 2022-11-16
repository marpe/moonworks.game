using System.Diagnostics.CodeAnalysis;
using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public enum BlendState
{
    Additive,
    AlphaBlend,
    NonPremultiplied,
    Opaque,
    None,
    Disable,
    Custom,
}

public class Renderer
{
    public CommandBuffer CommandBuffer =>
        _commandBuffer ?? throw new InvalidOperationException("CommandBuffer is null, did you forget to call BeginFrame?");

    public BMFont[] BMFonts { get; }
    
    public static Sampler PointClamp = null!;
    private readonly Sprite _blankSprite;
    private readonly Texture _blankTexture;

    private readonly GraphicsDevice _device;

    private readonly MyGameMain _game;
    private readonly GraphicsPipeline[] _pipelines;

    public readonly SpriteBatch SpriteBatch;
    public readonly TextBatcher TextBatcher;

    private CommandBuffer? _commandBuffer;

    private Texture? _swapTexture;

    public BlendState BlendState = BlendState.AlphaBlend;
    public ColorAttachmentInfo ColorAttachmentInfo;

    public ColorAttachmentBlendState CustomBlendState = new()
    {
        BlendEnable = true,
        AlphaBlendOp = BlendOp.Add,
        ColorBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RGBA,
        SourceColorBlendFactor = BlendFactor.One,
        SourceAlphaBlendFactor = BlendFactor.SourceAlpha,
        DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
        DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha,
    };

    public Color DefaultClearColor = Color.CornflowerBlue;

    public DepthStencilAttachmentInfo DepthStencilAttachmentInfo;
    public Texture DepthTexture;

    public Renderer(MyGameMain game)
    {
        _game = game;
        _device = game.GraphicsDevice;
        PointClamp = new Sampler(_device, SamplerCreateInfo.PointClamp);
        SpriteBatch = new SpriteBatch(_device);
        TextBatcher = new TextBatcher(_device);

        _blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _blankSprite = new Sprite(_blankTexture);

        var blendStates = Enum.GetValues<BlendState>();
        _pipelines = new GraphicsPipeline[blendStates.Length];
        for (var i = 0; i < blendStates.Length; i++)
        {
            var blendState = blendStates[i] switch
            {
                BlendState.Additive => ColorAttachmentBlendState.Additive,
                BlendState.AlphaBlend => ColorAttachmentBlendState.AlphaBlend,
                BlendState.NonPremultiplied => ColorAttachmentBlendState.NonPremultiplied,
                BlendState.Opaque => ColorAttachmentBlendState.Opaque,
                BlendState.None => ColorAttachmentBlendState.None,
                BlendState.Disable => ColorAttachmentBlendState.Disable,
                BlendState.Custom => CustomBlendState,
                _ => throw new ArgumentOutOfRangeException(),
            };
            _pipelines[i] = CreateGraphicsPipeline(_device, blendState);
        }

        var bmFontTypes = new[]
        {
            (BMFontType.ConsolasMonoSmall, ContentPaths.bmfonts.consolas_fnt),
            (BMFontType.ConsolasMonoMedium, ContentPaths.bmfonts.consolas48_fnt),
            (BMFontType.ConsolasMonoLarge, ContentPaths.bmfonts.consolas60_fnt),
            (BMFontType.ConsolasMonoHuge, ContentPaths.bmfonts.consolas72_fnt),
        };

        BMFonts = new BMFont[bmFontTypes.Length];
        for (var i = 0; i < bmFontTypes.Length; i++)
        {
            var (type, path) = bmFontTypes[i];
            BMFonts[i] = new BMFont(game.GraphicsDevice, path);
        }

        DepthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        DepthStencilAttachmentInfo = new DepthStencilAttachmentInfo()
        {
            DepthStencilClearValue = new DepthStencilValue(0, 0),
            Texture = DepthTexture,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store,
        };
        ColorAttachmentInfo = new ColorAttachmentInfo()
        {
            ClearColor = Color.CornflowerBlue,
            LoadOp = LoadOp.Clear,
        };
    }

    public BMFont GetFont(BMFontType fontType)
    {
        return BMFonts[(int)fontType];
    }
    
    public void DrawPoint(Vector2 position, Color color, float size = 1.0f, float depth = 0)
    {
        var scale = Matrix3x2.CreateTranslation(-size * 0.5f, -size * 0.5f) *
                    Matrix3x2.CreateScale(size, size) * 
                    Matrix3x2.CreateTranslation(position.X, position.Y);
        SpriteBatch.Draw(_blankSprite, color, depth, scale, PointClamp);
    }

    public void DrawRect(Rectangle rect, Color color, float depth = 0)
    {
        var scale = Matrix3x2.CreateScale(rect.Width, rect.Height) * Matrix3x2.CreateTranslation(rect.X, rect.Y);
        SpriteBatch.Draw(_blankSprite, color, depth, scale, PointClamp);
    }

    public void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        var length = (from - to).Length();
        var origin = Matrix3x2.CreateTranslation(0, -0.5f);
        var scale = Matrix3x2.CreateScale(length, thickness);
        var rotation = Matrix3x2.CreateRotation(MathF.AngleBetweenVectors(from, to));
        var translation = Matrix3x2.CreateTranslation(from);
        var tAll = origin * scale * rotation * translation;
        SpriteBatch.Draw(_blankSprite, color, 0, tAll, PointClamp);
    }

    public void DrawRect(Vector2 min, Vector2 max, Color color, float thickness)
    {
        ReadOnlySpan<Vector2> points = stackalloc Vector2[]
        {
            min,
            new(max.X, min.Y),
            max,
            new(min.X, max.Y),
        };
        for (var i = 0; i < 4; i++)
        {
            DrawLine(points[i], points[(i + 1) % 4], color, thickness);
        }
    }

    public void DrawSprite(Sprite sprite, Matrix3x2 transform, Color color, float depth, SpriteFlip flip = SpriteFlip.None)
    {
        SpriteBatch.Draw(sprite, color, depth, transform, PointClamp, flip);
    }

    public void DrawText(FontType fontType, ReadOnlySpan<char> text, Vector2 position, float depth, Color color,
        HorizontalAlignment alignH = HorizontalAlignment.Left, VerticalAlignment alignV = VerticalAlignment.Top)
    {
        TextBatcher.Add(fontType, text, position.X, position.Y, depth, color, alignH, alignV);
    }

    public void DrawBMText(BMFontType fontType, ReadOnlySpan<char> text, Vector2 position, Vector2 origin, Vector2 scale, float rotation, float depth,
        Color color)
    {
        BMFont.DrawInto(this, BMFonts[(int)fontType], text, position, origin, rotation, scale, color, depth);
    }
    
    public bool BeginFrame([NotNullWhen(true)] out Texture? swapTexture)
    {
        _commandBuffer = _device.AcquireCommandBuffer();
        _swapTexture = _commandBuffer?.AcquireSwapchainTexture(_game.MainWindow);

        if (_swapTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            swapTexture = null;
            return false;
        }

        swapTexture = _swapTexture;
        var windowSize = _game.MainWindow.Size;
        TextureUtils.EnsureTextureSize(ref DepthTexture, _device, (uint)windowSize.X, (uint)windowSize.Y);
        DepthStencilAttachmentInfo.Texture = DepthTexture;

        return true;
    }

    public void FlushBatches(Texture renderTarget, Matrix4x4? viewProjection = null, Color? clearColor = null)
    {
        var commandBuffer = CommandBuffer;
        
        ColorAttachmentInfo.Texture = renderTarget;
        ColorAttachmentInfo.ClearColor = clearColor ?? DefaultClearColor;
        ColorAttachmentInfo.LoadOp = clearColor != null ? LoadOp.Clear : LoadOp.Load;

        TextBatcher.FlushToSpriteBatch(SpriteBatch);
        SpriteBatch.UpdateBuffers(commandBuffer);

        commandBuffer.BeginRenderPass(DepthStencilAttachmentInfo, ColorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(_pipelines[(int)BlendState]);
        SpriteBatch.Flush(commandBuffer, viewProjection ?? GetViewProjection(renderTarget.Width, renderTarget.Height));
        commandBuffer.EndRenderPass();
    }

    public void EndFrame()
    {
        _device.Submit(CommandBuffer);
        _commandBuffer = null;
        _swapTexture = null;
    }

    public void Unload()
    {
        for (var i = 0; i < BMFonts.Length; i++)
        {
            BMFonts[i].Dispose();
        }

        for (var i = 0; i < _pipelines.Length; i++)
        {
            _pipelines[i].Dispose();
        }

        _blankTexture.Dispose();
        TextBatcher.Unload();
        SpriteBatch.Unload();
        PointClamp.Dispose();

        DepthTexture.Dispose();
    }

    public static VertexInputState GetVertexInputState()
    {
        var myVertexBindings = new[]
        {
            VertexBinding.Create<Position3DTextureColorVertex>(),
        };

        var myVertexAttributes = new[]
        {
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Position), 0),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Color), 2),
        };

        return new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes,
        };
    }

    public static GraphicsPipeline CreateGraphicsPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var spriteVertexShader = new ShaderModule(device, ContentPaths.Shaders.sg2_sprite_vert_spv);
        var spriteFragmentShader = new ShaderModule(device, ContentPaths.Shaders.sprite_frag_spv);

        var myDepthStencilState = new DepthStencilState
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            CompareOp = CompareOp.GreaterOrEqual,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false,
        };

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1);

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                TextureFormat.D16,
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
            ),
            DepthStencilState = myDepthStencilState,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GraphicsPipeline(
            device,
            myGraphicsPipelineCreateInfo
        );
    }
    
    public static (Matrix4x4, Rectangle) GetViewportTransform(Point screenResolution, Point designResolution)
    {
        var scaleUniform = Math.Min(
            screenResolution.X / (float)designResolution.X,
            screenResolution.Y / (float)designResolution.Y
        );

        var renderSize = new Point(
            (int)(scaleUniform * designResolution.X),
            (int)(scaleUniform * designResolution.Y)
        );

        var offset = new Point(
            (int)((screenResolution.X - renderSize.X) * 0.5f),
            (int)((screenResolution.Y - renderSize.Y) * 0.5f)
        );

        var transform = Matrix3x2.CreateScale(scaleUniform, scaleUniform) *
                        Matrix3x2.CreateTranslation(offset.X, offset.Y);

        return (transform.ToMatrix4x4(), new Rect(offset.X, offset.Y, renderSize.X, renderSize.Y));
    }
    
    public static Matrix4x4 GetViewProjection(uint width, uint height)
    {
        var view = Matrix4x4.CreateTranslation(0, 0, -1000);
        var projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0.0001f, 10000f);
        return view * projection;
    }
}
