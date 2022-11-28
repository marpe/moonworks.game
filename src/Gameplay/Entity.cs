namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
public partial class Entity
{
    public bool IsInitialized;
    public bool IsDestroyed;

    public Bounds Bounds => new(Position.Current.X, Position.Current.Y, Size.X, Size.Y);
    public Vector2 Center => new(Position.Current.X + 0.5f * Size.X, Position.Current.Y + 0.5f * Size.Y);

    private World? _world;

    [HideInInspector] public World World => _world ?? throw new InvalidOperationException();

    public CoroutineManager CoroutineManager = new();
    public Collider Collider = new();

    public Position Position = new();

    public GridCoords GridCoords => new(Position.Current);

    /*public Point Cell;
    /// Relative position in cell, ranges between 0 - 1; e.g 0, 0 = left, top, 1, 1 = right, bottom 
    public Vector2 CellPos;*/

    public virtual void Initialize(World world)
    {
        _world = world;
        Collider.Initialize(this);
        Position.Initialize();
        IsInitialized = true;
    }

    public virtual void Update(float deltaSeconds)
    {
        CoroutineManager.Update(deltaSeconds);
    }

    public override string ToString()
    {
        return $"Type: {EntityType}";
    }
}

public partial class Gun_Pickup : Entity
{
}

public partial class RefTest : Entity
{
}
