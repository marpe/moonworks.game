using MyGame.Generated;
using MyGame.Graphics;
using MyGame.TWConsole;
using MyGame.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyGame;

public partial class Entity
{
}

public partial class Player : Entity
{
    public enum PlayerState
    {
        Idle,
        Locomote,
    }

    public PlayerState State = PlayerState.Idle;

    public uint FrameIndex;
    public float TotalTime;
}

public class World
{
    [CVar("world.debug", "Toggle world debugging")]
    public static bool Debug;

    private LdtkJson LdtkRaw;
    private Dictionary<long, Texture> TilesetTextures;
    private Dictionary<string, Texture> Textures = new();
    public Point WorldSize;

    private static JsonSerializer _jsonSerializer = new() { Converters = { new ColorConverter() } };

    private List<Enemy> _enemies;
    private Player _player;

    public World(GraphicsDevice device, ReadOnlySpan<char> ldtkPath)
    {
        var jsonString = File.ReadAllText(ldtkPath.ToString());
        LdtkRaw = LdtkJson.FromJson(jsonString);
        TilesetTextures = LoadTilesets(device, ldtkPath, LdtkRaw.Defs.Tilesets);
        WorldSize = GetWorldSize(LdtkRaw);

        foreach (var entityDef in LdtkRaw.Defs.Entities)
        {
        }

        var allEntities = new List<Entity>();
        var isMultiWorld = LdtkRaw.Worlds.Length > 0;
        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;
        foreach (var level in levels)
        {
            var entities = LoadEntitiesInLevel(level);
            allEntities.AddRange(entities);
        }

        var textures = new[] { ContentPaths.ldtk.Example.Characters_png };
        foreach (var texturePath in textures)
        {
            if (texturePath.EndsWith(".aseprite"))
            {
                var texture = TextureUtils.LoadAseprite(device, texturePath);
                Textures.Add(texturePath, texture);
            }
            else
            {
                var texture = TextureUtils.LoadPngTexture(device, texturePath);
                Textures.Add(texturePath, texture);
            }
        }

        _player = (Player)allEntities.First(t => t.EntityType == EntityType.Player);
        _enemies = allEntities.Where(x => x.EntityType == EntityType.Enemy).Cast<Enemy>().ToList();
    }

    private static List<Entity> LoadEntitiesInLevel(Level level)
    {
        var entities = new List<Entity>();
        foreach (var layer in level.LayerInstances)
        {
            if (layer.Type != "Entities")
                continue;

            foreach (var entityInstance in layer.EntityInstances)
            {
                var parsedType = Enum.Parse<EntityType>(entityInstance.Identifier);
                var type = Entity.TypeMap[parsedType];
                var entity = (Entity)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());

                entity.EntityType = parsedType;
                entity.Iid = Guid.Parse(entityInstance.Iid);
                entity.Position = level.Position + entityInstance.Position - entityInstance.PivotP * entityInstance.Size;
                entity.Size = new Point((int)entityInstance.Width, (int)entityInstance.Height);
                entity.SmartColor = ColorExt.FromHex(entityInstance.SmartColor[1..]);

                foreach (var field in entityInstance.FieldInstances)
                {
                    var fieldValue = (JToken)field.Value;
                    var fieldInfo = entity.GetType().GetField(field.Identifier) ?? throw new InvalidOperationException();
                    var deserializedValue = fieldValue?.ToObject(fieldInfo.FieldType, _jsonSerializer);
                    fieldInfo.SetValue(entity, deserializedValue);
                }

                entities.Add(entity);
            }
        }

        return entities;
    }

    public void Update(float deltaSeconds)
    {
        _player.TotalTime += deltaSeconds;
        _player.FrameIndex = (uint)(_player.TotalTime * 10) % 2;
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

        if (Debug)
            renderer.DrawRect(Vector2.Zero, WorldSize, Color.Magenta, 1.0f);

        // DrawDebug(renderer, _player);

        var srcRect = new Rectangle((int)(_player.FrameIndex * 16), 0, 16, 16);
        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
        var xform = Matrix3x2.CreateTranslation(_player.Position.X, _player.Position.Y);
        renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0);

        for (var i = 0; i < _enemies.Count; i++)
        {
            var entity = _enemies[i];
            DrawDebug(renderer, entity);
        }
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

    public static void DrawDebug(Renderer renderer, Entity e)
    {
        renderer.DrawRect(new Rectangle(e.Position.X, e.Position.Y, e.Size.X, e.Size.Y), e.SmartColor);
    }

    private static Point WorldToTilePosition(Vector2 worldPosition, int gridSize, long width, long height)
    {
        var x = MathF.FastFloorToInt(worldPosition.X / gridSize);
        var y = MathF.FastFloorToInt(worldPosition.Y / gridSize);
        return new Point((int)MathF.Clamp(x, 0, width - 1), (int)MathF.Clamp(y, 0, height - 1));
    }

    private static Point GetWorldSize(LdtkJson ldtk)
    {
        var isMultiWorld = ldtk.Worlds.Length > 0;
        var levels = isMultiWorld ? ldtk.Worlds[0].Levels : ldtk.Levels;

        var worldSize = Point.Zero;

        for (var i = 0; i < levels.Length; i++)
        {
            var max = levels[i].Position + levels[i].Size;
            if (worldSize.X < max.X) worldSize.X = max.X;
            if (worldSize.Y < max.Y) worldSize.Y = max.Y;
        }

        return worldSize;
    }

    private static Dictionary<long, Texture> LoadTilesets(GraphicsDevice device, ReadOnlySpan<char> ldtkPath, TilesetDefinition[] tilesets)
    {
        var textures = new Dictionary<long, Texture>();

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var tilesetDef in tilesets)
        {
            if (string.IsNullOrWhiteSpace(tilesetDef.RelPath))
                continue;
            var tilesetPath = Path.Combine(Path.GetDirectoryName(ldtkPath).ToString(), tilesetDef.RelPath);
            if (tilesetPath.EndsWith(".aseprite"))
            {
                var asepriteTexture = TextureUtils.LoadAseprite(device, tilesetPath);
                textures.Add(tilesetDef.Uid, asepriteTexture);
            }
            else
            {
                var texture = Texture.LoadPNG(device, commandBuffer, tilesetPath);
                textures.Add(tilesetDef.Uid, texture);
            }
        }

        device.Submit(commandBuffer);

        return textures;
    }
}
