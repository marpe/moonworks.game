namespace MyGame.Screens.Transitions;

public class FadeToBlack : SceneTransition
{
    public override void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state, Texture? compositeOldCopy, Texture compositeNewCopy)
    {
        var isLoading = state is TransitionState.TransitionOn or TransitionState.Active;
        if (compositeOldCopy != null && isLoading)
        {
            renderer.DrawSprite(compositeOldCopy, Matrix4x4.Identity, Color.White);
        }
        renderer.DrawRect(new Rectangle(0, 0, (int)renderDestination.Width, (int)renderDestination.Height), Color.Black * progress);
    }
}
