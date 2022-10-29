using MoonWorks.Graphics.Font;
using MyGame.TWConsole;

namespace MyGame.Graphics;

public class Renderer
{
    public static Sampler PointClamp = null!;

    private readonly SpriteBatch SpriteBatch;
    private readonly TextBatcher TextBatcher;

    public BlendState BlendState
    {
        get => SpriteBatch.BlendState;
        set => SpriteBatch.BlendState = value;
    }

    private Texture _depthTexture;
    private GraphicsPipeline _fontPipeline;

    private readonly MyGameMain _game;
    private readonly GraphicsDevice _device;
    private readonly Texture _blankTextures;
    private readonly GraphicsPipeline[] _pipelines;

    public CommandBuffer? CommandBuffer { get; private set; }
    public Texture? SwapTexture { get; private set; }
    public ColorAttachmentBlendState FontPipelineBlend = ColorAttachmentBlendState.NonPremultiplied;

    public ColorAttachmentBlendState CustomBlendState = new ColorAttachmentBlendState
    {
        BlendEnable = true,
        AlphaBlendOp = BlendOp.Add,
        ColorBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RGBA,
        SourceColorBlendFactor = BlendFactor.One,
        SourceAlphaBlendFactor = BlendFactor.SourceAlpha,
        DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
        DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha
    };

    public Renderer(MyGameMain game)
    {
        _game = game;
        _device = game.GraphicsDevice;
        PointClamp = new Sampler(_device, SamplerCreateInfo.PointClamp);
        SpriteBatch = new SpriteBatch(_device);
        TextBatcher = new TextBatcher(_device);
        _depthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);

        _blankTextures = Texture.CreateTexture2D(_device, 1, 1, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        var command = game.GraphicsDevice.AcquireCommandBuffer();
        command.SetTextureData(_blankTextures, new[] { Color.White });
        game.GraphicsDevice.Submit(command);

        _fontPipeline = CreateGraphicsPipeline(_device, FontPipelineBlend);

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
                _ => throw new ArgumentOutOfRangeException()
            };
            _pipelines[i] = CreateGraphicsPipeline(_device, blendState);
        }
    }

    public void UpdateCustomBlendPipeline()
    {
        _pipelines[(int)BlendState.Custom] = CreateGraphicsPipeline(_device, CustomBlendState);
    }

    public void RecreateFontPipeline()
    {
        _fontPipeline.Dispose();
        _fontPipeline = CreateGraphicsPipeline(_device, FontPipelineBlend);
    }

    public bool BeginFrame()
    {
        var command = _device.AcquireCommandBuffer();
        CommandBuffer = command;
        var swap = command.AcquireSwapchainTexture(_game.MainWindow);
        SwapTexture = swap;
        if (swap == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return false;
        }

        var windowSize = _game.MainWindow.Size;
        if (windowSize.X != _depthTexture.Width || windowSize.Y != _depthTexture.Height)
        {
            _depthTexture.Dispose();
            _depthTexture = Texture.CreateTexture2D(_device, (uint)windowSize.X, (uint)windowSize.Y,
                TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        }

        return true;
    }

    public void DrawRect(Rectangle rect, Color color, float depth = 0)
    {
        var scale = Matrix3x2.CreateScale(rect.Width, rect.Height) * Matrix3x2.CreateTranslation(rect.X, rect.Y);
        SpriteBatch.Add(new Sprite(_blankTextures), color, depth, scale, PointClamp);
    }

    public void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var offset = from - to;
        var length = offset.Length();
        var scale = Matrix3x2.CreateScale(length, 1f, new Vector2(0, 0.5f));
        var rotationRad = MathF.AngleBetweenVectors(from, to);
        var rotation = Matrix3x2.CreateRotation(rotationRad, new Vector2(0, 0.5f));
        var translation = Matrix3x2.CreateTranslation(from);
        SpriteBatch.Add(new Sprite(_blankTextures), color, 0, scale * rotation * translation, PointClamp);
    }

    public void DrawLine(Point from, Point to, Color color)
    {
        DrawLine(from.ToVec2(), to.ToVec2(), color);
    }

    public void DrawSprite(Sprite sprite, Matrix3x2 transform, Color color, float depth)
    {
        var commandBuffer = CommandBuffer ?? throw new InvalidOperationException();
        SpriteBatch.Add(sprite, color, depth, transform, PointClamp);
    }

    public void DrawText(TextFont font, ReadOnlySpan<char> text, float x, float y, float depth, Color color,
        HorizontalAlignment alignH = HorizontalAlignment.Left, VerticalAlignment alignV = VerticalAlignment.Top)
    {
        var commandBuffer = CommandBuffer ?? throw new InvalidOperationException();
        TextBatcher.Add(font, text, x, y, depth, color, alignH, alignV);
    }

    public void BeginRenderPass(Matrix4x4 viewProjection, bool clear = true)
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        var swap = SwapTexture ?? throw new InvalidOperationException();

        var colorAttachmentInfo = clear ? new ColorAttachmentInfo(swap, Color.CornflowerBlue) : new ColorAttachmentInfo(swap, LoadOp.Load);

        if (SpriteBatch.AddCountSinceDraw > 0)
            SpriteBatch.PushVertexData(command);
        if (TextBatcher.AddCountSinceDraw > 0)
            TextBatcher.PushVertexData(command);

        command.BeginRenderPass(
            new DepthStencilAttachmentInfo(_depthTexture, new DepthStencilValue(0, 0)),
            colorAttachmentInfo
        );

        // commandBuffer.SetViewport(new Viewport(0, 0, windowSize.X, windowSize.Y));
        // commandBuffer.SetScissor(new Rect(0, 0, windowSize.X, windowSize.Y));

        if (SpriteBatch.AddCountSinceDraw > 0)
        {
            command.BindGraphicsPipeline(_pipelines[(int)BlendState.AlphaBlend]);
            SpriteBatch.Draw(command, viewProjection);
        }

        if (TextBatcher.AddCountSinceDraw > 0)
        {
            command.BindGraphicsPipeline(_fontPipeline);
            TextBatcher.Draw(command, viewProjection);
        }
    }

    public void EndRenderPass()
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        var swap = SwapTexture ?? throw new InvalidOperationException();

        command.EndRenderPass();
    }

    public void EndFrame()
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        _device.Submit(command);
    }

    public static byte[] ConvertTextureFormat(GraphicsDevice device, Texture texture)
    {
        var pixelSize = texture.Format switch
        {
            TextureFormat.R8 => 8u,
            _ => 32u,
        };
        var buffer = MoonWorks.Graphics.Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * pixelSize);
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.CopyTextureToBuffer(texture, buffer);
        device.Submit(commandBuffer);
        device.Wait();
        var pixels = new byte[buffer.Size];
        buffer.GetData(pixels, (uint)pixels.Length);
        if (texture.Format == TextureFormat.R8)
        {
            var prevLength = pixels.Length;
            Array.Resize(ref pixels, pixels.Length * 4);
            for (var i = prevLength - 1; i >= 0; i--)
            {
                var p = pixels[i];
                pixels[i] = 0;
                pixels[i * 4] = 255;
                pixels[i * 4 + 1] = 255;
                pixels[i * 4 + 2] = 255;
                pixels[i * 4 + 3] = p;
            }
        }

        return pixels;
    }

    public static unsafe Texture CreateTexture(GraphicsDevice device, uint width, uint height, byte[] pixels)
    {
        var texture = Texture.CreateTexture2D(device, width, height,
            TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler
        );
        var cmdBuffer = device.AcquireCommandBuffer();
        fixed (byte* p = pixels)
        {
            cmdBuffer.SetTextureData(texture, (IntPtr)p, (uint)pixels.Length);
            device.Submit(cmdBuffer);
        }

        return texture;
    }

    public static GraphicsPipeline CreateGraphicsPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var spriteVertexShader = new ShaderModule(device, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.Sg2_spriteVertSpv));
        var spriteFragmentShader = new ShaderModule(device, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.SpriteFragSpv));

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
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
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
            device,
            myGraphicsPipelineCreateInfo
        );
    }

    public void DrawText(ReadOnlySpan<char> text, Vector2 pos, float depth, Color color)
    {
        DrawText(TextFont.ConsolasMono, text, pos.X, pos.Y, depth, color);
    }

    public void DrawText(ReadOnlySpan<char> text, Vector2 pos, Color color)
    {
        DrawText(text, pos, 0, color);
    }

    public void DrawText(TextFont font, ReadOnlySpan<char> text, Vector2 pos, Color color)
    {
        DrawText(font, text, pos.X, pos.Y, 0, color);
    }
}
