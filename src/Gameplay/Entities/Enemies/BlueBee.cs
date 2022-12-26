namespace MyGame.Entities;

public class BlueBee : Enemy
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.bluebee_aseprite;
        
        _behaviour = new BeeBehaviour();
        _behaviour.Initialize(this);
        
        base.Initialize(world);
    }
}
