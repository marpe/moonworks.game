namespace MyGame.Screens.Transitions;

public class CircleCropTransition : SceneTransition
{
    private readonly QuadRenderer quadRenderer;
    public readonly GraphicsPipeline Pipeline;

    [Range(0, 1.0f)] public float CenterX = 0.5f;

    [Range(0, 1.0f)] public float CenterY = 0.5f;

    public Color BackgroundColor = Color.Black;
    public Vector2 Scaling = new(1.778f, 1.0f);

    public Color ClearColor = Color.Black;

    private ColorAttachmentInfo _colorAttachmentInfo;

    public CircleCropTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 1);

        var blendState = new ColorAttachmentBlendState()
        {
            BlendEnable = true,
            AlphaBlendOp = BlendOp.Add,
            ColorBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RGBA,
            SourceColorBlendFactor = BlendFactor.SourceAlpha,
            SourceAlphaBlendFactor = BlendFactor.One,
            DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
            DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha,
        };

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = Renderer.GetVertexInputState(),
        };

        Pipeline = new GraphicsPipeline(device, myGraphicsPipelineCreateInfo);

        _colorAttachmentInfo = new ColorAttachmentInfo();


        quadRenderer = new QuadRenderer(device);
    }

    public override void Unload()
    {
        Pipeline.Dispose();
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        if (state == TransitionState.Hidden || compositeOldCopy == null)
            return;

        var uniforms = new Uniforms()
        {
            Progress = progress,
            Center = new Vector2(CenterX, CenterY),
            Scaling = Scaling,
            BackgroundColor = BackgroundColor.ToVector4()
        };

        var renderToDraw = state switch
        {
            TransitionState.Active or TransitionState.TransitionOn => compositeOldCopy,
            _ => compositeNewCopy
        };
        
        quadRenderer.Draw(new Sprite(renderToDraw), Color.White, 0, Matrix4x4.Identity, Renderer.PointClamp);
        // quadRenderer.DrawRect(RectangleExt.FromTexture(renderDestination), Color.Transparent);

        quadRenderer.UpdateBuffers(commandBuffer);
        _colorAttachmentInfo.Texture = renderDestination;
        _colorAttachmentInfo.LoadOp = LoadOp.Clear;
        _colorAttachmentInfo.ClearColor = ClearColor;
        commandBuffer.BeginRenderPass(_colorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(Pipeline);
        commandBuffer.PushFragmentShaderUniforms(uniforms);
        quadRenderer.Flush(commandBuffer, Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height), null);
        commandBuffer.EndRenderPass();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms
    {
        public float Progress;
        public float Padding0;
        public Vector2 Center;
        public Vector2 Scaling;
        public Vector4 BackgroundColor;
        public Vector4 Padding1;
        public Vector2 Padding2;
    }
}
