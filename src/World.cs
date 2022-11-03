using MyGame.Graphics;
using MyGame.TWConsole;
using MyGame.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyGame;

public class World
{
    [CVar("world.debug", "Toggle world debugging")]
    public static bool Debug;

    private LdtkJson LdtkRaw;
    private Dictionary<long, Texture> TilesetTextures;

    public Point WorldSize;

    private List<Entity> _entities = new();
    private static JsonSerializer _jsonSerializer = new() { Converters = { new ColorConverter() } };

    public World(LdtkJson ldtk, Dictionary<long, Texture> tilesetTextures)
    {
        LdtkRaw = ldtk;
        TilesetTextures = tilesetTextures;
    }

    private static Point WorldToTilePosition(Vector2 worldPosition, int gridSize, long width, long height)
    {
        var x = MathF.FastFloorToInt(worldPosition.X / gridSize);
        var y = MathF.FastFloorToInt(worldPosition.Y / gridSize);
        return new Point((int)MathF.Clamp(x, 0, width - 1), (int)MathF.Clamp(y, 0, height - 1));
    }

    public void Initialize()
    {
        var isMultiWorld = LdtkRaw.Worlds.Length > 0;

        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;

        var level = levels[0];

        WorldSize = Point.Zero;

        for (var i = 0; i < levels.Length; i++)
        {
            var max = levels[i].Position + levels[i].Size;
            if (WorldSize.X < max.X)
                WorldSize.X = max.X;
            if (WorldSize.Y < max.Y)
                WorldSize.Y = max.Y;
        }

        foreach (var entityDef in LdtkRaw.Defs.Entities)
        {
        }

        foreach (var layer in level.LayerInstances)
        {
            if (layer.Type != "Entities")
                continue;

            foreach (var entityInstance in layer.EntityInstances)
            {
                var parsedType = Enum.Parse<EntityType>(entityInstance.Identifier);
                var type = Entity.TypeMap[parsedType];
                var entity = (Entity)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());
                ParseFields(entity, entityInstance);
                _entities.Add(entity);
            }
        }
    }

    private static void ParseFields(Entity entity, EntityInstance entityInstance)
    {
        entity.Iid = Guid.Parse(entityInstance.Iid);
        entity.Position = new Point((int)entityInstance.Px[0], (int)entityInstance.Px[1]);
        entity.Size = new Point((int)entityInstance.Width, (int)entityInstance.Height);
        
        foreach (var field in entityInstance.FieldInstances)
        {
            var fieldValue = (JToken)field.Value;
            var fieldInfo = entity.GetType().GetField(field.Identifier) ?? throw new InvalidOperationException();
            var deserializedValue = fieldValue?.ToObject(fieldInfo.FieldType, _jsonSerializer);
            fieldInfo.SetValue(entity, deserializedValue);
        }
    }

    public void Draw(Renderer renderer)
    {
        var cameraBounds = new Rectangle(0, 0, 1920, 1080);

        var isMultiWorld = LdtkRaw.Worlds.Length > 0;
        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;

        for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            var level = levels[levelIndex];
            var color = ColorExt.FromHex(level.BgColor[1..]);
            renderer.DrawRect(level.Bounds, color);

            for (var layerIndex = level.LayerInstances.Length - 1; layerIndex >= 0; layerIndex--)
            {
                var layer = level.LayerInstances[layerIndex];
                DrawLayer(renderer, level, layer, cameraBounds);
            }

            if (Debug)
                renderer.DrawRect(level.Position, level.Position + level.Size, Color.Red, 1.0f);
        }

        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            renderer.DrawRect(new Rectangle(entity.Position.X, entity.Position.Y, entity.Size.X, entity.Size.Y), Color.Black);
        }

        if (Debug)
            renderer.DrawRect(Vector2.Zero, WorldSize, Color.Magenta, 1.0f);
    }

    private void DrawLayer(Renderer renderer, Level level, LayerInstance layer, Rectangle cameraBounds)
    {
        if (!layer.TilesetDefUid.HasValue)
            return;

        var texture = TilesetTextures[layer.TilesetDefUid.Value];

        var layerWidth = layer.CWid;
        var layerHeight = layer.CHei;

        var min = cameraBounds
            .MinVec(); // WorldToTilePosition(cameraBounds.MinVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);
        var max = cameraBounds
            .MaxVec(); // WorldToTilePosition(cameraBounds.MaxVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);

        for (var i = 0; i < layer.GridTiles.Length; i++)
        {
            var tile = layer.GridTiles[i];
            var tilePos = level.Position + layer.TotalOffset + tile.Position;
            if (tilePos.X < min.X || tilePos.Y < min.Y ||
                tilePos.X > max.X || tilePos.Y > max.Y)
                continue;
            RenderTile(renderer, tilePos, tile, layer, texture);
        }

        for (var i = 0; i < layer.AutoLayerTiles.Length; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            var tilePos = level.Position + layer.TotalOffset + tile.Position;
            if (tilePos.X < min.X || tilePos.Y < min.Y ||
                tilePos.X > max.X || tilePos.Y > max.Y)
                continue;
            RenderTile(renderer, tilePos, tile, layer, texture);
        }

        /*for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    // var tile = layer.Tiles[x, y];
                    // if (tile == null)
                        // continue;
                    // RenderTile(batcher, tile, layer.LayerInstance, levelPosition, texture);
                }
            }*/
    }

    private static void RenderTile(Renderer renderer, Point position, TileInstance tile, LayerInstance layer, Texture texture)
    {
        var srcRect = new Rectangle((int)tile.Src[0], (int)tile.Src[1], (int)layer.GridSize, (int)layer.GridSize);
        var sprite = new Sprite(texture, srcRect);
        var transform = Matrix3x2.CreateTranslation(position.X, position.Y);
        renderer.DrawSprite(sprite, transform, Color.White, 0);
    }

    public void Dispose()
    {
        foreach (var (key, texture) in TilesetTextures)
        {
            texture.Dispose();
        }

        TilesetTextures.Clear();
    }
}
