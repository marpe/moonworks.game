namespace MyGame.Entities;

public class MuzzleFlash : Entity
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.muzzle_flash_aseprite;
        base.Initialize(world);
        CoroutineManager.StartCoroutine(DelayedDestroy());
    }

    private IEnumerator DelayedDestroy()
    {
        yield return Coroutine.WaitForSeconds(0.05f);
        Destroy();
    }
}
