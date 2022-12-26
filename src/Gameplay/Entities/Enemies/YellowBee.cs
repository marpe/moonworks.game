namespace MyGame.Entities;

public class YellowBee : Enemy
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.bee_aseprite;
        
        _behaviour = new BeeBehaviour();
        _behaviour.Initialize(this);
        
        base.Initialize(world);
    }
}
