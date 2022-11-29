namespace MyGame.Screens.Transitions;

public class PixelizeTransition : SceneTransition
{
    public Pipelines.PixelizeUniforms FragUniform = new()
    {
        Progress = 0,
        SquaresMin = new Point(20, 12),
        Steps = 20,
    };

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        if (state == TransitionState.Hidden || compositeOldCopy == null)
            return;

        renderer.DrawSprite(new Sprite(compositeOldCopy), Matrix4x4.Identity, Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, Color.Cyan, PipelineType.PixelizeTransition);
        FragUniform.Progress = progress;
        FragUniform.Steps = 30;

        var vertUniform = Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height);
        var fragmentBindings = new[]
            { new TextureSamplerBinding(compositeNewCopy, Renderer.PointClamp), new TextureSamplerBinding(compositeNewCopy, Renderer.PointClamp) };
        renderer.DrawIndexedSprites(ref commandBuffer, vertUniform, FragUniform, fragmentBindings);

        renderer.EndRenderPass(ref commandBuffer);
    }
}
