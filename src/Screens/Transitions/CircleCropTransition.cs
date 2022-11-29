namespace MyGame.Screens.Transitions;

public class CircleCropTransition : SceneTransition
{
    [Range(0, 1.0f)] public float CenterX = 0.5f;

    [Range(0, 1.0f)] public float CenterY = 0.5f;

    public Color BackgroundColor = Color.Black;
    public Vector2 Scaling = new(1.778f, 1.0f);

    public Color ClearColor = Color.Black;

    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        if (state == TransitionState.Hidden || compositeOldCopy == null)
            return;

        var uniforms = new Pipelines.CircleCropUniforms()
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

        renderer.DrawSprite(new Sprite(renderToDraw), Matrix4x4.Identity, Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.BeginRenderPass(ref commandBuffer, renderDestination, ClearColor, PipelineType.CircleCropTransition);
        commandBuffer.PushFragmentShaderUniforms(uniforms);
        renderer.DrawIndexedSprites(ref commandBuffer, null);
        commandBuffer.EndRenderPass();
    }
}
