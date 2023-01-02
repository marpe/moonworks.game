using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Entities;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

public unsafe class EditorWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "EditorTabs";
    private static ResizeEditorTool _resizeEditorLevel = new();
    private static ResizeEditorTool _resizeEditorEntity = new();
    private const string SelectedEntityPopupName = "SelectedEntityInstance";
    private const string EditorWindowTitle = "Editor";
    private const string EditorSettingsWindowTitle = "EditorSettings";
    private const string CurrentLevelWindowName = "CurrentLevelWindow";

    private static int _rowMinHeight = 60;

    [CVar("editor.deselected_layer_alpha", "")]
    public static float DeselectedLayerAlpha = 0.1f;

    [CVar("editor.deselected_auto_layer_alpha", "")]
    public static float DeselectedAutoLayerAlpha = 1.0f;

    [CVar("editor.int_grid_alpha", "")]
    public static float IntGridAlpha = 0.1f;

    private readonly EntityDefWindow _entityDefWindow;
    private readonly LayerDefWindow _layerDefWindow;
    private readonly LevelsWindow _levelsWindow;
    private readonly TileSetDefWindow _tileSetDefWindow;
    private readonly WorldsWindow _worldsWindow;

    private static Dictionary<(int groupUid, int ruleUid), Dictionary<(int x, int y), AutoRuleTile>> _autoTileCache = new();
    private MyEditorMain _editor;

    private static float _cameraMinZoom = 0.1f;

    private static Num.Vector2 _cameraPan = Num.Vector2.Zero;

    public static float _cameraZoom = 1f;

    private static int _selectedEntityDefinitionIndex;
    private static int _selectedEntityInstanceIndex = -1;
    private static int _selectedIntGridValueIndex;
    private static int _selectedLayerInstanceIndex;

    [CVar("editor.grid_color", "")]
    public static Color GridColor = new(0, 0, 0, 0.04f);

    [CVar("editor.grid_thickness", "")]
    public static float GridThickness = 1.0f;

    [CVar("editor.background_color", "")]
    public static Color BackgroundColor = Color.Black;

    [CVar("editor.stripe_color", "")]
    public static Color StripeColor = new(1.0f, 1.0f, 1.0f, 0.1f);

    private static Num.Vector2 _contentAvail;
    private static Num.Vector2 _cursorScreenPos;
    private static uint _selectedEntityPopupId;
    private static bool _isAdding;
    private static ButtonState btnState = new();
    private static Point _resizeStartPos;
    private static UPoint _resizeStartSize;
    private static int _entityPopupPos = 1;
    private static bool _isInstancePopupOpen;
    private static Point _startPos;
    private static Point _resizeEntityStartPos;
    private static UPoint _resizeEntityStartSize;
    private static Point _moveEntityStart;
    private static Point _moveLevelStart;
    private static List<EntityInstance> _tempEntityList = new();

    private static List<EntityInstance> _hoveringEntities = new();
    private static Point _lastAddedCell;
    private static List<EntityDef> _tempEntityDefList = new();
    public static bool ResetDock = true;
    private bool _currentLevelWindowWasDocked;

    public EditorWindow(MyEditorMain editor) : base(WindowTitle)
    {
        KeyboardShortcut = "^E";
        _editor = editor;

        _entityDefWindow = new EntityDefWindow(editor) { IsOpen = true };
        _layerDefWindow = new LayerDefWindow(editor) { IsOpen = true };
        _levelsWindow = new LevelsWindow(editor) { IsOpen = true };
        _tileSetDefWindow = new TileSetDefWindow(editor) { IsOpen = true };
        _worldsWindow = new WorldsWindow(editor) { IsOpen = true };
    }

    private static bool GetSelectedLayerInstance([NotNullWhen(true)] out WorldsRoot.World? world, [NotNullWhen(true)] out Level? level,
        [NotNullWhen(true)] out LayerInstance? layerInstance, [NotNullWhen(true)] out LayerDef? layerDef)
    {
        layerInstance = null;
        layerDef = null;
        level = null;
        world = null;

        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;

        if (WorldsWindow.SelectedWorldIndex > root.Worlds.Count - 1)
        {
            return false;
        }

        world = root.Worlds[WorldsWindow.SelectedWorldIndex];
        if (LevelsWindow.SelectedLevelIndex > world.Levels.Count - 1)
        {
            return false;
        }

        level = world.Levels[LevelsWindow.SelectedLevelIndex];

        if (_selectedLayerInstanceIndex > level.LayerInstances.Count - 1)
        {
            return false;
        }

        layerInstance = level.LayerInstances[_selectedLayerInstanceIndex];
        for (var i = 0; i < root.LayerDefinitions.Count; i++)
        {
            var def = root.LayerDefinitions[i];
            if (def.Uid == layerInstance.LayerDefId)
            {
                layerDef = def;
                break;
            }
        }

        if (layerDef == null)
            return false;

        return true;
    }

    public override void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        var windowClass = new ImGuiWindowClass();
        // windowClass.DockNodeFlagsOverrideSet = (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_HiddenTabBar;

        void InitializeLayout(uint dockSpaceId)
        {
            var dockNode = ImGuiInternal.DockBuilderGetNode(dockSpaceId);
            /*dockNode->LocalFlags |= ImGuiDockNodeFlags.AutoHideTabBar |
                                    (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                                    (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_HiddenTabBar;*/
            ImGuiInternal.DockBuilderDockWindow(EntityDefWindow.WindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(LayerDefWindow.WindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(LevelsWindow.WindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(WorldsWindow.WindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(TileSetDefWindow.WindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(EditorSettingsWindowTitle, dockSpaceId);
            ImGuiInternal.DockBuilderDockWindow(CurrentLevelWindowName, dockSpaceId);
        }

        void DrawMenu()
        {
            if (ImGui.BeginPopupContextWindow("EditorContextMenu"))
            {
                if (ImGui.MenuItem("Dock", default))
                {
                    MyEditorMain.ResetDock = true;
                }

                ImGui.EndPopup();
            }
        }

        var shouldDrawContent =
            ImGuiExt.BeginWorkspaceWindow(WindowTitle, "EditorDockSpace", InitializeLayout, ImGuiExt.RefPtr(ref IsOpen), ref windowClass,
                ImGuiDockNodeFlags.None, ResetDock, DrawMenu);
        ResetDock = false;

        if (shouldDrawContent)
        {
            _entityDefWindow.Draw();
            _layerDefWindow.Draw();
            _levelsWindow.Draw();
            _tileSetDefWindow.Draw();
            _worldsWindow.Draw();
        }

        DrawEditorWindow();

        DrawEditorSettings();

        DrawCurrentLevelData();
    }


    private static void DrawSelectedEntityInstancePopup()
    {
        if (!_isInstancePopupOpen)
        {
            if (_selectedEntityInstanceIndex != -1)
            {
                _selectedEntityInstanceIndex = -1;
                Logs.LogInfo("Stopped inspecting entity instance");
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) /*||
            ImGui.IsMouseReleased(ImGuiMouseButton.Right)*/)
        {
            _isInstancePopupOpen = false;
            return;
        }

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
        {
            _isInstancePopupOpen = false;
            return;
        }

        if (!GetSelectedEntityInstance(out var instance))
        {
            _isInstancePopupOpen = false;
            return;
        }

        if (_selectedEntityInstanceIndex == -1)
        {
            _isInstancePopupOpen = false;
            return;
        }

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 0), ImGuiCond.Always);

        var popupPos = GetWorldPosInScreen(level.WorldPos + instance.Position + new Vector2(0, layerDef.GridSize));
        if (_entityPopupPos == 0)
        {
            ImGui.SetNextWindowPos(popupPos, ImGuiCond.Appearing, System.Numerics.Vector2.Zero);
        }
        else
        {
            var contentMin = ImGui.GetWindowContentRegionMin();
            var contentMax = ImGui.GetWindowContentRegionMax();

            var windowPos = ImGui.GetWindowPos();
            var windowPadding = 10f;
            var overlayPos = new Num.Vector2(
                windowPos.X + contentMax.X - windowPadding,
                windowPos.Y + contentMax.Y - windowPadding
            );
            var windowPosPivot = new Num.Vector2(
                1.0f,
                1.0f
            );
            ImGui.SetNextWindowPos(overlayPos, ImGuiCond.Always, windowPosPivot);
        }

        // ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);

        var windowFlags = ImGuiWindowFlags.NoDecoration |
                          ImGuiWindowFlags.NoDocking |
                          ImGuiWindowFlags.NoFocusOnAppearing |
                          ImGuiWindowFlags.NoBringToFrontOnFocus |
                          ImGuiWindowFlags.NoSavedSettings |
                          ImGuiWindowFlags.NoNav;
        var result = ImGui.Begin(SelectedEntityPopupName, ImGuiExt.RefPtr(ref _isInstancePopupOpen), windowFlags);
        if (!result)
        {
            _isInstancePopupOpen = false;
            ImGui.End();
            return;
        }

        if (GetEntityDef(instance.EntityDefId, out var entityDef))
        {
            ImGui.BeginDisabled();
            var guidStr = instance.Iid.ToString();
            SimpleTypeInspector.InspectString("Guid", ref guidStr);
            SimpleTypeInspector.InspectString("Identifier", ref entityDef.Identifier);
            ImGui.EndDisabled();

            SimpleTypeInspector.InspectPoint("Position", ref instance.Position);

            var tmpPoint = (Point)instance.Size;
            if (ImGuiExt.InspectPoint("Size", ref tmpPoint.X, ref tmpPoint.Y, "W", "Width", "H", "Height", 1, 1, 512))
            {
                instance.Width = (uint)tmpPoint.X;
                instance.Height = (uint)tmpPoint.Y;
            }

            ImGuiExt.SeparatorText("Custom Fields");

            FieldInstanceInspector.DrawFieldInstances(instance.FieldInstances, entityDef.FieldDefinitions);

            ImGui.Separator();

            if (ImGuiExt.ColoredButton(FontAwesome6.Trash, Color.White, ImGuiExt.Colors[2], new Num.Vector2(-ImGuiExt.FLT_MIN, 0), "Remove"))
            {
                layerInstance.EntityInstances.RemoveAt(_selectedEntityInstanceIndex);
                _selectedEntityInstanceIndex = -1;
            }

            if (ImGuiExt.ColoredButton(FontAwesome6.Magnet, Color.White, ImGuiExt.Colors[0], new Num.Vector2(-ImGuiExt.FLT_MIN, 0), "Snap To Grid"))
            {
                instance.Position = SnapToGrid(instance, entityDef, layerDef.GridSize);
            }
        }
        else
        {
            ImGui.TextDisabled($"Could not find an entity definition with id \"{instance.EntityDefId}\"");
        }

        ImGui.End();
    }

    private void DrawCurrentLevelData()
    {
        var windowFlags = ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin(CurrentLevelWindowName, default, windowFlags))
        {
            if (!ImGui.IsWindowDocked() && _currentLevelWindowWasDocked)
            {
                ImGui.SetWindowSize(new Num.Vector2(300, 500), ImGuiCond.Always);
            }

            DrawLayersInSelectedLevel();
            _currentLevelWindowWasDocked = ImGui.IsWindowDocked();
        }

        ImGui.End();
    }

    private void DrawEditorSettings()
    {
        var windowFlags = ImGuiWindowFlags.NoCollapse; /*|
                          ImGuiWindowFlags.NoTitleBar |
                          ImGuiWindowFlags.NoDecoration;*/
        ImGui.SetNextWindowSize(new Num.Vector2(200, 200), ImGuiCond.Appearing);
        var result = ImGui.Begin(EditorSettingsWindowTitle, default, windowFlags);
        if (!result)
        {
            ImGui.End();
            return;
        }

        DrawCleanButton();

        SimpleTypeInspector.InspectPercentage("Deselected Auto Layer Alpha", ref DeselectedAutoLayerAlpha);
        SimpleTypeInspector.InspectPercentage("Deselected Layer Alpha", ref DeselectedLayerAlpha);
        SimpleTypeInspector.InspectPercentage("IntGrid Alpha", ref IntGridAlpha);
        SimpleTypeInspector.InspectColor("BackgroundColor", ref BackgroundColor);
        SimpleTypeInspector.InspectColor("StripeColor", ref StripeColor);
        SimpleTypeInspector.InspectColor("GridColor", ref GridColor);
        SimpleTypeInspector.InspectFloat("GridThickness", ref GridThickness, new RangeSettings(0, 10, 0.25f, false));
        SimpleTypeInspector.InspectFloat("CameraScale", ref _cameraZoom, new RangeSettings(0.001f, 10, 0.25f, false));
        SimpleTypeInspector.InspectNumVector2("CameraPos", ref _cameraPan);

        ImGui.End();
    }

    private static bool RuleMatches(AutoRule rule, LayerInstance layerInstance, LayerDef layerDef, Level level, int x, int y)
    {
        if (rule.TileIds.Count == 0)
        {
            return false;
        }

        if (rule.Chance <= 0 || (rule.Chance < 1 && Random.Shared.NextSingle() >= rule.Chance))
        {
            return false;
        }

        var cols = level.Width / layerDef.GridSize;
        var radius = rule.Size / 2;
        for (var py = 0; py < rule.Size; py++)
        {
            for (var px = 0; px < rule.Size; px++)
            {
                var patternId = py * rule.Size + px;
                var patternValue = rule.Pattern[patternId];
                if (patternValue == 0)
                {
                    continue;
                }

                var gridId = (y + py - radius) * cols + (x + px - radius);
                if (gridId < 0 || gridId > layerInstance.IntGrid.Length - 1)
                {
                    return false; // out of bounds
                }

                var value = layerInstance.IntGrid[gridId];

                switch (patternValue)
                {
                    case LayerDefWindow.ANYTHING_TILE_ID when value == 0:
                    case LayerDefWindow.NOTHING_TILE_ID when value != 0:
                    case > 0 when patternValue != value:
                    case < 0 when patternValue == -value:
                        return false;
                }
            }
        }

        return true;
    }

    private static void AddRuleTilesAt(int groupUid, AutoRule rule, LayerInstance layerInstance, LayerDef layerDef, TileSetDef tileSetDef, Level level, int x,
        int y)
    {
        if (!_autoTileCache.TryGetValue((groupUid, rule.Uid), out var ruleCache))
        {
            ruleCache = new Dictionary<(int x, int y), AutoRuleTile>();
            _autoTileCache.Add((groupUid, rule.Uid), ruleCache);
        }

        ruleCache.Add(
            (x, y),
            new AutoRuleTile
            {
                Cell = new UPoint((uint)x, (uint)y),
                TileId = rule.TileIds[Random.Shared.Next(rule.TileIds.Count)],
                TileSetDefId = tileSetDef.Uid,
                LevelWorldPos = level.WorldPos,
                LayerGridSize = layerDef.GridSize,
            }
        );
    }

    private static void ApplyIntGridAutoRules()
    {
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
        {
            return;
        }

        if (!GetTileSetDef(layerDef.TileSetDefId, out var tileSetDef))
        {
            return;
        }

        _autoTileCache.Clear();

        var left = 0;
        var top = 0;
        var right = level.Width / layerDef.GridSize;
        var bottom = level.Height / layerDef.GridSize;

        var matchedCells = new HashSet<(int x, int y)>();

        for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
        {
            var group = layerDef.AutoRuleGroups[i];
            if (!group.IsActive)
            {
                continue;
            }

            for (var j = 0; j < group.Rules.Count; j++)
            {
                var rule = group.Rules[j];
                if (!rule.IsActive)
                {
                    continue;
                }

                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        if (matchedCells.Contains((x, y)))
                        {
                            continue;
                        }

                        if (RuleMatches(rule, layerInstance, layerDef, level, x, y))
                        {
                            AddRuleTilesAt(group.Uid, rule, layerInstance, layerDef, tileSetDef, level, x, y);
                            matchedCells.Add((x, y));
                        }
                    }
                }
            }
        }

        layerInstance.AutoLayerTiles.Clear();
        foreach (var ((groupUid, ruleUid), ruleCache) in _autoTileCache)
        {
            foreach (var ((x, y), tile) in ruleCache)
            {
                layerInstance.AutoLayerTiles.Add(
                    new AutoLayerTile
                    {
                        TileId = (uint)tile.TileId,
                        Cell = new UPoint((uint)x, (uint)y),
                    }
                );
            }
        }
    }

    private static void DrawCleanButton()
    {
        if (ImGuiExt.ColoredButton("Cleanup", new System.Numerics.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            var editor = (MyEditorMain)Shared.Game;
            var root = editor.RootJson;
            for (var i = 0; i < root.Worlds.Count; i++)
            {
                var world = root.Worlds[i];
                for (var j = 0; j < world.Levels.Count; j++)
                {
                    var level = world.Levels[j];

                    var levelSnappedX = (int)(level.WorldPos.X / (float)World.DefaultGridSize) * World.DefaultGridSize;
                    var levelSnappedY = (int)(level.WorldPos.Y / (float)World.DefaultGridSize) * World.DefaultGridSize;

                    if (levelSnappedX != level.WorldPos.X || levelSnappedY != level.WorldPos.Y)
                    {
                        Logs.LogWarn("Snapping level world position to grid");
                        level.WorldPos = new Point(levelSnappedX, levelSnappedY);
                    }

                    var levelSnappedWidth = (int)(level.Width / (float)World.DefaultGridSize) * World.DefaultGridSize;
                    var levelSnappedHeight = (int)(level.Height / (float)World.DefaultGridSize) * World.DefaultGridSize;

                    if (levelSnappedWidth != level.Width || levelSnappedHeight != level.Height)
                    {
                        Logs.LogWarn("Snapping level size to grid");
                        level.Width = (uint)levelSnappedWidth;
                        level.Height = (uint)levelSnappedHeight;
                    }

                    for (var k = level.LayerInstances.Count - 1; k >= 0; k--)
                    {
                        var layerInstance = level.LayerInstances[k];
                        if (!GetLayerDefinition(layerInstance.LayerDefId, out var layerDef))
                        {
                            Logs.LogWarn($"Removing layer instance \"{k}\" since there's no layer definition with id \"{layerInstance.LayerDefId}\"");
                            level.LayerInstances.RemoveAt(k);
                            continue;
                        }

                        if (layerInstance.EntityInstances.Count > 0 && layerDef.LayerType != LayerType.Entities)
                        {
                            Logs.LogWarn("Removing entity instances since the layer isn't an entity layer");
                            layerInstance.EntityInstances.Clear();
                        }

                        if (layerInstance.IntGrid.Length > 0 && layerDef.LayerType != LayerType.IntGrid)
                        {
                            Logs.LogWarn("Clearing int grid values since the layer isn't of type IntGrid");
                            layerInstance.IntGrid = Array.Empty<int>();
                        }

                        for (var l = layerInstance.AutoLayerTiles.Count - 1; l >= 0; l--)
                        {
                            var tile = layerInstance.AutoLayerTiles[l];
                            if (tile.Cell.X >= level.Width / layerDef.GridSize ||
                                tile.Cell.Y >= level.Height / layerDef.GridSize)
                            {
                                Logs.LogWarn("Removing auto tile since it's outside the level bounds");
                                layerInstance.AutoLayerTiles.RemoveAt(l);
                            }
                        }

                        var gridDictionary = new Dictionary<(int x, int y), EntityInstance>();

                        for (var l = layerInstance.EntityInstances.Count - 1; l >= 0; l--)
                        {
                            var entity = layerInstance.EntityInstances[l];
                            /*if (entity.Position.X < 0 || entity.Position.X + entity.Size.X > level.Width ||
                                entity.Position.Y < 0 || entity.Position.Y + entity.Size.Y > level.Height)
                            {
                                Logs.LogWarn("Removing entity instance since it's outside the level bounds");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }*/

                            var cellX = (int)(entity.Position.X / (float)layerDef.GridSize);
                            var cellY = (int)(entity.Position.Y / (float)layerDef.GridSize);

                            if (gridDictionary.ContainsKey((cellX, cellY)))
                            {
                                Logs.LogWarn("Removing entity instance since another entity occupies the space coordinates");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }

                            if (!GetEntityDef(entity.EntityDefId, out var entityDef))
                            {
                                Logs.LogWarn($"Removing entity instance since there\'s no entity definition with id \"{entity.EntityDefId}\"");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }

                            entity.EntityDefIdentifier = entityDef.Identifier;

                            if (IsExcluded(entityDef, layerDef))
                            {
                                Logs.LogWarn("Removing entity instance since the layer it's on excludes entities of this type");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }

                            gridDictionary.Add((cellX, cellY), entity);

                            if (entity.Width != entityDef.Width && !entityDef.ResizableX)
                            {
                                Logs.LogWarn("Resetting entity instance width to entity definition");
                                entity.Width = entityDef.Width;
                            }

                            if (entity.Height != entityDef.Height && !entityDef.ResizableY)
                            {
                                Logs.LogWarn("Resetting entity instance height to entity definition");
                                entity.Height = entityDef.Height;
                            }

                            if (entityDef.KeepAspectRatio)
                            {
                                var entityDefAr = entityDef.Width / (float)entityDef.Height;
                                var instanceAr = entity.Width / (float)entity.Height;
                                if (MathF.NotApprox(entityDefAr, instanceAr))
                                {
                                    entity.Height = (uint)(entity.Width * entityDefAr);
                                    Logs.LogWarn("Resetting entity instance height to match definition aspect ratio");
                                }
                            }

                            var snapped = SnapToGrid(entity, entityDef, layerDef.GridSize);
                            if (snapped != entity.Position)
                            {
                                Logs.LogWarn("Snapping entity instance to grid");
                                entity.Position = snapped;
                            }

                            for (var m = entity.FieldInstances.Count - 1; m >= 0; m--)
                            {
                                var fieldInstance = entity.FieldInstances[m];
                                if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
                                {
                                    Logs.LogWarn($"Removing field instance there\'s no field definition with id \"{fieldInstance.FieldDefId}\"");
                                    entity.FieldInstances.RemoveAt(m);
                                    continue;
                                }

                                fieldInstance.FieldDefIdentifier = fieldDef.Identifier;
                            }
                        }
                    }
                }
            }

            SortLevelInstances();

            Logs.LogInfo("Cleaning done!");
        }
    }

    private void DrawLayersInSelectedLevel()
    {
        if (!GetSelectedWorld(out var world))
            return;

        if (!GetSelectedLevel(out var level))
            return;

        DrawLayersInLevel(level);
    }

    private void DrawLayersInLevel(Level level)
    {
        ImGuiExt.SeparatorText("Layers");

        CreateMissingLayerInstances(level);

        DrawLayerInstances(level.LayerInstances, _editor.RootJson.LayerDefinitions);

        /*if (ImGuiExt.ColoredButton("Sort Layer Instances", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            SortLevelInstances();
        }*/
    }

    private void CreateMissingLayerInstances(Level level)
    {
        for (var i = 0; i < _editor.RootJson.LayerDefinitions.Count; i++)
        {
            var layerDef = _editor.RootJson.LayerDefinitions[i];

            var hasLayerInstance = false;
            for (var j = 0; j < level.LayerInstances.Count; j++)
            {
                if (level.LayerInstances[j].LayerDefId == layerDef.Uid)
                {
                    hasLayerInstance = true;
                    break;
                }
            }

            if (hasLayerInstance)
                continue;

            var layerInstance = CreateLayerInstance(layerDef, level);
            level.LayerInstances.Add(layerInstance);
        }
    }

    private static void SortLevelInstances()
    {
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        for (var i = 0; i < root.Worlds.Count; i++)
        {
            var world = root.Worlds[i];
            for (var j = 0; j < world.Levels.Count; j++)
            {
                var level = world.Levels[j];
                level.LayerInstances.Sort((a, b) =>
                {
                    var indexA = root.LayerDefinitions.FindIndex(def => def.Uid == a.LayerDefId);
                    var indexB = root.LayerDefinitions.FindIndex(def => def.Uid == b.LayerDefId);
                    return indexA.CompareTo(indexB);
                });
            }
        }
    }

    private static LayerInstance CreateLayerInstance(LayerDef layerDef, Level level)
    {
        var cols = level.Width / layerDef.GridSize;
        var rows = level.Height / layerDef.GridSize;
        return new LayerInstance
        {
            LayerDefId = layerDef.Uid,
            IntGrid = new int[cols * rows],
        };
    }

    private static void DrawEditorWindow()
    {
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(1024, 768), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(128, 128), new System.Numerics.Vector2(ImGuiExt.FLT_MAX, ImGuiExt.FLT_MAX));
        var editor = (MyEditorMain)Shared.Game;
        var centralNode = ImGuiInternal.DockBuilderGetCentralNode(editor.ViewportDockSpaceId);
        ImGui.SetNextWindowDockID(centralNode->ID, ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
        var result = ImGui.Begin(EditorWindowTitle, default, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar);

        _cursorScreenPos = ImGui.GetCursorScreenPos();
        _contentAvail = ImGui.GetContentRegionAvail();

        if (!result)
        {
            ImGui.PopStyleVar();
            ImGui.End();
            return;
        }

        ImGui.PopStyleVar();

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
        {
            MyEditorMain.ActiveInput = ActiveInput.EditorWindow;
        }

        var dl = ImGui.GetWindowDrawList();
        dl->AddRectFilled(_cursorScreenPos, _cursorScreenPos + _contentAvail, BackgroundColor.PackedValue);
        ImGuiExt.FillWithStripes(dl, new ImRect(_cursorScreenPos, _cursorScreenPos + _contentAvail), StripeColor.PackedValue);

        if (_selectedEntityPopupId == 0)
            _selectedEntityPopupId = ImGui.GetID(SelectedEntityPopupName);

        DrawWorld();

        if (ImGui.IsWindowHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
        {
            _cameraPan += -ImGui.GetIO()->MouseDelta * 1.0f / _cameraZoom;
        }

        if (ImGui.IsWindowHovered() && ImGui.GetIO()->MouseWheel != 0)
        {
            _cameraZoom += ImGui.GetIO()->MouseWheel * 0.1f * _cameraZoom;
            if (_cameraZoom < _cameraMinZoom)
                _cameraZoom = _cameraMinZoom;
        }

        DrawMouseInfoOverlay();

        DrawSelectedEntityInstancePopup();

        // imgui sets WantCaptureKeyboard when an item is active which we don't want for the game window
        if (ImGui.IsWindowHovered() &&
            (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Right)))
        {
            ImGui.SetWindowFocus(EditorWindowTitle);
            ImGui.SetNextFrameWantCaptureKeyboard(false);
        }

        ImGui.End();
    }

    private static void DrawMouseInfoOverlay()
    {
        var showOverlay = true;
        ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);
        if (GameWindow.BeginOverlay("MouseOverlay", ref showOverlay))
        {
            ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
            ImGuiExt.PrintVector("ImGui.GetMousePos()", ImGui.GetMousePos().ToXNA());
            if (GetSelectedLevel(out var level))
            {
                var (mouseSnappedToGrid, mouseCell, mouseInLevel) = GetMouseInLevel(level);
                ImGuiExt.PrintVector("MousePosInLevel", mouseInLevel);
                ImGuiExt.PrintVector("MouseCell", mouseCell);
            }

            if (GetSelectedLayerInstance(out var world, out var lvl, out var layerInstance, out var layerDef))
            {
                if (layerDef.LayerType == LayerType.Entities)
                {
                    ImGui.Text("NumberOfEntities");
                    ImGui.SameLine(0.6f * ImGui.GetContentRegionAvail().X);
                    ImGui.Text(layerInstance.EntityInstances.Count.ToString());
                }
            }

            ImGui.Dummy(new System.Numerics.Vector2(400, 0));
            ImGui.PopFont();
        }

        ImGui.End();
    }

    private static Vector2 GetScreenPosInWorld(Num.Vector2 position)
    {
        var center = _contentAvail * 0.5f;
        var posRelativeToRenderCenter = position - _cursorScreenPos - center;
        var result = posRelativeToRenderCenter / _cameraZoom + _cameraPan;
        return result.ToXNA();
    }

    public static Num.Vector2 GetWorldPosInScreen(Vector2 position)
    {
        return _cursorScreenPos + _contentAvail * 0.5f +
               -_cameraPan * _cameraZoom +
               position.ToNumerics() * _cameraZoom;
    }

    private static Num.Vector2 GetWorldPosInWindow(Vector2 position)
    {
        return GetWorldPosInScreen(position) - _cursorScreenPos;
    }

    private void DrawLayerInstances(List<LayerInstance> layerInstances, List<LayerDef> layerDefs)
    {
        var rowHeight = 40;
        if (ImGui.BeginTable("LayerInstances", 1, SplitWindow.TableFlags, new System.Numerics.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var layerToDelete = -1;
            for (var i = 0; i < layerInstances.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn();
                ImGui.PushID(i);

                var avail = ImGui.GetContentRegionAvail();
                var layerInstance = layerInstances[i];
                GetLayerDefinition(layerInstance.LayerDefId, out var layerDef);

                var isSelected = _selectedLayerInstanceIndex == i;

                var typeColor = LayerDefWindow.GetLayerDefColor(layerDef?.LayerType ?? LayerType.IntGrid);

                var cursorPos = ImGui.GetCursorScreenPos();
                if (SplitWindow.GiantButton("##Selectable", isSelected, typeColor.MultiplyAlpha(0.66f), rowHeight))
                {
                    _selectedLayerInstanceIndex = i;
                    SelectFirstEntityDefinition();
                    _selectedIntGridValueIndex = 0;
                }

                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(4, 4));
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted($"LayerDefId: {layerInstance.LayerDefId}");
                    if (layerDef != null && layerDef.LayerType == LayerType.Entities)
                    {
                        ImGui.TextUnformatted($"Entities: {layerInstance.EntityInstances.Count}");
                    }

                    ImGui.EndTooltip();
                }

                ImGui.PopStyleVar();

                ImGui.SameLine(0, 0);

                if (ImGui.BeginPopupContextItem("Popup"))
                {
                    if (ImGui.MenuItem("Clear", default))
                    {
                        ClearLayerInstance(layerInstance);
                    }

                    if (ImGui.MenuItem("Delete", default))
                    {
                        layerToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                string label;
                Color color;
                if (layerDef != null)
                {
                    label = layerDef.Identifier;
                    color = typeColor;
                }
                else
                {
                    label = $"Couldn't find a LayerDefinition with Uid \"{layerInstance.LayerDefId}\"";
                    color = isSelected ? Color.White : Color.Red.MultiplyAlpha(0.9f);
                }

                var labelColor = isSelected ? Color.White : color;
                SplitWindow.Icon(SplitWindow.LayerTypeIcon(layerDef?.LayerType ?? LayerType.IntGrid), color, rowHeight);
                ImGui.SameLine(0, 24);
                SplitWindow.GiantLabel(label, labelColor, rowHeight);

                var buttonSize = new System.Numerics.Vector2(30, 30);
                ImGui.SetCursorScreenPos(cursorPos +
                                         new System.Numerics.Vector2(avail.X - buttonSize.X - ImGui.GetStyle()->FramePadding.X,
                                             rowHeight * 0.5f - buttonSize.Y * 0.5f));
                var icon = layerInstance.IsVisible ? FontAwesome6.Eye : FontAwesome6.EyeSlash;
                var tooltip = layerInstance.IsVisible ? "Hide" : "Show";
                if (ImGuiExt.ColoredButton(icon, Color.White, Color.Black, tooltip))
                {
                    layerInstance.IsVisible = !layerInstance.IsVisible;
                }

                ImGui.PopID();
            }

            if (layerToDelete != -1)
            {
                layerInstances.RemoveAt(layerToDelete);
            }

            ImGui.EndTable();
        }

        ImGuiExt.SeparatorText("Selected Layer");

        GetSelectedLayerInstance(out var world, out var level, out var selectedLayerInstance, out var selectedLayerDef);

        if (selectedLayerInstance != null)
        {
            if (selectedLayerDef != null)
            {
                DrawSelectedLayerBrushes(selectedLayerDef, _editor.RootJson, ref _selectedIntGridValueIndex, ref _selectedEntityDefinitionIndex);
            }
            else
            {
                ImGui.TextColored(Color.Red.ToNumerics(), $"Could not find a layer definition with Uid \"{selectedLayerInstance.LayerDefId}\"");
            }
        }
        else
        {
            ImGui.TextDisabled("No layer selected");
        }
    }

    private static void ClearLayerInstance(LayerInstance layerInstance)
    {
        layerInstance.AutoLayerTiles.Clear();
        layerInstance.EntityInstances.Clear();
        layerInstance.IntGrid.AsSpan().Fill(0);
    }

    private static void SelectFirstEntityDefinition()
    {
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (layerDef.LayerType != LayerType.Entities)
            return;

        var entityDefs = EntityDefinitions.All;
        for (var i = 0; i < entityDefs.Count; i++)
        {
            var entityDef = entityDefs[i];
            if (IsExcluded(entityDef, layerDef))
                continue;
            _selectedEntityDefinitionIndex = i;
            _selectedEntityInstanceIndex = -1;
            return;
        }
    }

    private static void DrawSelectedLayerBrushes(LayerDef layerDef, RootJson rootJson, ref int selectedIntGridValueIndex,
        ref int selectedEntityDefinitionIndex)
    {
        switch (layerDef.LayerType)
        {
            case LayerType.IntGrid:
                DrawIntGridBrushes(layerDef, ref selectedIntGridValueIndex);
                break;
            case LayerType.Entities:
                DrawEntityDefBrushes(layerDef, ref selectedEntityDefinitionIndex);
                break;
            case LayerType.Tiles:
                break;
            case LayerType.AutoLayer:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void DrawEntityDefBrushes(LayerDef layerDef, ref int selectedEntityDefinitionIndex)
    {
        var entityDefs = EntityDefinitions.All;

        if (entityDefs.Count == 0)
        {
            ImGui.TextDisabled("There are no entities defined");
            return;
        }

        _tempEntityDefList.Clear();

        var hasSelected = false;
        for (var i = 0; i < entityDefs.Count; i++)
        {
            if (!IsExcluded(entityDefs[i], layerDef))
            {
                _tempEntityDefList.Add(entityDefs[i]);
                if (i == selectedEntityDefinitionIndex)
                    hasSelected = true;
            }
        }

        if (_tempEntityDefList.Count > 0 && !hasSelected)
        {
            SelectFirstEntityDefinition();
        }

        if (_tempEntityDefList.Count == 0)
        {
            ImGui.TextDisabled("No entities match the required layer tags");
            return;
        }

        if (!ImGui.BeginTable("EntityDefTable", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
            return;

        ImGui.TableSetupColumn("Value");

        for (var i = 0; i < entityDefs.Count; i++)
        {
            var entityDef = entityDefs[i];
            if (IsExcluded(entityDef, layerDef))
                continue;

            ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
            ImGui.TableNextColumn();
            ImGui.PushID(i);

            var isSelected = selectedEntityDefinitionIndex == i;
            var cursorPos = ImGui.GetCursorScreenPos();
            if (SplitWindow.GiantButton("##Selectable", isSelected, entityDef.Color, _rowMinHeight))
            {
                selectedEntityDefinitionIndex = i;
            }

            if (ImGui.BeginPopupContextItem("Popup"))
            {
                ImGui.MenuItem("Copy", default);
                ImGui.MenuItem("Cut", default);
                ImGui.MenuItem("Duplicate", default);
                ImGui.MenuItem("Delete", default);
                ImGui.EndPopup();
            }

            if (GetTileSetDef(entityDef.TileSetDefId, out var tileSet))
            {
                DrawEntityDefIcon(tileSet, entityDef.TileId, entityDef.Color, cursorPos);
            }

            ImGui.SameLine(0, 70f);

            var labelColor = isSelected ? Color.White.MultiplyAlpha(0.8f) : entityDef.Color;
            SplitWindow.GiantLabel(entityDef.Identifier, labelColor, _rowMinHeight);

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private static void DrawEntityDefIcon(TileSetDef tileSet, uint tileId, Color color, Num.Vector2 cursorPos)
    {
        var dl = ImGui.GetWindowDrawList();

        var iconSize = new Num.Vector2(32, 32);
        var iconPos = cursorPos + iconSize / 2;
        var rectPadding = new Num.Vector2(4, 4);

        ImGuiExt.RectWithOutline(
            dl,
            iconPos - rectPadding,
            iconPos + iconSize + rectPadding * 2,
            color.MultiplyAlpha(0.4f),
            color,
            2f
        );

        if (SplitWindow.GetTileSetTexture(tileSet.Path, out var texture))
        {
            var sprite = LevelRenderer.GetTileSprite(texture, tileId, tileSet);

            dl->AddImage(
                (void*)sprite.TextureSlice.Texture.Handle,
                iconPos,
                iconPos + iconSize,
                sprite.UV.TopLeft.ToNumerics(),
                sprite.UV.BottomRight.ToNumerics()
            );
        }
    }

    private static void DrawIntGridBrushes(LayerDef layerDef, ref int selectedIntGridValueIndex)
    {
        if (layerDef.IntGridValues.Count == 0)
        {
            ImGui.TextDisabled("No values are defined");
            return;
        }

        if (!ImGui.BeginTable("IntGridTable", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
            return;

        ImGui.TableSetupColumn("Name");

        for (var i = 0; i < layerDef.IntGridValues.Count; i++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
            ImGui.TableNextColumn();
            ImGui.PushID(i);

            var cursorPos = ImGui.GetCursorScreenPos();

            var isSelected = selectedIntGridValueIndex == i;
            var intGridValue = layerDef.IntGridValues[i];
            if (SplitWindow.GiantButton("##Selectable", isSelected, intGridValue.Color, _rowMinHeight))
            {
                selectedIntGridValueIndex = i;
            }

            ImGui.SameLine(0, 0);

            var dl = ImGui.GetWindowDrawList();
            var rectHeight = _rowMinHeight * 0.6f;
            var min = cursorPos + new Num.Vector2(8, (_rowMinHeight - rectHeight) / 2);
            var max = min + new Num.Vector2(32, rectHeight);
            ImGuiExt.RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
            var label = intGridValue.Value.ToString();
            var textSize = ImGui.CalcTextSize(label);
            var rectSize = max - min;
            dl->AddText(min + new Num.Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2),
                Color.White.PackedValue,
                label);

            ImGui.SameLine(60);

            SplitWindow.GiantLabel(intGridValue.Identifier, intGridValue.Color, _rowMinHeight);

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private static bool IsExcluded(EntityDef entityDef, LayerDef layerDef)
    {
        if (layerDef.RequiredTags.Count > 0)
        {
            var hasRequiredTags = entityDef.Tags.Intersect(layerDef.RequiredTags).Count() == layerDef.RequiredTags.Count;
            if (!hasRequiredTags)
            {
                return true;
            }
        }

        if (layerDef.ExcludedTags.Count > 0)
        {
            var hasAnExcludedTag = entityDef.Tags.Intersect(layerDef.ExcludedTags).Any();
            if (hasAnExcludedTag)
            {
                return true;
            }
        }

        return false;
    }

    private record struct ButtonState(bool Active, bool Activated, bool Hovered, bool Clicked, bool Focused);

    private static void DrawWorld()
    {
        if (!GetSelectedWorld(out var world))
            return;

        for (var i = 0; i < world.Levels.Count; i++)
        {
            // draw selected level last
            if (i == LevelsWindow.SelectedLevelIndex)
                continue;

            ImGui.PushID(i);
            var level = world.Levels[i];
            DrawLevel(level, i);
            ImGui.PopID();
        }

        if (GetSelectedLevel(out var selectedLevel))
        {
            ImGui.PushID(LevelsWindow.SelectedLevelIndex);
            DrawLevel(selectedLevel, LevelsWindow.SelectedLevelIndex);
            ImGui.PopID();
        }

        DrawHoveredEntitiesMenu();

        DrawMouse(btnState);

        /*foreach (var ((groupUid, ruleUid), ruleCache) in _autoTileCache)
            {
                foreach (var ((x, y), tile) in ruleCache)
                {
                    if (!GetTileSetDef((uint)tile.TileSetDefId, out var tileSetDef))
                        continue;
                    var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
                    var sprite = GetTileSprite(texture, (uint)tile.TileId);
                    var transform = (
                        Matrix3x2.CreateScale(1f, 1f) *
                        Matrix3x2.CreateTranslation(
                            tile.LevelWorldPos.X + x * tile.LayerGridSize,
                            tile.LevelWorldPos.Y + y * tile.LayerGridSize
                        )
                    ).ToMatrix4x4();
                    renderer.DrawSprite(sprite, transform, Color.White);
                }
            }*/
    }

    private static void DrawHoveredEntitiesMenu()
    {
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.Space))
        {
            ImGui.OpenPopup("HoveringEntitiesPopup");
        }

        var dl = ImGui.GetWindowDrawList();
        if (ImGui.BeginPopup("HoveringEntitiesPopup"))
        {
            for (var i = 0; i < _hoveringEntities.Count; i++)
            {
                ImGui.PushID(i);
                var instance = _hoveringEntities[i];
                string label;
                if (GetEntityDef(instance.EntityDefId, out var def))
                {
                    label = def.Identifier;
                }
                else
                {
                    label = "Missing Entity Definition";
                }

                if (ImGui.MenuItem(label, default))
                {
                    _selectedEntityInstanceIndex = layerInstance.EntityInstances.IndexOf(instance);
                    _isInstancePopupOpen = true;
                }

                if (ImGui.IsItemHovered())
                {
                    var boundsMin = GetWorldPosInScreen(level.WorldPos + instance.Position);
                    var boundsMax = GetWorldPosInScreen(level.WorldPos + instance.Position + instance.Size.ToPoint());
                    dl->AddRect(boundsMin, boundsMax, Color.Red.PackedValue, 0, ImDrawFlags.None, _cameraZoom * 4f);
                }

                ImGui.PopID();
            }

            ImGui.EndPopup();
        }

        if (!ImGui.IsPopupOpen("HoveringEntitiesPopup"))
        {
            var (mouseSnappedToGrid, mouseCell, mouseInLevel) = GetMouseInLevel(level);
            GetEntitiesAtPosition(mouseInLevel, layerInstance.EntityInstances, _hoveringEntities);
        }
    }

    private static void DrawLevel(Level level, int levelIndex)
    {
        var levelMin = GetWorldPosInScreen(level.WorldPos);
        var levelMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2());
        var dl = ImGui.GetWindowDrawList();

        dl->AddRectFilled(levelMin, levelMax, level.BackgroundColor.PackedValue);

        var isSelectedLevel = levelIndex == LevelsWindow.SelectedLevelIndex;

        // draw grid
        var editor = (MyEditorMain)Shared.Game;
        var gridSize = editor.RootJson.DefaultGridSize;

        if (isSelectedLevel && _cameraZoom >= 0.5f)
        {
            var gridMin = GetWorldPosInScreen(level.WorldPos);
            var gridMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2());
            DrawGrid(dl, gridMin, gridMax, gridSize * _cameraZoom, GridColor, _cameraZoom * GridThickness);
        }

        // draw outer level border
        // if (isSelectedLevel)
        {
            var color = isSelectedLevel ? Color.CornflowerBlue : Color.Purple.MultiplyAlpha(0.5f);

            var thickness = 2f * _cameraZoom;
            var padding = new Vector2(thickness * 0.5f / _cameraZoom);
            var rectMin = GetWorldPosInScreen(level.WorldPos - padding);
            var rectMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2() + padding);
            dl->AddRect(rectMin, rectMax, color.PackedValue, 0, ImDrawFlags.None, thickness);
        }

        var levelSize = levelMax - levelMin;
        if (!isSelectedLevel)
        {
            ImGui.SetCursorScreenPos(levelMin);
            if (ImGui.InvisibleButton("LevelButton", levelSize.EnsureNotZero(), (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap))
            {
                LevelsWindow.SelectedLevelIndex = levelIndex;
            }

            ImGui.SetItemAllowOverlap();
        }
        else
        {
            ImGui.SetCursorScreenPos(levelMin);
            /*
            ImGui.SetItemAllowOverlap();
            if (ImGui.InvisibleButton("SelectedLevelButton", levelSize))
            {
            }
            */

            // var min = ImGui.GetItemRectMin();
            // var max = ImGui.GetItemRectMax();
            var min = levelMin;
            var max = min + levelSize;

            HandleLevelResize(level, min, max, gridSize);

            var xLabelPos = min + (max - min) * new Num.Vector2(0.5f, 1);
            DrawLevelSizeLabel(level, dl, xLabelPos);

            btnState = new ButtonState(
                ImGui.IsItemActive(),
                ImGui.IsItemActivated(),
                ImGui.IsItemHovered(),
                ImGui.IsItemClicked(),
                ImGui.IsItemFocused()
            );

            DrawMoveLevelButton(level, min, max);
        }

        DrawLayerInstances(dl, level, isSelectedLevel);
    }

    private static void DrawLevelSizeLabel(Level level, ImDrawList* dl, System.Numerics.Vector2 xLabelPos)
    {
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        var cols = level.Width / root.DefaultGridSize;
        var rows = level.Height / root.DefaultGridSize;
        var xLabel = $"{level.Width}x{level.Height} px ({cols}x{rows} cells)";
        var xLabelSize = ImGui.CalcTextSize(xLabel);
        var offset = new Num.Vector2(-0.5f, 2.0f) * xLabelSize;
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, (xLabelPos + offset).Round(), Color.White.PackedValue, xLabel, 0, default);
    }

    private static void HandleLevelResize(Level level, Num.Vector2 min, Num.Vector2 max, uint gridSize)
    {
        var state = _resizeEditorLevel.Draw(min, max, _cameraZoom * 5f, true, true);

        if (state == ToolState.Started)
        {
            _resizeStartPos = level.WorldPos;
            _resizeStartSize = level.Size;
        }

        if ((state == ToolState.Started ||
             state == ToolState.Active ||
             state == ToolState.Ended) &&
            ImGui.GetMouseDragDelta().LengthSquared() >= 4 * 4)
        {
            var sizeDelta = _resizeEditorLevel.TotSizeDelta.ToVec2() / _cameraZoom;
            var gridSizeDelta = (sizeDelta / gridSize).Round();

            var newSize = (_resizeStartSize + gridSizeDelta * gridSize).ToUPoint();

            if (newSize != level.Size)
            {
                var prevPos = level.WorldPos;
                var prevSize = level.Size;

                var startPos = _resizeStartPos;
                var newX = _resizeEditorLevel.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft => (int)(startPos.X - (gridSizeDelta.X * gridSize)),
                    _ => level.WorldPos.X,
                };

                var newY = _resizeEditorLevel.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Top or RectHandlePos.TopRight => (int)(startPos.Y - (gridSizeDelta.Y * gridSize)),
                    _ => level.WorldPos.Y,
                };

                var newPos = new Point(newX, newY);

                level.WorldPos = newPos;
                level.Size = newSize;

                var moveDelta = new Point(
                    (int)((prevPos.X - newPos.X) / gridSize),
                    (int)((prevPos.Y - newPos.Y) / gridSize)
                );
                LevelsWindow.ResizeLevel(level, prevSize, level.Size, moveDelta, gridSize);
            }
        }
    }

    private static Point SnapToGrid(EntityInstance instance, EntityDef entityDef, uint gridSize)
    {
        var offset = Vector2.Zero;
        // if (instance.Size.X <= gridSize && instance.Size.Y <= gridSize)
        {
            var numCells = new Vector2(
                MathF.Ceil(instance.Size.X / (float)gridSize) * gridSize,
                MathF.Ceil(instance.Size.Y / (float)gridSize) * gridSize
            );
            offset = (numCells - instance.Size) * entityDef.Pivot;
        }
        var (snapped, _) = SnapToGrid(instance.Position + offset, gridSize);
        return (snapped + offset).ToPoint();
    }

    private static (Vector2 snapped, Point cell) SnapToGrid(Point position, uint gridSize)
    {
        return SnapToGrid(position.X, position.Y, gridSize);
    }

    private static (Vector2 snapped, Point cell) SnapToGrid(Vector2 position, uint gridSize)
    {
        return SnapToGrid(position.X, position.Y, gridSize);
    }

    private static (Vector2 snapped, Point cell) SnapToGrid(Num.Vector2 position, uint gridSize)
    {
        return SnapToGrid(position.X, position.Y, gridSize);
    }

    private static (Vector2 snapped, Point cell) SnapToGrid(float x, float y, uint gridSize)
    {
        var col = MathF.FloorToInt(x / gridSize);
        var row = MathF.FloorToInt(y / gridSize);
        var cell = new Point(col, row);
        var snapped = cell * (int)gridSize;
        return (snapped, cell);
    }

    private static void DrawLayerInstances(ImDrawList* dl, Level level, bool isSelectedLevel)
    {
        for (var i = level.LayerInstances.Count - 1; i >= 0; i--)
        {
            var layer = level.LayerInstances[i];
            if (!layer.IsVisible)
                continue;

            var isSelectedLayer = _selectedLayerInstanceIndex == i;
            if (!GetLayerDefinition(layer.LayerDefId, out var layerDef))
                continue;

            if (layerDef.LayerType == LayerType.IntGrid)
            {
                DrawIntGridLayer(dl, level, layerDef, layer, isSelectedLayer);

                if (isSelectedLevel)
                {
                    DrawAutoLayerTiles(dl, level, layerDef, layer, isSelectedLayer);
                }
            }
            else if (layerDef.LayerType == LayerType.Entities)
            {
                if (isSelectedLevel)
                {
                    DrawEntityLayer(dl, level, layer, layerDef, isSelectedLayer);
                }
            }
        }
    }

    private static void DrawAutoLayerTiles(ImDrawList* dl, Level level, LayerDef layerDef, LayerInstance layer, bool isSelectedLayer)
    {
        if (!GetTileSetDef(layerDef.TileSetDefId, out var tileSetDef))
            return;

        if (!SplitWindow.GetTileSetTexture(tileSetDef.Path, out var texture))
            return;

        foreach (var tile in layer.AutoLayerTiles)
        {
            var sprite = LevelRenderer.GetTileSprite(texture, tile.TileId, tileSetDef);
            var uvMin = sprite.UV.TopLeft.ToNumerics();
            var uvMax = sprite.UV.BottomRight.ToNumerics();
            var iconMin = GetWorldPosInScreen(level.WorldPos + tile.Cell.ToVec2() * layerDef.GridSize);
            var iconMax = GetWorldPosInScreen(level.WorldPos + tile.Cell.ToVec2() * layerDef.GridSize + new Vector2(layerDef.GridSize));
            var tint = isSelectedLayer ? Color.White : Color.White.MultiplyAlpha(DeselectedAutoLayerAlpha);
            dl->AddImage((void*)sprite.TextureSlice.Texture.Handle, iconMin, iconMax, uvMin, uvMax, tint.PackedValue);
        }
    }


    private static void DrawEntityLayer(ImDrawList* dl, Level level, LayerInstance layer, LayerDef layerDef, bool isSelectedLayer)
    {
        for (var i = 0; i < layer.EntityInstances.Count; i++)
        {
            ImGui.PushID(i);
            // draw selected last so mouse clicks etc doesn't get hijacked by another entity in front
            if (_selectedEntityInstanceIndex == i)
            {
                ImGui.PopID();
                continue;
            }

            DrawEntityInstance(dl, level, layer, layerDef, isSelectedLayer, i);
            ImGui.PopID();
        }

        if (_selectedEntityInstanceIndex != -1 && _selectedEntityInstanceIndex <= layer.EntityInstances.Count - 1)
        {
            ImGui.PushID(_selectedEntityInstanceIndex);
            DrawEntityInstance(dl, level, layer, layerDef, isSelectedLayer, _selectedEntityInstanceIndex);
            ImGui.PopID();
        }
    }

    private static void DrawEntityInstance(ImDrawList* dl, Level level, LayerInstance layer, LayerDef layerDef, bool isSelectedLayer, int index)
    {
        var entityInstance = layer.EntityInstances[index];
        var entityDefId = entityInstance.EntityDefId;
        if (!GetEntityDef(entityDefId, out var entityDef))
        {
            DrawWarningRect(dl, level.WorldPos, entityInstance.Position, entityInstance.Size);
            return;
        }

        GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef);

        // var sprite = renderer.BlankSprite;
        var editor = (MyEditorMain)Shared.Game;
        var sprite = editor.Renderer.BlankSprite;
        Color fillColor;
        Color iconTint = Color.White;
        var uvMin = Num.Vector2.Zero;
        var uvMax = Num.Vector2.One;
        if (tileSetDef != null)
        {
            var colorField = entityInstance.FieldInstances.FirstOrDefault(x => x.Value is Color);
            fillColor = colorField != null ? (Color)colorField.Value! : entityDef.Color;
            iconTint = colorField != null ? (Color)colorField.Value! : Color.White;

            if (SplitWindow.GetTileSetTexture(tileSetDef.Path, out var texture))
            {
                sprite = LevelRenderer.GetTileSprite(texture, entityDef.TileId, tileSetDef);
                uvMin = sprite.UV.TopLeft.ToNumerics();
                uvMax = sprite.UV.BottomRight.ToNumerics();
            }
        }
        else
        {
            fillColor = entityDef.Color;
        }

        var contentMin = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMax();

        var boundsMin = GetWorldPosInScreen(level.WorldPos + entityInstance.Position);
        var boundsMax = GetWorldPosInScreen(level.WorldPos + entityInstance.Position + entityInstance.Size.ToPoint());

        if (boundsMin.X > contentMax.X || boundsMin.Y > contentMax.Y ||
            boundsMax.X < contentMin.X || boundsMax.Y < contentMin.Y)
        {
            return;
        }

        var bounds = (boundsMax - boundsMin);
        if (bounds.X <= 5 && bounds.Y <= 5)
        {
            return;
        }

        // - new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * 0.5f * _gameRenderScale
        var entitySize = entityInstance.Size.ToVec2() * _cameraZoom;
        var gridSize = new Vector2(layerDef.GridSize, layerDef.GridSize) * _cameraZoom;
        var iconSize = gridSize;
        var iconMin = boundsMin - ((gridSize - entitySize) * entityDef.Pivot).ToNumerics();
        var iconMax = iconMin + iconSize.ToNumerics();

        fillColor = fillColor.MultiplyAlpha(entityDef.FillOpacity);
        fillColor = isSelectedLayer ? fillColor : fillColor.MultiplyAlpha(DeselectedLayerAlpha);
        iconTint = isSelectedLayer ? iconTint : iconTint.MultiplyAlpha(DeselectedLayerAlpha);
        if (entityDef.Identifier != "Light")
            dl->AddRectFilled(boundsMin, boundsMax, fillColor.PackedValue);
        dl->AddImage((void*)sprite.TextureSlice.Texture.Handle, iconMin, iconMax, uvMin, uvMax, iconTint.PackedValue);

        if (isSelectedLayer)
        {
            ImGui.SetCursorScreenPos(boundsMin);
            var idStr = "EntityButton";
            var id = ImGui.GetID(idStr);
            var buttonSize = boundsMax - boundsMin;

            // if (!ImGui.IsMouseHoveringRect(boundsMin, boundsMax))
            // return;


            if (ImGui.InvisibleButton(idStr, buttonSize.EnsureNotZero(), (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap))
            {
            }

            // if (ImGui.GetCurrentContext()->ActiveId != id)
            ImGui.SetItemAllowOverlap();

            if (ImGui.IsItemActivated())
            {
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift))
                {
                    var instance = DuplicateInstance(entityInstance, entityDef);
                    layer.EntityInstances.Add(instance);

                    // swap duplicate with this
                    (layer.EntityInstances[index], layer.EntityInstances[^1]) = (layer.EntityInstances[^1], layer.EntityInstances[index]);
                }

                {
                    _selectedEntityInstanceIndex = index;

                    entityInstance.Position = SnapToGrid(entityInstance, entityDef, layerDef.GridSize);
                    _startPos = entityInstance.Position;
                    _isInstancePopupOpen = true;
                }
            }

            if (ImGui.IsItemActive() && ImGui.GetMouseDragDelta().LengthSquared() >= 2f * 2f)
            {
                var gridDelta = ImGui.GetMouseDragDelta() / _cameraZoom / layerDef.GridSize;
                var snapped = new Point(
                    (int)(Math.Round(gridDelta.X) * layerDef.GridSize),
                    (int)(Math.Round(gridDelta.Y) * layerDef.GridSize)
                );
                entityInstance.Position = _startPos + snapped;
            }

            if (_selectedEntityInstanceIndex == index)
            {
                // dl->AddRect(boundsMin, boundsMax, Color.CornflowerBlue.PackedValue, 0, ImDrawFlags.None, _gameRenderScale * 2f);
                var t = 0; //((MathF.Sin(Shared.Game.Time.TotalElapsedTime) + 1.0f) * 0.5f);
                var rectOffset = new Num.Vector2(t * _cameraZoom * 2f);
                ImGuiExt.AddRectDashed(
                    dl,
                    boundsMin - rectOffset, boundsMax + rectOffset,
                    ImGuiExt.Colors[0].PackedValue,
                    _cameraZoom * 1f,
                    (int)(_cameraZoom * 10f),
                    0.5f,
                    true
                );
                HandleEntityResize(layerDef.GridSize, boundsMin, boundsMax, entityInstance, entityDef);

                // DrawMoveEntityButton(entityInstance, boundsMin, boundsMax);
            }
            else if (ImGui.IsItemHovered())
            {
                dl->AddRect(boundsMin, boundsMax, ImGuiExt.Colors[0].MultiplyAlpha(0.66f).PackedValue, 0, ImDrawFlags.None, _cameraZoom * 1f);
            }

            /*if (ImGui.BeginPopupContextItem("EntityInstanceContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
                {
                    if (ImGui.MenuItem("Remove", default))
                        instanceToRemove = k;

                    ImGui.EndPopup();
                }*/
        }

        var fieldYOffset = 1;
        for (var j = 0; j < entityInstance.FieldInstances.Count; j++)
        {
            var fieldInstance = entityInstance.FieldInstances[j];

            // Draw blinking rect if there are field instances without matching field definitions
            if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
            {
                DrawWarningRect(dl, level.WorldPos, entityInstance.Position, entityInstance.Size);
            }
            else if (fieldDef.EditorDisplayMode != EditorDisplayMode.Hidden && isSelectedLayer)
            {
                if (fieldDef.DefaultValue!.Equals(fieldInstance.Value))
                    continue;
                var valueStr = fieldInstance.Value?.ToString() ?? "NULL";
                var fieldStr = fieldDef.Identifier + " = " + valueStr;
                var fieldPadding = new Num.Vector2(4, 2);
                var fieldSize = ImGui.CalcTextSize(fieldStr) + fieldPadding;
                var fieldPos = boundsMin + new Num.Vector2((boundsMax.X - boundsMin.X) * 0.5f, 0) -
                               new Num.Vector2(fieldSize.X * 0.5f, fieldSize.Y * fieldYOffset);
                dl->AddRectFilled(fieldPos, fieldPos + fieldSize, Color.Black.MultiplyAlpha(0.5f).PackedValue, 0, ImDrawFlags.None);
                dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), ImGui.GetFontSize(), fieldPos + fieldPadding * 0.5f, Color.White.PackedValue, fieldStr);
                fieldYOffset++;
            }
        }

        if (entityDef.Identifier == "Light")
        {
            var angleDef = entityDef.FieldDefinitions.FirstOrDefault(x => x.Identifier == "Angle");
            var coneAngleDef = entityDef.FieldDefinitions.FirstOrDefault(x => x.Identifier == "ConeAngle");
            var intensityDef = entityDef.FieldDefinitions.FirstOrDefault(x => x.Identifier == "Intensity");
            if (angleDef != null && coneAngleDef != null && intensityDef != null)
            {
                var angleInstance = entityInstance.FieldInstances.FirstOrDefault(x => x.FieldDefId == angleDef.Uid);
                var coneAngleInstance = entityInstance.FieldInstances.FirstOrDefault(x => x.FieldDefId == coneAngleDef.Uid);
                var intensityInstance = entityInstance.FieldInstances.FirstOrDefault(x => x.FieldDefId == intensityDef.Uid);

                if (angleInstance != null && coneAngleInstance != null && intensityInstance != null)
                {
                    var angle = (float)angleInstance.Value! * MathF.Deg2Rad;
                    var coneAngle = (float)coneAngleInstance.Value! * MathF.Deg2Rad;
                    var intensity = (float)intensityInstance.Value!;

                    var center = GetWorldPosInScreen(level.WorldPos + entityInstance.Position + entityInstance.Size.ToVec2() * 0.5f);
                    var radius = entityInstance.Size.X * 0.5f * _cameraZoom;

                    ImGuiExt.DrawCone(dl, center, coneAngle, angle, radius, fillColor.MultiplyAlpha(intensity));
                }
            }
        }
    }

    private static EntityInstance DuplicateInstance(EntityInstance entityInstance, EntityDef entityDef)
    {
        var serialized = JsonConvert.SerializeObject(entityInstance, ContentManager.JsonSerializerSettings);
        var copy = JsonConvert.DeserializeObject<EntityInstance>(serialized, ContentManager.JsonSerializerSettings) ?? throw new Exception();
        copy.Iid = Guid.NewGuid();
        RootJson.EnsureFieldsAreValid(copy, entityDef);
        return copy;
    }

    private static void DrawMoveLevelButton(Level level, Num.Vector2 boundsMin, Num.Vector2 boundsMax)
    {
        var moveButtonSize = new Num.Vector2(10, 10) * _cameraZoom;
        ImGui.SetCursorScreenPos(
            boundsMin + new Num.Vector2((boundsMax.X - boundsMin.X) * 0.5f, 0) - new Num.Vector2(moveButtonSize.X * 0.5f, 3 * moveButtonSize.Y));

        var wasHovered = ImGui.GetCurrentContext()->HoveredIdPreviousFrame == ImGui.GetID("##MoveLevel");
        if (wasHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        }

        if (ImGuiExt.ColoredButton("##MoveLevel", Color.White, Color.Black.MultiplyAlpha(wasHovered ? 1.0f : 0.66f), moveButtonSize, "Move"))
        {
        }

        var dl = ImGui.GetWindowDrawList();
        var iconText = FontAwesome6.ArrowsUpDownLeftRight;
        var iconSize = ImGui.GetFontSize() * _cameraZoom * 0.33f;
        var iconPos = ImGui.GetItemRectMin() + ImGui.GetItemRectSize() * 0.5f - new Num.Vector2(iconSize * 0.45f, iconSize * 0.5f);
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.Medium), iconSize, iconPos, Color.White.PackedValue, iconText, 0, default);

        if (ImGui.IsItemActivated())
        {
            _moveLevelStart = level.WorldPos;
        }

        if (ImGui.IsItemActive())
        {
            var gridSize = ((MyEditorMain)Shared.Game).RootJson.DefaultGridSize;
            var (newPos, _) = SnapToGrid(_moveLevelStart + (ImGui.GetMouseDragDelta() / _cameraZoom).ToPoint(), gridSize);
            level.WorldPos = newPos.ToPoint();
        }
    }

    private static void DrawMoveEntityButton(EntityInstance entity, Num.Vector2 boundsMin, Num.Vector2 boundsMax)
    {
        var moveButtonSize = new Num.Vector2(10, 10) * _cameraZoom;
        ImGui.SetCursorScreenPos(
            boundsMin + new Num.Vector2((boundsMax.X - boundsMin.X) * 0.5f, 0) - new Num.Vector2(moveButtonSize.X * 0.5f, 2 * moveButtonSize.Y));
        if (ImGuiExt.ColoredButton(FontAwesome6.ArrowsUpDownLeftRight, Color.White, Color.Black, moveButtonSize, "Move"))
        {
        }

        if (ImGui.IsItemActivated())
        {
            _moveEntityStart = entity.Position;
        }

        if (ImGui.IsItemActive())
        {
            var gridSize = ((MyEditorMain)Shared.Game).RootJson.DefaultGridSize;
            var (newPos, _) = SnapToGrid(_moveEntityStart + (ImGui.GetMouseDragDelta() / _cameraZoom).ToPoint(), gridSize);
            entity.Position = newPos.ToPoint();
        }
    }

    private static void HandleEntityResize(uint gridSize, Num.Vector2 min, Num.Vector2 max, EntityInstance entityInstance, EntityDef entityDef)
    {
        var state = _resizeEditorEntity.Draw(min, max, 5f, entityDef.ResizableX, entityDef.ResizableY);

        if (state == ToolState.Started)
        {
            _resizeEntityStartPos = entityInstance.Position;
            if (entityDef.KeepAspectRatio)
            {
                var instanceAr = entityInstance.Width / (float)entityInstance.Height;
                var defAr = entityDef.Width / (float)entityDef.Height;
                if (MathF.NotApprox(instanceAr, defAr))
                {
                    entityInstance.Height = (uint)(defAr * entityInstance.Width);
                }
            }

            _resizeEntityStartSize = entityInstance.Size;
        }

        if (_resizeEditorEntity.State == ToolState.Started ||
            _resizeEditorEntity.State == ToolState.Active ||
            _resizeEditorEntity.State == ToolState.Ended)
        {
            var totSizeDelta = _resizeEditorEntity.TotSizeDelta.ToVec2();
            if (entityDef.KeepAspectRatio)
            {
                var ar = entityDef.Width / (float)entityDef.Height;
                if (ResizeEditorTool.IsXHandle(_resizeEditorEntity.ActiveHandle))
                    totSizeDelta.Y = totSizeDelta.X * ar;
                else
                    totSizeDelta.X = totSizeDelta.Y / ar;
            }

            var sizeDelta = totSizeDelta / _cameraZoom;
            var gridSizeDelta = (sizeDelta / gridSize).Round();
            var newSize = _resizeEntityStartSize + gridSizeDelta * gridSize;

            if (newSize != entityInstance.Size && newSize.X > 0 && newSize.Y > 0)
            {
                var startPos = _resizeEntityStartPos;
                var newX = _resizeEditorEntity.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft => (int)(startPos.X - (gridSizeDelta.X * gridSize)),
                    _ => entityInstance.Position.X,
                };

                var newY = _resizeEditorEntity.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Top or RectHandlePos.TopRight => (int)(startPos.Y - (gridSizeDelta.Y * gridSize)),
                    _ => entityInstance.Position.Y,
                };

                // scaling from center
                if (ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt))
                {
                    newX = (int)(startPos.X - (gridSizeDelta.X * gridSize) * 0.5f);
                    newY = (int)(startPos.Y - (gridSizeDelta.Y * gridSize) * 0.5f);
                }

                var newPos = new Point(newX, newY);

                entityInstance.Position = newPos;
                entityInstance.Size = newSize.ToUPoint();
            }
        }
    }

    private static void DrawWarningRect(ImDrawList* dl, Vector2 levelPos, Vector2 position, Vector2 size)
    {
        var editor = (MyEditorMain)Shared.Game;
        var scale = (MathF.Sin(editor.Time.TotalElapsedTime * 3f) + 1.0f) * 0.5f;
        var rectScale = scale * 0.1f + 1.0f; // 1.0 - 1.1
        var center = levelPos + position + size * 0.5f;
        var min = GetWorldPosInScreen(center - size * 0.5f * rectScale);
        var max = GetWorldPosInScreen(center + size * 0.5f * rectScale);
        dl->AddRect(min, max, Color.Red.MultiplyAlpha(0.1f).PackedValue, 0, ImDrawFlags.None, _cameraZoom * 10f);
    }

    private static void DrawIntGridLayer(ImDrawList* dl, Level level, LayerDef layerDef, LayerInstance layer, bool isSelected)
    {
        var cols = (int)(level.Width / layerDef.GridSize);
        // var rows = (int)(level.Height / layerDef.GridSize);

        var contentMin = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMax();

        for (var j = 0; j < layer.IntGrid.Length; j++)
        {
            var cellValue = layer.IntGrid[j];
            if (cellValue == 0)
                continue;

            GetIntDef(layerDef, cellValue, out var intDef);
            var color = intDef?.Color ?? Color.Red;
            if (!isSelected)
                color = color.MultiplyAlpha(DeselectedLayerAlpha);

            color = color.MultiplyAlpha(IntGridAlpha);

            var (x, y) = (j % cols, j / cols);
            var tilePos = new Vector2(x, y) * layerDef.GridSize;
            var iconMin = GetWorldPosInScreen(level.WorldPos + tilePos);
            var iconMax = GetWorldPosInScreen(level.WorldPos + tilePos + new Vector2(layerDef.GridSize));
            if (iconMin.X > contentMax.X || iconMin.Y > contentMax.Y ||
                iconMax.X < contentMin.X || iconMax.Y < contentMin.Y)
            {
                continue;
            }

            dl->AddRectFilled(iconMin, iconMax, color.PackedValue);
        }
    }

    private static (Vector2 snapped, Point cell, Vector2 mouseInLevel) GetMouseInLevel(Level level)
    {
        var mouseInWorld = GetScreenPosInWorld(ImGui.GetMousePos()) - level.WorldPos;
        var mouseInLevel = mouseInWorld;
        var (snapped, cell) = SnapToGrid(mouseInLevel, ((MyEditorMain)Shared.Game).RootJson.DefaultGridSize);
        return (snapped, cell, mouseInLevel);
    }

    private static bool IsMouseInLevelBounds(Level lvl)
    {
        var (_, mouseCell, _) = GetMouseInLevel(lvl);
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        return mouseCell.X >= 0 && mouseCell.X < lvl.Width / root.DefaultGridSize &&
               mouseCell.Y >= 0 && mouseCell.Y < lvl.Height / root.DefaultGridSize;
    }

    private static void DrawMouse(ButtonState buttonState)
    {
        if (MyEditorMain.ActiveInput != ActiveInput.EditorWindow)
            return;

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (!layerInstance.IsVisible)
            return;

        if (!IsMouseInLevelBounds(level))
            return;

        if (_isInstancePopupOpen)
            return;

        var dl = ImGui.GetWindowDrawList();

        var (mouseSnappedToGrid, mouseCell, mouseInLevel) = GetMouseInLevel(level);

        if (layerDef.LayerType == LayerType.Entities)
        {
            if (EntityDefinitions.Count == 0)
                return;

            if (_selectedEntityDefinitionIndex > EntityDefinitions.Count - 1)
            {
                SelectFirstEntityDefinition();
                return;
            }

            var entityDef = EntityDefinitions.ByIndex(_selectedEntityDefinitionIndex);

            if (IsExcluded(entityDef, layerDef))
            {
                SelectFirstEntityDefinition();
                return;
            }

            if (!GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef))
                return;

            if (_isAdding)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _isAdding = false;
                    _lastAddedCell = new Point(-1, -1);
                    return;
                }

                if (_hoveringEntities.Count == 0)
                {
                    var mouseGridPoint = mouseSnappedToGrid.ToPoint();
                    if (mouseGridPoint == _lastAddedCell)
                        return;

                    var entityInstance = EntityDef.CreateEntityInstance(entityDef);
                    entityInstance.Position = mouseGridPoint;
                    entityInstance.Position = SnapToGrid(entityInstance, entityDef, layerDef.GridSize);

                    var entityInstanceCell = Entity.ToCell(entityInstance.Position);

                    GetEntitiesInCell(entityInstanceCell, layerInstance.EntityInstances, _tempEntityList);
                    foreach (var entity in _tempEntityList)
                    {
                        layerInstance.EntityInstances.Remove(entity);
                    }

                    layerInstance.EntityInstances.Add(entityInstance);
                    _lastAddedCell = mouseGridPoint;

                    Logs.LogInfo($"Added {entityDef.Identifier} at: {_lastAddedCell.ToString()} ({entityInstanceCell})");
                }

                return;
            }

            if (_hoveringEntities.Count > 0)
            {
                if (!ImGui.IsAnyItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
                {
                    for (var i = 0; i < _hoveringEntities.Count; i++)
                    {
                        layerInstance.EntityInstances.Remove(_hoveringEntities[i]);
                    }

                    _hoveringEntities.Clear();
                }

                return;
            }

            {
                if (!ImGui.IsAnyItemHovered())
                {
                    // draw preview of entity being added
                    if (!SplitWindow.GetTileSetTexture(tileSetDef.Path, out var texture))
                        return;

                    var sprite = LevelRenderer.GetTileSprite(texture, entityDef.TileId, tileSetDef);
                    var uvMin = sprite.UV.TopLeft.ToNumerics();
                    var uvMax = sprite.UV.BottomRight.ToNumerics();
                    var iconMin = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid);
                    var iconMax = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid + new Vector2(layerDef.GridSize));
                    var color = Color.White.MultiplyAlpha(0.33f);
                    dl->AddImage((void*)sprite.TextureSlice.Texture.Handle, iconMin, iconMax, uvMin, uvMax, color.PackedValue);

                    if (!ImGui.IsAnyItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        _isAdding = true;
                        Logs.LogInfo($"Started Adding: {Shared.Game.Time.UpdateCount}");
                    }
                }
            }

            return;
        }

        {
            if (layerDef.LayerType != LayerType.IntGrid)
                return;

            if (_selectedIntGridValueIndex > layerDef.IntGridValues.Count - 1)
                return;

            var intGridValue = layerDef.IntGridValues[_selectedIntGridValueIndex];

            var iconMin = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid);
            var iconMax = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid + new Vector2(layerDef.GridSize));

            var color = intGridValue.Color.MultiplyAlpha(0.33f);
            dl->AddRectFilled(iconMin, iconMax, color.PackedValue);

            var cols = (int)(level.Width / layerDef.GridSize);
            // var rows = (int)(level.Height / layerDef.GridSize);
            var cellIndex = (int)mouseCell.Y * cols + (int)mouseCell.X;
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                layerInstance.IntGrid[cellIndex] = intGridValue.Value;
            }
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                layerInstance.IntGrid[cellIndex] = 0;
            }

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                ApplyIntGridAutoRules();
        }
    }

    private static void DrawGrid(ImDrawList* dl, Num.Vector2 min, Num.Vector2 max, float gridSize, Color lineColor, float thickness)
    {
        var startX = min.X;
        var endX = max.X;
        var startY = min.Y;
        var endY = max.Y;

        // vertical lines
        for (var x = startX; x < endX; x += gridSize)
        {
            var lineStart = new Num.Vector2(x, startY);
            var lineEnd = new Num.Vector2(lineStart.X, endY);
            dl->AddLine(lineStart, lineEnd, lineColor.PackedValue, thickness);
        }

        // horizontal lines
        for (var y = startY; y < endY; y += gridSize)
        {
            var lineStart = new Num.Vector2(startX, y);
            var lineEnd = new Num.Vector2(endX, lineStart.Y);
            dl->AddLine(lineStart, lineEnd, lineColor.PackedValue, thickness);
        }
    }

    #region Getters

    public static bool GetSelectedLevel([NotNullWhen(true)] out Level? level)
    {
        level = null;
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        if (WorldsWindow.SelectedWorldIndex > root.Worlds.Count - 1)
            return false;

        var world = root.Worlds[WorldsWindow.SelectedWorldIndex];
        if (LevelsWindow.SelectedLevelIndex > world.Levels.Count - 1)
            return false;

        level = world.Levels[LevelsWindow.SelectedLevelIndex];
        return true;
    }

    private static bool GetFieldDef(EntityDef entityDef, int fieldDefId, [NotNullWhen(true)] out FieldDef? fieldDef)
    {
        for (var i = 0; i < entityDef.FieldDefinitions.Count; i++)
        {
            if (entityDef.FieldDefinitions[i].Uid == fieldDefId)
            {
                fieldDef = entityDef.FieldDefinitions[i];
                return true;
            }
        }

        fieldDef = null;
        return false;
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

    private static bool GetEntityDef(int uid, [NotNullWhen(true)] out EntityDef? entityDef)
    {
        for (var i = 0; i < EntityDefinitions.Count; i++)
        {
            if (EntityDefinitions.ByIndex(i).Uid == uid)
            {
                entityDef = EntityDefinitions.ByIndex(i);
                return true;
            }
        }

        entityDef = null;
        return false;
    }

    private static bool GetLayerDefinition(long layerDefUid, [NotNullWhen(true)] out LayerDef? layerDef)
    {
        var editor = (MyEditorMain)Shared.Game;
        for (var j = 0; j < editor.RootJson.LayerDefinitions.Count; j++)
        {
            if (editor.RootJson.LayerDefinitions[j].Uid == layerDefUid)
            {
                layerDef = editor.RootJson.LayerDefinitions[j];
                return true;
            }
        }

        layerDef = null;
        return false;
    }

    private static bool GetSelectedWorld([NotNullWhen(true)] out WorldsRoot.World? selectedWorld)
    {
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        if (WorldsWindow.SelectedWorldIndex <= root.Worlds.Count - 1)
        {
            selectedWorld = root.Worlds[WorldsWindow.SelectedWorldIndex];
            return true;
        }

        selectedWorld = null;
        return false;
    }

    private static void GetEntitiesAtPosition(Vector2 position, List<EntityInstance> entities, List<EntityInstance> results)
    {
        results.Clear();

        for (var i = 0; i < entities.Count; i++)
        {
            var instance = entities[i];
            if (position.X >= instance.Position.X &&
                position.X < instance.Position.X + instance.Size.X &&
                position.Y >= instance.Position.Y &&
                position.Y < instance.Position.Y + +instance.Size.Y)
            {
                results.Add(instance);
            }
        }
    }

    private static void GetEntitiesInCell(Point cell, List<EntityInstance> entities, List<EntityInstance> results)
    {
        results.Clear();

        for (var i = 0; i < entities.Count; i++)
        {
            var instance = entities[i];
            var (minCell, maxCell) = Entity.GetMinMaxCell(instance.Position, instance.Size);
            for (var y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (var x = minCell.X; x <= maxCell.X; x++)
                {
                    if (x == cell.X && y == cell.Y)
                    {
                        results.Add(instance);
                    }
                }
            }
        }
    }

    private static int GetEntityAtPosition(Vector2 position, List<EntityInstance> entities, out EntityInstance? entity)
    {
        for (var i = 0; i < entities.Count; i++)
        {
            var instance = entities[i];
            if (position.X >= instance.Position.X &&
                position.X < instance.Position.X + instance.Size.X &&
                position.Y >= instance.Position.Y &&
                position.Y < instance.Position.Y + +instance.Size.Y)
            {
                entity = instance;
                return i;
            }
        }

        entity = null;
        return -1;
    }

    private static bool GetTileSetDef(int uid, [NotNullWhen(true)] out TileSetDef? tileSetDef)
    {
        var editor = (MyEditorMain)Shared.Game;
        var root = editor.RootJson;
        for (var i = 0; i < root.TileSetDefinitions.Count; i++)
        {
            if (root.TileSetDefinitions[i].Uid == uid)
            {
                tileSetDef = root.TileSetDefinitions[i];
                return true;
            }
        }

        tileSetDef = null;
        return false;
    }

    private static bool GetSelectedEntityInstance([NotNullWhen(true)] out EntityInstance? instance)
    {
        instance = null;
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return false;

        if (_selectedEntityInstanceIndex == -1 || _selectedEntityInstanceIndex > layerInstance.EntityInstances.Count - 1)
            return false;

        instance = layerInstance.EntityInstances[_selectedEntityInstanceIndex];
        return true;
    }

    #endregion
}
