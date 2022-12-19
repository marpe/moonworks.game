using System.Diagnostics.CodeAnalysis;
using MyGame.Cameras;
using MyGame.Editor;
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

    public Player Player = new();
    public List<Enemy> Enemies { get; } = new();
    public List<Bullet> Bullets { get; } = new();

    public List<Light> Lights { get; } = new();

    [HideInInspector] public ulong WorldUpdateCount;

    [HideInInspector] public float WorldTotalElapsedTime;

    private static Vector2 _savedPos;

    public float FreezeFrameTimer;

    public PipelineType LightsToDestinationBlend = PipelineType.Multiply;
    public PipelineType RimLightToDestinationBlend = PipelineType.Additive;

    [Range(0, 5, 0.1f)] [CVar("light_rim_intensity", "Sets the intensity of the rim lighting")]
    public static float RimLightIntensity = 1f;

    public RootJson Root = new();
    public Level Level = new();
    public bool DrawBackground = true;

    public World()
    {
        _debugDraw = new DebugDrawItems();
        // StartLevel("World_Level_1");
    }

    public static Level FindLevel(string identifier, RootJson root)
    {
        for (var i = 0; i < root.Worlds.Count; i++)
        {
            for (var j = 0; j < root.Worlds[i].Levels.Count; j++)
            {
                if (root.Worlds[i].Levels[j].Identifier == identifier)
                {
                    return root.Worlds[i].Levels[j];
                }
            }
        }

        Logs.LogError($"Level not found: {identifier}");

        return root.Worlds.FirstOrDefault()?.Levels.FirstOrDefault() ?? throw new InvalidOperationException();
    }

    public void SetRoot(RootJson root)
    {
        Root = root;
        IsLoaded = true;
    }

    [MemberNotNull(nameof(Level), nameof(Player))]
    public void StartLevel(string levelIdentifier)
    {
        WorldTotalElapsedTime = WorldUpdateCount = 0;

        var level = FindLevel(levelIdentifier, Root);

        foreach (var field in level.FieldInstances)
        {
            var fieldDef = Root.LevelFieldDefinitions.FirstOrDefault(x => x.Uid == field.FieldDefId);
            if (fieldDef == null)
            {
                Logs.LogError($"Could not find a field definition with id \"{field.FieldDefId}\"");
                continue;
            }

            if (fieldDef.Identifier == "AmbientLight")
            {
                var fieldValue = (Color?)field.Value;
                AmbientColor = fieldValue ?? Color.White;
            }
        }

        Enemies.Clear();
        Bullets.Clear();
        Lights.Clear();

        Level = level;

        var entities = LoadEntitiesInLevel(Root, level);
        Player = (Player)entities.First(t => t.EntityType == EntityType.Player);
        var enemyTypes = new[] { EntityType.BlueBee, EntityType.Slug };
        Enemies.AddRange(entities.Where(x => enemyTypes.Contains(x.EntityType)).Cast<Enemy>());
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

    private static List<Entity> LoadEntitiesInLevel(RootJson root, Level level)
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
                var parsedType = Enum.Parse<EntityType>(entityDef.Identifier);
                var type = Entity.TypeMap[parsedType];
                var entity = (Entity)(Activator.CreateInstance(type) ?? throw new InvalidOperationException());

                entity.EntityType = parsedType;
                entity.Iid = entityInstance.Iid;
                entity.Pivot = new Vector2(entityDef.PivotX, entityDef.PivotY);
                entity.Size = entityInstance.Size;
                entity.Position = new Position(level.WorldPos + entityInstance.Position - entity.Pivot * entity.Size);
                entity.SmartColor = entityDef.Color;
                
                foreach (var field in entityInstance.FieldInstances)
                {
                    var fieldDef = entityDef.FieldDefinitions.First(x => x.Uid == field.FieldDefId);
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

            if (!entity.IsInitialized)
                entity.Initialize(this);
            
            if (entity.IsDestroyed)
            {
                Enemies.RemoveAt(i);
                continue;
            }

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
        var color = level.BackgroundColor;
        if (DrawBackground)
            renderer.DrawRect(level.Bounds, color);

        for (var layerIndex = level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = level.LayerInstances[layerIndex];
            var layerDef = GetLayerDefinition(Root, layer.LayerDefId);
            DrawLayer(renderer, Root, level, layer, layerDef, (Rectangle)cameraBounds);
        }

        if (Debug && DebugLevel)
            renderer.DrawRectOutline(level.WorldPos, level.WorldPos + level.Size.ToVec2(), Color.Blue, 1.0f);
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
        if (!Shared.Content.HasTexture(ContentPaths.ldtk.Example.Characters_png))
        {
            Shared.Content.LoadAndAddTextures(new[] { ContentPaths.ldtk.Example.Characters_png });
        }

        var texture = Shared.Content.GetTexture(ContentPaths.ldtk.Example.Characters_png);
        for (var i = 0; i < Enemies.Count; i++)
        {
            var entity = Enemies[i];

            var offset = entity.EntityType switch
            {
                EntityType.Slug => 5,
                EntityType.BlueBee => 3,
                _ => 1,
            };

            var frameIndex = (int)(entity.TotalTimeActive * 10) % 2;
            var srcRect = new Rectangle(offset * 16 + frameIndex * 16, 16, 16, 16);
            var xform = entity.GetTransform(alpha);
            renderer.DrawSprite(new Sprite(texture, srcRect), xform, Color.White, 0, entity.Flip);
            if (Debug)
                DrawEntityDebug(renderer, entity, false, alpha);
        }
    }

    private void DrawLayer(Renderer renderer, RootJson root, Level level, LayerInstance layer, LayerDef layerDef, Rectangle cameraBounds)
    {
        var cols = level.Width / layerDef.GridSize;
        var rows = level.Height / layerDef.GridSize;

        var boundsMin = cameraBounds
            .MinVec(); // WorldToTilePosition(cameraBounds.MinVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);
        var boundsMax = cameraBounds
            .MaxVec(); // WorldToTilePosition(cameraBounds.MaxVec() - Position, (int)layer.GridSize, layerWidth, layerHeight);

        var tileSetDef = GetTilesetDef(Root, layerDef.TileSetDefId);
        var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
        
        if (layerDef.LayerType == LayerType.IntGrid && Debug && DebugLevel)
        {
            for (var i = 0; i < layer.IntGrid.Length; i++)
            {
                var value = layer.IntGrid[i];
                if (value == 0)
                    continue;

                var enumValue = (LayerDefs.Tiles)value;

                var gridSize = layerDef.GridSize;
                var gridY = (int)(i / cols);
                var gridX = (int)(i % cols);
                var min = level.WorldPos + new Vector2(gridX, gridY) * gridSize;
                var max = min + new Vector2(gridSize, gridSize);

                var color = LayerDefs.TilesColors[enumValue];
                if (GetIntDef(layerDef, value, out var intDef))
                {
                    color = intDef.Color;
                }

                renderer.DrawRect(min, max, color * 0.5f, 0);
            }
        }

        for (var i = 0; i < layer.AutoLayerTiles.Count; i++)
        {
            var tile = layer.AutoLayerTiles[i];
            var sprite = GetTileSprite(texture, tile.TileId, layerDef.GridSize);
            var transform = (
                Matrix3x2.CreateScale(1f, 1f) *
                Matrix3x2.CreateTranslation(
                    level.WorldPos.X + tile.Cell.X * layerDef.GridSize,
                    level.WorldPos.Y + tile.Cell.Y * layerDef.GridSize
                )
            ).ToMatrix4x4();
            renderer.DrawSprite(sprite, transform, Color.White);
        }
    }
    
    public static Sprite GetTileSprite(Texture texture, uint tileId, uint gridSize)
    {
        Sprite sprite;
        var tileSize = new Point(
            (int)(texture.Width / gridSize),
            (int)(texture.Height / gridSize)
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


    private static TileSetDef GetTilesetDef(RootJson root, long tilesetUid)
    {
        for (var i = 0; i < root.TileSetDefinitions.Count; i++)
        {
            if (root.TileSetDefinitions[i].Uid == tilesetUid)
            {
                return root.TileSetDefinitions[i];
            }
        }

        throw new Exception($"Could not find a TileSetDefinition with id \"{tilesetUid}\"");
    }

    public void Unload()
    {
        if (IsLoaded)
            return;

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

    public void SpawnBullet(Vector2 position, int direction)
    {
        var bullet = new Bullet();
        var def = GetEntityDefinition(Root, EntityType.Bullet);
        bullet.Position.SetPrevAndCurrent(position + new Vector2(4 * direction, 0));
        bullet.Velocity.X = direction * 300f;
        bullet.Pivot = new Vector2(def.PivotX, def.PivotY);
        bullet.Size = def.Size;
        bullet.SmartColor = def.Color;
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

    private static EntityDefinition GetEntityDefinition(RootJson root, int entityDefId)
    {
        for (var i = 0; i < root.EntityDefinitions.Count; i++)
        {
            if (root.EntityDefinitions[i].Uid == entityDefId)
                return root.EntityDefinitions[i];
        }

        throw new InvalidOperationException();
    }

    private static EntityDefinition GetEntityDefinition(RootJson root, EntityType entityType)
    {
        for (var i = 0; i < root.EntityDefinitions.Count; i++)
        {
            if (root.EntityDefinitions[i].Identifier == Entity.Identifiers[(int)entityType])
                return root.EntityDefinitions[i];
        }

        throw new InvalidOperationException();
    }

    public static LayerDef GetLayerDefinition(RootJson root, long layerDefUid)
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

        // draw ground for use with rim light
        for (var layerIndex = Level.LayerInstances.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = Level.LayerInstances[layerIndex];
            var layerDef = GetLayerDefinition(Root, layer.LayerDefId);
            DrawLayer(renderer, Root, Level, layer, layerDef, (Rectangle)camera.ZoomedBounds);
        }

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
                Debug = ((DebugLights || light.Debug) ? 1.0f : 0),
                RimIntensity = RimLightIntensity,
                Angle = light.Angle,
                ConeAngle = light.ConeAngle,

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
