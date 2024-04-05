namespace MyGame.Entities;

[CustomInspector<GroupInspector>]
public class Entity
{
    [HideInInspector]
    public Guid Iid;

    public Point Size;
    public Vector2 Pivot;
    public Color SmartColor;

    [HideInInspector]
    public bool IsInitialized { get; private set; }

    [HideInInspector]
    public bool IsDestroyed { get; private set; }

    [HideInInspector]
    public Bounds Bounds => new(Position.Current.X, Position.Current.Y, Size.X, Size.Y);

    [HideInInspector]
    public int Width => Size.X;

    [HideInInspector]
    public int Height => Size.Y;

    [HideInInspector]
    public Point HalfSize => new Point(Width / 2, Height / 2);

    [HideInInspector]
    public Vector2 Center
    {
        get => new(Position.Current.X + 0.5f * Size.X, Position.Current.Y + 0.5f * Size.Y);
        set => Position.Current = value - HalfSize.ToVec2();
    }

    private World? _world;

    [HideInInspector]
    public World World => _world ?? throw new InvalidOperationException();

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

    [HideInInspector]
    public float TotalTimeActive;

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

    public virtual void OnEntityAdded(World world)
    {
        Initialize(world);
    }

    public virtual void OnEntityRemoved()
    {
    }

    public virtual void Destroy()
    {
        IsDestroyed = true;
        World.Entities.Remove(this);
    }

    public virtual void DrawDebug(Renderer renderer, bool drawCoords, double alpha)
    {
        var cell = Cell;
        var cellInScreen = cell.ToVec2() * World.DefaultGridSize;
        renderer.DrawPoint(Position.Current, SmartColor, 2);

        // draw small crosshair
        {
            renderer.DrawRect(new Rectangle((int)(cellInScreen.X - 1), (int)cellInScreen.Y, 3, 1), SmartColor);
            renderer.DrawRect(new Rectangle((int)cellInScreen.X, (int)(cellInScreen.Y - 1), 1, 3), SmartColor);
        }

        renderer.DrawRectOutline(Bounds.Min, Bounds.Max, SmartColor, 1.0f);

        if (drawCoords)
        {
            var cellText = $"{cell.X.ToString()}, {cell.Y.ToString()}";
            var posText = $"{StringExt.TruncateNumber(Position.Current.X)}, {StringExt.TruncateNumber(Position.Current.Y)}";
            ReadOnlySpan<char> str = posText + " " + cellText;
            var textSize = renderer.MeasureString(BMFontType.ConsolasMonoSmall, str);
            // TODO (marpe): Fix
            // renderer.DrawBMText(BMFontType.ConsolasMonoSmall, str, Position.Current, textSize * new Vector2(0.5f, 1), Vector2.One * 0.25f, 0, 0, Color.Black);
        }
    }
}

public class Gun_Pickup : Entity
{
}

public class RefTest : Entity
{
}
