﻿using MyGame.WorldsRoot;

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
        var levelMin = world.Level.WorldPos / World.DefaultGridSize;
        var levelGridSize = new Point((int)world.Level.Width, (int)world.Level.Height) / World.DefaultGridSize;
        var levelMax = levelMin + levelGridSize;

        if (x < levelMin.X || y < levelMin.Y || x >= levelMax.X || y >= levelMax.Y)
            return true;

        foreach (var layer in world.Level.LayerInstances)
        {
            var layerDef = World.GetLayerDefinition(world.Root, layer.LayerDefId);
            if (layerDef.Identifier != "Tiles" || layerDef.LayerType != LayerType.IntGrid)
                continue;

            var (ix, iy) = (x - levelMin.X, y - levelMin.Y);
            var gridId = iy * levelGridSize.X + ix;
            if (gridId < 0 || gridId > layer.IntGrid.Length - 1) // TODO (marpe): Check which scenarios this occurs 
                continue;
            
            var value = layer.IntGrid[gridId];
            if ((LayerDefs.Tiles)value is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
                return true;
        }

        return false;
    }

    public static (Point min, Point max) GetMinMaxCell(Vector2 position, Vector2 size)
    {
        var minCell = ToCell(position);
        // if the size is exactly a multiple of the grid, e.g (32, 16)
        // then the bottom right corner should still be in cell 1, 0 if the position is 0, 0 so we remove 1.
        // and a bottom right coordinate of 32.1f, 16.1f should be cell 2, 1 so we ceil 
        var max = new Vector2(
            MathF.Ceil(position.X + size.X - 1),
            MathF.Ceil(position.Y + size.Y - 1)
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
        return new Point(MathF.FloorToInt(position.X / gridSize), MathF.FloorToInt(position.Y / gridSize));
    }
}

public class Gun_Pickup : Entity
{
}

public class RefTest : Entity
{
}