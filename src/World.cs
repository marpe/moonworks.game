namespace MyGame;

public class World
{
    public const int DefaultGridSize = 16;

    [CVar("world.debug", "Toggle world debugging")]
    public static bool Debug;

    private static readonly JsonSerializer _jsonSerializer = new() { Converters = { new ColorConverter() } };

    private readonly GameScreen _parent;

    private readonly DebugDrawItems _debugDraw;

    public float Gravity = 800f;

    public List<Enemy> Enemies { get; }

    public Player Player { get; }

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

            if (!entity.IsInitialized)
                entity.Initialize(this);

            entity.Update(deltaSeconds);
        }
    }


    private void UpdatePlayer(float deltaSeconds, InputHandler input)
    {
        if (!Player.IsInitialized)
            Player.Initialize(this);

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
        var (cell, _) = Entity.GetGridCoords(entity);
        return velocity.Y == 0 && HasCollision(cell.X, cell.Y + 1);
    }

    public CollisionDir HandleCollisions(Entity entity, Velocity velocity, float deltaSeconds)
    {
        if (velocity.Delta.LengthSquared() == 0)
            return CollisionDir.None;

        var result = CollisionDir.None;
        var size = new Vector2(0.4f, 0.8f);
        var halfSize = size * 0.5f;

        if (velocity.X != 0)
        {
            var deltaMove = velocity * deltaSeconds / DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(entity);
            var dx = cellPos.X + deltaMove.X; // relative cell pos ( e.g < 0 means we moved to the previous cell )
            
            var maxX = (1.0f - halfSize.X);
            var minX = halfSize.X;
            
            if (velocity.X > 0 && dx > maxX && HasCollision(cell.X + 1, cell.Y))
            {
                result |= CollisionDir.Right;
                entity.Position.X = (cell.X + maxX) * DefaultGridSize;
                velocity.X = 0;
            }
            else if (velocity.X < 0 && dx < minX && HasCollision(cell.X - 1, cell.Y))
            {
                result |= CollisionDir.Left;
                entity.Position.X = (cell.X + minX) * DefaultGridSize;
                velocity.X = 0;
            }
            else
            {
                entity.Position.X += velocity.X * deltaSeconds;
            }
            
            (entity.Cell, entity.CellPos) = Entity.GetGridCoords(entity);
        }

        if (velocity.Y != 0)
        {
            var deltaMove = velocity * deltaSeconds / DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(entity);
            var dy = cellPos.Y + deltaMove.Y; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            var maxY = 1.0f;
            var minY = size.Y;
        
            if (velocity.Y > 0 && dy > maxY && HasCollision(cell.X, cell.Y + 1))
            {
                result |= CollisionDir.Down;
                entity.Position.Y = (cell.Y + maxY) * DefaultGridSize;
                velocity.Y = 0;
            }
            else if (velocity.Y < 0 && dy < minY && HasCollision(cell.X, cell.Y - 1))
            {
                result |= CollisionDir.Top;
                entity.Position.Y = (cell.Y + minY) * DefaultGridSize;
                velocity.Y = 0;
            }
            else
            {
                entity.Position.Y += velocity.Y * deltaSeconds;
            }
            
            (entity.Cell, entity.CellPos) = Entity.GetGridCoords(entity);
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

    public bool HasCollision(int x, int y)
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
        var (cell, cellRel) = Entity.GetGridCoords(e);
        var cellInScreen = cell * DefaultGridSize;
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
