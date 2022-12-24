namespace MyGame;

public partial class BlueBee : Enemy
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.bluebee_aseprite;
        base.Initialize(world);
    }
}
