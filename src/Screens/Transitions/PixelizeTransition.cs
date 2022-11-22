namespace MyGame.Screens.Transitions;

public class PixelizeTransition : SceneTransition
{
    private readonly QuadRenderer quadRenderer;
    public readonly GraphicsPipeline Pipeline;

    public Uniforms Uniform = new()
    {
        Progress = 0,
        SquaresMin = new Point(20, 12),
        Steps = 20,
    };

    private TextureSamplerBinding _sourceBinding;
    private ColorAttachmentInfo _colorAttachmentInfo;

    public PixelizeTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 2);

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = Renderer.GetVertexInputState(),
        };

        Pipeline = new GraphicsPipeline(
            device,
            myGraphicsPipelineCreateInfo
        );

        _sourceBinding = new TextureSamplerBinding();
        _sourceBinding.Sampler = Renderer.PointClamp;

        _colorAttachmentInfo = new ColorAttachmentInfo();
        _colorAttachmentInfo.LoadOp = LoadOp.Clear;
        _colorAttachmentInfo.ClearColor = Color.Cyan;

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

        quadRenderer.Draw(new Sprite(compositeOldCopy), Color.White, 0, Matrix4x4.Identity, Renderer.PointClamp);
        quadRenderer.UpdateBuffers(commandBuffer);
        _colorAttachmentInfo.Texture = renderDestination;
        commandBuffer.BeginRenderPass(_colorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(Pipeline);
        Uniform.Progress = progress;
        Uniform.Steps = 30;
        commandBuffer.PushFragmentShaderUniforms(Uniform);
        _sourceBinding.Texture = compositeNewCopy;
        quadRenderer.Flush(commandBuffer, Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height), _sourceBinding);
        commandBuffer.EndRenderPass();
    }

    public struct Uniforms
    {
        public float Progress;
        public int Steps;
        public Point SquaresMin;
    }
}
