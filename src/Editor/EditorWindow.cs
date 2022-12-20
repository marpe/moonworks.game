using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

public unsafe class EditorWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "EditorTabs";
    private const string PreviewWindowTitle = "Editor";
    private static int _rowMinHeight = 60;

    [CVar("editor.deselected_layer_alpha", "")]
    public static float DeselectedLayerAlpha = 0.1f;

    [CVar("editor.int_grid_alpha", "")] public static float IntGridAlpha = 0.1f;

    private readonly EntityDefWindow _entityDefWindow;
    private readonly LayerDefWindow _layerDefWindow;
    private readonly LevelsWindow _levelsWindow;
    private readonly TileSetDefWindow _tileSetDefWindow;
    private readonly WorldsWindow _worldsWindow;

    private Dictionary<(int groupUid, int ruleUid), Dictionary<(int x, int y), AutoRuleTile>> _autoTileCache = new();
    private MyEditorMain _editor;

    private float _cameraMinZoom = 0.1f;

    /// <summary>User panning offset</summary>
    private static Num.Vector2 _gameRenderPosition = Num.Vector2.Zero;

    /// <summary>User zoom</summary>
    private static float _gameRenderScale = 1f;

    private int _prevSelectedEntityInstanceIndex;
    private int _selectedEntityDefinitionIndex;
    private int _selectedEntityInstanceIndex = -1;
    private int _selectedIntGridValueIndex;
    private int _selectedLayerInstanceIndex;

    public Matrix4x4 PreviewRenderViewportTransform = Matrix4x4.Identity;

    [CVar("editor.background_color", "")] public static Color BackgroundColor = Color.Black;

    [CVar("editor.stripe_color", "")] public static Color StripeColor = new Color(1.0f, 1.0f, 1.0f, 0.1f);
    private Num.Vector2 _renderSize;

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

    private bool GetSelectedLayerInstance([NotNullWhen(true)] out WorldsRoot.World? world, [NotNullWhen(true)] out Level? level,
        [NotNullWhen(true)] out LayerInstance? layerInstance, [NotNullWhen(true)] out LayerDef? layerDef)
    {
        layerInstance = null;
        layerDef = null;
        level = null;
        world = null;

        if (WorldsWindow.SelectedWorldIndex > _editor.RootJson.Worlds.Count - 1)
        {
            return false;
        }

        world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
        if (LevelsWindow.SelectedLevelIndex > world.Levels.Count - 1)
        {
            return false;
        }

        level = world.Levels[LevelsWindow.SelectedLevelIndex];

        if (_selectedLayerInstanceIndex > level.LayerInstances.Count - 1)
        {
            return false;
        }

        var instance = level.LayerInstances[_selectedLayerInstanceIndex];
        var def = _editor.RootJson.LayerDefinitions.FirstOrDefault(x => x.Uid == instance.LayerDefId);
        if (def == null)
        {
            return false;
        }

        layerInstance = instance;
        layerDef = def;
        return true;
    }

    /*public void Update(float deltaSeconds)
    {
        if (!GetSelectedLevel(out var selectedLevel))
            return;

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (!layerInstance.IsVisible)
            return;

        var (mouseWorld, mouseLevel, mouseGrid) = GetMousePositions();
        var cols = (int)(level.Width / layerDef.GridSize);
        var rows = (int)(level.Height / layerDef.GridSize);

        _mouseIsInLevelBounds = mouseGrid.X >= 0 &&
                                mouseGrid.X < cols &&
                                mouseGrid.Y >= 0 &&
                                mouseGrid.Y < rows;

        var leftMouseDown = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Left);
        var rightMouseDown = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Right);
        var middleMouseDown = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Middle);
        if (!_mouseIsInLevelBounds)
        {
            if (rightMouseDown)
            {
                for (var i = 0; i < world.Levels.Count; i++)
                {
                    var lvl = world.Levels[i];
                    if (mouseWorld.X >= lvl.WorldPos.X && mouseWorld.X < lvl.WorldPos.X + lvl.Size.X &&
                        mouseWorld.Y >= lvl.WorldPos.Y && mouseWorld.Y < lvl.WorldPos.Y + lvl.Size.Y)
                    {
                        LevelsWindow.SelectedLevelIndex = i;
                        _levelChanged = true;
                        return;
                    }
                }
            }

            return;
        }

        if (_levelChanged && !rightMouseDown)
            _levelChanged = false;

        if (_levelChanged)
            return;

        switch (layerDef.LayerType)
        {
            case LayerType.IntGrid:

                if (leftMouseDown || rightMouseDown)
                {
                    var cellIndex = (int)mouseGrid.Y * cols + (int)mouseGrid.X;
                    if (cellIndex > layerInstance.IntGrid.Length - 1)
                        break;
                    if (_selectedIntGridValueIndex > layerDef.IntGridValues.Count - 1)
                        break;

                    var value = leftMouseDown ? layerDef.IntGridValues[_selectedIntGridValueIndex].Value : 0;
                    layerInstance.IntGrid[cellIndex] = value;
                }

                if (_editor.InputHandler.IsMouseButtonReleased(MouseButtonCode.Left) ||
                    _editor.InputHandler.IsMouseButtonReleased(MouseButtonCode.Right))
                    ApplyIntGridAutoRules();

                break;
            case LayerType.Entities:
            {
                var rightMousePressed = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Right);
                if (rightMousePressed)
                {
                    var index = GetEntityAtPosition(mouseLevel, layerDef.GridSize, layerInstance.EntityInstances, out _);
                    if (index != -1)
                        layerInstance.EntityInstances.RemoveAt(index);

                    break;
                }

                var leftMousePressed = _editor.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left);
                if (leftMousePressed)
                {
                    var selectedEntityInstanceIndex =
                        GetEntityAtPosition(mouseLevel, layerDef.GridSize, layerInstance.EntityInstances, out var selectedEntityInstance);
                    if (selectedEntityInstanceIndex != -1)
                    {
                        _selectedEntityInstanceIndex = selectedEntityInstanceIndex;
                        break;
                    }

                    if (_selectedEntityDefinitionIndex > _editor.RootJson.EntityDefinitions.Count - 1)
                        break;

                    var entityDef = _editor.RootJson.EntityDefinitions[_selectedEntityDefinitionIndex];

                    if (IsExcluded(entityDef, layerDef))
                        break;

                    var prevEntityIndex = GetEntityAtPosition(mouseLevel, layerDef.GridSize, layerInstance.EntityInstances, out _);
                    if (prevEntityIndex != -1)
                        layerInstance.EntityInstances.RemoveAt(prevEntityIndex);

                    var instance = new EntityInstance()
                    {
                        Position = new Point((int)(mouseGrid.X * layerDef.GridSize), (int)(mouseGrid.Y * layerDef.GridSize)),
                        Width = entityDef.Width,
                        Height = entityDef.Height,
                        EntityDefId = entityDef.Uid,
                    };

                    foreach (var fieldDef in entityDef.FieldDefinitions)
                    {
                        instance.FieldInstances.Add(
                            new FieldInstance
                            {
                                Value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray),
                                FieldDefId = fieldDef.Uid
                            }
                        );
                    }

                    layerInstance.EntityInstances.Add(instance);
                }

                break;
            }
            case LayerType.Tiles:
                break;
            case LayerType.AutoLayer:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }*/


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
            ImGuiInternal.DockBuilderDockWindow("CurrentLevelWindow", dockSpaceId);
        }

        var shouldDrawContent = ImGuiExt.BeginWorkspaceWindow(WindowTitle, "EditorDockSpace", InitializeLayout, ImGuiExt.RefPtr(ref IsOpen), ref windowClass);

        if (shouldDrawContent)
        {
            _entityDefWindow.Draw();
            _layerDefWindow.Draw();
            _levelsWindow.Draw();
            _tileSetDefWindow.Draw();
            _worldsWindow.Draw();
        }

        DrawEditorWindow();

        DrawSelectedEntityInstancePopup();

        DrawCurrentLevelData();
    }

    private void DrawSelectedEntityInstancePopup()
    {
        if (_selectedEntityInstanceIndex == -1)
            return;

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (_selectedEntityInstanceIndex > layerInstance.EntityInstances.Count - 1)
            return;

        var selectedInstance = layerInstance.EntityInstances[_selectedEntityInstanceIndex];

        var popupName = "SelectedEntityInstance";
        if (_prevSelectedEntityInstanceIndex != _selectedEntityInstanceIndex)
            ImGui.OpenPopup(popupName);

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 0), ImGuiCond.Always);

        var worldInScreen = level.WorldPos + selectedInstance.Position + new Vector2(0, layerDef.GridSize) - (_gameRenderPosition * _gameRenderScale).ToXNA();
        worldInScreen = Vector2.Transform(worldInScreen, PreviewRenderViewportTransform);
        var windowPos = ImGui.GetMainViewport()->Pos + worldInScreen.ToNumerics();
        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, System.Numerics.Vector2.Zero);
        if (ImGui.BeginPopup(popupName))
        {
            if (GetEntityDef(selectedInstance.EntityDefId, out var entityDef))
            {
                ImGui.BeginDisabled();
                SimpleTypeInspector.InspectString("Identifier", ref entityDef.Identifier);
                ImGui.EndDisabled();

                SimpleTypeInspector.InspectPoint("Position", ref selectedInstance.Position);

                var tmpPoint = (Point)selectedInstance.Size;
                if (ImGuiExt.InspectPoint("Size", ref tmpPoint.X, ref tmpPoint.Y, "W", "Width", "H", "Height", 1, 1, 512))
                {
                    selectedInstance.Width = (uint)tmpPoint.X;
                    selectedInstance.Height = (uint)tmpPoint.Y;
                }

                FieldInstanceInspector.DrawFieldInstances(selectedInstance.FieldInstances, entityDef.FieldDefinitions);
            }
            else
            {
                ImGui.TextDisabled($"Could not find a entity definition with id \"{selectedInstance.EntityDefId}\"");
            }

            ImGui.EndPopup();
        }

        if (!ImGui.IsPopupOpen(popupName))
        {
            _selectedEntityInstanceIndex = -1;
        }

        _prevSelectedEntityInstanceIndex = _selectedEntityInstanceIndex;
    }

    private void DrawCurrentLevelData()
    {
        var windowFlags = ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin("CurrentLevelWindow", default, windowFlags))
        {
            DrawLayersInSelectedLevel();

            DrawCleanButton();

            DrawAutoRuleButton();

            SimpleTypeInspector.InspectFloat("Deselected Layer Alpha", ref DeselectedLayerAlpha, new RangeSettings(0, 1.0f, 0.1f, false));
            SimpleTypeInspector.InspectFloat("IntGrid Alpha", ref IntGridAlpha, new RangeSettings(0, 1.0f, 0.1f, false));
            SimpleTypeInspector.InspectColor("BackgroundColor", ref BackgroundColor);
            SimpleTypeInspector.InspectColor("StripeColor", ref StripeColor);
        }

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

    private void AddRuleTilesAt(int groupUid, AutoRule rule, LayerInstance layerInstance, LayerDef layerDef, TileSetDef tileSetDef, Level level, int x, int y)
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
                TileId = rule.TileIds[0],
                TileSetDefId = tileSetDef.Uid,
                LevelWorldPos = level.WorldPos,
                LayerGridSize = layerDef.GridSize,
            }
        );
    }

    private void DrawAutoRuleButton()
    {
        if (ImGuiExt.ColoredButton("Apply Rules", new System.Numerics.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            ApplyIntGridAutoRules();
        }
    }

    private void ApplyIntGridAutoRules()
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

    private void DrawCleanButton()
    {
        if (ImGuiExt.ColoredButton("Cleanup!", new System.Numerics.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            for (var i = 0; i < _editor.RootJson.Worlds.Count; i++)
            {
                var world = _editor.RootJson.Worlds[i];
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
                            if (entity.Position.X < 0 || entity.Position.X + entity.Size.X > level.Width ||
                                entity.Position.Y < 0 || entity.Position.Y + entity.Size.Y > level.Height)
                            {
                                Logs.LogWarn("Removing entity instance since it's outside the level bounds");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }

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

                            if (IsExcluded(entityDef, layerDef))
                            {
                                Logs.LogWarn("Removing entity instance since the layer it's on excludes entities of this type");
                                layerInstance.EntityInstances.RemoveAt(l);
                                continue;
                            }

                            gridDictionary.Add((cellX, cellY), entity);

                            if (entity.Width != entityDef.Width)
                            {
                                Logs.LogWarn("Resetting entity instance width to entity definition");
                                entity.Width = entityDef.Width;
                            }

                            if (entity.Height != entityDef.Height)
                            {
                                Logs.LogWarn("Resetting entity instance height to entity definition");
                                entity.Height = entityDef.Height;
                            }

                            var snappedX = (int)(entity.Position.X / (float)layerDef.GridSize) * layerDef.GridSize;
                            if (snappedX != entity.Position.X)
                            {
                                Logs.LogWarn("Snapping entity instance x-position to grid");
                                entity.Position.X = (int)snappedX;
                            }

                            var snappedY = (int)(entity.Position.Y / (float)layerDef.GridSize) * layerDef.GridSize;
                            if (snappedY != entity.Position.Y)
                            {
                                Logs.LogWarn("Snapping entity instance y-position to grid");
                                entity.Position.Y = (int)snappedY;
                            }

                            for (var m = entity.FieldInstances.Count - 1; m >= 0; m--)
                            {
                                var fieldInstance = entity.FieldInstances[m];
                                if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
                                {
                                    Logs.LogWarn($"Removing field instance there\'s no field definition with id \"{fieldInstance.FieldDefId}\"");
                                    entity.FieldInstances.RemoveAt(m);
                                }
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

        for (var i = 0; i < _editor.RootJson.LayerDefinitions.Count; i++)
        {
            if (level.LayerInstances.Any(x => x.LayerDefId == _editor.RootJson.LayerDefinitions[i].Uid))
            {
                continue;
            }

            var layerInstance = CreateLayerInstance(_editor.RootJson.LayerDefinitions[i], level);
            level.LayerInstances.Add(layerInstance);
        }

        DrawLayerInstances(level.LayerInstances, _editor.RootJson.LayerDefinitions);

        /*if (ImGuiExt.ColoredButton("Sort Layer Instances", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            SortLevelInstances();
        }*/
    }

    private void SortLevelInstances()
    {
        for (var i = 0; i < _editor.RootJson.Worlds.Count; i++)
        {
            var world = _editor.RootJson.Worlds[i];
            for (var j = 0; j < world.Levels.Count; j++)
            {
                var level = world.Levels[j];
                level.LayerInstances.Sort((a, b) =>
                {
                    var indexA = _editor.RootJson.LayerDefinitions.FindIndex(def => def.Uid == a.LayerDefId);
                    var indexB = _editor.RootJson.LayerDefinitions.FindIndex(def => def.Uid == b.LayerDefId);
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

    private void DrawEditorWindow()
    {
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(1024, 768), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(128, 128), new System.Numerics.Vector2(ImGuiExt.FLT_MAX, ImGuiExt.FLT_MAX));
        var centralNode = ImGuiInternal.DockBuilderGetCentralNode(_editor.ViewportDockSpaceId);
        ImGui.SetNextWindowDockID(centralNode->ID, ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
        if (ImGui.Begin(PreviewWindowTitle, default, ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (ImGui.IsWindowHovered())
            {
                MyEditorMain.ActiveInput = ActiveInput.EditorWindow;
            }

            var dl = ImGui.GetWindowDrawList();
            _renderSize = ImGui.GetContentRegionAvail();
            dl->AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail(), BackgroundColor.PackedValue);
            ImGuiExt.FillWithStripes(dl, new ImRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail()),
                StripeColor.PackedValue);
            DrawWorld();

            var t = ImGui.GetCursorScreenPos() - ImGui.GetWindowViewport()->Pos;
            PreviewRenderViewportTransform = (
                Matrix3x2.CreateScale(_gameRenderScale, _gameRenderScale) *
                Matrix3x2.CreateTranslation(t.X, t.Y)
            ).ToMatrix4x4();

            if (ImGui.IsWindowHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _gameRenderPosition += -ImGui.GetIO()->MouseDelta * 1.0f / _gameRenderScale;
            }

            if (ImGui.IsWindowHovered() && ImGui.GetIO()->MouseWheel != 0)
            {
                _gameRenderScale += ImGui.GetIO()->MouseWheel * 0.1f * _gameRenderScale;
                if (_gameRenderScale < _cameraMinZoom)
                    _gameRenderScale = _cameraMinZoom;
            }

            DrawMouseInfoOverlay();

            // imgui sets WantCaptureKeyboard when an item is active which we don't want for the game window
            if (ImGui.IsWindowHovered() &&
                (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
                 ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
                 ImGui.IsMouseDown(ImGuiMouseButton.Right)))
            {
                ImGui.SetWindowFocus(PreviewWindowTitle);
                ImGui.SetNextFrameWantCaptureKeyboard(false);
            }
        }

        ImGui.PopStyleVar();

        ImGui.End();
    }

    private void DrawMouseInfoOverlay()
    {
        var showOverlay = true;
        if (GameWindow.BeginOverlay("MouseOverlay", ref showOverlay))
        {
            var mouseInWorld = GetMouseInWorld();

            ImGuiExt.PrintVector("MouseInWorld", mouseInWorld.ToXNA());
            ImGuiExt.PrintVector("RenderSize", _renderSize.ToXNA());
            ImGuiExt.PrintVector("WindowSize", ImGui.GetWindowSize().ToXNA());
            ImGuiExt.PrintVector("WorkspaceSize", ImGui.GetWindowViewport()->WorkSize.ToXNA());
            ImGui.Dummy(new System.Numerics.Vector2(400, 0));
        }

        ImGui.End();
    }

    private Num.Vector2 GetMouseInWorld()
    {
        var mousePos = _editor.InputHandler.MousePosition.ToNumerics();
        var center = _renderSize * 0.5f / _gameRenderScale;
        var mouseInWorld = mousePos - center + _gameRenderPosition;
        return mouseInWorld;
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
                var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);

                var isSelected = _selectedLayerInstanceIndex == i;

                var typeColor = LayerDefWindow.GetLayerDefColor(layerDef?.LayerType ?? LayerType.IntGrid);

                var cursorPos = ImGui.GetCursorScreenPos();
                if (SplitWindow.GiantButton("##Selectable", isSelected, typeColor.MultiplyAlpha(0.66f), rowHeight))
                {
                    _selectedLayerInstanceIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
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

        if (_selectedLayerInstanceIndex <= layerInstances.Count - 1)
        {
            var layerInstance = layerInstances[_selectedLayerInstanceIndex];
            var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);
            if (layerDef != null)
            {
                DrawSelectedLayerBrushes(layerDef, _editor.RootJson, ref _selectedIntGridValueIndex, ref _selectedEntityDefinitionIndex);
            }
            else
            {
                ImGui.TextColored(Color.Red.ToNumerics(), $"Could not find a layer definition with Uid \"{layerInstance.LayerDefId}\"");
            }
        }
        else
        {
            ImGui.TextDisabled("No layer selected");
        }
    }

    private static void DrawSelectedLayerBrushes(LayerDef layerDef, RootJson rootJson, ref int selectedIntGridValueIndex,
        ref int selectedEntityDefinitionIndex)
    {
        switch (layerDef.LayerType)
        {
            case LayerType.IntGrid:

                if (layerDef.IntGridValues.Count == 0)
                {
                    ImGui.TextDisabled("No values are defined");
                    break;
                }

                if (ImGui.BeginTable("IntGridTable", 1, SplitWindow.TableFlags, new System.Numerics.Vector2(0, 0)))
                {
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

                        var dl = ImGui.GetWindowDrawList();
                        var rectHeight = _rowMinHeight * 0.6f;
                        var min = cursorPos + new System.Numerics.Vector2(8, (_rowMinHeight - rectHeight) / 2);
                        var max = min + new System.Numerics.Vector2(32, rectHeight);
                        ImGuiExt.RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
                        var label = intGridValue.Value.ToString();
                        var textSize = ImGui.CalcTextSize(label);
                        var rectSize = max - min;
                        dl->AddText(min + new System.Numerics.Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2), Color.White.PackedValue,
                            label);

                        ImGui.SameLine(60);

                        SplitWindow.GiantLabel(intGridValue.Identifier, intGridValue.Color, _rowMinHeight);

                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }

                break;
            case LayerType.Entities:

                var entityDefs = rootJson.EntityDefinitions;

                if (entityDefs.Count == 0)
                {
                    ImGui.TextDisabled("There are no entities defined");
                    break;
                }

                var tileSetDefs = rootJson.TileSetDefinitions;
                var gridSize = rootJson.DefaultGridSize;

                if (ImGui.BeginTable("EntityDefTable", 1, SplitWindow.TableFlags, new System.Numerics.Vector2(0, 0)))
                {
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

                        if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                        {
                            ImGui.MenuItem("Copy", default);
                            ImGui.MenuItem("Cut", default);
                            ImGui.MenuItem("Duplicate", default);
                            ImGui.MenuItem("Delete", default);
                            ImGui.EndPopup();
                        }

                        var tileSet = tileSetDefs.FirstOrDefault(x => x.Uid == entityDef.TileSetDefId);
                        if (tileSet != null)
                        {
                            var dl = ImGui.GetWindowDrawList();
                            var texture = SplitWindow.GetTileSetTexture(tileSet.Path);

                            // TODO (marpe): Replace gridSize with whatever grid size is being used for the tileset
                            var tileSize = new Point((int)(texture.Width / gridSize), (int)(texture.Height / gridSize));
                            var cellX = tileSize.X > 0 ? entityDef.TileId % tileSize.X : 0;
                            var cellY = tileSize.X > 0 ? (int)(entityDef.TileId / tileSize.X) : 0;
                            var uvMin = new System.Numerics.Vector2(1.0f / texture.Width * cellX * gridSize,
                                1.0f / texture.Height * cellY * gridSize);
                            var uvMax = uvMin + new System.Numerics.Vector2(gridSize / (float)texture.Width, gridSize / (float)texture.Height);
                            var iconSize = new System.Numerics.Vector2(32, 32);
                            var iconPos = cursorPos + iconSize / 2;
                            var rectPadding = new System.Numerics.Vector2(4, 4);
                            ImGuiExt.RectWithOutline(
                                dl,
                                iconPos - rectPadding,
                                iconPos + iconSize + rectPadding * 2,
                                entityDef.Color.MultiplyAlpha(0.4f),
                                entityDef.Color,
                                2f
                            );
                            dl->AddImage(
                                (void*)texture.Handle,
                                iconPos,
                                iconPos + iconSize,
                                uvMin,
                                uvMax
                            );
                        }

                        ImGui.SameLine(0, 70f);

                        var labelColor = isSelected ? Color.White.MultiplyAlpha(0.8f) : entityDef.Color;
                        SplitWindow.GiantLabel(entityDef.Identifier, labelColor, _rowMinHeight);

                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }

                break;
            case LayerType.Tiles:
                break;
            case LayerType.AutoLayer:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool IsExcluded(EntityDefinition entityDef, LayerDef layerDef)
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

    /*public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        var commandBuffer = _editor.GraphicsDevice.AcquireCommandBuffer();
        var viewProjection = _camera.GetViewProjection(renderDestination.Width, renderDestination.Height);

        DrawWorld(renderer);

        HighlightSelectedEntityInstance(renderer);

        if (_mouseIsInLevelBounds && MyEditorMain.ActiveInput == ActiveInput.EditorWindow)
            DrawMouse(renderer);

        DrawGrid(renderer, Color.Black * 0.1f, 1.0f / _camera.Zoom);

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection, PipelineType.AlphaBlend);

        _editor.GraphicsDevice.Submit(commandBuffer);
    }*/

    private void DrawWorld()
    {
        if (!GetSelectedWorld(out var world))
            return;

        var dl = ImGui.GetWindowDrawList();

        var cameraOffset = -_gameRenderPosition * _gameRenderScale;
        var renderOffset = ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * 0.5f + cameraOffset;

        for (var i = 0; i < world.Levels.Count; i++)
        {
            ImGui.PushID(i);
            var level = world.Levels[i];

            var levelPosition = renderOffset + level.WorldPos.ToNumerics() * _gameRenderScale;
            var levelSize = new Num.Vector2(level.Size.X, level.Size.Y) * _gameRenderScale;
            dl->AddRectFilled(levelPosition, levelPosition + levelSize, level.BackgroundColor.PackedValue);

            var isSelected = i == LevelsWindow.SelectedLevelIndex;
            if (!isSelected)
            {
                var prevScreenPos = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(levelPosition);
                if (ImGui.InvisibleButton("LevelButton", levelSize))
                {
                    LevelsWindow.SelectedLevelIndex = i;
                }

                ImGui.SetCursorScreenPos(prevScreenPos);
            }

            DrawLayerInstances(dl, level, i == LevelsWindow.SelectedLevelIndex);

            var color = i == LevelsWindow.SelectedLevelIndex ? Color.Green : Color.Red;

            var rectMin = levelPosition;
            var rectMax = levelPosition + levelSize;
            dl->AddRect(rectMin, rectMax, color.PackedValue, 0, ImDrawFlags.None, 4f * _gameRenderScale);

            var gridMin = levelPosition;
            var gridMax = gridMin + levelSize;
            DrawGrid(dl, gridMin, gridMax, _editor.RootJson.DefaultGridSize * _gameRenderScale, Color.Black * 0.1f, _gameRenderScale);

            DrawMouse();

            ImGui.PopID();
        }


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

    private void DrawLayerInstances(ImDrawList* dl, Level level, bool isSelectedLevel)
    {
        for (var i = level.LayerInstances.Count - 1; i >= 0; i--)
        {
            var layer = level.LayerInstances[i];
            if (!layer.IsVisible)
                continue;

            var isSelected = _selectedLayerInstanceIndex == i;
            if (!GetLayerDefinition(layer.LayerDefId, out var layerDef))
                continue;

            if (layerDef.LayerType == LayerType.IntGrid)
            {
                DrawIntGridLayer(dl, level, layerDef, layer, isSelected);

                if (isSelectedLevel)
                {
                    DrawAutoLayerTiles(dl, level, layerDef, layer, isSelected);
                }
            }
            else if (layerDef.LayerType == LayerType.Entities)
            {
                if (isSelectedLevel)
                {
                    DrawEntityLayer(dl, level, layer, layerDef, isSelected);
                }
            }
        }
    }

    private void DrawAutoLayerTiles(ImDrawList* dl, Level level, LayerDef layerDef, LayerInstance layer, bool isSelected)
    {
        if (!GetTileSetDef(layerDef.TileSetDefId, out var tileSetDef))
            return;

        var cameraOffset = -_gameRenderPosition * _gameRenderScale;
        var renderOffset = ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * 0.5f + cameraOffset;
        var levelPosition = renderOffset + level.WorldPos.ToNumerics() * _gameRenderScale;
        var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
        foreach (var tile in layer.AutoLayerTiles)
        {
            var sprite = World.GetTileSprite(texture, tile.TileId, layerDef.GridSize);
            var uvMin = sprite.UV.TopLeft.ToNumerics();
            var uvMax = sprite.UV.BottomRight.ToNumerics();
            var iconMin = levelPosition + (tile.Cell.ToVec2() * layerDef.GridSize * _gameRenderScale).ToNumerics();
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;
            dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax);

            // var origin = entityDef.Pivot * (entityInstance.Size.ToVec2() + new Vector2(layerDef.GridSize));

            // renderer.DrawSprite(sprite, transform, isSelected ? Color.White : Color.White * DeselectedLayerAlpha);
        }
    }


    private void DrawEntityLayer(ImDrawList* dl, Level level, LayerInstance layer, LayerDef layerDef, bool isSelected)
    {
        for (var k = 0; k < layer.EntityInstances.Count; k++)
        {
            var entityInstance = layer.EntityInstances[k];
            var entityDefId = entityInstance.EntityDefId;
            if (!GetEntityDef(entityDefId, out var entityDef))
            {
                DrawWarningRect(dl, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                continue;
            }

            GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef);

            // var sprite = renderer.BlankSprite;
            var sprite = _editor.Renderer.BlankSprite;
            Matrix4x4 entityTransform;
            Color fillColor;
            Color outline;
            var uvMin = Num.Vector2.Zero;
            var uvMax = Num.Vector2.One;
            if (tileSetDef != null)
            {
                fillColor = Color.White;
                outline = entityDef.Color;

                var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
                sprite = World.GetTileSprite(texture, entityDef.TileId, layerDef.GridSize);
                uvMin = sprite.UV.TopLeft.ToNumerics();
                uvMax = sprite.UV.BottomRight.ToNumerics();
            }
            else
            {
                fillColor = entityDef.Color;
                outline = entityDef.Color;
            }

            var cameraOffset = -_gameRenderPosition * _gameRenderScale;
            var renderOffset = ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * 0.5f + cameraOffset;
            var levelPosition = renderOffset + level.WorldPos.ToNumerics() * _gameRenderScale;

            var iconMin = levelPosition + (entityInstance.Position.ToVec2() * _gameRenderScale).ToNumerics();
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;

            fillColor = isSelected ? fillColor : fillColor * DeselectedLayerAlpha;
            dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax, fillColor.PackedValue);

            // Draw blinking rect if there are field instances without matching field definitions
            for (var j = 0; j < entityInstance.FieldInstances.Count; j++)
            {
                var fieldInstance = entityInstance.FieldInstances[j];
                if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
                {
                    DrawWarningRect(dl, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                }
            }
        }
    }

    private void DrawWarningRect(ImDrawList* dl, Vector2 worldPos, uint gridSize, Vector2 position)
    {
        var scale = gridSize + (MathF.Sin(_editor.Time.TotalElapsedTime * 3f) + 1.0f) * 0.5f * gridSize;
        var transform = (
            Matrix3x2.CreateTranslation(-0.5f, -0.5f) *
            Matrix3x2.CreateScale(scale, scale) *
            Matrix3x2.CreateTranslation(
                worldPos.X + position.X + gridSize * 0.5f,
                worldPos.Y + position.Y + gridSize * 0.5f
            )).ToMatrix4x4();
        // TODO (marpe): Fix
        // renderer.DrawSprite(renderer.BlankSprite, transform, Color.Red.MultiplyAlpha(0.33f));
    }

    private static void DrawIntGridLayer(ImDrawList* dl, Level level, LayerDef layerDef, LayerInstance layer, bool isSelected)
    {
        var cols = (int)(level.Width / layerDef.GridSize);
        var rows = (int)(level.Height / layerDef.GridSize);

        var cameraOffset = -_gameRenderPosition * _gameRenderScale;
        var renderOffset = ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * 0.5f + cameraOffset;
        var levelPosition = renderOffset + level.WorldPos.ToNumerics() * _gameRenderScale;

        for (var j = 0; j < layer.IntGrid.Length; j++)
        {
            var cellValue = layer.IntGrid[j];
            if (cellValue == 0)
                continue;


            GetIntDef(layerDef, cellValue, out var intDef);
            var color = intDef?.Color ?? Color.Red;
            if (isSelected)
                color *= 0.5f;

            color *= IntGridAlpha;

            var (x, y) = (j % cols, j / cols);
            var tilePos = new Num.Vector2(x, y) * layerDef.GridSize;
            var iconMin = levelPosition + tilePos * _gameRenderScale;
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;
            dl->AddRectFilled(iconMin, iconMax, color.PackedValue);
        }
    }

    private void DrawMouse()
    {
        Vector2 MouseSnappedToGrid(Num.Vector2 position, uint gridSize)
        {
            var col = MathF.FloorToInt(position.X / gridSize);
            var row = MathF.FloorToInt(position.Y / gridSize);
            return new Vector2(col, row) * (int)gridSize;
        }

        if (MyEditorMain.ActiveInput != ActiveInput.EditorWindow)
            return;
        
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (!layerInstance.IsVisible)
            return;

        var mouseInWorld = GetMouseInWorld();
        var mouseInLevel = mouseInWorld - level.WorldPos.ToNumerics();
        var mouseSnappedToGrid = MouseSnappedToGrid(mouseInLevel, layerDef.GridSize);
        var dl = ImGui.GetWindowDrawList();

        var cameraOffset = -_gameRenderPosition * _gameRenderScale;
        var renderOffset = ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail() * 0.5f + cameraOffset;
        var levelPosition = renderOffset + level.WorldPos.ToNumerics() * _gameRenderScale;

        if (layerDef.LayerType == LayerType.Entities)
        {
            if (_selectedEntityDefinitionIndex > _editor.RootJson.EntityDefinitions.Count - 1)
                return;

            var entityDef = _editor.RootJson.EntityDefinitions[_selectedEntityDefinitionIndex];

            if (IsExcluded(entityDef, layerDef))
                return;

            if (!GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef))
                return;

            var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
            var sprite = World.GetTileSprite(texture, entityDef.TileId, layerDef.GridSize);
            var uvMin = sprite.UV.TopLeft.ToNumerics();
            var uvMax = sprite.UV.BottomRight.ToNumerics();
            var iconMin = levelPosition + mouseSnappedToGrid.ToNumerics() * _gameRenderScale;
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;
            dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax, (Color.White * 0.33f).PackedValue);
        }
        else if (layerDef.LayerType == LayerType.IntGrid)
        {
            if (_selectedIntGridValueIndex > layerDef.IntGridValues.Count - 1)
                return;

            var intGridValue = layerDef.IntGridValues[_selectedIntGridValueIndex];

            var iconMin = levelPosition + mouseSnappedToGrid.ToNumerics() * _gameRenderScale;
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;
            dl->AddRectFilled(iconMin, iconMax, (intGridValue.Color * 0.33f).PackedValue);
        }
    }

    private void HighlightSelectedEntityInstance(Renderer renderer)
    {
        if (_selectedEntityInstanceIndex == -1)
            return;

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (_selectedEntityInstanceIndex > layerInstance.EntityInstances.Count - 1)
            return;

        var selectedInstance = layerInstance.EntityInstances[_selectedEntityInstanceIndex];

        if (!GetEntityDef(selectedInstance.EntityDefId, out var entityDef))
            return;

        renderer.DrawRectWithOutline(
            level.WorldPos + selectedInstance.Position,
            level.WorldPos + selectedInstance.Position + new Vector2(layerDef.GridSize),
            entityDef.Color * 0.33f,
            entityDef.Color
        );
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

    private bool GetSelectedLevel([NotNullWhen(true)] out Level? level)
    {
        level = null;

        if (WorldsWindow.SelectedWorldIndex > _editor.RootJson.Worlds.Count - 1)
            return false;

        var world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
        if (LevelsWindow.SelectedLevelIndex > world.Levels.Count - 1)
            return false;

        level = world.Levels[LevelsWindow.SelectedLevelIndex];
        return true;
    }

    private static bool GetFieldDef(EntityDefinition entityDef, int fieldDefId, [NotNullWhen(true)] out FieldDef? fieldDef)
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

    private bool GetEntityDef(int uid, [NotNullWhen(true)] out EntityDefinition? entityDef)
    {
        for (var i = 0; i < _editor.RootJson.EntityDefinitions.Count; i++)
        {
            if (_editor.RootJson.EntityDefinitions[i].Uid == uid)
            {
                entityDef = _editor.RootJson.EntityDefinitions[i];
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

    private bool GetSelectedWorld([NotNullWhen(true)] out WorldsRoot.World? selectedWorld)
    {
        if (WorldsWindow.SelectedWorldIndex <= _editor.RootJson.Worlds.Count - 1)
        {
            selectedWorld = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
            return true;
        }

        selectedWorld = null;
        return false;
    }

    private static int GetEntityAtPosition(Vector2 position, uint gridSize, List<EntityInstance> entities, out EntityInstance? entity)
    {
        for (var i = 0; i < entities.Count; i++)
        {
            var instance = entities[i];
            if (position.X >= instance.Position.X &&
                position.X <= instance.Position.X + gridSize &&
                position.Y >= instance.Position.Y &&
                position.Y <= instance.Position.Y + gridSize)
            {
                entity = instance;
                return i;
            }
        }

        entity = null;
        return -1;
    }

    private bool GetTileSetDef(uint uid, [NotNullWhen(true)] out TileSetDef? tileSetDef)
    {
        for (var i = 0; i < _editor.RootJson.TileSetDefinitions.Count; i++)
        {
            if (_editor.RootJson.TileSetDefinitions[i].Uid == uid)
            {
                tileSetDef = _editor.RootJson.TileSetDefinitions[i];
                return true;
            }
        }

        tileSetDef = null;
        return false;
    }

    #endregion
}
