using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

public enum RectHandlePos
{
    Top,
    Bottom,
    Left,
    Right,

    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public class Tool
{
}

public class SelectTool : Tool
{
}

public enum ToolState
{
    Inactive,
    Started,
    Active,
    Ended,
}

public unsafe class ResizeTool : Tool
{
    private static RectHandlePos[] _handlePositions = Enum.GetValues<RectHandlePos>();

    public RectHandlePos ActiveHandle = RectHandlePos.Top;

    private ToolState _state = ToolState.Inactive;
    public ToolState State => _state;

    public Point SizeDelta;
    public Point TotSizeDelta;

    private ImGuiMouseCursor[] _imGuiCursors =
    {
        ImGuiMouseCursor.ResizeNS,
        ImGuiMouseCursor.ResizeNS,
        ImGuiMouseCursor.ResizeEW,
        ImGuiMouseCursor.ResizeEW,

        ImGuiMouseCursor.ResizeNWSE,
        ImGuiMouseCursor.ResizeNESW,
        ImGuiMouseCursor.ResizeNESW,
        ImGuiMouseCursor.ResizeNWSE,
    };

    private Num.Vector2[] _pivots =
    {
        new(0, -0.5f),
        new(0, 0.5f),
        new(-0.5f, 0),
        new(0.5f, 0),

        new(-0.5f, -0.5f),
        new(0.5f, -0.5f),
        new(-0.5f, 0.5f),
        new(0.5f, 0.5f),
    };

    public ToolState Draw(Num.Vector2 min, Num.Vector2 max, float handleRadius, bool enableX, bool enableY)
    {
        if (enableX == false && enableY == false)
            return ToolState.Inactive;

        var size = max - min;
        var center = min + size * 0.5f;
        var padding = new Num.Vector2(20, 20);

        var activeHandleIndex = -1;

        for (var i = 0; i < _handlePositions.Length; i++)
        {
            var handle = _handlePositions[i];
            if (!enableX && IsXHandle(handle))
                continue;
            if (!enableY && IsYHandle(handle))
                continue;

            ImGui.PushID(i);
            var x = i % _handlePositions.Length;
            var y = i / _handlePositions.Length;

            var dl = ImGui.GetWindowDrawList();

            var pivot = _pivots[i];

            var handlePos = (center + (size + padding) * pivot);

            var wasHovered = ImGui.GetCurrentContext()->HoveredIdPreviousFrame == ImGui.GetID("ResizeHandle");

            var (fill, outline) = wasHovered switch
            {
                true => (Color.White, Color.Black),
                _ => (Color.White.MultiplyAlpha(0.33f), Color.Black.MultiplyAlpha(0.33f)),
            };

            dl->AddCircleFilled(handlePos, handleRadius, fill.PackedValue);
            dl->AddCircle(handlePos, handleRadius, outline.PackedValue);
            var handleRadiusSize = new Num.Vector2(handleRadius, handleRadius);
            ImGui.SetCursorScreenPos(handlePos - handleRadiusSize);

            ImGui.SetItemAllowOverlap();
            if (ImGui.InvisibleButton("ResizeHandle", handleRadiusSize * 2.0f))
            {
            }

            if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            {
                ImGui.SetMouseCursor(_imGuiCursors[i]);
            }

            if (ImGui.IsItemActive())
            {
                activeHandleIndex = i;
            }

            ImGui.PopID();
        }

        if (activeHandleIndex == -1)
        {
            _state = _state switch
            {
                ToolState.Ended => ToolState.Inactive,
                not ToolState.Inactive => ToolState.Ended,
                _ => ToolState.Inactive,
            };

            return _state;
        }

        ActiveHandle = _handlePositions[activeHandleIndex];

        _state = _state switch
        {
            ToolState.Started => ToolState.Active,
            not ToolState.Active => ToolState.Started,
            _ => ToolState.Active,
        };

        SizeDelta = Point.Zero;

        if (_state == ToolState.Started)
        {
            TotSizeDelta = Point.Zero;
        }

        var (invertX, invertY) = GetInvert(ActiveHandle);

        if (IsYHandle(ActiveHandle))
        {
            SizeDelta.Y = (int)ImGui.GetIO()->MouseDelta.Y * invertY;
            TotSizeDelta.Y += SizeDelta.Y;
        }

        if (IsXHandle(ActiveHandle))
        {
            SizeDelta.X = (int)ImGui.GetIO()->MouseDelta.X * invertX;
            TotSizeDelta.X += SizeDelta.X;
        }

        return _state;
    }

    private static (int invertX, int invertY) GetInvert(RectHandlePos handle)
    {
        var invertX = handle is RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft ? -1 : 1;
        var invertY = handle is RectHandlePos.TopLeft or RectHandlePos.Top or RectHandlePos.TopRight ? -1 : 1;
        return (invertX, invertY);
    }

    private static bool IsYHandle(RectHandlePos handle)
    {
        return handle is
            RectHandlePos.TopLeft or
            RectHandlePos.Top or
            RectHandlePos.TopRight or
            RectHandlePos.Bottom or
            RectHandlePos.BottomRight or
            RectHandlePos.BottomLeft;
    }

    private static bool IsXHandle(RectHandlePos handle)
    {
        return handle is
            RectHandlePos.TopLeft or
            RectHandlePos.Left or
            RectHandlePos.BottomLeft or
            RectHandlePos.Right or
            RectHandlePos.TopRight or
            RectHandlePos.BottomRight;
    }
}

public unsafe class EditorWindow : ImGuiEditorWindow
{
    private ResizeTool _resizeLevel = new();
    private ResizeTool _resizeEntity = new();
    const string SelectedEntityPopupName = "SelectedEntityInstance";
    public const string WindowTitle = "EditorTabs";
    private const string PreviewWindowTitle = "Editor";
    private const string EditorSettingsWindowTitle = "EditorSettings";

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
    public static float _gameRenderScale = 1f;

    private int _selectedEntityDefinitionIndex;
    private int _selectedEntityInstanceIndex = -1;
    private int _selectedIntGridValueIndex;
    private int _selectedLayerInstanceIndex;

    public Matrix4x4 PreviewRenderViewportTransform = Matrix4x4.Identity;

    [CVar("editor.grid_color", "")] public static Color GridColor = new(0.6f, 0.6f, 0.6f);

    [CVar("editor.grid_thickness", "")] public static float GridThickness = 1.0f;

    [CVar("editor.background_color", "")] public static Color BackgroundColor = Color.Black;

    [CVar("editor.stripe_color", "")] public static Color StripeColor = new(1.0f, 1.0f, 1.0f, 0.1f);
    private static Num.Vector2 _renderSize;
    private static Num.Vector2 _renderPos;
    private uint _selectedEntityPopupId;
    private bool _isAdding;
    private ButtonState btnState = new();
    private Point _resizeStartPos;
    private UPoint _resizeStartSize;
    private int _entityPopupPos = 1;
    private bool _isInstancePopupOpen;
    private Point _startPos;
    private Point _resizeEntityStartPos;
    private UPoint _resizeEntityStartSize;
    private static Point _moveEntityStart;
    private static Point _moveLevelStart;

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
            ImGuiInternal.DockBuilderDockWindow("CurrentLevelWindow", dockSpaceId);
        }

        var shouldDrawContent =
            ImGuiExt.BeginWorkspaceWindow(WindowTitle, "EditorDockSpace", InitializeLayout, ImGuiExt.RefPtr(ref IsOpen), ref windowClass);

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


    private void DrawSelectedEntityInstancePopup()
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

        var popupPos = Num.Vector2.Zero;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 0), ImGuiCond.Always);
        if (_entityPopupPos == 0)
        {
            if (GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef) && GetSelectedEntityInstance(out var e))
            {
                popupPos = GetWorldPosInScreen(level.WorldPos + e.Position + new Vector2(0, layerDef.GridSize));
            }

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
            ImGui.End();
            return;
        }

        if (_selectedEntityInstanceIndex == -1)
        {
            _isInstancePopupOpen = false;
            ImGui.End();
            return;
        }

        if (!GetSelectedEntityInstance(out var instance))
        {
            _isInstancePopupOpen = false;
            ImGui.End();
            return;
        }

        if (GetEntityDef(instance.EntityDefId, out var entityDef))
        {
            ImGui.BeginDisabled();
            SimpleTypeInspector.InspectString("Identifier", ref entityDef.Identifier);
            ImGui.EndDisabled();

            SimpleTypeInspector.InspectPoint("Position", ref instance.Position);

            var tmpPoint = (Point)instance.Size;
            if (ImGuiExt.InspectPoint("Size", ref tmpPoint.X, ref tmpPoint.Y, "W", "Width", "H", "Height", 1, 1, 512))
            {
                instance.Width = (uint)tmpPoint.X;
                instance.Height = (uint)tmpPoint.Y;
            }

            FieldInstanceInspector.DrawFieldInstances(instance.FieldInstances, entityDef.FieldDefinitions);
        }
        else
        {
            ImGui.TextDisabled($"Could not find a entity definition with id \"{instance.EntityDefId}\"");
        }

        ImGui.End();
    }

    private void DrawCurrentLevelData()
    {
        var windowFlags = ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin("CurrentLevelWindow", default, windowFlags))
        {
            DrawLayersInSelectedLevel();
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

        SimpleTypeInspector.InspectFloat("Deselected Layer Alpha", ref DeselectedLayerAlpha, new RangeSettings(0, 1.0f, 0.1f, false), "%.2f%%");
        SimpleTypeInspector.InspectFloat("IntGrid Alpha", ref IntGridAlpha, new RangeSettings(0, 1.0f, 0.1f, false), "%.2f%%");
        SimpleTypeInspector.InspectColor("BackgroundColor", ref BackgroundColor);
        SimpleTypeInspector.InspectColor("StripeColor", ref StripeColor);
        SimpleTypeInspector.InspectColor("GridColor", ref GridColor);
        SimpleTypeInspector.InspectFloat("GridThickness", ref GridThickness, new RangeSettings(0, 10, 0.25f, false));
        SimpleTypeInspector.InspectFloat("CameraScale", ref _gameRenderScale, new RangeSettings(0.001f, 10, 0.25f, false));
        SimpleTypeInspector.InspectNumVector2("CameraPos", ref _gameRenderPosition);

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

    private void AddRuleTilesAt(int groupUid, AutoRule rule, LayerInstance layerInstance, LayerDef layerDef, TileSetDef tileSetDef, Level level, int x,
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
                TileId = rule.TileIds[0],
                TileSetDefId = tileSetDef.Uid,
                LevelWorldPos = level.WorldPos,
                LayerGridSize = layerDef.GridSize,
            }
        );
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
        if (ImGuiExt.ColoredButton("Cleanup", new System.Numerics.Vector2(-ImGuiExt.FLT_MIN, 0)))
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
        var result = ImGui.Begin(PreviewWindowTitle, default, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar);

        _renderPos = ImGui.GetCursorScreenPos();
        _renderSize = ImGui.GetContentRegionAvail();

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
        dl->AddRectFilled(_renderPos, _renderPos + _renderSize, BackgroundColor.PackedValue);
        ImGuiExt.FillWithStripes(dl, new ImRect(_renderPos, _renderPos + _renderSize), StripeColor.PackedValue);

        if (_selectedEntityPopupId == 0)
            _selectedEntityPopupId = ImGui.GetID(SelectedEntityPopupName);

        DrawWorld();

        var t = _renderPos - ImGui.GetWindowViewport()->Pos;
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

        DrawSelectedEntityInstancePopup();

        // imgui sets WantCaptureKeyboard when an item is active which we don't want for the game window
        if (ImGui.IsWindowHovered() &&
            (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Right)))
        {
            ImGui.SetWindowFocus(PreviewWindowTitle);
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
            ImGuiExt.PrintVector("MousePosInWorld", GetScreenPosInWorld(ImGui.GetMousePos()));
            ImGui.Dummy(new System.Numerics.Vector2(400, 0));
            ImGui.PopFont();
        }

        ImGui.End();
    }

    private Vector2 GetMouseInWorld()
    {
        var mousePos = _editor.InputHandler.MousePosition.ToNumerics();
        var center = _renderSize * 0.5f / _gameRenderScale;
        return (mousePos - center + _gameRenderPosition).ToXNA();
    }

    private static Vector2 GetScreenPosInWorld(Num.Vector2 position)
    {
        var center = _renderSize * 0.5f;
        var posRelativeToRenderCenter = position - _renderPos - center;
        var result = posRelativeToRenderCenter / _gameRenderScale + _gameRenderPosition;
        return result.ToXNA();
    }

    private static Num.Vector2 GetWorldPosInScreen(Vector2 position)
    {
        return _renderPos + _renderSize * 0.5f +
               -_gameRenderPosition * _gameRenderScale +
               position.ToNumerics() * _gameRenderScale;
    }

    private static Num.Vector2 GetWorldPosInWindow(Vector2 position)
    {
        return GetWorldPosInScreen(position) - _renderPos;
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
                    SelectFirstEntityDefinition();
                    _selectedIntGridValueIndex = 0;
                }

                ImGui.SameLine(0, 0);

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    if (ImGui.MenuItem("Clear", default))
                    {
                        ClearLayerInstance(layerInstance);
                    }

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

    private static void ClearLayerInstance(LayerInstance layerInstance)
    {
        layerInstance.AutoLayerTiles.Clear();
        layerInstance.EntityInstances.Clear();
        layerInstance.IntGrid.AsSpan().Fill(0);
    }

    private void SelectFirstEntityDefinition()
    {
        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (layerDef.LayerType != LayerType.Entities)
            return;

        var entityDefs = _editor.RootJson.EntityDefinitions;
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

                        ImGui.SameLine(0, 0);
                        
                        var dl = ImGui.GetWindowDrawList();
                        var rectHeight = _rowMinHeight * 0.6f;
                        var min = cursorPos + new System.Numerics.Vector2(8, (_rowMinHeight - rectHeight) / 2);
                        var max = min + new System.Numerics.Vector2(32, rectHeight);
                        ImGuiExt.RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
                        var label = intGridValue.Value.ToString();
                        var textSize = ImGui.CalcTextSize(label);
                        var rectSize = max - min;
                        dl->AddText(min + new System.Numerics.Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2),
                            Color.White.PackedValue,
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

    private record struct ButtonState(bool Active, bool Activated, bool Hovered, bool Clicked, bool Focused);

    private void DrawWorld()
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

    private void DrawLevel(Level level, int levelIndex)
    {
        var levelMin = GetWorldPosInScreen(level.WorldPos);
        var levelMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2());
        var dl = ImGui.GetWindowDrawList();

        dl->AddRectFilled(levelMin, levelMax, level.BackgroundColor.PackedValue);

        var isSelectedLevel = levelIndex == LevelsWindow.SelectedLevelIndex;

        // draw grid
        var gridSize = _editor.RootJson.DefaultGridSize;
        {
            var gridMin = GetWorldPosInScreen(level.WorldPos);
            var gridMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2());
            DrawGrid(dl, gridMin, gridMax, gridSize * _gameRenderScale, GridColor, _gameRenderScale * GridThickness);
        }

        // draw outer level border
        if (isSelectedLevel)
        {
            var color = isSelectedLevel ? Color.CornflowerBlue : Color.Red;

            var thickness = 2f * _gameRenderScale;
            var padding = new Vector2(thickness * 0.5f / _gameRenderScale);
            var rectMin = GetWorldPosInScreen(level.WorldPos - padding);
            var rectMax = GetWorldPosInScreen(level.WorldPos + level.Size.ToVec2() + padding);
            dl->AddRect(rectMin, rectMax, color.PackedValue, 0, ImDrawFlags.None, thickness);
        }

        var levelSize = levelMax - levelMin;
        if (!isSelectedLevel)
        {
            ImGui.SetCursorScreenPos(levelMin);
            ImGui.SetItemAllowOverlap();
            if (ImGui.InvisibleButton("LevelButton", levelSize.EnsureNotZero(), (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap))
            {
                LevelsWindow.SelectedLevelIndex = levelIndex;
            }
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

    private void DrawLevelSizeLabel(Level level, ImDrawList* dl, System.Numerics.Vector2 xLabelPos)
    {
        var cols = level.Width / _editor.RootJson.DefaultGridSize;
        var rows = level.Height / _editor.RootJson.DefaultGridSize;
        var xLabel = $"{level.Width}x{level.Height} px ({cols}x{rows} cells)";
        var xLabelSize = ImGui.CalcTextSize(xLabel);
        var offset = new Num.Vector2(-0.5f, 2.0f) * xLabelSize;
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, (xLabelPos + offset).Round(), Color.White.PackedValue, xLabel, 0, default);
    }

    private void HandleLevelResize(Level level, Num.Vector2 min, Num.Vector2 max, uint gridSize)
    {
        var state = _resizeLevel.Draw(min, max, _gameRenderScale * 5f, true, true);

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
            var sizeDelta = _resizeLevel.TotSizeDelta.ToVec2() / _gameRenderScale;
            var gridSizeDelta = (sizeDelta / gridSize).Round();

            var newSize = (_resizeStartSize + gridSizeDelta * gridSize).ToUPoint();

            if (newSize != level.Size)
            {
                var prevPos = level.WorldPos;
                var prevSize = level.Size;

                var startPos = _resizeStartPos;
                var newX = _resizeLevel.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft => (int)(startPos.X - (gridSizeDelta.X * gridSize)),
                    _ => level.WorldPos.X,
                };

                var newY = _resizeLevel.ActiveHandle switch
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

    private void DrawLayerInstances(ImDrawList* dl, Level level, bool isSelectedLevel)
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

    private void DrawAutoLayerTiles(ImDrawList* dl, Level level, LayerDef layerDef, LayerInstance layer, bool isSelected)
    {
        if (!GetTileSetDef(layerDef.TileSetDefId, out var tileSetDef))
            return;

        var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
        foreach (var tile in layer.AutoLayerTiles)
        {
            var sprite = World.GetTileSprite(texture, tile.TileId, layerDef.GridSize);
            var uvMin = sprite.UV.TopLeft.ToNumerics();
            var uvMax = sprite.UV.BottomRight.ToNumerics();
            var iconMin = GetWorldPosInScreen(level.WorldPos + tile.Cell.ToVec2() * layerDef.GridSize);
            var iconMax = GetWorldPosInScreen(level.WorldPos + tile.Cell.ToVec2() * layerDef.GridSize + new Vector2(layerDef.GridSize));
            dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax);
        }
    }


    private void DrawEntityLayer(ImDrawList* dl, Level level, LayerInstance layer, LayerDef layerDef, bool isSelectedLayer)
    {
        var instanceToRemove = -1;
        for (var k = 0; k < layer.EntityInstances.Count; k++)
        {
            ImGui.PushID(k);
            var entityInstance = layer.EntityInstances[k];
            var entityDefId = entityInstance.EntityDefId;
            if (!GetEntityDef(entityDefId, out var entityDef))
            {
                DrawWarningRect(dl, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                ImGui.PopID();
                continue;
            }

            GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef);

            // var sprite = renderer.BlankSprite;
            var sprite = _editor.Renderer.BlankSprite;
            Matrix4x4 entityTransform;
            Color fillColor;
            Color iconTint = Color.White;
            Color outline;
            var uvMin = Num.Vector2.Zero;
            var uvMax = Num.Vector2.One;
            if (tileSetDef != null)
            {
                var colorField = entityInstance.FieldInstances.FirstOrDefault(x => x.Value is Color);
                fillColor = colorField != null ? (Color)colorField.Value! : entityDef.Color;
                iconTint = colorField != null ? (Color)colorField.Value! : Color.White;
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

            var boundsMin = GetWorldPosInScreen(level.WorldPos + entityInstance.Position);
            var boundsMax = GetWorldPosInScreen(level.WorldPos + entityInstance.Position + entityInstance.Size.ToPoint());

            var iconMin = boundsMin + (boundsMax - boundsMin) * 0.5f - new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * 0.5f * _gameRenderScale;
            var iconMax = iconMin + new Num.Vector2(layerDef.GridSize, layerDef.GridSize) * _gameRenderScale;

            fillColor *= entityDef.FillOpacity;
            fillColor = isSelectedLayer ? fillColor : fillColor * DeselectedLayerAlpha;
            iconTint = isSelectedLayer ? iconTint : iconTint * DeselectedLayerAlpha;
            dl->AddRectFilled(boundsMin, boundsMax, fillColor.PackedValue);
            dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax, iconTint.PackedValue);

            if (isSelectedLayer)
            {
                ImGui.SetCursorScreenPos(boundsMin);
                ImGui.SetItemAllowOverlap();
                var buttonSize = boundsMax - boundsMin;
                if (ImGui.InvisibleButton("EntityButton", buttonSize.EnsureNotZero()))
                {
                }

                if (ImGui.IsItemActivated())
                {
                    if (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift))
                    {
                        var instance = DuplicateInstance(entityInstance);
                        layer.EntityInstances.Add(instance);
                    }
               
                    {
                        _selectedEntityInstanceIndex = k;
                        _startPos = entityInstance.Position;
                        _isInstancePopupOpen = true;   
                    }
                }

                if (ImGui.IsItemActive() && ImGui.GetMouseDragDelta().LengthSquared() >= 2f * 2f)
                {
                    var newPos = _startPos + (ImGui.GetMouseDragDelta() / _gameRenderScale).ToPoint();
                    var (snapped, _) = SnapToGrid(newPos, layerDef.GridSize);
                    entityInstance.Position = snapped.ToPoint();
                }

                if (_selectedEntityInstanceIndex == k)
                {
                    dl->AddRect(boundsMin, boundsMax, Color.CornflowerBlue.PackedValue, 0, ImDrawFlags.None, _gameRenderScale * 2f);
                    HandleEntityResize(layerDef.GridSize, boundsMin, boundsMax, entityInstance, entityDef);
                    
                    // DrawMoveEntityButton(entityInstance, boundsMin, boundsMax);
                }
                else if (ImGui.IsItemHovered())
                {
                    dl->AddRect(boundsMin, boundsMax, Color.CornflowerBlue.MultiplyAlpha(0.66f).PackedValue, 0, ImDrawFlags.None, _gameRenderScale * 2f);
                }

                /*if (ImGui.BeginPopupContextItem("EntityInstanceContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
                {
                    if (ImGui.MenuItem("Remove", default))
                        instanceToRemove = k;

                    ImGui.EndPopup();
                }*/
            }

            // Draw blinking rect if there are field instances without matching field definitions
            for (var j = 0; j < entityInstance.FieldInstances.Count; j++)
            {
                var fieldInstance = entityInstance.FieldInstances[j];
                if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
                {
                    DrawWarningRect(dl, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                }
            }

            ImGui.PopID();
        }

        if (instanceToRemove != -1)
        {
            layer.EntityInstances.RemoveAt(instanceToRemove);
        }
    }

    private static EntityInstance DuplicateInstance(EntityInstance entityInstance)
    {
        var serialized = JsonConvert.SerializeObject(entityInstance, ContentManager.JsonSerializerSettings);
        var copy = JsonConvert.DeserializeObject<EntityInstance>(serialized, ContentManager.JsonSerializerSettings) ?? throw new Exception();
        return copy;
    }

    private static void DrawMoveLevelButton(Level level, Num.Vector2 boundsMin, Num.Vector2 boundsMax)
    {
        var moveButtonSize = new Num.Vector2(10, 10) * _gameRenderScale;
        ImGui.SetCursorScreenPos(
            boundsMin + new Num.Vector2((boundsMax.X - boundsMin.X) * 0.5f, 0) - new Num.Vector2(moveButtonSize.X * 0.5f, 2 * moveButtonSize.Y));
        if (ImGuiExt.ColoredButton(FontAwesome6.ArrowsUpDownLeftRight, Color.White, Color.Black, moveButtonSize, "Move"))
        {
        }

        if (ImGui.IsItemActivated())
        {
            _moveLevelStart = level.WorldPos;
        }

        if (ImGui.IsItemActive())
        {
            var gridSize = ((MyEditorMain)Shared.Game).RootJson.DefaultGridSize;
            var (newPos, _) = SnapToGrid(_moveLevelStart + (ImGui.GetMouseDragDelta() / _gameRenderScale).ToPoint(), gridSize);
            level.WorldPos = newPos.ToPoint();
        }
    }
    
    private static void DrawMoveEntityButton(EntityInstance entity, Num.Vector2 boundsMin, Num.Vector2 boundsMax)
    {
        var moveButtonSize = new Num.Vector2(10, 10) * _gameRenderScale;
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
            var (newPos, _) = SnapToGrid(_moveEntityStart + (ImGui.GetMouseDragDelta() / _gameRenderScale).ToPoint(), gridSize);
            entity.Position = newPos.ToPoint();
        }
    }

    private void HandleEntityResize(uint gridSize, Num.Vector2 min, Num.Vector2 max, EntityInstance entityInstance, EntityDefinition entityDef)
    {
        var state = _resizeEntity.Draw(min, max, 5f, entityDef.ResizableX, entityDef.ResizableY);

        if (state == ToolState.Started)
        {
            _resizeEntityStartPos = entityInstance.Position;
            _resizeEntityStartSize = entityInstance.Size;
        }

        if (_resizeEntity.State == ToolState.Started ||
            _resizeEntity.State == ToolState.Active ||
            _resizeEntity.State == ToolState.Ended)
        {
            var sizeDelta = _resizeEntity.TotSizeDelta.ToVec2() / _gameRenderScale;
            var gridSizeDelta = (sizeDelta / gridSize).Round();
            var newSize = (_resizeEntityStartSize + gridSizeDelta * gridSize).ToUPoint();

            if (newSize != entityInstance.Size)
            {
                var startPos = _resizeEntityStartPos;
                var newX = _resizeEntity.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Left or RectHandlePos.BottomLeft => (int)(startPos.X - (gridSizeDelta.X * gridSize)),
                    _ => entityInstance.Position.X,
                };

                var newY = _resizeEntity.ActiveHandle switch
                {
                    RectHandlePos.TopLeft or RectHandlePos.Top or RectHandlePos.TopRight => (int)(startPos.Y - (gridSizeDelta.Y * gridSize)),
                    _ => entityInstance.Position.Y,
                };

                var newPos = new Point(newX, newY);

                entityInstance.Position = newPos;
                entityInstance.Size = newSize;
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
        // var rows = (int)(level.Height / layerDef.GridSize);

        for (var j = 0; j < layer.IntGrid.Length; j++)
        {
            var cellValue = layer.IntGrid[j];
            if (cellValue == 0)
                continue;

            GetIntDef(layerDef, cellValue, out var intDef);
            var color = intDef?.Color ?? Color.Red;
            if (!isSelected)
                color *= 0.5f;

            color *= IntGridAlpha;

            var (x, y) = (j % cols, j / cols);
            var tilePos = new Vector2(x, y) * layerDef.GridSize;
            var iconMin = GetWorldPosInScreen(level.WorldPos + tilePos);
            var iconMax = GetWorldPosInScreen(level.WorldPos + tilePos + new Vector2(layerDef.GridSize));
            dl->AddRectFilled(iconMin, iconMax, color.PackedValue);
        }
    }

    private (Vector2 snapped, Point cell, Vector2 mouseInLevel) GetMouseInLevel(Level level)
    {
        var mouseInWorld = GetMouseInWorld();
        var mouseInLevel = mouseInWorld - level.WorldPos;
        var (snapped, cell) = SnapToGrid(mouseInLevel, _editor.RootJson.DefaultGridSize);
        return (snapped, cell, mouseInLevel);
    }

    private bool IsMouseInLevelBounds(Level lvl)
    {
        var (_, mouseCell, _) = GetMouseInLevel(lvl);
        return mouseCell.X >= 0 && mouseCell.X < lvl.Width / _editor.RootJson.DefaultGridSize &&
               mouseCell.Y >= 0 && mouseCell.Y < lvl.Height / _editor.RootJson.DefaultGridSize;
    }

    private void DrawMouse(ButtonState buttonState)
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
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                _isInstancePopupOpen = false;
            }

            return;
        }

        var dl = ImGui.GetWindowDrawList();

        var (mouseSnappedToGrid, mouseCell, mouseInLevel) = GetMouseInLevel(level);

        if (layerDef.LayerType == LayerType.Entities)
        {
            if (_editor.RootJson.EntityDefinitions.Count == 0)
                return;

            if (_selectedEntityDefinitionIndex > _editor.RootJson.EntityDefinitions.Count - 1)
            {
                SelectFirstEntityDefinition();
                return;
            }

            var entityDef = _editor.RootJson.EntityDefinitions[_selectedEntityDefinitionIndex];

            if (IsExcluded(entityDef, layerDef))
            {
                SelectFirstEntityDefinition();
                return;
            }

            if (!GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef))
                return;

            var hoveringEntityIndex = GetEntityAtPosition(mouseInLevel, layerDef.GridSize, layerInstance.EntityInstances, out var hoveredEntity);

            if (_isAdding)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _isAdding = false;
                    return;
                }

                if (hoveringEntityIndex == -1)
                {
                    layerInstance.EntityInstances.Add(CreateNewEntityInstance(mouseSnappedToGrid.ToPoint(), entityDef));
                    Logs.LogInfo($"Adding..: {Shared.Game.Time.UpdateCount}");
                }

                return;
            }

            if (hoveringEntityIndex != -1 && hoveredEntity != null)
            {
                if (!ImGui.IsAnyItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
                {
                    layerInstance.EntityInstances.RemoveAt(hoveringEntityIndex);
                }

                return;
            }

            {
                if (!ImGui.IsAnyItemHovered())
                {
                    // draw preview of entity being added
                    var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
                    var sprite = World.GetTileSprite(texture, entityDef.TileId, layerDef.GridSize);
                    var uvMin = sprite.UV.TopLeft.ToNumerics();
                    var uvMax = sprite.UV.BottomRight.ToNumerics();
                    var iconMin = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid);
                    var iconMax = GetWorldPosInScreen(level.WorldPos + mouseSnappedToGrid + new Vector2(layerDef.GridSize));
                    dl->AddImage((void*)sprite.Texture.Handle, iconMin, iconMax, uvMin, uvMax, (Color.White * 0.33f).PackedValue);

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

            dl->AddRectFilled(iconMin, iconMax, (intGridValue.Color * 0.33f).PackedValue);

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

    private EntityInstance CreateNewEntityInstance(Point position, EntityDefinition entityDef)
    {
        var instance = new EntityInstance()
        {
            Position = position,
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
                    FieldDefId = fieldDef.Uid,
                }
            );
        }

        return instance;
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
                position.X < instance.Position.X + instance.Size.X &&
                position.Y >= instance.Position.Y &&
                position.Y < instance.Position.Y + + instance.Size.Y)
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

    private bool GetSelectedEntityInstance([NotNullWhen(true)] out EntityInstance? instance)
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
