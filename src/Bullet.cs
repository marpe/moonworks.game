namespace MyGame;

public class Bullet : Entity
{
    public float Timer;
    public float Lifetime = 3f;

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(1f, 1f),
    };

    public SpriteFlip Flip = SpriteFlip.None;

    public override void Initialize(World world)
    {
        base.Initialize(world);
        if (Velocity.Delta.X < 0)
            Flip = SpriteFlip.FlipHorizontally;
    }

    public override void Update(float deltaSeconds)
    {
        Timer += deltaSeconds;
        if (Timer >= Lifetime)
            IsDestroyed = true;
        base.Update(deltaSeconds);
    }
}
