namespace MyGame.Screens.Transitions;

public abstract class SceneTransition
{
    public virtual void Unload()
    {
    }

    
    /// <summary>
    /// Progress ranges from 0 at the start of loading, 1 when the loading screen has faded in then goes back to 0 when
    /// loading has finished 
    /// </summary>
    public abstract void Draw(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy);
}
