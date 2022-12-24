namespace MyGame;

public enum FacingDirection
{
    Left,
    Right
}

public partial class Enemy : Entity
{
    public Velocity Velocity = new();

    [HideInInspector] public float TimeOffset;

    private EnemyBehaviour? _behaviour;
    public EnemyBehaviour Behaviour => _behaviour ?? throw new InvalidOperationException();

    [HideInInspector] public float FreezeMovementUntil;

    public Mover Mover = new();

    [HideInInspector] public FacingDirection FacingDirection = FacingDirection.Right;

    public bool IsDead;

    public override void Initialize(World world)
    {
        base.Initialize(world);

        Mover.Initialize(this);

        TimeOffset = Position.Current.X;

        if (EntityType == EntityType.Slug)
        {
            var randomDirection = Random.Shared.Next() % 2 == 0 ? -1 : 1;
            Velocity.Delta = new Vector2(randomDirection * 50f, 0);
            Velocity.Friction = new Vector2(0.99f, 0.99f);

            _behaviour = new SlugBehaviour();
            _behaviour.Initialize(this);

            if (HasCollision(Position, Size))
            {
                Logs.LogWarn("Colliding on spawn, destroying immediately");
                IsDestroyed = true;
            }
        }
        else if (EntityType == EntityType.BlueBee) // || EntityType == EntityType.YellowBee)
        {
            _behaviour = new BeeBehaviour();
            _behaviour.Initialize(this);
        }
    }

    public void FreezeMovement(float freezeTime)
    {
        FreezeMovementUntil = Math.Max(FreezeMovementUntil, TotalTimeActive + freezeTime);
    }

    public override void Update(float deltaSeconds)
    {
        base.Update(deltaSeconds);
        Behaviour.Update(deltaSeconds);
    }
}
