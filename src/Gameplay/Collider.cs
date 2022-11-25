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

    public bool HasCollision(in Point cell)
    {
        return HasCollision(cell.X, cell.Y);
    }

    public bool HasCollision(int x, int y)
    {
        var ldtk = Parent.World.LdtkRaw; 
            
        var isMultiWorld = ldtk.Worlds.Length > 0;
        var levels = isMultiWorld ? ldtk.Worlds[0].Levels : ldtk.Levels;

        foreach (var level in levels)
        {
            foreach (var layer in level.LayerInstances)
            {
                if (layer.Identifier != "Tiles" || layer.Type != "IntGrid")
                    continue;

                var layerDef = World.GetLayerDefinition(ldtk, layer.LayerDefUid);
                var levelMin = level.Position / (int)layerDef.GridSize;
                var levelMax = levelMin + level.Size / (int)layerDef.GridSize;
                if (x < levelMin.X || y < levelMin.Y ||
                    x >= levelMax.X || y >= levelMax.Y)
                {
                    continue;
                }

                var gridCoords = new Point(x - levelMin.X, y - levelMin.Y);
                var value = layer.IntGridCsv[gridCoords.Y * layer.CWid + gridCoords.X];
                if ((LayerDefs.Tiles)value is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
