using MyGame.Cameras;
using MyGame.Graphics;
using MyGame.Input;
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

    private static readonly JsonSerializer _jsonSerializer = new() { Converters = { new ColorConverter() } };

    private readonly GameScreen _parent;

    private readonly DebugDrawItems _debugDraw;

    public float Gravity = 800f;

    public List<Enemy> Enemies { get; }

    public Player Player { get; }

    public long GridSize => LdtkRaw.DefaultGridSize;

    private readonly LdtkJson LdtkRaw;
    private readonly Dictionary<string, Texture> Textures = new();
    private readonly Dictionary<long, Texture> TilesetTextures;
    public Point WorldSize;

    public World(GameScreen parent, GraphicsDevice device, ReadOnlySpan<char> ldtkPath)
    {
        _parent = parent;
        var jsonString = File.ReadAllText(ldtkPath.ToString());
        LdtkRaw = LdtkJson.FromJson(jsonString);
        TilesetTextures = LoadTilesets(device, ldtkPath, LdtkRaw.Defs.Tilesets);
        WorldSize = GetWorldSize(LdtkRaw);

        _debugDraw = new DebugDrawItems();

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
            if (ent is Enemy enemy)
            {
                enemy.TimeOffset = enemy.Position.X;
                if (enemy.Type == EnemyType.Slug)
                {
                    var randomDirection = Random.Shared.Next() % 2 == 0 ? -1 : 1;
                    enemy.Velocity.Delta = new Vector2(randomDirection * 50f, 0);
                    enemy.Velocity.Friction = new Vector2(0.99f, 0.99f);
                }
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

        Player = (Player)allEntities.First(t => t.EntityType == EntityType.Player);
        _parent.CameraController.TrackEntity(Player);
        Enemies = allEntities.Where(x => x.EntityType == EntityType.Enemy).Cast<Enemy>().ToList();
    }

    private static List<Entity> LoadEntitiesInLevel(Level level)
    {
        var entities = new List<Entity>();
        foreach (var layer in level.LayerInstances)
        {
            if (layer.Type != "Entities")
            {
                continue;
            }

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
                entity.SmartColor = ColorExt.FromHex(entityInstance.SmartColor.AsSpan(1));

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
        Player.PreviousPosition = Player.Position;

        for (var i = 0; i < Enemies.Count; i++)
        {
            var entity = Enemies[i];
            entity.PreviousPosition = entity.Position;
        }
    }

    public void Update(bool isPaused, float deltaSeconds, InputHandler input)
    {
        UpdatePrevious();
        if (isPaused)
            return;

        UpdatePlayer(deltaSeconds, input);
        UpdateEnemies(deltaSeconds);
    }

    private void UpdateEnemies(float deltaSeconds)
    {
        for (var i = 0; i < Enemies.Count; i++)
        {
            var entity = Enemies[i];

            entity.TotalTime += deltaSeconds;

            if (entity.Type == EnemyType.Slug)
            {
                var (cell, cellRel) = GetGridCoords(entity);
                var turnDistanceFromEdge = 0.1f;
                if (entity.Velocity.X > 0 && !HasCollision(cell.X + 1, cell.Y + 1) && cellRel.X > (1.0f - turnDistanceFromEdge))
                {
                    entity.Velocity.X *= -1;
                }
                else if (entity.Velocity.X < 0 && !HasCollision(cell.X - 1, cell.Y + 1) && cellRel.X < turnDistanceFromEdge)
                {
                    entity.Velocity.X *= -1;
                }

                var collisions = HandleCollisions(entity, entity.Velocity, deltaSeconds);

                entity.Position += entity.Velocity * deltaSeconds;
                var slugSpeed = 50f;
                if ((collisions & CollisionDir.Left) != 0)
                {
                    entity.Velocity.Delta = new Vector2(slugSpeed, 0);
                }
                else if ((collisions & CollisionDir.Right) != 0)
                {
                    entity.Velocity.Delta = new Vector2(-slugSpeed, 0);
                }

                Velocity.ApplyFriction(entity.Velocity);

                if (entity.Velocity.X > 0)
                {
                    entity.Flip = SpriteFlip.None;
                }
                else if (entity.Velocity.X < 0)
                {
                    entity.Flip = SpriteFlip.FlipHorizontally;
                }

                if (!IsGrounded(entity, entity.Velocity))
                {
                    entity.Velocity.Y += Gravity * deltaSeconds;
                }

                if (Math.Abs(entity.Velocity.X) < slugSpeed * 0.5f)
                {
                    entity.Velocity.X += entity.Velocity.X;
                }
            }
            else if (entity.Type == EnemyType.YellowBee)
            {
                var speed = 2f;
                var radius = 25f;
                var t = entity.TimeOffset + entity.TotalTime * speed;
                var deltaMove = new Vector2(MathF.Cos(t) * 2.0f, MathF.Cos(t) * MathF.Cos(t) - MathF.Sin(t) * MathF.Sin(t)) * 2.0f * radius;
                entity.Velocity.Delta = deltaMove;
                entity.Position += entity.Velocity * deltaSeconds;

                if (entity.Velocity.X > 0)
                {
                    entity.Flip = SpriteFlip.None;
                }
                else if (entity.Velocity.X < 0)
                {
                    entity.Flip = SpriteFlip.FlipHorizontally;
                }
            }
        }
    }


    private void UpdatePlayer(float deltaSeconds, InputHandler input)
    {
        HandleInput(input, out var movementX);
        var isJumpDown = input.IsKeyDown(KeyCode.Space);
        var isJumpPressed = input.IsKeyPressed(KeyCode.Space);

        if (Player.Position.Y > 300)
        {
            Player.SetPositions(Player.InitialPosition);
        }

        Player.TotalTime += deltaSeconds;
        Player.FrameIndex = MathF.IsNearZero(Player.Velocity.X) ? 0 : (uint)(Player.TotalTime * 10) % 2;

        if (IsGrounded(Player, Player.Velocity))
        {
            Player.LastOnGroundTime = Player.TotalTime;
        }

        if (movementX != 0)
        {
            Player.Velocity.X += movementX * Player.Speed;
        }

        if (!Player.IsJumping && isJumpPressed)
        {
            var timeSinceOnGround = Player.TotalTime - Player.LastOnGroundTime;
            if (timeSinceOnGround < 0.1f)
            {
                Player.Squash = new Vector2(0.6f, 1.4f);
                Player.LastOnGroundTime = 0;
                Player.Velocity.Y = Player.JumpSpeed;
                Player.LastJumpStartTime = Player.TotalTime;
                Player.IsJumping = true;
            }
        }

        if (Player.IsJumping)
        {
            if (!isJumpDown)
            {
                Player.IsJumping = false;
            }
            else
            {
                var timeAirborne = Player.TotalTime - Player.LastJumpStartTime;
                if (timeAirborne > Player.JumpHoldTime)
                {
                    Player.IsJumping = false;
                }
            }
        }

        var collisions = HandleCollisions(Player, Player.Velocity, deltaSeconds);
        if ((collisions & CollisionDir.Down) == CollisionDir.Down)
        {
            Player.Squash = new Vector2(1.5f, 0.5f);
        }

        if ((collisions & CollisionDir.Top) == CollisionDir.Top)
        {
            Player.IsJumping = false;
        }

        Player.Position += Player.Velocity * deltaSeconds;

        Velocity.ApplyFriction(Player.Velocity);
        if (Player.Velocity.X > 0)
        {
            Player.Flip = SpriteFlip.None;
        }
        else if (Player.Velocity.X < 0)
        {
            Player.Flip = SpriteFlip.FlipHorizontally;
        }

        if (!IsGrounded(Player, Player.Velocity) && !Player.IsJumping)
        {
            Player.Velocity.Y += Gravity * deltaSeconds;
        }

        Player.Squash = Vector2.SmoothStep(Player.Squash, Vector2.One, deltaSeconds * 20f);
    }

    private void HandleInput(InputHandler input, out int movementX)
    {
        movementX = 0;
        if (input.IsKeyPressed(KeyCode.Insert))
        {
            Player.Position = new Vector2(100, 50);
        }

        if (input.IsKeyDown(KeyCode.Right) ||
            input.IsKeyDown(KeyCode.D))
        {
            movementX += 1;
        }

        if (input.IsKeyDown(KeyCode.Left) ||
            input.IsKeyDown(KeyCode.A))
        {
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
        if (velocity.Delta.LengthSquared() == 0)
            return CollisionDir.None;

        var result = CollisionDir.None;
        var deltaMove = velocity * deltaSeconds / GridSize;
        var (cell, relativeCellPos) = GetGridCoords(entity);

        var relativeCellDelta = relativeCellPos + deltaMove; // relative cell pos ( e.g < 0 means we moved to the previous cell ) 
        var cellDelta = GetCellDelta(relativeCellDelta);

        var size = new Vector2(0.4f, 0.8f);
        var halfSize = size * 0.5f;

        var maxX = (1.0f - halfSize.X);
        if (velocity.X > 0 && relativeCellDelta.X > maxX && HasCollision(cell.X + 1, cell.Y))
        {
            result |= CollisionDir.Right;
            entity.Position.X = (cell.X + maxX) * GridSize;
            velocity.X = 0;
        }

        var minX = halfSize.X;
        if (velocity.X < 0 && relativeCellDelta.X < minX && HasCollision(cell.X - 1, cell.Y))
        {
            result |= CollisionDir.Left;
            entity.Position.X = (cell.X + minX) * GridSize;
            velocity.X = 0;
        }

        if (velocity.Y > 0 && relativeCellDelta.Y > 1.0f && HasCollision(cell.X, cell.Y + 1))
        {
            result |= CollisionDir.Down;
            entity.Position.Y = (cell.Y + 1.0f) * GridSize;
            velocity.Y = 0;
        }

        if (velocity.Y < 0 && relativeCellDelta.Y < size.Y && HasCollision(cell.X, cell.Y - 1))
        {
            result |= CollisionDir.Top;
            entity.Position.Y = (cell.Y + size.Y) * GridSize;
            velocity.Y = 0;
        }

        if (HasCollision(cell.X + cellDelta.X, cell.Y + cellDelta.Y) && result == CollisionDir.None)
        {
            Logger.LogError($"Results in a collision, cellDelta: {cellDelta}, prevCell: {cell}, newCell: {cell + cellDelta}");

            if (velocity.Y > 0)
            {
                result |= CollisionDir.Down;
                entity.Position.Y = (cell.Y + 1.0f) * GridSize;
                velocity.Y = 0;
            }
            else if (velocity.Y < 0)
            {
                result |= CollisionDir.Top;
                entity.Position.Y = (cell.Y + size.Y) * GridSize;
                velocity.Y = 0;
            }

            var collisionResolved = !HasCollision(cell.X + cellDelta.X, cell.Y);

            if (collisionResolved)
            {
                Logger.LogInfo($"Collision was resolved at: {new Point(cell.X + cellDelta.X, cell.Y)}!");
            }

            if (velocity.X < 0 && !collisionResolved)
            {
                result |= CollisionDir.Left;
                entity.Position.X = (cell.X + minX) * GridSize;
                velocity.X = 0;
            }
            else if (velocity.X > 0 && !collisionResolved)
            {
                result |= CollisionDir.Right;
                entity.Position.X = (cell.X + maxX) * GridSize;
                velocity.X = 0;
            }
        }

        return result;
    }

    private static Point GetCellDelta(Vector2 relativeCellPosition)
    {
        var cellDelta = Point.Zero;

        while (relativeCellPosition.X < 0.0f)
        {
            relativeCellPosition.X++;
            cellDelta.X--;
        }

        while (relativeCellPosition.X > 1.0f)
        {
            relativeCellPosition.X--;
            cellDelta.X++;
        }

        while (relativeCellPosition.Y > 1.0f)
        {
            relativeCellPosition.Y--;
            cellDelta.Y++;
        }

        while (relativeCellPosition.Y < 0.0f)
        {
            relativeCellPosition.Y++;
            cellDelta.Y--;
        }

        return cellDelta;
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
            {
                return LdtkRaw.Defs.Layers[i];
            }
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
                {
                    continue;
                }

                if (layer.Type != "IntGrid")
                {
                    continue;
                }

                var layerDef = GetLayerDefinition(layer.LayerDefUid);
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

    public void Draw(Renderer renderer, Bounds cameraBounds, double alpha)
    {
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
        {
            renderer.DrawRect(Vector2.Zero, WorldSize, Color.Magenta, 1.0f);
        }

        // DrawDebug(renderer, _player);

        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
        {
            var srcRect = new Rectangle((int)(Player.FrameIndex * 16), 0, 16, 16);
            var position = Vector2.Lerp(Player.PreviousPosition, Player.Position, (float)alpha);
            var xform = Matrix3x2.CreateTranslation(-Player.Origin.X, -Player.Origin.Y) *
                        Matrix3x2.CreateScale(Player.EnableSquash ? Player.Squash : Vector2.One) *
                        Matrix3x2.CreateTranslation(position.X, position.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, Player.Flip);
            if (Debug)
            {
                DrawDebug(renderer, Player, alpha);
            }
        }

        for (var i = 0; i < Enemies.Count; i++)
        {
            var entity = Enemies[i];

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
            {
                DrawDebug(renderer, entity, alpha);
            }
        }

        if (Debug)
        {
            _debugDraw.Render(renderer);
        }

        if (Debug)
        {
            var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
            renderer.DrawRect(boundsMin, boundsMax, Color.Red, 1f);
        }
    }

    private void DrawLayer(Renderer renderer, Level level, LayerInstance layer, LayerDefinition layerDef, Rectangle cameraBounds)
    {
        if (!layer.TilesetDefUid.HasValue)
        {
            return;
        }

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
            if (tilePos.X + layer.GridSize < boundsMin.X || tilePos.Y + layer.GridSize < boundsMin.Y ||
                tilePos.X > boundsMax.X || tilePos.Y > boundsMax.Y)
            {
                continue;
            }

            RenderTile(renderer, tilePos, tile, layer, texture);
        }

        for (var i = 0; i < layer.AutoLayerTiles.Length; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            var tilePos = level.Position + layer.TotalOffset + tile.Position;
            if (tilePos.X + layer.GridSize < boundsMin.X || tilePos.Y + layer.GridSize < boundsMin.Y ||
                tilePos.X > boundsMax.X || tilePos.Y > boundsMax.Y)
            {
                continue;
            }

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
        renderer.DrawRect(new Rectangle((int)lerpMin.X, (int)lerpMin.Y, 1, 1), e.SmartColor);
        var (cell, cellRel) = GetGridCoords(e);
        var cellInScreen = cell * (int)GridSize;
        renderer.DrawRect(new Rectangle(cellInScreen.X - 1, cellInScreen.Y, 3, 1), e.SmartColor);
        renderer.DrawRect(new Rectangle(cellInScreen.X, cellInScreen.Y - 1, 1, 3), e.SmartColor);
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
            if (worldSize.X < max.X)
            {
                worldSize.X = max.X;
            }

            if (worldSize.Y < max.Y)
            {
                worldSize.Y = max.Y;
            }
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
            {
                continue;
            }

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
