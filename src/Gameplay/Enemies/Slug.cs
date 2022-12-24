namespace MyGame;

public partial class Slug : Enemy
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.slug_aseprite;
        base.Initialize(world);
    }
}
