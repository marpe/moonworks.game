using MyGame.Cameras;
using MyGame.Graphics;
using MyGame.Screens;
using MyGame.TWConsole;
using MyGame.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyGame;

public struct Bounds
{
    public Vector2 Min;
    public Vector2 Size;
    public Vector2 Max => Min + Size;

    public Bounds(float x, float y, float w, float h)
    {
        Min = new Vector2(x, y);
        Size = new Vector2(w, h);
    }

    public Bounds(Vector2 min, Vector2 max) : this(min.X, min.Y, max.X - min.X, max.Y - min.Y)
    {
    }

    public static implicit operator Rectangle(Bounds b)
    {
        return new Rectangle((int)b.Min.X, (int)b.Min.Y, (int)b.Size.X, (int)b.Size.Y);
    }

    public static Bounds Lerp(Bounds a, Bounds b, double alpha)
    {
        var min = Vector2.Lerp(a.Min, b.Min, (float)alpha);
        var max = Vector2.Lerp(a.Max, b.Max, (float)alpha);
        return new Bounds(min, max);
    }
}

public partial class Entity
{
    public Vector2 InitialPosition;
    public Vector2 PreviousPosition;
    public Vector2 Origin => Pivot * Size;
    public Bounds Bounds => new Bounds(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);
    public Vector2 Center => new Vector2(Position.X + (0.5f - Pivot.X) * Size.X, Position.Y + (0.5f - Pivot.Y) * Size.Y);
}

public class Velocity
{
    public const float KillThreshold = 0.0005f;
    public Vector2 Delta = Vector2.Zero;
    public Vector2 Friction = new Vector2(0.84f, 0.94f);

    public float X
    {
        get => Delta.X;
        set => Delta.X = value;
    }

    public float Y
    {
        get => Delta.Y;
        set => Delta.Y = value;
    }

    public static void ApplyFriction(Velocity velocity)
    {
        velocity.Delta *= velocity.Friction;
        if (MathF.IsNearZero(velocity.X, KillThreshold))
            velocity.X = 0;
        if (MathF.IsNearZero(velocity.Y, KillThreshold))
            velocity.Y = 0;
    }

    public static Vector2 operator *(Velocity velocity, float value) => velocity.Delta * value;
    public static implicit operator Vector2(Velocity velocity) => velocity.Delta;
}

public partial class Enemy : Entity
{
    public Velocity Velocity = new();
}

public partial class Gun_Pickup : Entity
{
}

public partial class RefTest : Entity
{
}

public partial class Player : Entity
{
    public Velocity Velocity = new();
    public uint FrameIndex;
    public float TotalTime;
    public float Speed = 20f;
    public float JumpSpeed = -500f;
    public float LastOnGroundTime;
    public Vector2 Squash = Vector2.One;
    public bool EnableSquash = true;
}

public class DebugDraw
{
    public Rectangle Rectangle;
    public Color Color;
    public ulong UpdateCountAtDraw;
    public string? Text;
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
    public Player Player => _player;

    private float _totalTime;
    public float Gravity = 512f;

    private readonly GameScreen _parent;
    private List<DebugDraw> _debugDrawCalls = new();

    public long GridSize => LdtkRaw.DefaultGridSize;

    public World(GameScreen parent, GraphicsDevice device, ReadOnlySpan<char> ldtkPath)
    {
        _parent = parent;
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
        _parent.CameraController.TrackEntity(_player);
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
                entity.Pivot = entityInstance.PivotVec;
                entity.Position = level.Position + entityInstance.Position;
                entity.InitialPosition = entity.PreviousPosition = entity.Position;
                entity.Size = new Vector2(entityInstance.Width, entityInstance.Height);
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

    public void Update(float deltaSeconds, InputHandler input, bool allowKeyboard, bool allowMouse)
    {
        var (movementX, movementY) = HandleInput(input, allowKeyboard);

        if (_player.Position.Y > 300)
        {
            _player.Position = _player.InitialPosition;
        }

        _player.PreviousPosition = _player.Position;
        _totalTime += deltaSeconds;
        _player.TotalTime += deltaSeconds;
        _player.FrameIndex = MathF.IsNearZero(_player.Velocity.X) ? 0 : (uint)(_player.TotalTime * 10) % 2;

        if (IsGrounded(_player, _player.Velocity))
            _player.LastOnGroundTime = _totalTime;

        if (movementX != 0)
            _player.Velocity.X += movementX * _player.Speed;
        var canJump = (_totalTime - _player.LastOnGroundTime) < 0.1f;
        if (movementY == -1 && canJump)
        {
            _player.Squash = new Vector2(0.6f, 1.4f);
            _player.LastOnGroundTime = 0;
            _player.Velocity.Y = _player.JumpSpeed;
        }

        HandleCollisions(_player, _player.Velocity, deltaSeconds);
        _player.Position += _player.Velocity * deltaSeconds;

        Velocity.ApplyFriction(_player.Velocity);

        if (!IsGrounded(_player, _player.Velocity))
            _player.Velocity.Y += Gravity * deltaSeconds;

        _player.Squash = Vector2.SmoothStep(_player.Squash, Vector2.One, deltaSeconds * 20f);
    }

    private (int movementX, int movementY) HandleInput(InputHandler input, bool allowKeyboard)
    {
        var movementX = 0;
        var movementY = 0;

        if (allowKeyboard)
        {
            if (input.IsKeyPressed(KeyCode.Insert))
                _player.Position = new Vector2(100, 50);

            if (input.IsKeyDown(KeyCode.Right) ||
                input.IsKeyDown(KeyCode.D))
                movementX += 1;

            if (input.IsKeyDown(KeyCode.Left) ||
                input.IsKeyDown(KeyCode.A))
                movementX += -1;

            if (input.IsKeyPressed(KeyCode.Space))
                movementY -= 1;
        }

        return (movementX, movementY);
    }

    public bool IsGrounded(Entity entity, Vector2 velocity)
    {
        var cell = GetGridCoords(entity);
        return velocity.Y == 0 && HasCollision(cell.X, cell.Y + 1);
    }

    private void HandleCollisions(Entity entity, Velocity velocity, float deltaSeconds)
    {
        var deltaMove = (velocity * deltaSeconds) / GridSize;
        var cell = GetGridCoords(entity);
        var (adjustX, adjustY) = (MathF.Approx(entity.Pivot.X, 1) ? -1 : 0, MathF.Approx(entity.Pivot.Y, 1) ? -1 : 0);
        var positionInCell = new Vector2(
            ((entity.Position.X + adjustX) % GridSize) / GridSize,
            ((entity.Position.Y + adjustY) % GridSize) / GridSize
        );
        var resultCellPos = positionInCell + deltaMove; // relative cell pos ( e.g < 0 means we moved to the previous cell ) 
        if (resultCellPos.X > 0.8f && HasCollision(cell.X + 1, cell.Y))
        {
            // Logger.LogInfo("Collide +x");
            entity.Position.X = (cell.X + 0.8f - MathF.Epsilon) * GridSize;
            velocity.X = 0;
        }

        if (resultCellPos.X < 0.2f && HasCollision(cell.X - 1, cell.Y))
        {
            // Logger.LogInfo("Collide -x");
            entity.Position.X = (cell.X + 0.2f + MathF.Epsilon) * GridSize;
            velocity.X = 0;
        }

        if (resultCellPos.Y > 1.0f && HasCollision(cell.X, cell.Y + 1))
        {
            // Logger.LogInfo("Collide +y");
            entity.Position.Y = (cell.Y + 1.0f) * GridSize;
            if (entity is Player p)
                p.Squash = new Vector2(1.5f, 0.5f);

            velocity.Y = 0;
        }

        if (velocity.Y < 0 && resultCellPos.Y < 0.8f && HasCollision(cell.X, cell.Y - 1))
        {
            // Logger.LogInfo("Collide -y");
            entity.Position.Y = (cell.Y + 0.8f) * GridSize;
            velocity.Y = 0;
        }
    }

    public Point GetGridCoords(Entity entity)
    {
        return GetGridCoords(entity.Position, entity.Pivot, GridSize);
    }

    public static Point GetGridCoords(Vector2 position, Vector2 pivot, long gridSize)
    {
        var (x, y) = (position.X, position.Y);
        var (adjustX, adjustY) = (MathF.Approx(pivot.X, 1) ? -1 : 0, MathF.Approx(pivot.Y, 1) ? -1 : 0);
        var gridX = (int)((x + adjustX) / gridSize);
        var gridY = (int)((y + adjustY) / gridSize);
        return new Point(gridX, gridY);
    }

    private bool HasCollision(int x, int y)
    {
        var isMultiWorld = LdtkRaw.Worlds.Length > 0;
        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;

        LayerDefinition GetLayerDefinition(long layerDefUid)
        {
            for (var i = 0; i < LdtkRaw.Defs.Layers.Length; i++)
            {
                if (LdtkRaw.Defs.Layers[i].Uid == layerDefUid)
                    return LdtkRaw.Defs.Layers[i];
            }

            throw new InvalidOperationException();
        }

        foreach (var level in levels)
        {
            foreach (var layer in level.LayerInstances)
            {
                if (layer.Identifier != "Tiles")
                    continue;
                if (layer.Type != "IntGrid")
                    continue;
                var layerDef = GetLayerDefinition(layer.LayerDefUid);
                var levelMin = level.Position / (int)layerDef.GridSize;
                var levelMax = levelMin + level.Size / (int)layerDef.GridSize;
                if (x < levelMin.X || y < levelMin.Y ||
                    x >= levelMax.X || y >= levelMax.Y)
                    continue;
                var gridCoords = new Point(x - levelMin.X, y - levelMin.Y);
                var value = layer.IntGridCsv[gridCoords.Y * layer.CWid + gridCoords.X];
                if (value == 5 || value == 6)
                    return true;
            }
        }

        return false;
    }

    public void Draw(Renderer renderer, Camera camera, double alpha)
    {
        var cameraBounds = Bounds.Lerp(camera.PreviousBounds, camera.Bounds, alpha);

        var isMultiWorld = LdtkRaw.Worlds.Length > 0;
        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;

        for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            var level = levels[levelIndex];
            var color = ColorExt.FromHex(level.BgColor.AsSpan().Slice(1));
            renderer.DrawRect(level.Bounds, color);

            for (var layerIndex = level.LayerInstances.Length - 1; layerIndex >= 0; layerIndex--)
            {
                var layer = level.LayerInstances[layerIndex];
                DrawLayer(renderer, level, layer, cameraBounds);
            }

            if (Debug)
            {
                renderer.DrawRect(level.Position, level.Position + level.Size, Color.Red, 1.0f);
            }
        }

        if (Debug)
            renderer.DrawRect(Vector2.Zero, WorldSize, Color.Magenta, 1.0f);

        // DrawDebug(renderer, _player);

        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
        {
            var srcRect = new Rectangle((int)(_player.FrameIndex * 16), 0, 16, 16);
            var position = Vector2.Lerp(_player.PreviousPosition, _player.Position, (float)alpha);
            var xform = Matrix3x2.CreateTranslation(-_player.Origin.X, -_player.Origin.Y) *
                        Matrix3x2.CreateScale(_player.EnableSquash ? _player.Squash : Vector2.One) *
                        Matrix3x2.CreateTranslation(position.X, position.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0);
            if (Debug)
                DrawDebug(renderer, _player);
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            var entity = _enemies[i];

            var offset = entity.Type switch
            {
                EnemyType.Slug => 5,
                EnemyType.BlueBee => 3,
                EnemyType.YellowBee or _ => 1,
            };

            var frameIndex = (int)(_totalTime * 10) % 2;
            var srcRect = new Rectangle(offset * 16 + frameIndex * 16, 16, 16, 16);
            var position = Vector2.Lerp(entity.PreviousPosition, entity.Position, (float)alpha);
            var xform = Matrix3x2.CreateTranslation(position.X - entity.Origin.X, position.Y - entity.Origin.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0);
            if (Debug)
                DrawDebug(renderer, entity);
        }

        for (var i = _debugDrawCalls.Count - 1; i >= 0; i--)
        {
            if (_debugDrawCalls[i].UpdateCountAtDraw < Shared.Game.UpdateCount)
                _debugDrawCalls.RemoveAt(i);
        }

        foreach (var debugDrawCall in _debugDrawCalls)
        {
            if (debugDrawCall.Text != null)
            {
                renderer.DrawText(FontType.ConsolasMonoMedium, debugDrawCall.Text, debugDrawCall.Rectangle.Min(), debugDrawCall.Color);
            }
            else
            {
                renderer.DrawRect(debugDrawCall.Rectangle, debugDrawCall.Color);
            }
        }

        if (Debug)
        {
            var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
            renderer.DrawRect(boundsMin, boundsMax, Color.Red, 1f);
        }
    }

    private void DrawLayer(Renderer renderer, Level level, LayerInstance layer, Rectangle cameraBounds)
    {
        if (!layer.TilesetDefUid.HasValue)
            return;

        var texture = TilesetTextures[layer.TilesetDefUid.Value];

        var layerWidth = layer.CWid;
        var layerHeight = layer.CHei;

        var boundsMin = cameraBounds
            .MinVec(); // WorldToTilePosition(cameraBounds.MinVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);
        var boundsMax = cameraBounds
            .MaxVec(); // WorldToTilePosition(cameraBounds.MaxVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);

        if (layer.Type == "IntGrid" && layer.Identifier == "Tiles" && Debug)
        {
            for (var i = 0; i < layer.IntGridCsv.Length; i++)
            {
                var value = layer.IntGridCsv[i];
                if (value is 5 or 6)
                {
                    var gridSize = layer.GridSize;
                    var gridY = (int)(i / layer.CWid);
                    var gridX = (int)(i % layer.CWid);
                    var min = level.Position + layer.TotalOffset + new Vector2(gridX, gridY) * gridSize;
                    var max = min + new Vector2(gridSize, gridSize);
                    renderer.DrawRect(min, max, Color.Red, 1.0f);
                }
            }
        }

        for (var i = 0; i < layer.GridTiles.Length; i++)
        {
            var tile = layer.GridTiles[i];
            var tilePos = level.Position + layer.TotalOffset + tile.Position;
            if ((tilePos.X + layer.GridSize) < boundsMin.X || (tilePos.Y + layer.GridSize) < boundsMin.Y ||
                tilePos.X > boundsMax.X || tilePos.Y > boundsMax.Y)
                continue;
            RenderTile(renderer, tilePos, tile, layer, texture);
        }

        for (var i = 0; i < layer.AutoLayerTiles.Length; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            var tilePos = level.Position + layer.TotalOffset + tile.Position;
            if ((tilePos.X + layer.GridSize) < boundsMin.X || (tilePos.Y + layer.GridSize) < boundsMin.Y ||
                tilePos.X > boundsMax.X || tilePos.Y > boundsMax.Y)
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

    public void DrawDebug(Renderer renderer, Entity e)
    {
        var min = e.Bounds.Min;
        var max = e.Bounds.Max;
        renderer.DrawRect(min, max, e.SmartColor, 1.0f);
        renderer.DrawRect(new Rectangle((int)e.Position.X, (int)e.Position.Y, 1, 1), Color.Magenta, 0);
        var snappedToGrid = GetGridCoords(_player) * (int)LdtkRaw.DefaultGridSize;
        renderer.DrawRect(new Rectangle(snappedToGrid.X - 1, snappedToGrid.Y, 3, 1), Color.Red, 0);
        renderer.DrawRect(new Rectangle(snappedToGrid.X, snappedToGrid.Y - 1, 1, 3), Color.Red, 0);
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
