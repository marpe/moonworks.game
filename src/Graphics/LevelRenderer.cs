using MyGame.Entities;
using MyGame.WorldsRoot;

namespace MyGame.Graphics;

public class LevelRenderer
{
    public static void DrawBackground(Renderer renderer, World world, RootJson root, Level level, Bounds cameraBounds)
    {
        renderer.DrawRect(level.Bounds, level.BackgroundColor);

        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = world.GetLayerDefinition(layer.LayerDefId);
            if (layerDef.Identifier != "Background")
                continue;
            DrawLayer(renderer, world, root, level, layer, layerDef, (Rectangle)cameraBounds);
        }
    }

    public static void DrawLevel(Renderer renderer, World world, RootJson root, Level level, Bounds cameraBounds)
    {
        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = world.GetLayerDefinition(layer.LayerDefId);
            if (layerDef.Identifier == "Background")
                continue;
            DrawLayer(renderer, world, root, level, layer, layerDef, (Rectangle)cameraBounds);
        }
    }

    private static void DrawLayer(Renderer renderer, World world, RootJson root, Level level, LayerInstance layer, LayerDef layerDef, Rectangle cameraBounds)
    {
        var boundsMin = Entity.ToCell(cameraBounds.MinVec() - level.WorldPos);
        var boundsMax = Entity.ToCell(cameraBounds.MaxVec() - level.WorldPos);

        var tileSetDef = world.GetTileSetDef(layerDef.TileSetDefId);
        var texturePath = world.GetTileSetTexture(tileSetDef.Uid);
        var texture = Shared.Content.Load<TextureAsset>(texturePath).TextureSlice;

        for (var i = 0; i < layer.AutoLayerTiles.Count; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            if (tile.Cell.X < boundsMin.X || tile.Cell.X > boundsMax.X ||
                tile.Cell.Y < boundsMin.Y || tile.Cell.Y > boundsMax.Y)
                continue;

            var sprite = GetTileSprite(texture, tile.TileId, tileSetDef);
            var transform = (
                Matrix3x2.CreateScale(1f, 1f) *
                Matrix3x2.CreateTranslation(
                    level.WorldPos.X + tile.Cell.X * layerDef.GridSize,
                    level.WorldPos.Y + tile.Cell.Y * layerDef.GridSize
                )
            ).ToMatrix4x4();
            renderer.DrawSprite(sprite, transform, Color.White, 0f, SpriteFlip.None);
        }
    }

    public static void DrawLevelDebug(Renderer renderer, World world, RootJson root, Level level, Bounds cameraBounds)
    {
        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = world.GetLayerDefinition(layer.LayerDefId);
            DrawLayerDebug(renderer, world, level, layer, layerDef, (Rectangle)cameraBounds);
        }

        renderer.DrawRectOutline(level.WorldPos, level.WorldPos + level.Size.ToVec2(), Color.Blue, 1.0f);
    }

    private static void DrawLayerDebug(Renderer renderer, World world, Level level, LayerInstance layer, LayerDef layerDef, Rectangle cameraBounds)
    {
        var boundsMin = Entity.ToCell(cameraBounds.MinVec() - level.WorldPos);
        var boundsMax = Entity.ToCell(cameraBounds.MaxVec() - level.WorldPos);

        var cols = level.Width / layerDef.GridSize;
        var rows = level.Height / layerDef.GridSize;

        if (layerDef.LayerType != LayerType.IntGrid)
            return;

        for (var y = boundsMin.Y; y <= boundsMax.Y; y++)
        {
            if (y < 0 || y >= rows)
                continue;

            for (var x = boundsMin.X; x <= boundsMax.X; x++)
            {
                if (x < 0 || x >= cols)
                    continue;

                var cellId = y * cols + x;
                if (cellId < 0 || cellId > layer.IntGrid.Length - 1)
                    continue;

                var value = layer.IntGrid[cellId];
                if (value == 0)
                    continue;

                var enumValue = (LayerDefs.Tiles)value;
                var gridSize = layerDef.GridSize;
                var min = level.WorldPos + new Vector2(x, y) * gridSize;
                var max = min + new Vector2(gridSize, gridSize);

                var color = LayerDefs.TilesColors[enumValue];
                if (world.GetIntDef(layerDef, value, out var intDef))
                {
                    color = intDef.Color;
                }

                renderer.DrawRect(min, max, color * 0.5f, 0);
            }
        }
    }

    public static Sprite GetTileSprite(TextureSlice texture, uint tileId, TileSetDef tileSetDef)
    {
        var cWid = (int)MathF.Ceil((texture.Rectangle.W - tileSetDef.Padding * 2) / (float)(tileSetDef.TileGridSize + tileSetDef.Spacing));
        var cHei = (int)MathF.Ceil((texture.Rectangle.H - tileSetDef.Padding * 2) / (float)(tileSetDef.TileGridSize + tileSetDef.Spacing));

        var cellX = tileId % cWid;
        var cellY = (int)(tileId / cWid);
        var srcRect = new Rectangle(
            (int)(tileSetDef.Padding + cellX * (tileSetDef.TileGridSize + tileSetDef.Spacing)),
            (int)(tileSetDef.Padding + cellY * (tileSetDef.TileGridSize + tileSetDef.Spacing)),
            (int)tileSetDef.TileGridSize,
            (int)tileSetDef.TileGridSize
        );

        return new Sprite(texture, srcRect);
    }
}
