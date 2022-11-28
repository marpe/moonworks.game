namespace MyGame;

[Flags]
public enum CollisionDir
{
    None = 0,
    Up = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3,
    UpRight = 1 << 4,
    UpLeft = 1 << 5,
    DownRight = 1 << 6,
    DownLeft = 1 << 7,
}

public class Collider
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public void Initialize(Entity parent)
    {
        _parent = parent;
    }

    public bool HasCollision(Vector2 position, Vector2 size)
    {
        var minCell = GridCoords.ToCell(position);
        var max = new Vector2(
            MathF.Ceil(position.X + size.X - 1),
            MathF.Ceil(position.Y + size.Y - 1)
        );
        var maxCell = GridCoords.ToCell(max);

        for (var x = minCell.X; x <= maxCell.X; x++)
        {
            for (var y = minCell.Y; y <= maxCell.Y; y++)
            {
                if (HasCollision(x, y))
                    return true;
            }
        }

        return false;
    }

    public bool HasCollision(int x, int y)
    {
        var levelMin = Parent.World.Level.Position / World.DefaultGridSize;
        var levelMax = levelMin + Parent.World.Level.Size / World.DefaultGridSize;

        if (x < levelMin.X || y < levelMin.Y || x >= levelMax.X || y >= levelMax.Y)
            return true;

        foreach (var layer in Parent.World.Level.LayerInstances)
        {
            if (layer.Identifier != "Tiles" || layer.Type != "IntGrid")
                continue;

            var (ix, iy) = (x - levelMin.X, y - levelMin.Y);
            var value = layer.IntGridCsv[iy * layer.CWid + ix];
            if ((LayerDefs.Tiles)value is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
                return true;
        }

        return false;
    }
}
