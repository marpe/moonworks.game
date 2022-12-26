namespace MyGame.Entities;

public class Slug : Enemy
{
    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.slug_aseprite;
        
        var randomDirection = Random.Shared.Next() % 2 == 0 ? -1 : 1;
        Velocity.Delta = new Vector2(randomDirection * 50f, 0);
        Velocity.Friction = new Vector2(0.99f, 0.99f);

        _behaviour = new SlugBehaviour();
        _behaviour.Initialize(this);

        base.Initialize(world);
        
        
        if (HasCollision(Position, Size))
        {
            Logs.LogWarn("Colliding on spawn, destroying immediately");
            IsDestroyed = true;
        }
    }
}
