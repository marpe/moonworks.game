namespace MyGame;

public partial class Enemy : Entity
{
    public SpriteFlip Flip;
    public float TotalTime;
    public Velocity Velocity = new();
    public float TimeOffset;
    
    private EnemyBehaviour? _behaviour;
    public EnemyBehaviour Behaviour => _behaviour ?? throw new InvalidOperationException();
    public bool CanMove => TotalTime >= FreezeMovementUntil;
    public float FreezeMovementUntil;

    public override void Initialize(World world)
    {
        base.Initialize(world);

        TimeOffset = Position.X;
        
        if (Type == EnemyType.Slug)
        {
            var randomDirection = Random.Shared.Next() % 2 == 0 ? -1 : 1;
            Velocity.Delta = new Vector2(randomDirection * 50f, 0);
            Velocity.Friction = new Vector2(0.99f, 0.99f);
            
            _behaviour = new SlugBehaviour();
            _behaviour.Initialize(this);
        }
        else if (Type == EnemyType.BlueBee || Type == EnemyType.YellowBee)
        {
            _behaviour = new BeeBehaviour();
            _behaviour.Initialize(this);
        }
    }

    public void FreezeMovement(float freezeTime)
    {
        FreezeMovementUntil = Math.Max(FreezeMovementUntil, TotalTime + freezeTime);
    }
    
    public override void Update(float deltaSeconds)
    {
        TotalTime += deltaSeconds;
        Behaviour.Update(deltaSeconds);
        base.Update(deltaSeconds);
    }
}
