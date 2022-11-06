using MyGame.Cameras;
using MyGame.Graphics;
using MyGame.Screens;
using MyGame.TWConsole;
using MyGame.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyGame;

[Flags]
public enum CollisionDir
{
    None = 0,
    Top = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3,
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
    public List<Enemy> Enemies => _enemies;
    
    private Player _player;
    public Player Player => _player;

    public float Gravity = 800f;

    private readonly GameScreen _parent;

    private DebugDrawItems _debugDraw;

    public long GridSize => LdtkRaw.DefaultGridSize;

    public World(GameScreen parent, GraphicsDevice device, ReadOnlySpan<char> ldtkPath)
    {
        _parent = parent;
        var jsonString = File.ReadAllText(ldtkPath.ToString());
        LdtkRaw = LdtkJson.FromJson(jsonString);
        TilesetTextures = LoadTilesets(device, ldtkPath, LdtkRaw.Defs.Tilesets);
        WorldSize = GetWorldSize(LdtkRaw);

        _debugDraw = new();

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

        foreach (var ent in allEntities)
        {
            if (ent is Enemy enemy && enemy.Type == EnemyType.Slug)
            {
                var randomDirection = Random.Shared.Next() % 2 == 0 ? -1 : 1;
                enemy.Velocity.Delta = new Vector2(randomDirection * 100f, 0);
                enemy.Velocity.Friction = new Vector2(0.99f, 0.99f);
            }
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

    private void UpdatePrevious()
    {
        _player.PreviousPosition = _player.Position;

        for (var i = 0; i < _enemies.Count; i++)
        {
            var entity = _enemies[i];
            entity.PreviousPosition = entity.Position;
        }
    }

    public void Update(bool isPaused, float deltaSeconds, InputHandler input, bool allowKeyboard, bool allowMouse)
    {
        UpdatePrevious();
        if (isPaused)
            return;

        UpdatePlayer(deltaSeconds, input, allowKeyboard);
        UpdateEnemies(deltaSeconds);
    }

    private void UpdateEnemies(float deltaSeconds)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var entity = _enemies[i];

            entity.TotalTime += deltaSeconds;

            if (entity.Type == EnemyType.Slug)
            {
                var (cell, cellRel) = GetGridCoords(entity);
                if (entity.Velocity.X > 0 && !HasCollision(cell.X + 1, cell.Y + 1))
                    entity.Velocity.X *= -1;
                else if (entity.Velocity.X < 0 && !HasCollision(cell.X - 1, cell.Y + 1))
                    entity.Velocity.X *= -1;

                var prevVelocity = entity.Velocity.Delta;
                var collisions = HandleCollisions(entity, entity.Velocity, deltaSeconds);

                entity.Position += entity.Velocity * deltaSeconds;
                if ((collisions & CollisionDir.Left) != 0)
                    entity.Velocity.Delta = new Vector2(100f, 0);
                else if ((collisions & CollisionDir.Right) != 0)
                    entity.Velocity.Delta = new Vector2(-100f, 0);

                Velocity.ApplyFriction(entity.Velocity);

                if (entity.Velocity.X > 0)
                    entity.Flip = SpriteFlip.None;
                else if (entity.Velocity.X < 0)
                    entity.Flip = SpriteFlip.FlipHorizontally;

                if (!IsGrounded(entity, entity.Velocity))
                    entity.Velocity.Y += Gravity * deltaSeconds;
                if (Math.Abs(entity.Velocity.X) < 50f)
                {
                    entity.Velocity.X += entity.Velocity.X;
                }
            }
            else if (entity.Type == EnemyType.YellowBee)
            {
                var speed = 3f;
                var deltaMove = new Vector2(MathF.Cos(entity.TotalTime * 3f), MathF.Sin(entity.TotalTime * 3f)) * 10f;
                entity.Velocity.Delta = deltaMove;
                entity.Position += entity.Velocity * deltaSeconds;

                if (entity.Velocity.X > 0)
                    entity.Flip = SpriteFlip.None;
                else if (entity.Velocity.X < 0)
                    entity.Flip = SpriteFlip.FlipHorizontally;
            }
        }
    }


    private void UpdatePlayer(float deltaSeconds, InputHandler input, bool allowKeyboard)
    {
        HandleInput(input, allowKeyboard, out var movementX);
        var isJumpDown = input.IsKeyDown(KeyCode.Space);
        var isJumpPressed = input.IsKeyPressed(KeyCode.Space);

        if (_player.Position.Y > 300)
        {
            _player.SetPositions(_player.InitialPosition);
        }

        _player.TotalTime += deltaSeconds;
        _player.FrameIndex = MathF.IsNearZero(_player.Velocity.X) ? 0 : (uint)(_player.TotalTime * 10) % 2;

        if (IsGrounded(_player, _player.Velocity))
            _player.LastOnGroundTime = _player.TotalTime;

        if (movementX != 0)
            _player.Velocity.X += movementX * _player.Speed;

        if (!_player.IsJumping && isJumpPressed)
        {
            var timeSinceOnGround = _player.TotalTime - _player.LastOnGroundTime;
            if (timeSinceOnGround < 0.1f)
            {
                _player.Squash = new Vector2(0.6f, 1.4f);
                _player.LastOnGroundTime = 0;
                _player.Velocity.Y = _player.JumpSpeed;
                _player.LastJumpStartTime = _player.TotalTime;
                _player.IsJumping = true;
            }
        }

        if (_player.IsJumping)
        {
            if (!isJumpDown)
            {
                _player.IsJumping = false;
            }
            else
            {
                var timeAirborne = _player.TotalTime - _player.LastJumpStartTime;
                if (timeAirborne > _player.JumpHoldTime)
                    _player.IsJumping = false;
            }
        }

        var collisions = HandleCollisions(_player, _player.Velocity, deltaSeconds);
        if ((collisions & CollisionDir.Down) == CollisionDir.Down)
        {
            _player.Squash = new Vector2(1.5f, 0.5f);
        }

        if ((collisions & CollisionDir.Top) == CollisionDir.Top)
        {
            _player.IsJumping = false;
        }

        _player.Position += _player.Velocity * deltaSeconds;

        Velocity.ApplyFriction(_player.Velocity);
        if (_player.Velocity.X > 0)
            _player.Flip = SpriteFlip.None;
        else if (_player.Velocity.X < 0)
            _player.Flip = SpriteFlip.FlipHorizontally;

        if (!IsGrounded(_player, _player.Velocity) && !_player.IsJumping)
            _player.Velocity.Y += Gravity * deltaSeconds;

        _player.Squash = Vector2.SmoothStep(_player.Squash, Vector2.One, deltaSeconds * 20f);
    }

    private void HandleInput(InputHandler input, bool allowKeyboard, out int movementX)
    {
        movementX = 0;
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
        }
    }

    public bool IsGrounded(Entity entity, Vector2 velocity)
    {
        var (cell, _) = GetGridCoords(entity);
        return velocity.Y == 0 && HasCollision(cell.X, cell.Y + 1);
    }

    private CollisionDir HandleCollisions(Entity entity, Velocity velocity, float deltaSeconds)
    {
        var result = CollisionDir.None;

        var deltaMove = (velocity * deltaSeconds) / GridSize;
        var (cell, cellRel) = GetGridCoords(entity);

        var resultCellPos = cellRel + deltaMove; // relative cell pos ( e.g < 0 means we moved to the previous cell ) 
        if (resultCellPos.X > 0.8f && HasCollision(cell.X + 1, cell.Y))
        {
            // Logger.LogInfo("Collide +x");
            result |= CollisionDir.Right;
            entity.Position.X = (cell.X + 0.8f - MathF.Epsilon) * GridSize;
            velocity.X = 0;
        }

        if (resultCellPos.X < 0.2f && HasCollision(cell.X - 1, cell.Y))
        {
            // Logger.LogInfo("Collide -x");
            result |= CollisionDir.Left;
            entity.Position.X = (cell.X + 0.2f + MathF.Epsilon) * GridSize;
            velocity.X = 0;
        }

        if (resultCellPos.Y > 1.0f && HasCollision(cell.X, cell.Y + 1))
        {
            // Logger.LogInfo("Collide +y");
            result |= CollisionDir.Down;
            entity.Position.Y = (cell.Y + 1.0f) * GridSize;
            velocity.Y = 0;
        }

        if (velocity.Y < 0 && resultCellPos.Y < 0.8f && HasCollision(cell.X, cell.Y - 1))
        {
            // Logger.LogInfo("Collide -y");
            result |= CollisionDir.Top;
            entity.Position.Y = (cell.Y + 0.8f) * GridSize;
            velocity.Y = 0;
        }

        return result;
    }

    public (Point, Vector2) GetGridCoords(Entity entity)
    {
        var (adjustX, adjustY) = (MathF.Approx(entity.Pivot.X, 1) ? -1 : 0, MathF.Approx(entity.Pivot.Y, 1) ? -1 : 0);
        var cell = new Point(
            (int)((entity.Position.X + adjustX) / GridSize),
            (int)((entity.Position.Y + adjustY) / GridSize)
        );
        var relativeCell = new Vector2(
            (entity.Position.X + adjustX) % GridSize / GridSize,
            (entity.Position.Y + adjustY) % GridSize / GridSize
        );
        return (cell, relativeCell);
    }

    private LayerDefinition GetLayerDefinition(long layerDefUid)
    {
        for (var i = 0; i < LdtkRaw.Defs.Layers.Length; i++)
        {
            if (LdtkRaw.Defs.Layers[i].Uid == layerDefUid)
                return LdtkRaw.Defs.Layers[i];
        }

        throw new InvalidOperationException();
    }

    private bool HasCollision(int x, int y)
    {
        var isMultiWorld = LdtkRaw.Worlds.Length > 0;
        var levels = isMultiWorld ? LdtkRaw.Worlds[0].Levels : LdtkRaw.Levels;

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
                if ((LayerDefs.Tiles)value is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
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
                var layerDef = GetLayerDefinition(layer.LayerDefUid);
                DrawLayer(renderer, level, layer, layerDef, cameraBounds);
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
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, _player.Flip);
            if (Debug)
                DrawDebug(renderer, _player, alpha);
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

            var frameIndex = (int)(entity.TotalTime * 10) % 2;
            var srcRect = new Rectangle(offset * 16 + frameIndex * 16, 16, 16, 16);
            var position = Vector2.Lerp(entity.PreviousPosition, entity.Position, (float)alpha);
            var xform = Matrix3x2.CreateTranslation(position.X - entity.Origin.X, position.Y - entity.Origin.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, entity.Flip);
            if (Debug)
                DrawDebug(renderer, entity, alpha);
        }

        if (Debug)
            _debugDraw.Render(renderer);

        if (Debug)
        {
            var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
            renderer.DrawRect(boundsMin, boundsMax, Color.Red, 1f);
        }
    }

    private void DrawLayer(Renderer renderer, Level level, LayerInstance layer, LayerDefinition layerDef, Rectangle cameraBounds)
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
                var enumValue = (LayerDefs.Tiles)value;
                if (enumValue is LayerDefs.Tiles.Ground or LayerDefs.Tiles.Left_Ground)
                {
                    var gridSize = layer.GridSize;
                    var gridY = (int)(i / layer.CWid);
                    var gridX = (int)(i % layer.CWid);
                    var min = level.Position + layer.TotalOffset + new Vector2(gridX, gridY) * gridSize;
                    var max = min + new Vector2(gridSize, gridSize);
                    var intGridValue = layerDef.IntGridValues[value - 1];
                    var color = LayerDefs.TilesColors[enumValue]; // ColorExt.FromHex(intGridValue.Color.AsSpan().Slice(1));
                    renderer.DrawRect(min, max, color, 1.0f);
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

    public void DrawDebug(Renderer renderer, Entity e, double alpha)
    {
        var prevMin = e.PreviousPosition - e.Origin;
        var min = e.Position - e.Origin;
        var lerpMin = Vector2.Lerp(prevMin, min, (float)alpha);

        renderer.DrawRect(lerpMin, lerpMin + e.Size, e.SmartColor, 1.0f);
        renderer.DrawRect(new Rectangle((int)lerpMin.X, (int)lerpMin.Y, 1, 1), e.SmartColor, 0);
        var (cell, cellRel) = GetGridCoords(e);
        var cellInScreen = cell * (int)GridSize;
        renderer.DrawRect(new Rectangle(cellInScreen.X - 1, cellInScreen.Y, 3, 1), e.SmartColor, 0);
        renderer.DrawRect(new Rectangle(cellInScreen.X, cellInScreen.Y - 1, 1, 3), e.SmartColor, 0);
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
