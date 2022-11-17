namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
public partial class Entity
{
    public bool IsInitialized;
    public bool IsDestroyed;
    
    public Vector2 Origin => Pivot * Size;
    public Bounds Bounds => new(Position.Current.X - Origin.X, Position.Current.Y - Origin.Y, Size.X, Size.Y);
    public Vector2 Center => new(Position.Current.X + (0.5f - Pivot.X) * Size.X, Position.Current.Y + (0.5f - Pivot.Y) * Size.Y);

    private World? _world;
    public World World => _world ?? throw new InvalidOperationException();

    public CoroutineManager CoroutineManager = new();
    public Collider Collider = new();

    public Position Position = new();
    
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

    public static (Point, Vector2) GetGridCoords(Entity entity)
    {
        return GetGridCoords(entity.Position.Current, entity.Pivot, World.DefaultGridSize);
    }
    
    public static (Point, Vector2) GetGridCoords(Vector2 position, Vector2 pivot, float gridSize)
    {
        var (adjustX, adjustY) = (MathF.Approx(pivot.X, 1) ? -1 : 0, MathF.Approx(pivot.Y, 1) ? -1 : 0);
        var cell = new Point(
            (int)((position.X + adjustX) / gridSize),
            (int)((position.Y + adjustY) / gridSize)
        );
        var relativeCell = new Vector2(
            (position.X + adjustX) % gridSize / gridSize,
            (position.Y + adjustY) % gridSize / gridSize
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
