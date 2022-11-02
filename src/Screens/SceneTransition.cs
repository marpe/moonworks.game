using MyGame.Graphics;

namespace MyGame.Screens;

public abstract class SceneTransition
{
    public abstract void Draw(Renderer renderer, float progress);
}

public class FadeToBlack : SceneTransition
{
    public override void Draw(Renderer renderer, float progress)
    {
        var swap = renderer.SwapTexture;
        renderer.DrawRect(new Rectangle(0, 0, (int)swap.Width, (int)swap.Height), Color.Black * progress);
    }
}

public class DiamondTransition : SceneTransition
{
    public struct Uniforms
    {
        public float Progress;
        public float DiamondPixelSize;
    }

    public readonly GraphicsPipeline Pipeline;
    public Uniforms Uniform = new() { Progress = 0, DiamondPixelSize = 36 };

    public DiamondTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.DiamondTransition.Diamond_transitionVertSpv));
        var fragmentShader = new ShaderModule(device,
            Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.DiamondTransition.Diamond_transitionFragSpv));

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 1);

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

    public override void Draw(Renderer renderer, float progress)
    {
        var commandBuffer = renderer.CommandBuffer;
        var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.DrawRect(new Rectangle(0, 0, (int)swap.Width, (int)swap.Height), Color.Black, 1f);
        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(renderer.DepthStencilAttachmentInfo.Texture, LoadOp.Load),
            new ColorAttachmentInfo(swap, LoadOp.Load));
        commandBuffer.BindGraphicsPipeline(Pipeline);
        Uniform.Progress = progress;
        commandBuffer.PushFragmentShaderUniforms(Uniform);
        renderer.SpriteBatch.Flush(commandBuffer, viewProjection);
        commandBuffer.EndRenderPass();
    }
}
