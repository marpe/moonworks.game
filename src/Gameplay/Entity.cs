namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
public partial class Entity
{
    public bool IsInitialized;
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

public partial class Player : Entity
{
    public bool EnableSquash = true;
    public SpriteFlip Flip = SpriteFlip.None;

    public uint FrameIndex;
    public bool IsJumping;
    public float JumpHoldTime = 0.3f;
    public float JumpSpeed = -300f;
    public float LastJumpStartTime;
    public float LastOnGroundTime;
    public float Speed = 20f;
    public Vector2 Squash = Vector2.One;
    public float TotalTime;

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(0.84f, 0.98f),
    };
}
