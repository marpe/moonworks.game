namespace MyGame.Screens;

public abstract class SceneTransition
{
    public virtual void Unload()
    {
    }

    
    /// <summary>
    /// Progress ranges from 0 at the start of loading, 1 when the loading screen has faded in then goes back to 0 when
    /// loading has finished 
    /// </summary>
    public abstract void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy);
}

public class FadeToBlack : SceneTransition
{
    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        var isLoading = state is TransitionState.TransitionOn or TransitionState.Active;
        if (compositeOldCopy != null && isLoading)
        {
            renderer.DrawSprite(compositeOldCopy, Matrix4x4.Identity, Color.White);
        }
        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black * progress);
    }
}

public class DiamondTransition : SceneTransition
{
    public readonly GraphicsPipeline Pipeline;
    public Uniforms Uniform = new() { Progress = 0, DiamondPixelSize = 36 };

    public DiamondTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 1);

        var myDepthStencilState = new DepthStencilState
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            CompareOp = CompareOp.GreaterOrEqual,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false,
        };

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                TextureFormat.D16,
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = myDepthStencilState,
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
    }

    public override void Unload()
    {
        Pipeline.Dispose();
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        var isLoading = state is TransitionState.TransitionOn or TransitionState.Active;
        if (compositeOldCopy != null && isLoading)
        {
            renderer.DrawSprite(new Sprite(compositeOldCopy), Matrix4x4.Identity, Color.White);
            renderer.Flush(commandBuffer, renderDestination, null, null);
        }

        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black, 1f);
        renderer.SpriteBatch.UpdateBuffers(commandBuffer);
        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(renderer.DepthStencilAttachmentInfo.Texture, LoadOp.Load),
            new ColorAttachmentInfo(renderDestination, LoadOp.Load));
        commandBuffer.BindGraphicsPipeline(Pipeline);
        Uniform.Progress = progress;
        var fragmentParamOffset = commandBuffer.PushFragmentShaderUniforms(Uniform);
        var viewProjection = Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height);
        renderer.SpriteBatch.Flush(commandBuffer, viewProjection);
        commandBuffer.EndRenderPass();
    }

    public struct Uniforms
    {
        public float Progress;
        public float DiamondPixelSize;
    }
}
