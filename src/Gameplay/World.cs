﻿namespace MyGame;

public class World
{
    public const int DefaultGridSize = 16;

    public bool IsDisposed { get; private set; }

    [CVar("world.debug", "Toggle world debugging")]
    public static bool Debug;

    private static readonly JsonSerializer _jsonSerializer = new() { Converters = { new ColorConverter() } };

    private readonly GameScreen _parent;

    private readonly DebugDrawItems _debugDraw;

    public float Gravity = 800f;

    public readonly LdtkJson LdtkRaw;
    private readonly Dictionary<string, Texture> Textures = new();
    private readonly Dictionary<long, Texture> TilesetTextures;
    public Point WorldSize;

    public Player Player { get; }
    public List<Enemy> Enemies { get; } = new();
    public List<Bullet> Bullets { get; } = new();

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
        Enemies.AddRange(allEntities.Where(x => x.EntityType == EntityType.Enemy).Cast<Enemy>());
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
                entity.Position = new Position(level.Position + entityInstance.Position);
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

    public void Update(float deltaSeconds, InputHandler input)
    {
        UpdatePlayer(deltaSeconds, input);
        UpdateEnemies(deltaSeconds);
        UpdateBullets(deltaSeconds);
    }

    private void UpdateBullets(float deltaSeconds)
    {
        for (var i = Bullets.Count - 1; i >= 0; i--)
        {
            if (!Bullets[i].IsInitialized)
                Bullets[i].Initialize(this);

            Bullets[i].Update(deltaSeconds);
            if (Bullets[i].IsDestroyed)
                Bullets.RemoveAt(i);
        }
    }

    private void UpdatePlayer(float deltaSeconds, InputHandler input)
    {
        if (!Player.IsInitialized)
            Player.Initialize(this);
        Player.Update(deltaSeconds, input);
    }

    private void UpdateEnemies(float deltaSeconds)
    {
        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var entity = Enemies[i];

            if (entity.IsDestroyed)
            {
                Enemies.RemoveAt(i);
                continue;
            }

            if (!entity.IsInitialized)
                entity.Initialize(this);

            entity.Update(deltaSeconds);
        }
    }

    public void Draw(Renderer renderer, Bounds cameraBounds, double alpha)
    {
        DrawLevel(renderer, cameraBounds);
        DrawPlayer(renderer, alpha);
        DrawEnemies(renderer, alpha);
        DrawBullets(renderer, alpha);
        DrawCameraBounds(renderer, cameraBounds);

        if (Debug)
            _debugDraw.Render(renderer);
    }

    private static void DrawCameraBounds(Renderer renderer, Bounds cameraBounds)
    {
        if (!Debug)
            return;

        var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
        renderer.DrawRect(boundsMin, boundsMax, Color.Red, 1f);
    }

    private void DrawLevel(Renderer renderer, Bounds cameraBounds)
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
                var layerDef = GetLayerDefinition(LdtkRaw, layer.LayerDefUid);
                DrawLayer(renderer, level, layer, layerDef, cameraBounds);
            }

            if (Debug)
                renderer.DrawRect(level.Position, level.Position + level.Size, Color.Red, 1.0f);
        }

        if (Debug)
            renderer.DrawRect(Vector2.Zero, WorldSize, Color.Magenta, 1.0f);
    }

    private void DrawBullets(Renderer renderer, double alpha)
    {
        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
        for (var i = 0; i < Bullets.Count; i++)
        {
            var bullet = Bullets[i];
            var srcRect = new Rectangle(4 * 16, 0, 16, 16);
            var position = bullet.Position.Lerp(alpha);
            var xform = Matrix3x2.CreateTranslation(position.X - bullet.Origin.X, position.Y - bullet.Origin.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform.ToMatrix4x4(), Color.White, 0, bullet.Flip);
            if (Debug)
                DrawDebug(renderer, bullet, alpha);
        }
    }

    private void DrawPlayer(Renderer renderer, double alpha)
    {
        var srcRect = new Rectangle((int)(Player.FrameIndex * 16), 0, 16, 16);
        var position = Player.Position.Lerp(alpha);
        var xform = Matrix3x2.CreateTranslation(-Player.Origin.X, -Player.Origin.Y) *
                    Matrix3x2.CreateScale(Player.EnableSquash ? Player.Squash : Vector2.One) *
                    Matrix3x2.CreateTranslation(position.X, position.Y);
        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
        renderer.DrawSprite(new Sprite(texture, srcRect), xform.ToMatrix4x4(), Color.White, 0, Player.Flip);
        if (Debug)
            DrawDebug(renderer, Player, alpha);
    }

    private void DrawEnemies(Renderer renderer, double alpha)
    {
        var texture = Textures[ContentPaths.ldtk.Example.Characters_png];
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
            var position = entity.Position.Lerp(alpha);
            var xform = Matrix3x2.CreateTranslation(position.X - entity.Origin.X, position.Y - entity.Origin.Y);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform.ToMatrix4x4(), Color.White, 0, entity.Flip);
            if (Debug)
                DrawDebug(renderer, entity, alpha);
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
        renderer.DrawSprite(sprite, transform.ToMatrix4x4(), Color.White, 0);
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        foreach (var (key, texture) in TilesetTextures)
        {
            texture.Dispose();
        }

        TilesetTextures.Clear();

        IsDisposed = true;
    }

    public void DrawDebug(Renderer renderer, Entity e, double alpha)
    {
        var (cell, cellRel) = Entity.GetGridCoords(e);
        var cellInScreen = cell * DefaultGridSize;
        renderer.DrawPoint(e.Position.Current, e.SmartColor, 2);

        // draw small crosshair
        {
            renderer.DrawRect(new Rectangle(cellInScreen.X - 1, cellInScreen.Y, 3, 1), e.SmartColor);
            renderer.DrawRect(new Rectangle(cellInScreen.X, cellInScreen.Y - 1, 1, 3), e.SmartColor);
        }

        renderer.DrawRect(e.Bounds.Min, e.Bounds.Max, Color.LimeGreen, 1.0f);
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

    public void SpawnBullet(Vector2 position, int direction)
    {
        var bullet = new Bullet();
        bullet.Position.SetPrevAndCurrent(position + new Vector2(4 * direction, -8));
        bullet.Velocity.X = direction * 300f;
        bullet.Pivot = new Vector2(0.5f, 0.5f);
        bullet.Size = new Vector2(16, 16);
        Bullets.Add(bullet);
    }

    public static LayerDefinition GetLayerDefinition(LdtkJson ldtkRaw, long layerDefUid)
    {
        for (var i = 0; i < ldtkRaw.Defs.Layers.Length; i++)
        {
            if (ldtkRaw.Defs.Layers[i].Uid == layerDefUid)
                return ldtkRaw.Defs.Layers[i];
        }

        throw new InvalidOperationException();
    }
}