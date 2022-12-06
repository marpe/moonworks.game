using System.Diagnostics.CodeAnalysis;
using MyGame.Cameras;

namespace MyGame;

public static class MouseDebug
{
    public static Vector2 MousePivot = new Vector2(0f, 0f);
    public static Point MouseSize = new Point(8, 12);

    [CVar("mouse.debug", "Toggle mouse debugging", false)]
    public static bool DebugMouse;

    public static void DrawMousePosition(Renderer renderer)
    {
        if (!DebugMouse)
            return;

        var mousePosition = Shared.Game.InputHandler.MousePosition;
        var view = Shared.Game.Camera.GetView();
        Matrix3x2.Invert(view, out var invertedView);
        var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
        var mouseCell = Entity.ToCell(mouseInWorld);

        var mouseCellRect = new Rectangle(
            mouseCell.X * World.DefaultGridSize,
            mouseCell.Y * World.DefaultGridSize,
            World.DefaultGridSize,
            World.DefaultGridSize
        );
        renderer.DrawRectOutline(mouseCellRect, Color.Red * 0.5f, 1f);

        var mousePosRect = new Bounds(
            mouseInWorld.X,
            mouseInWorld.Y,
            MouseSize.X,
            MouseSize.Y
        );
        renderer.DrawRectOutline(mousePosRect, Color.Blue * 0.5f);

        var mouseRenderRect = new Bounds(
            mouseInWorld.X,
            mouseInWorld.Y,
            MouseSize.X,
            MouseSize.Y
        );
        renderer.DrawRectOutline(mouseRenderRect, Color.Magenta * 0.5f);

        // renderer.DrawPoint(mouseInWorld, Color.Red, 2f);
    }
}

public static class CameraDebug
{
    [CVar("cam.debug", "Toggle camera debugging")]
    public static bool DebugCamera;

    public static void DrawCameraBounds(Renderer renderer, Camera camera)
    {
        if (!DebugCamera)
            return;

        var cameraBounds = camera.ZoomedBounds;
        var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
        renderer.DrawRectOutline(boundsMin, boundsMax, Color.Red, 1f);

        var offset = camera.TargetPosition - camera.Position;
        var dz = camera.DeadZone / 2;
        var lengthX = MathF.Abs(offset.X) - dz.X;
        var lengthY = MathF.Abs(offset.Y) - dz.Y;
        var isDeadZoneActive = lengthX > 0 || lengthY > 0;
        if (isDeadZoneActive)
        {
            var pointOnDeadZone = new Vector2(
                MathF.Clamp(camera.TargetPosition.X, camera.Position.X - dz.X, camera.Position.X + dz.X),
                MathF.Clamp(camera.TargetPosition.Y, camera.Position.Y - dz.Y, camera.Position.Y + dz.Y)
            );
            renderer.DrawLine(pointOnDeadZone, camera.TargetPosition, Color.Red);
        }

        renderer.DrawRectOutline(camera.Position - dz, camera.Position + dz, Color.Magenta * (isDeadZoneActive ? 1.0f : 0.33f));
        renderer.DrawPoint(camera.TargetPosition, Color.Cyan, 4f);

        var posInLevel = camera.Position - camera.LevelBounds.MinVec();
        var cameraMin = posInLevel - camera.ZoomedSize * 0.5f;
        var cameraMax = posInLevel + camera.ZoomedSize * 0.5f;

        if (camera.Velocity.X < 0 && cameraMin.X < camera.BrakeZone.X)
        {
            renderer.DrawLine(camera.Position, camera.Position - new Vector2(camera.BrakeZone.X - cameraMin.X, 0), Color.Red);
        }
        else if (camera.Velocity.X > 0 && cameraMax.X > camera.LevelBounds.Width - camera.BrakeZone.X)
        {
            renderer.DrawLine(camera.Position, camera.Position + new Vector2(cameraMax.X - (camera.LevelBounds.Width - camera.BrakeZone.X), 0), Color.Red);
        }

        if (camera.Velocity.Y < 0 && cameraMin.Y < camera.BrakeZone.Y)
        {
            renderer.DrawLine(camera.Position, camera.Position - new Vector2(0, camera.BrakeZone.Y - cameraMin.Y), Color.Red);
        }
        else if (camera.Velocity.Y > 0 && cameraMax.Y > camera.LevelBounds.Height - camera.BrakeZone.Y)
        {
            renderer.DrawLine(camera.Position, camera.Position + new Vector2(0, cameraMax.Y - (camera.LevelBounds.Height - camera.BrakeZone.Y)), Color.Red);
        }

        renderer.DrawRectOutline(camera.LevelBounds.MinVec() + camera.BrakeZone, camera.LevelBounds.MaxVec() - camera.BrakeZone, Color.Green);
    }
}

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

    public Player Player = new();
    public List<Enemy> Enemies { get; } = new();
    public List<Bullet> Bullets { get; } = new();

    public List<Light> Lights { get; } = new();

    [HideInInspector] public ulong WorldUpdateCount;

    [HideInInspector] public float WorldTotalElapsedTime;

    private static Vector2 _savedPos;

    public float FreezeFrameTimer;

    public Level Level = new();
    public LDtkAsset LDtk = new();
    public PipelineType LightsToDestinationBlend = PipelineType.Multiply;
    public PipelineType RimLightToDestinationBlend = PipelineType.Additive;
    public float RimLightIntensity = 3f;

    public World()
    {
        _debugDraw = new DebugDrawItems();
        // StartLevel("World_Level_1");
    }

    public static Level FindLevel(string identifier, LdtkJson ldtk)
    {
        var levels = ldtk.Worlds.Length > 0 ? ldtk.Worlds[0].Levels : ldtk.Levels;
        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i].Identifier == identifier)
                return levels[i];
        }

        throw new InvalidOperationException($"Level not found: {identifier}");
    }

    public void SetLDtk(LDtkAsset ldtk)
    {
        LDtk = ldtk;
        IsLoaded = true;
    }

    [MemberNotNull(nameof(Level), nameof(Player))]
    public void StartLevel(string levelIdentifier)
    {
        WorldTotalElapsedTime = WorldUpdateCount = 0;

        var level = FindLevel(levelIdentifier, LDtk.LdtkRaw);

        Enemies.Clear();
        Bullets.Clear();
        Lights.Clear();

        Level = level;

        var entities = LoadEntitiesInLevel(level);
        Player = (Player)entities.First(t => t.EntityType == EntityType.Player);
        Enemies.AddRange(entities.Where(x => x.EntityType == EntityType.Enemy).Cast<Enemy>());
        Lights.AddRange(entities.Where(x => x.EntityType == EntityType.Light).Cast<Light>());
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
        var levels = world.LDtk.LdtkRaw.Worlds.Length > 0 ? world.LDtk.LdtkRaw.Worlds[0].Levels : world.LDtk.LdtkRaw.Levels;
        var currIndex = Array.IndexOf(levels, world.Level);
        var nextIndex = (currIndex + 1) % levels.Length;
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
        var levels = world.LDtk.LdtkRaw.Worlds.Length > 0 ? world.LDtk.LdtkRaw.Worlds[0].Levels : world.LDtk.LdtkRaw.Levels;
        var currIndex = Array.IndexOf(levels, world.Level);
        var prevIndex = (levels.Length + (currIndex - 1)) % levels.Length;
        var prevLevel = levels[prevIndex];
        world.StartLevel(prevLevel.Identifier);
        Logs.LogInfo($"Set prev level {prevLevel.Identifier} ({prevIndex})");
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
                entity.Size = entityInstance.Size;
                entity.Position = new Position(level.Position + entityInstance.Position - entity.Pivot * entity.Size);
                entity.SmartColor = ColorExt.FromHex(entityInstance.SmartColor.AsSpan(1));

                foreach (var field in entityInstance.FieldInstances)
                {
                    var fieldValue = (JToken)field.Value;
                    var fieldInfo = entity.GetType().GetField(field.Identifier) ?? throw new InvalidOperationException();
                    var deserializedValue = fieldValue?.ToObject(fieldInfo.FieldType, ContentManager.JsonSerializer);
                    fieldInfo.SetValue(entity, deserializedValue);
                }

                entities.Add(entity);
            }
        }

        return entities;
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

        for (var i = world.Enemies.Count - 1; i >= 0; i--)
        {
            world.Enemies.RemoveAt(i);
        }

        Shared.Console.Print("Killed all enemies");
    }

    private void UpdateFreezeTime(float deltaSeconds)
    {
        if (FreezeFrameTimer > 0)
            FreezeFrameTimer = MathF.Max(0, FreezeFrameTimer - deltaSeconds);
    }

    public void UpdateLastPositions()
    {
        Player.Position.SetLastUpdatePosition();
        for (var i = 0; i < Enemies.Count; i++)
            Enemies[i].Position.SetLastUpdatePosition();
        for (var i = 0; i < Bullets.Count; i++)
            Bullets[i].Position.SetLastUpdatePosition();
    }

    public void Update(float deltaSeconds, InputHandler input, Camera camera)
    {
        // first update stuff
        if (WorldUpdateCount == 0)
        {
            camera.LevelBounds = Level.Bounds;
        }

        WorldUpdateCount++;
        WorldTotalElapsedTime += deltaSeconds;

        UpdateFreezeTime(deltaSeconds);

        if (FreezeFrameTimer > 0)
            return;

        UpdatePlayer(deltaSeconds, input, camera);
        UpdateEnemies(deltaSeconds);
        UpdateBullets(deltaSeconds);
        camera.Update(deltaSeconds, input);
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

    private void UpdatePlayer(float deltaSeconds, InputHandler input, Camera camera)
    {
        if (!Player.IsInitialized)
        {
            camera.TrackEntity(Player);
            Player.Initialize(this);
        }

        var command = Binds.Player.ToPlayerCommand();
        Player.Update(deltaSeconds, command);
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

    public void Draw(Renderer renderer, Camera camera, double alpha)
    {
        DrawLevel(renderer, Level, camera.ZoomedBounds);
        DrawEntities(renderer, alpha);

        if (Debug)
        {
            CameraDebug.DrawCameraBounds(renderer, camera);
            DrawLightBounds(renderer);
            MouseDebug.DrawMousePosition(renderer);
            _debugDraw.Render(renderer);
        }
    }

    private void DrawLightBounds(Renderer renderer)
    {
        if (!Debug && !DebugLights)
            return;
        for (var i = 0; i < Lights.Count; i++)
        {
            var light = Lights[i];
            if (!light.DrawDebug)
                continue;
            renderer.DrawRectOutline(light.Bounds.Min, light.Bounds.Max, light.Color);
        }
    }

    public void DrawEntities(Renderer renderer, double alpha)
    {
        DrawEnemies(renderer, alpha);
        DrawPlayer(renderer, alpha);
        DrawBullets(renderer, alpha);
    }


    private void DrawLevel(Renderer renderer, Level level, Bounds cameraBounds)
    {
        var color = ColorExt.FromHex(level.BgColor.AsSpan().Slice(1));
        renderer.DrawRect(level.Bounds, color);

        for (var layerIndex = level.LayerInstances.Length - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = GetLayerDefinition(LDtk.LdtkRaw, layer.LayerDefUid);
            DrawLayer(renderer, LDtk.LdtkRaw, level, layer, layerDef, (Rectangle)cameraBounds);
        }

        if (Debug && DebugLevel)
            renderer.DrawRectOutline(level.Position, level.Position + level.Size, Color.Blue, 1.0f);
    }

    private void DrawBullets(Renderer renderer, double alpha)
    {
        var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        for (var i = 0; i < Bullets.Count; i++)
        {
            var bullet = Bullets[i];
            var srcRect = new Rectangle(4 * 16, 0, 16, 16);
            var xform = bullet.GetTransform(alpha);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, bullet.Flip);
            if (Debug)
            {
                if (bullet.DrawDebug || DebugEntities)
                {
                    var radius = MathF.Min(bullet.Size.X, bullet.Size.Y) * 0.5f;
                    renderer.DrawCircleOutline(bullet.Bounds.Center, radius, Color.Blue, 1.0f);
                }

                DrawEntityDebug(renderer, bullet, false, alpha);
            }
        }
    }

    private void DrawPlayer(Renderer renderer, double alpha)
    {
        var srcRect = new Rectangle((int)(Player.FrameIndex * 16), 0, 16, 16);
        var xform = Player.GetTransform(alpha);
        var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, Player.Flip);
        if (Debug)
            DrawEntityDebug(renderer, Player, true, alpha);
    }

    private void DrawEnemies(Renderer renderer, double alpha)
    {
        var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        for (var i = 0; i < Enemies.Count; i++)
        {
            var entity = Enemies[i];

            var offset = entity.Type switch
            {
                EnemyType.Slug => 5,
                EnemyType.BlueBee => 3,
                EnemyType.YellowBee or _ => 1,
            };

            var frameIndex = (int)(entity.TotalTimeActive * 10) % 2;
            var srcRect = new Rectangle(offset * 16 + frameIndex * 16, 16, 16, 16);
            var xform = entity.GetTransform(alpha);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, entity.Flip);
            if (Debug)
                DrawEntityDebug(renderer, entity, false, alpha);
        }
    }

    private void DrawLayer(Renderer renderer, LdtkJson ldtk, Level level, LayerInstance layer, LayerDefinition layerDef, Rectangle cameraBounds)
    {
        if (!layer.TilesetDefUid.HasValue)
            return;

        var tilesetDef = GetTilesetDef(ldtk, layerDef.TilesetDefUid!.Value);
        var texture = Shared.Content.GetTexture(tilesetDef.RelPath);

        var layerWidth = layer.CWid;
        var layerHeight = layer.CHei;

        var boundsMin = cameraBounds
            .MinVec(); // WorldToTilePosition(cameraBounds.MinVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);
        var boundsMax = cameraBounds
            .MaxVec(); // WorldToTilePosition(cameraBounds.MaxVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);

        if (layer.Type == "IntGrid" && layer.Identifier == "Tiles" && Debug && DebugLevel)
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
                    renderer.DrawRect(min, max, color * 0.5f, 0);
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

    private static TilesetDefinition GetTilesetDef(LdtkJson ldtk, long tilesetUid)
    {
        for (var i = 0; i < ldtk.Defs.Tilesets.Length; i++)
        {
            if (ldtk.Defs.Tilesets[i].Uid == tilesetUid)
            {
                return ldtk.Defs.Tilesets[i];
            }
        }

        throw new Exception($"Could not find a TilesetDefinition with id \"{tilesetUid}\"");
    }

    private static void RenderTile(Renderer renderer, Point position, TileInstance tile, LayerInstance layer, Texture texture)
    {
        var srcRect = new Rectangle((int)tile.Src[0], (int)tile.Src[1], (int)layer.GridSize, (int)layer.GridSize);
        var sprite = new Sprite(texture, srcRect);
        var transform = Matrix3x2.CreateTranslation(position.X, position.Y);
        renderer.DrawSprite(sprite, transform.ToMatrix4x4(), Color.White);
    }

    public void Unload()
    {
        if (IsLoaded)
            return;

        Level = new Level();
        LDtk = new LDtkAsset();


        Player = new();
        Enemies.Clear();
        Bullets.Clear();
        Lights.Clear();

        IsLoaded = false;
    }

    public void DrawEntityDebug(Renderer renderer, Entity e, bool drawCoords, double alpha)
    {
        if (!e.DrawDebug && !DebugEntities)
            return;

        var cell = e.Cell;
        var cellInScreen = cell * DefaultGridSize;
        renderer.DrawPoint(e.Position.Current, e.SmartColor, 2);

        // draw small crosshair
        {
            renderer.DrawRect(new Rectangle(cellInScreen.X - 1, cellInScreen.Y, 3, 1), e.SmartColor);
            renderer.DrawRect(new Rectangle(cellInScreen.X, cellInScreen.Y - 1, 1, 3), e.SmartColor);
        }

        renderer.DrawRectOutline(e.Bounds.Min, e.Bounds.Max, e.SmartColor, 1.0f);

        if (drawCoords)
        {
            var cellText = $"{cell.X.ToString()}, {cell.Y.ToString()}";
            var posText = $"{StringExt.TruncateNumber(e.Position.Current.X)}, {StringExt.TruncateNumber(e.Position.Current.Y)}";
            ReadOnlySpan<char> str = posText + " " + cellText;
            var textSize = renderer.GetFont(BMFontType.ConsolasMonoSmall).MeasureString(str);
            renderer.DrawBMText(BMFontType.ConsolasMonoSmall, str, e.Position.Current, textSize * new Vector2(0.5f, 1), Vector2.One * 0.25f, 0, 0, Color.Black);
            // renderer.DrawText(FontType.RobotoMedium, str, e.Position.Current, 0, Color.Black, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    private static Dictionary<long, string> LoadTilesets(ReadOnlySpan<char> ldtkPath, TilesetDefinition[] tilesets)
    {
        var mapping = new Dictionary<long, string>();

        foreach (var tilesetDef in tilesets)
        {
            if (string.IsNullOrWhiteSpace(tilesetDef.RelPath))
                continue;

            var tilesetPath = Path.Combine(Path.GetDirectoryName(ldtkPath).ToString(), tilesetDef.RelPath);
            mapping.Add(tilesetDef.Uid, tilesetPath);
        }

        return mapping;
    }

    public void SpawnBullet(Vector2 position, int direction)
    {
        var bullet = new Bullet();
        var def = GetEntityDefinition(LDtk.LdtkRaw, EntityType.Bullet);
        bullet.Position.SetPrevAndCurrent(position + new Vector2(4 * direction, 0));
        bullet.Velocity.X = direction * 300f;
        bullet.Pivot = def.PivotVec;
        bullet.Size = def.Size;
        bullet.SmartColor = ColorExt.FromHex(def.Color.AsSpan(1));
        bullet.Iid = Guid.NewGuid();
        bullet.EntityType = EntityType.Bullet;

        bullet.Initialize(this);
        if (bullet.HasCollision(bullet.Position.Current, bullet.Size))
        {
            // Logs.LogInfo("Can't spawn bullet");
            // return;
        }

        Bullets.Add(bullet);
    }

    private static EntityDefinition GetEntityDefinition(LdtkJson ldtkRaw, EntityType entityType)
    {
        for (var i = 0; i < ldtkRaw.Defs.Entities.Length; i++)
        {
            if (ldtkRaw.Defs.Entities[i].Identifier == Entity.Identifiers[(int)entityType])
                return ldtkRaw.Defs.Entities[i];
        }

        throw new InvalidOperationException();
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

    [ConsoleHandler("save_pos")]
    public static void SavePos(Vector2? position = null)
    {
        if (!Shared.Game.World.IsLoaded)
            return;
        _savedPos = position ?? Shared.Game.World.Player.Position;
        Shared.Console.Print($"Saved position: {_savedPos.ToString()}");
    }

    [ConsoleHandler("load_pos")]
    public static void LoadPos(Vector2? position = null)
    {
        if (!Shared.Game.World.IsLoaded)
            return;

        var loadPos = position ?? _savedPos;
        Shared.Game.World.Player.Position.SetPrevAndCurrent(loadPos);
        Shared.Console.Print($"Loaded position: {loadPos.ToString()}");
    }

    [ConsoleHandler("unstuck")]
    public static void Unstuck()
    {
        if (!Shared.Game.World.IsLoaded)
            return;
        Shared.Game.World.Player.Mover.Unstuck();
    }

    public void FreezeFrame(float duration, bool force = false)
    {
        FreezeFrameTimer = force ? duration : MathF.Max(duration, FreezeFrameTimer);
    }

    public void DrawLights(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, Camera camera,
        RenderTarget lightSource, RenderTarget lightTarget, double alpha)
    {
        // render lights
        renderer.DrawSprite(renderer.BlankSprite, Matrix3x2.CreateScale(renderDestination.Width, renderDestination.Height).ToMatrix4x4(), Color.White);
        renderer.UpdateBuffers(ref commandBuffer);
        renderer.SpriteBatch.Discard();
        renderer.BeginRenderPass(ref commandBuffer, lightTarget, AmbientColor, PipelineType.Light);
        DrawAllLights(
            ref commandBuffer,
            renderDestination.Size(),
            camera.ZoomedBounds,
            new[]
            {
                new TextureSamplerBinding(renderer.BlankSprite.Texture, Renderer.PointClamp),
            },
            false
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render light to game
        renderer.DrawSprite(lightTarget.Target, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, LightsToDestinationBlend);

        // render entities for use with rim light
        DrawEntities(renderer, alpha);
        var viewProjection = camera.GetViewProjection(lightSource.Width, lightSource.Height);
        renderer.RunRenderPass(ref commandBuffer, lightSource, Color.Transparent, viewProjection); // lightSource contains only entities

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
                new TextureSamplerBinding(renderer.BlankSprite.Texture, Renderer.PointClamp),
                new TextureSamplerBinding(lightSource.Target, Renderer.PointClamp),
            },
            true
        );
        renderer.EndRenderPass(ref commandBuffer);

        // render rim light to game
        renderer.DrawSprite(lightTarget.Target, Matrix4x4.Identity, Color.White);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, RimLightToDestinationBlend);
    }

    private void DrawAllLights(ref CommandBuffer commandBuffer, UPoint renderDestinationSize, in Bounds cameraBounds, TextureSamplerBinding[] fragmentBindings,
        bool useRimIntensity)
    {
        for (var i = 0; i < Lights.Count; i++)
        {
            var light = Lights[i];
            if (!light.IsEnabled)
                continue;
            var vertUniform = Renderer.GetViewProjection(renderDestinationSize.X, renderDestinationSize.Y);
            var fragUniform = new Pipelines.RimLightUniforms()
            {
                LightColor = new Vector3(light.Color.R / 255f, light.Color.G / 255f, light.Color.B / 255f),
                LightIntensity = light.Intensity,
                LightRadius = Math.Max(light.Width, light.Height) / MathF.Sqrt(2),
                LightPos = light.Position + light.Size.ToVec2() * light.Pivot,
                Debug = useRimIntensity ? RimLightIntensity : ((DebugLights || light.Debug) ? 1.0f : 0),
                Angle = light.Angle,
                ConeAngle = light.ConeAngle, // TODO (marpe): Not working atm

                TexelSize = new Vector4(
                    1.0f / renderDestinationSize.X,
                    1.0f / renderDestinationSize.Y,
                    renderDestinationSize.X,
                    renderDestinationSize.Y
                ),
                Bounds = new Vector4(
                    cameraBounds.Min.X,
                    cameraBounds.Min.Y,
                    cameraBounds.Width,
                    cameraBounds.Height
                ),
            };
            var fragmentParamOffset = commandBuffer.PushFragmentShaderUniforms(fragUniform);
            var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(vertUniform);
            commandBuffer.BindFragmentSamplers(fragmentBindings);
            SpriteBatch.DrawIndexedQuads(ref commandBuffer, 0, 1, vertexParamOffset, fragmentParamOffset);
        }
    }
}
