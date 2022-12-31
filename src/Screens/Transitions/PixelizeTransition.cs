namespace MyGame.Screens.Transitions;

public class PixelizeTransition : SceneTransition
{
    [CustomDrawInspector(nameof(DrawUniform))]
    public Pipelines.PixelizeUniforms FragUniform = new()
    {
        Progress = 0,
        SquaresMin = new Point(20, 12),
        Steps = 20,
    };

    public void DrawUniform()
    {
        SimpleTypeInspector.InspectPoint("SquaresMin", ref FragUniform.SquaresMin);
        SimpleTypeInspector.InspectInt("Steps", ref FragUniform.Steps, new RangeSettings(0, 100, 1, false));
    }

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state, Texture? compositeOldCopy, Texture compositeNewCopy)
    {
        if (state == TransitionState.Hidden || compositeOldCopy == null)
            return;

        var (fromTexture, toTexture) = state switch
        {
            TransitionState.TransitionOn or TransitionState.Active => (compositeOldCopy, compositeOldCopy),
            TransitionState.TransitionOff => (compositeOldCopy, compositeNewCopy),
            _ => throw new Exception(),
        };
        
        renderer.DrawSprite(fromTexture, Matrix4x4.Identity, Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, Color.Cyan, PipelineType.PixelizeTransition);

        FragUniform.Progress = progress;

        var vertUniform = Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height);
        var fragmentBindings = new[]
        {
            new TextureSamplerBinding(), // dummy entry, will be replaced with whatever texture was supplied to DrawSprite, which is horribly ugly I know...
            new TextureSamplerBinding(toTexture, SpriteBatch.PointClamp)
        };
        renderer.DrawIndexedSprites(ref commandBuffer, vertUniform, FragUniform, fragmentBindings, true);

        renderer.EndRenderPass(ref commandBuffer);
    }
}
