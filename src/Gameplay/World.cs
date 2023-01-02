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

    [CVar("entities.debug", "Toggle debugging of entities")]
    public static bool DebugEntities;

    [CVar("cam.debug", "Toggle camera debugging")]
    public static bool DebugCamera;
    
    [CVar("mouse.debug", "Toggle mouse debugging", false)]
    public static bool DebugMouse;
    
    [CVar("lights_enabled", "Toggle lights")]
    public static bool LightsEnabled = true;
    
    [CVar("rim_lights_enabled", "Toggle lights")]
    public static bool RimLightsEnabled = true;
    
    private readonly DebugDrawItems _debugDraw;

    public float Gravity = 800f;

    public EntityList Entities { get; } = new();

    [HideInInspector]
    public ulong WorldUpdateCount;

    [HideInInspector]
    public float WorldTotalElapsedTime;

    public float FreezeFrameTimer;

    public RootJson Root = new();
    public Level Level = new();

    private Dictionary<int, string> _tileSetTextures = new();
    public string Filepath = "";
    private bool _isTrackingPlayer;

    public int[] CollisionLayer = Array.Empty<int>();

    public Point LevelMin;
    public Point LevelGridSize;
    public Point LevelMax;
    private Dictionary<(int layerDefUId, int tileValue), IntGridValue> _intGridValueCache = new();
    private Dictionary<int, LayerDef> _layerDefCache = new();
    private Dictionary<int, TileSetDef> _tileSetDefCache = new();

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
        
        _tileSetTextures.Clear();
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

        _intGridValueCache.Clear();
        _layerDefCache.Clear();
        _tileSetDefCache.Clear();
        
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
        var entities = LoadEntitiesInLevel(this, level);
        Entities.AddRange(entities);
    }

    [ConsoleHandler("restart_level")]
    public static void RestartLevel()
    {
        if (!Shared.Game.World.IsLoaded)
        {
            Logs.LogInfo("Requires a world to be loaded");
            return;
        }

        var world = Shared.Game.World;
        world.StartLevel(world.Level.Identifier);
        Logs.LogInfo($"Restarted level {world.Level.Identifier}");
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

    private static List<Entity> LoadEntitiesInLevel(World world, Level level)
    {
        var entities = new List<Entity>();
        foreach (var layer in level.LayerInstances)
        {
            var layerDef = world.GetLayerDefinition(layer.LayerDefId);
            if (layerDef.LayerType != LayerType.Entities)
                continue;

            foreach (var entityInstance in layer.EntityInstances)
            {
                var entityDef = EntityDefinitions.ById[entityInstance.EntityDefId];
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
        Entities.ForEach((entity) =>
        {
            entity.Position.SetLastUpdatePosition();
            entity.Draw.SetLastUpdateTransform();
        });
    }

    public void Update(float deltaSeconds, Camera camera)
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
    
    public void DrawEntities(Renderer renderer, double alpha)
    {
        Entities.Draw(renderer, alpha);
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

    public static T CreateEntity<T>() where T : Entity
    {
        var def = EntityDefinitions.ByName[typeof(T).Name];
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

    public LayerDef GetLayerDefinition(int layerDefUid)
    {
        if (_layerDefCache.TryGetValue(layerDefUid, out var layerDef))
            return layerDef;
        
        for (var j = 0; j < Root.LayerDefinitions.Count; j++)
        {
            layerDef = Root.LayerDefinitions[j];
            if (layerDef.Uid == layerDefUid)
            {
                _layerDefCache.Add(layerDefUid, layerDef);
                return layerDef;
            }
        }

        throw new InvalidOperationException();
    }
    
    public TileSetDef GetTileSetDef(int tileSetUid)
    {
        if (_tileSetDefCache.TryGetValue(tileSetUid, out var tileSetDef))
            return tileSetDef;
        
        for (var i = 0; i < Root.TileSetDefinitions.Count; i++)
        {
            tileSetDef = Root.TileSetDefinitions[i];
            if (tileSetDef.Uid == tileSetUid)
            {
                _tileSetDefCache.Add(tileSetUid, tileSetDef);
                return tileSetDef;
            }
        }

        throw new Exception($"Could not find a TileSetDefinition with id \"{tileSetUid}\"");
    }

    public bool GetIntDef(LayerDef layerDef, int tileValue, [NotNullWhen(true)] out IntGridValue? intGridValue)
    {
        if (_intGridValueCache.TryGetValue((layerDef.Uid, tileValue), out intGridValue))
            return true;
        
        for (var i = 0; i < layerDef.IntGridValues.Count; i++)
        {
            if (layerDef.IntGridValues[i].Value != tileValue)
                continue;

            intGridValue = layerDef.IntGridValues[i];
            _intGridValueCache.Add((layerDef.Uid, tileValue), intGridValue);
            return true;
        }

        intGridValue = null;
        return false;
    }

    public void FreezeFrame(float duration, bool force = false)
    {
        FreezeFrameTimer = force ? duration : MathF.Max(duration, FreezeFrameTimer);
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
           LevelRenderer.DrawLevelDebug(renderer, this, Root, Level, camera.ZoomedBounds);
        if (DebugEntities)
            DrawEntityDebug(renderer, alpha);
        if (DebugCamera)
            CameraDebug.DrawCameraBounds(renderer, camera);
        if (DebugMouse)
            MouseDebug.DrawMousePosition(renderer);
        _debugDraw.Render(renderer);
    }
    
    private void DrawEntityDebug(Renderer renderer, double alpha)
    {
        Entities.ForEach((entity) => { entity.DrawDebug(renderer, false, alpha); });
    }

    public string GetTileSetTexture(int uid)
    {
        return _tileSetTextures[uid];
    }

    public static Vector2 GetWorldPosInScreen(Vector2 position)
    {
        var view = Shared.Game.Camera.GetView(0);
        return Vector2.Transform(position, view) * Shared.Game.RenderTargets.RenderScale;
    }

    public static Vector2 GetScreenPosInWorld(Vector2 position)
    {
        var view = Shared.Game.Camera.GetView(0);
        Matrix4x4.Invert(ref view, out var invertedView);
        return Vector2.Transform(position / Shared.Game.RenderTargets.RenderScale, invertedView);
    }
    
    public static Vector2 GetMouseInWorld()
    {
        return GetScreenPosInWorld(Shared.Game.InputHandler.MousePosition);
    }
}
