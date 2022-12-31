namespace MyGame.Screens.Transitions;

public class DiamondTransition : SceneTransition
{
    public Pipelines.DiamondUniforms Uniform = new() { Progress = 0, DiamondPixelSize = 36 };

    public DiamondTransition()
    {
        
    }

    public override void Unload()
    {
    }

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state, Texture? compositeOldCopy, Texture compositeNewCopy)
    {
        var isLoading = state is TransitionState.TransitionOn or TransitionState.Active;
        if (compositeOldCopy != null && isLoading)
        {
            renderer.DrawSprite(new Sprite(compositeOldCopy), Matrix4x4.Identity, Color.White);
            renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true);
        }

        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black, 1f);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, null, PipelineType.DiamondTransition);
        Uniform.Progress = progress;
        var fragmentParamOffset = commandBuffer.PushFragmentShaderUniforms(Uniform);
        renderer.DrawIndexedSprites(ref commandBuffer, null, true);
        renderer.EndRenderPass(ref commandBuffer);
    }


}
