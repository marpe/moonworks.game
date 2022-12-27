using MyGame.WorldsRoot;

namespace MyGame.Entities;

[CustomInspector<GroupInspector>]
public class Entity
{
    public Guid Iid;
    public Point Size;
    public Vector2 Pivot;
    public Color SmartColor;
    
    [HideInInspector] public bool IsInitialized;

    [HideInInspector] public bool IsDestroyed;

    public Bounds Bounds => new(Position.Current.X, Position.Current.Y, Size.X, Size.Y);

    [HideInInspector] public int Width => Size.X;

    [HideInInspector] public int Height => Size.Y;

    [HideInInspector]
    public Point HalfSize => new Point(Width / 2, Height / 2);
    
    [HideInInspector] public Vector2 Center
    {
        get => new(Position.Current.X + 0.5f * Size.X, Position.Current.Y + 0.5f * Size.Y);
        set => Position.Current = value - HalfSize;
    }

    private World? _world;

    [HideInInspector] public World World => _world ?? throw new InvalidOperationException();

    public CoroutineManager CoroutineManager = new();

    [CustomDrawInspector(nameof(DrawPosition))]
    public Position Position = new();

    private void DrawPosition()
    {
        if (ImGuiExt.InspectVector2("Position", ref Position.Current.X, ref Position.Current.Y))
        {
            Position.SetLastUpdatePosition();
        }
    }

    public Point Cell => ToCell(Position);

    [HideInInspector] public float TotalTimeActive;

    public bool DrawDebug;

    public DrawComponent Draw = new();

    public virtual void Initialize(World world)
    {
        _world = world;
        Position.Initialize();
        Draw.Initialize(this);
        IsInitialized = true;
    }

    public virtual void Update(float deltaSeconds)
    {
        CoroutineManager.Update(deltaSeconds);
        TotalTimeActive += deltaSeconds;
        Draw.Update(deltaSeconds);
    }

    public bool HasCollision(int x, int y)
    {
        return HasCollision(x, y, _world ?? throw new Exception());
    }
    
    public static bool HasCollision(int x, int y, World world)
    {
        if (x < world.LevelMin.X || y < world.LevelMin.Y || x >= world.LevelMax.X || y >= world.LevelMax.Y)
            return true;

        var (ix, iy) = (x - world.LevelMin.X, y - world.LevelMin.Y);
        var gridIdx = iy * world.LevelGridSize.X + ix;
        if (gridIdx < 0)
            return true;
        if (gridIdx > world.CollisionLayer.Length - 1)
            return true;

        var collisionValue = world.CollisionLayer[gridIdx];
        return collisionValue != 0 &&
               (collisionValue == (int)LayerDefs.Tiles.Ground ||
                collisionValue == (int)LayerDefs.Tiles.Left_Ground);
    }

    public static (Point min, Point max) GetMinMaxCell(Vector2 position, Vector2 size)
    {
        var minCell = ToCell(position);
        // if the size is exactly a multiple of the grid, e.g (32, 16)
        // then the bottom right corner should still be in cell 1, 0 if the position is 0, 0 so we remove 1.
        // and a bottom right coordinate of 32.1f, 16.1f should be cell 2, 1 so we ceil 
        var max = new Vector2(
            MathF.FastCeilToInt(position.X + size.X - 1),
            MathF.FastCeilToInt(position.Y + size.Y - 1)
        );
        var maxCell = ToCell(max);
        return (minCell, maxCell);
    }

    public bool HasCollision(Vector2 position, Vector2 size)
    {
        return HasCollision(position, size, _world ?? throw new Exception());
    }

    public static bool HasCollision(Vector2 position, Vector2 size, World world)
    {
        var (minCell, maxCell) = GetMinMaxCell(position, size);

        for (var x = minCell.X; x <= maxCell.X; x++)
        {
            for (var y = minCell.Y; y <= maxCell.Y; y++)
            {
                if (HasCollision(x, y, world))
                    return true;
            }
        }

        return false;
    }

    public static Point ToCell(Vector2 position, int gridSize = World.DefaultGridSize)
    {
        return new Point(MathF.FastFloorToInt(position.X / gridSize), MathF.FastFloorToInt(position.Y / gridSize));
    }
}

public class Gun_Pickup : Entity
{
}

public class RefTest : Entity
{
}
