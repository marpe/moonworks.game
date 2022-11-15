namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
public partial class Entity
{
    public bool IsInitialized;
    public bool IsDestroyed;
    
    public Point Cell;
    public Vector2 CellPos;
    public Vector2 InitialPosition;
    public Vector2 PreviousPosition;
    public Vector2 Origin => Pivot * Size;
    public Bounds Bounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);
    public Vector2 Center => new(Position.X + (0.5f - Pivot.X) * Size.X, Position.Y + (0.5f - Pivot.Y) * Size.Y);

    private World? _world;
    public World World => _world ?? throw new InvalidOperationException();

    public CoroutineManager CoroutineManager = new();
    public Collider Collider = new();

    public virtual void Initialize(World world)
    {
        _world = world;
        Collider.Initialize(this);
        (Cell, CellPos) = GetGridCoords(this);
        InitialPosition = PreviousPosition = Position;
        IsInitialized = true;
    }

    public virtual void Update(float deltaSeconds)
    {
        CoroutineManager.Update(deltaSeconds);
    }

    public void SetPositions(Vector2 position)
    {
        PreviousPosition = Position = position;
        (Cell, CellPos) = GetGridCoords(this);
    }

    public static (Point, Vector2) GetGridCoords(Entity entity, int gridSize = World.DefaultGridSize)
    {
        var (adjustX, adjustY) = (MathF.Approx(entity.Pivot.X, 1) ? -1 : 0, MathF.Approx(entity.Pivot.Y, 1) ? -1 : 0);
        var cell = new Point(
            (int)((entity.Position.X + adjustX) / gridSize),
            (int)((entity.Position.Y + adjustY) / gridSize)
        );
        var relativeCell = new Vector2(
            (entity.Position.X + adjustX) % gridSize / gridSize,
            (entity.Position.Y + adjustY) % gridSize / gridSize
        );
        return (cell, relativeCell);
    }
}

public partial class Gun_Pickup : Entity
{
}

public partial class RefTest : Entity
{
}
