using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public class Renderer
{
    public static Sampler PointClamp = null!;

    public readonly SpriteBatch SpriteBatch;
    public readonly TextBatcher TextBatcher;

    private Texture _depthTexture;
    private GraphicsPipeline _fontPipeline;

    private readonly MyGameMain _game;
    private readonly GraphicsDevice _device;
    private readonly Texture _dummyTexture;
    private readonly GraphicsPipeline[] _pipelines;

    public CommandBuffer? CommandBuffer { get; private set; }
    public Texture? Swap { get; private set; }
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
        SpriteBatch = new SpriteBatch(_device);
        TextBatcher = new TextBatcher(_device);
        PointClamp = new Sampler(_device, SamplerCreateInfo.PointClamp);
        _depthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        _dummyTexture = Texture.CreateTexture2D(_device, 2, 2, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);

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
        Swap = swap;
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

        TextBatcher.Start();

        return true;
    }

    public void DrawSprite(Sprite sprite, Matrix3x2 transform, Color color, float depth)
    {
        var commandBuffer = CommandBuffer ?? throw new InvalidOperationException();
        SpriteBatch.AddSingle(sprite, color, depth, transform, PointClamp);
    }

    public void DrawText(ReadOnlySpan<char> text, float x, float y, float depth, Color color,
        HorizontalAlignment alignH = HorizontalAlignment.Left, VerticalAlignment alignV = VerticalAlignment.Baseline)
    {
        var commandBuffer = CommandBuffer ?? throw new InvalidOperationException();
        TextBatcher.Add(text.ToString(), x, y, depth, color, alignH, alignV);
    }

    public void BeginRenderPass(Matrix4x4 viewProjection, bool clear = true)
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        var swap = Swap ?? throw new InvalidOperationException();

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
        var swap = Swap ?? throw new InvalidOperationException();

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
}
