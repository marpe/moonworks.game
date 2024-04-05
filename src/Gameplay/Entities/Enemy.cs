namespace MyGame.Entities;

public enum FacingDirection
{
    Left,
    Right
}

public abstract class Enemy : Entity
{
    public Velocity Velocity = new();

    [HideInInspector]
    public float TimeOffset;

    protected EnemyBehaviour? _behaviour;
    
    [HideInInspector]
    public EnemyBehaviour Behaviour => _behaviour ?? throw new InvalidOperationException();

    [HideInInspector]
    public float FreezeMovementUntil;
    
    [HideInInspector]
    public Mover Mover = new();

    [HideInInspector]
    public FacingDirection FacingDirection = FacingDirection.Right;

    public bool IsDead;

    private Light? _light;

    public override void Initialize(World world)
    {
        base.Initialize(world);

        Mover.Initialize(this);
        TimeOffset = Position.Current.X;

        _light = World.CreateEntity<Light>();
        _light.Size = new Point(_light.Size.X * 4, _light.Size.Y * 4);
        _light.Color = new Color(Random.Shared.Next(255), Random.Shared.Next(255), Random.Shared.Next(255));
        world.Entities.Add(_light);
    }

    public override void Destroy()
    {
        base.Destroy();
        _light!.Destroy();
    }

    public void FreezeMovement(float freezeTime)
    {
        FreezeMovementUntil = Math.Max(FreezeMovementUntil, TotalTimeActive + freezeTime);
    }

    public override void Update(float deltaSeconds)
    {
        Behaviour.Update(deltaSeconds);
        _light!.Center = Center;
        base.Update(deltaSeconds);
    }

    public void Kill()
    {
        IsDead = true;
    }
}
