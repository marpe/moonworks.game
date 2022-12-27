using System.Diagnostics.CodeAnalysis;
using MyGame.Cameras;
using MyGame.Debug;
using MyGame.Entities;
using MyGame.WorldsRoot;

namespace MyGame;

public class World
{
    public Color AmbientColor = new Color(80, 80, 80, 255);

    public const int DefaultGridSize = 16;
    public bool IsLoaded { get; private set; }

    [CVar("world.debug", "Toggle world debugging")]
    public static bool Debug;

    [CVar("level.debug", "Toggle level debugging")]
    public static bool DebugLevel;

    [CVar("lights.debug", "Toggle light debugging", false)]
    public static bool DebugLights;

    [CVar("entities.debug", "Toggle debugging of entities")]
    public static bool DebugEntities;

    private readonly DebugDrawItems _debugDraw;

    public float Gravity = 800f;

    public EntityList Entities { get; } = new();

    [HideInInspector]
    public ulong WorldUpdateCount;

    [HideInInspector]
    public float WorldTotalElapsedTime;

    public float FreezeFrameTimer;

    public PipelineType LightsToDestinationBlend = PipelineType.Multiply;
    public PipelineType RimLightToDestinationBlend = PipelineType.Additive;

    public RootJson Root = new();
    public Level Level = new();

    private Dictionary<int, string> _tileSetTextures = new();
    public string Filepath = "";
    private bool _isTrackingPlayer;


    public int[] CollisionLayer = Array.Empty<int>();

    public Point LevelMin;
    public Point LevelGridSize;
    public Point LevelMax;

    public World()
    {
        _debugDraw = new DebugDrawItems();
    }

    public void SetRoot(RootJson root, string filepath)
    {
        Filepath = filepath;
        Root = root;
        LoadTileSetTextures(filepath);
        IsLoaded = true;
    }

    private void LoadTileSetTextures(string filepath)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Root.TileSetDefinitions.Count; i++)
        {
            var tileSet = Root.TileSetDefinitions[i];
            var worldFileDir = Path.GetDirectoryName(filepath);
            var path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, Path.Join(worldFileDir, tileSet.Path));
            Shared.Content.Load<TextureAsset>(path);
            _tileSetTextures.Add(tileSet.Uid, path);
        }

        sw.StopAndLog("LoadTileSetTextures");
    }

    public void StartLevel(string levelIdentifier)
    {
        WorldTotalElapsedTime = WorldUpdateCount = 0;

        var level = FindLevel(levelIdentifier, Root);

        LevelMin = level.WorldPos / DefaultGridSize;
        LevelGridSize = level.Size / DefaultGridSize;
        LevelMax = LevelMin + LevelGridSize;

        CollisionLayer = Array.Empty<int>();
        foreach (var layerDef in Root.LayerDefinitions)
        {
            if (layerDef.Identifier == "Tiles" && layerDef.LayerType == LayerType.IntGrid)
            {
                var layerInstance = level.LayerInstances.First(x => x.LayerDefId == layerDef.Uid);
                CollisionLayer = layerInstance.IntGrid;
            }
        }

        foreach (var field in level.FieldInstances)
        {
            var fieldDef = Root.LevelFieldDefinitions.FirstOrDefault(x => x.Uid == field.FieldDefId);
            if (fieldDef == null)
            {
                Logs.LogError($"Could not find a field definition with id \"{field.FieldDefId}\"");
                continue;
            }

            if (fieldDef.Identifier == nameof(AmbientColor))
            {
                var fieldValue = (Color?)field.Value;
                AmbientColor = fieldValue ?? Color.White;
            }
        }

        Entities.Clear();
        Level = level;
        _isTrackingPlayer = false;
        var entities = LoadEntitiesInLevel(this, Root, level);
        Entities.AddRange(entities);
    }

    [ConsoleHandler("next_level")]
    public static void NextLevel()
    {
        if (!Shared.Game.World.IsLoaded)
        {
            Logs.LogInfo("Requires a world to be loaded");
            return;
        }

        var world = Shared.Game.World;
        var levels = world.Root.Worlds[0].Levels;
        var currIndex = levels.IndexOf(world.Level);
        var nextIndex = (currIndex + 1) % levels.Count;
        var nextLevel = levels[nextIndex];
        world.StartLevel(nextLevel.Identifier);
        Logs.LogInfo($"Set next level {nextLevel.Identifier} ({nextIndex})");
    }

    [ConsoleHandler("prev_level")]
    public static void PrevLevel()
    {
        if (!Shared.Game.World.IsLoaded)
        {
            Logs.LogInfo("Requires a world to be loaded");
            return;
        }

        var world = Shared.Game.World;
        var levels = world.Root.Worlds[0].Levels;
        var currIndex = levels.IndexOf(world.Level);
        var prevIndex = (levels.Count + (currIndex - 1)) % levels.Count;
        var prevLevel = levels[prevIndex];
        world.StartLevel(prevLevel.Identifier);
        Logs.LogInfo($"Set prev level {prevLevel.Identifier} ({prevIndex})");
    }

    private static List<Entity> LoadEntitiesInLevel(World world, RootJson root, Level level)
    {
        var entities = new List<Entity>();
        foreach (var layer in level.LayerInstances)
        {
            var layerDef = GetLayerDefinition(root, layer.LayerDefId);
            if (layerDef.LayerType != LayerType.Entities)
                continue;

            foreach (var entityInstance in layer.EntityInstances)
            {
                var entityDef = GetEntityDefinition(root, entityInstance.EntityDefId);
                var entity = CreateEntity(entityDef, entityInstance);
                entity.Position.SetPrevAndCurrent(level.WorldPos + entityInstance.Position);
                entities.Add(entity);
            }
        }

        return entities;
    }

    private static FieldDef GetFieldDef(int fieldDefUid, List<FieldDef> fieldDefs)
    {
        for (var i = 0; i < fieldDefs.Count; i++)
        {
            var fieldDef = fieldDefs[i];
            if (fieldDef.Uid == fieldDefUid)
            {
                return fieldDef;
            }
        }

        throw new Exception();
    }

    private static Entity CreateEntity(EntityDef entityDef, EntityInstance entityInstance)
    {
        var type = EntityDefinitions.TypeMap[entityDef.Identifier];
        var entity = (Entity)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());

        entity.Iid = entityInstance.Iid;
        entity.Pivot = new Vector2(entityDef.PivotX, entityDef.PivotY);
        entity.Size = entityInstance.Size;
        entity.SmartColor = entityDef.Color;

        foreach (var field in entityInstance.FieldInstances)
        {
            var fieldDef = GetFieldDef(field.FieldDefId, entityDef.FieldDefinitions);
            var fieldValue = field.Value;
            var fieldInfo = entity.GetType().GetField(fieldDef.Identifier);
            if (fieldInfo == null)
            {
                Logs.LogError($"Entity is missing field: {fieldDef.Identifier}");
                continue;
            }

            if (fieldValue == null)
            {
                Logs.LogError($"Field \"{fieldInfo.Name}\" is null");
                continue;
            }

            if (fieldValue.GetType() == fieldInfo.FieldType)
            {
                fieldInfo.SetValue(entity, fieldValue);
                continue;
            }

            if (fieldInfo.FieldType == typeof(Color) && fieldValue is string colorStr)
            {
                fieldInfo.SetValue(entity, ColorExt.FromHex(colorStr.AsSpan(1)));
            }
            else
            {
                fieldInfo.SetValue(entity, Convert.ChangeType(fieldValue, fieldInfo.FieldType));
            }
        }

        return entity;
    }

    [ConsoleHandler("kill_all")]
    public static void KillAllEnemies()
    {
        var world = Shared.Game.World;
        if (!world.IsLoaded)
        {
            Shared.Console.Print("World is not loaded");
            return;
        }

        world.Entities.ForEach((entity) =>
        {
            if (entity is Enemy enemy)
            {
                enemy.Kill();
            }
        });

        Shared.Console.Print("Killed all enemies");
    }

    private void UpdateFreezeTime(float deltaSeconds)
    {
        if (FreezeFrameTimer > 0)
            FreezeFrameTimer = MathF.Max(0, FreezeFrameTimer - deltaSeconds);
    }

    public void UpdateLastPositions()
    {
        Entities.ForEach((entity) => { entity.Position.SetLastUpdatePosition(); });
    }

    public void Update(float deltaSeconds, InputHandler input, Camera camera)
    {
        // first update stuff
        if (WorldUpdateCount == 0)
        {
            camera.LevelBounds = Level.Bounds;
        }

        if (!_isTrackingPlayer)
        {
            var player = Entities.FirstOrDefault<Player>();
            if (player != null)
            {
                camera.TrackEntity(player);
                _isTrackingPlayer = true;
            }
        }

        WorldUpdateCount++;
        WorldTotalElapsedTime += deltaSeconds;

        UpdateFreezeTime(deltaSeconds);

        if (FreezeFrameTimer > 0)
            return;

        Entities.Update(this, deltaSeconds);
    }

    public void Draw(Renderer renderer, Camera camera, double alpha, bool usePointFiltering)
    {
        DrawLevel(renderer, Level, camera.ZoomedBounds, usePointFiltering, drawBackground: true);
        DrawEntities(renderer, alpha, usePointFiltering);
    }

    private void DrawEntities(Renderer renderer, double alpha, bool usePointFiltering)
    {
        Entities.Draw(renderer, alpha, usePointFiltering);
    }

    private void DrawLevel(Renderer renderer, Level level, Bounds cameraBounds, bool usePointFiltering, bool drawBackground)
    {
        if (drawBackground)
            renderer.DrawRect(level.Bounds, level.BackgroundColor);

        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = GetLayerDefinition(Root, layer.LayerDefId);
            if (!drawBackground && layerDef.Identifier == "Background")
                continue;
            DrawLayer(renderer, Root, level, layer, layerDef, (Rectangle)cameraBounds, usePointFiltering);
        }
    }

    private void DrawLayer(Renderer renderer, RootJson root, Level level, LayerInstance layer, LayerDef layerDef, Rectangle cameraBounds,
        bool usePointFiltering)
    {
        var boundsMin = Entity.ToCell(cameraBounds.MinVec() - level.WorldPos);
        var boundsMax = Entity.ToCell(cameraBounds.MaxVec() - level.WorldPos);

        var tileSetDef = GetTileSetDef(Root, layerDef.TileSetDefId);
        var texturePath = _tileSetTextures[tileSetDef.Uid];
        var texture = Shared.Content.Load<TextureAsset>(texturePath).TextureSlice;

        for (var i = 0; i < layer.AutoLayerTiles.Count; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            if (tile.Cell.X < boundsMin.X || tile.Cell.X > boundsMax.X ||
                tile.Cell.Y < boundsMin.Y || tile.Cell.Y > boundsMax.Y)
                continue;

            var sprite = GetTileSprite(texture, tile.TileId, layerDef.GridSize);
            var transform = (
                Matrix3x2.CreateScale(1f, 1f) *
                Matrix3x2.CreateTranslation(
                    level.WorldPos.X + tile.Cell.X * layerDef.GridSize,
                    level.WorldPos.Y + tile.Cell.Y * layerDef.GridSize
                )
            ).ToMatrix4x4();
            renderer.DrawSprite(sprite, transform, Color.White, 0f, SpriteFlip.None, usePointFiltering);
        }
    }

    private static void DrawLayerDebug(Renderer renderer, Level level, LayerInstance layer, LayerDef layerDef, Rectangle cameraBounds)
    {
        var boundsMin = Entity.ToCell(cameraBounds.MinVec() - level.WorldPos);
        var boundsMax = Entity.ToCell(cameraBounds.MaxVec() - level.WorldPos);

        var cols = level.Width / layerDef.GridSize;
        var rows = level.Height / layerDef.GridSize;

        if (layerDef.LayerType == LayerType.IntGrid && Debug && DebugLevel)
        {
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
                    if (GetIntDef(layerDef, value, out var intDef))
                    {
                        color = intDef.Color;
                    }

                    renderer.DrawRect(min, max, color * 0.5f, 0);
                }
            }
        }
    }

    public static Sprite GetTileSprite(TextureSlice texture, uint tileId, uint gridSize)
    {
        Sprite sprite;
        var tileSize = new Point(
            (int)(texture.Rectangle.W / gridSize),
            (int)(texture.Rectangle.H / gridSize)
        );
        var cellX = tileSize.X > 0 ? tileId % tileSize.X : 0;
        var cellY = tileSize.X > 0 ? (int)(tileId / tileSize.X) : 0;
        sprite = new Sprite(texture, new Rectangle((int)(cellX * gridSize), (int)(cellY * gridSize), (int)gridSize, (int)gridSize));
        return sprite;
    }

    private static bool GetIntDef(LayerDef layerDef, int value, [NotNullWhen(true)] out IntGridValue? intValue)
    {
        for (var i = 0; i < layerDef.IntGridValues.Count; i++)
        {
            if (layerDef.IntGridValues[i].Value == value)
            {
                intValue = layerDef.IntGridValues[i];
                return true;
            }
        }

        intValue = null;
        return false;
    }

    private static TileSetDef GetTileSetDef(RootJson root, long tileSetUid)
    {
        for (var i = 0; i < root.TileSetDefinitions.Count; i++)
        {
            if (root.TileSetDefinitions[i].Uid == tileSetUid)
            {
                return root.TileSetDefinitions[i];
            }
        }

        throw new Exception($"Could not find a TileSetDefinition with id \"{tileSetUid}\"");
    }

    public void Unload()
    {
        if (!IsLoaded)
            return;

        CollisionLayer = Array.Empty<int>();
        _tileSetTextures.Clear();
        Entities.Clear();
        IsLoaded = false;
        _isTrackingPlayer = false;
    }

    public T CreateEntity<T>() where T : Entity
    {
        var def = GetEntityDefinition(Root, typeof(T).Name);
        var instance = EntityDef.CreateEntityInstance(def);
        var entity = CreateEntity(def, instance);
        return (T)entity;
    }

    public void SpawnBullet(Vector2 position, int direction)
    {
        var bullet = CreateEntity<Bullet>();
        bullet.Position.SetPrevAndCurrent(position + new Vector2(4 * direction, 0));
        bullet.Velocity.X = direction * 300f;

        if (Entity.HasCollision(bullet.Position.Current, bullet.Size, this))
        {
            Logs.LogInfo("Can't spawn bullet");
            return;
        }

        Entities.Add(bullet);
    }

    public void SpawnMuzzleFlash(Vector2 position, int direction)
    {
        var muzzleFlash = CreateEntity<MuzzleFlash>();
        muzzleFlash.Position.SetPrevAndCurrent(position + new Vector2(1, 0) + new Vector2(14 * direction, 3));
        muzzleFlash.Draw.Flip = direction < 0 ? SpriteFlip.FlipHorizontally : SpriteFlip.None;
        Entities.Add(muzzleFlash);
    }

    private static EntityDef GetEntityDefinition(RootJson root, int entityDefId)
    {
        for (var i = 0; i < EntityDefinitions.Count; i++)
        {
            if (EntityDefinitions.ByIndex(i).Uid == entityDefId)
                return EntityDefinitions.ByIndex(i);
        }

        throw new InvalidOperationException();
    }

    private static EntityDef GetEntityDefinition(RootJson root, string identifier)
    {
        for (var i = 0; i < EntityDefinitions.Count; i++)
        {
            if (EntityDefinitions.ByIndex(i).Identifier == identifier)
                return EntityDefinitions.ByIndex(i);
        }

        throw new InvalidOperationException();
    }

    private static LayerDef GetLayerDefinition(RootJson root, long layerDefUid)
    {
        for (var j = 0; j < root.LayerDefinitions.Count; j++)
        {
            if (root.LayerDefinitions[j].Uid == layerDefUid)
            {
                return root.LayerDefinitions[j];
            }
        }

        throw new InvalidOperationException();
    }

    public void FreezeFrame(float duration, bool force = false)
    {
        FreezeFrameTimer = force ? duration : MathF.Max(duration, FreezeFrameTimer);
    }

    private void DrawLightBaseLayer(Renderer renderer, ref CommandBuffer commandBuffer, RenderTarget lightSource, Camera camera, double alpha,
        bool usePointFiltering)
    {
        DrawLevel(renderer, Level, camera.ZoomedBounds, usePointFiltering, drawBackground: false);
        DrawEntities(renderer, alpha, usePointFiltering);

        var viewProjection = camera.GetViewProjection(lightSource.Width, lightSource.Height);
        renderer.RunRenderPass(ref commandBuffer, lightSource, Color.Transparent, viewProjection, PipelineType.PixelArt);
    }

    public void DrawLights(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, Camera camera,
        RenderTarget lightSource, RenderTarget lightTarget, double alpha, bool usePointFiltering)
    {
        DrawLightBaseLayer(renderer, ref commandBuffer, lightSource, camera, alpha, usePointFiltering);

        // render lights
        renderer.DrawSprite(lightSource.Target, Matrix4x4.Identity, Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, lightTarget, AmbientColor, PipelineType.Light);
        DrawAllLights(
            ref commandBuffer,
            renderDestination.Size(),
            camera.ZoomedBounds,
            new[]
            {
                new TextureSamplerBinding(lightSource.Target, Renderer.PointClamp),
            }
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render light to game
        renderer.DrawSprite(lightTarget.Target, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, LightsToDestinationBlend);

        // render rim
        renderer.DrawSprite(renderer.BlankSprite, Matrix3x2.CreateScale(renderDestination.Width, renderDestination.Height).ToMatrix4x4(), Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, lightTarget, Color.Transparent, PipelineType.RimLight);
        DrawAllLights(
            ref commandBuffer,
            renderDestination.Size(),
            camera.ZoomedBounds,
            new[]
            {
                new TextureSamplerBinding(renderer.BlankSprite.TextureSlice.Texture, Renderer.PointClamp),
                new TextureSamplerBinding(lightSource.Target, Renderer.PointClamp),
            }
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render rim light to game
        renderer.DrawSprite(lightTarget.Target, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, RimLightToDestinationBlend);
    }

    private void DrawAllLights(ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds, TextureSamplerBinding[] fragmentBindings)
    {
        var tempCameraBounds = cameraBounds;
        var tempCommandBuffer = commandBuffer;
        Entities.ForEach((entity) =>
        {
            if (entity is not Light light)
                return;
            if (!light.IsEnabled)
                return;
            if (!light.Bounds.Intersects(tempCameraBounds))
                return;
            var vertUniform = Renderer.GetViewProjection(renderDestinationSize.X, renderDestinationSize.Y);
            var fragUniform = new Pipelines.RimLightUniforms()
            {
                LightColor = new Vector3(light.Color.R / 255f, light.Color.G / 255f, light.Color.B / 255f),
                LightIntensity = light.Intensity,
                LightRadius = Math.Max(light.Width, light.Height) / MathF.Sqrt(2),
                LightPos = light.Position + light.Size.ToVec2() * light.Pivot,
                VolumetricIntensity = light.VolumetricIntensity,
                RimIntensity = light.RimIntensity,
                Angle = light.Angle,
                ConeAngle = light.ConeAngle,

                TexelSize = new Vector4(
                    1.0f / renderDestinationSize.X,
                    1.0f / renderDestinationSize.Y,
                    renderDestinationSize.X,
                    renderDestinationSize.Y
                ),
                Bounds = new Vector4(
                    tempCameraBounds.Min.X,
                    tempCameraBounds.Min.Y,
                    tempCameraBounds.Width,
                    tempCameraBounds.Height
                ),
            };
            var fragmentParamOffset = tempCommandBuffer.PushFragmentShaderUniforms(fragUniform);
            var vertexParamOffset = tempCommandBuffer.PushVertexShaderUniforms(vertUniform);
            tempCommandBuffer.BindFragmentSamplers(fragmentBindings);
            SpriteBatch.DrawIndexedQuads(ref tempCommandBuffer, 0, 1, vertexParamOffset, fragmentParamOffset);
        });
    }

    private static Level FindLevel(string identifier, RootJson root)
    {
        for (var i = 0; i < root.Worlds.Count; i++)
        {
            var world = root.Worlds[i];
            for (var j = 0; j < world.Levels.Count; j++)
            {
                var level = world.Levels[j];
                if (level.Identifier == identifier)
                {
                    return level;
                }
            }
        }

        Logs.LogError($"Level not found: {identifier}");

        return root.Worlds.FirstOrDefault()?.Levels.FirstOrDefault() ?? throw new InvalidOperationException();
    }

    public void DrawDebug(Renderer renderer, Camera camera, double alpha)
    {
        if (!Debug)
            return;

        if (DebugLevel)
            DrawLevelDebug(renderer, Level, camera.ZoomedBounds);
        if (DebugEntities)
            DrawEntityDebug(renderer, alpha);
        CameraDebug.DrawCameraBounds(renderer, camera);
        MouseDebug.DrawMousePosition(renderer);
        _debugDraw.Render(renderer);
    }

    private void DrawLevelDebug(Renderer renderer, Level level, Bounds cameraBounds)
    {
        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = GetLayerDefinition(Root, layer.LayerDefId);
            DrawLayerDebug(renderer, level, layer, layerDef, (Rectangle)cameraBounds);
        }

        renderer.DrawRectOutline(level.WorldPos, level.WorldPos + level.Size.ToVec2(), Color.Blue, 1.0f);
    }
    
    private void DrawEntityDebug(Renderer renderer, double alpha)
    {
        Entities.ForEach((entity) => { entity.DrawDebug(renderer, false, alpha); });
    }
}
