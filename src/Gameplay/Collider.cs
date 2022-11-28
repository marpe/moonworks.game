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

    public bool HasCollision(in Vector2 cell)
    {
        return HasCollision((int)cell.X, (int)cell.Y);
    }

    public bool HasCollision(in Point cell)
    {
        return HasCollision(cell.X, cell.Y);
    }

    public bool HasCollision(int x, int y)
    {
        var levelGridSize = Parent.World.Level.Size / World.DefaultGridSize;

        if (x < 0 || y < 0 || x >= levelGridSize.X || y >= levelGridSize.Y)
            return true;

        foreach (var layer in Parent.World.Level.LayerInstances)
        {
            if (layer.Identifier != "Tiles" || layer.Type != "IntGrid")
                continue;

            var value = layer.IntGridCsv[y * layer.CWid + x];
            if ((LayerDefs.Tiles)value is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
            {
                return true;
            }
        }

        return false;
    }
}
