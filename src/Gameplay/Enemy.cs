namespace MyGame;

public enum FacingDirection
{
    Left,
    Right
}

public partial class Enemy : Entity
{
    public Velocity Velocity = new();
    
    [HideInInspector]
    public SpriteFlip Flip;
    
    [HideInInspector]
    public float TimeOffset;

    private EnemyBehaviour? _behaviour;
    public EnemyBehaviour Behaviour => _behaviour ?? throw new InvalidOperationException();
    
    [HideInInspector]
    public float FreezeMovementUntil;
    
    public Mover Mover = new();

    [HideInInspector]
    public FacingDirection FacingDirection = FacingDirection.Right;

    public bool IsDead;

    public override void Initialize(World world)
    {
        base.Initialize(world);

        Mover.Initialize(this);

        TimeOffset = Position.Current.X;

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

    public Matrix4x4 GetTransform(double alpha)
    {
        var xform = Matrix3x2.CreateTranslation(Size * Pivot - Pivot * new Vector2(World.DefaultGridSize, World.DefaultGridSize)) *
                    Matrix3x2.CreateTranslation(Position.Lerp(alpha));
        return xform.ToMatrix4x4();
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
