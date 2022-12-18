using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Cameras;
using MyGame.WorldsRoot;
using EntityDefinition = MyGame.WorldsRoot.EntityDefinition;
using EntityInstance = MyGame.WorldsRoot.EntityInstance;
using FieldInstance = MyGame.WorldsRoot.FieldInstance;
using LayerInstance = MyGame.WorldsRoot.LayerInstance;
using Level = MyGame.WorldsRoot.Level;

namespace MyGame.Editor;

public unsafe class EditorWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Editor";
    private const string PreviewWindowTitle = "Preview";
    private MyEditorMain _editor;
    private IntPtr? _editorRenderTextureId;
    private int _selectedLayerInstanceIndex;
    private Camera _camera = new(1920, 1080);
    static int _rowMinHeight = 60;
    private int _selectedIntGridValueIndex;
    private int _selectedEntityDefinitionIndex;

    private readonly EntityDefWindow _entityDefWindow;
    private readonly LayerDefWindow _layerDefWindow;
    private readonly LevelsWindow _levelsWindow;
    private readonly TileSetDefWindow _tileSetDefWindow;
    private readonly WorldsWindow _worldsWindow;

    public Matrix4x4 PreviewRenderViewportTransform = Matrix4x4.Identity;

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
            return false;

        world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
        if (LevelsWindow.SelectedLevelIndex > world.Levels.Count - 1)
            return false;

        level = world.Levels[LevelsWindow.SelectedLevelIndex];

        if (_selectedLayerInstanceIndex > level.LayerInstances.Count - 1)
            return false;

        var instance = level.LayerInstances[_selectedLayerInstanceIndex];
        var def = _editor.RootJson.LayerDefinitions.FirstOrDefault(x => x.Uid == instance.LayerDefId);
        if (def == null)
            return false;

        layerInstance = instance;
        layerDef = def;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        var leftMouseDown = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Left);
        var rightMouseDown = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Right);
        if (leftMouseDown == false && rightMouseDown == false)
            return;

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (!layerInstance.IsVisible)
            return;

        var (mouseWorld, mouseLevel, mouseGrid) = GetMousePositions();
        var cols = (int)(level.Width / layerDef.GridSize);
        var rows = (int)(level.Height / layerDef.GridSize);

        if (!(mouseGrid.X >= 0) || !(mouseGrid.X < cols) || !(mouseGrid.Y >= 0) || !(mouseGrid.Y < rows))
            return;

        switch (layerDef.LayerType)
        {
            case LayerType.IntGrid:

                var cellIndex = (int)mouseGrid.Y * cols + (int)mouseGrid.X;
                if (cellIndex > layerInstance.IntGrid.Length - 1)
                    break;
                if (_selectedIntGridValueIndex > layerDef.IntGridValues.Count - 1)
                    break;

                var value = leftMouseDown ? layerDef.IntGridValues[_selectedIntGridValueIndex].Value : 0;
                layerInstance.IntGrid[cellIndex] = value;

                break;
            case LayerType.Entities:
            {
                var rightMousePressed = _editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Right);
                if (rightMousePressed)
                {
                    var index = GetEntityAtPosition(mouseLevel, layerInstance.EntityInstances, out _);
                    if (index != -1)
                        layerInstance.EntityInstances.RemoveAt(index);

                    break;
                }

                var leftMousePressed = _editor.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left);
                if (!leftMousePressed)
                    break;

                if (_selectedEntityDefinitionIndex > _editor.RootJson.EntityDefinitions.Count - 1)
                    break;

                var entityDef = _editor.RootJson.EntityDefinitions[_selectedEntityDefinitionIndex];

                if (IsExcluded(entityDef, layerDef))
                    break;

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
                            Value = FieldDef.GetDefaultValue(fieldDef.FieldType, fieldDef.IsArray),
                            FieldDefId = fieldDef.Uid
                        }
                    );
                }

                layerInstance.EntityInstances.Add(instance);

                break;
            }
            case LayerType.Tiles:
                break;
            case LayerType.AutoLayer:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private int GetEntityAtPosition(Vector2 position, List<EntityInstance> entities, out EntityInstance? entity)
    {
        for (var i = 0; i < entities.Count; i++)
        {
            var instance = entities[i];
            if (position.X >= instance.Position.X &&
                position.X <= instance.Position.X + instance.Width &&
                position.Y >= instance.Position.Y &&
                position.Y <= instance.Position.Y + instance.Height)
            {
                entity = instance;
                return i;
            }
        }

        entity = null;
        return -1;
    }

    private static void MakeTabVisible(string windowTitle)
    {
        var window = ImGuiInternal.FindWindowByName(windowTitle);
        if (window == null || window->DockNode == null || window->DockNode->TabBar == null)
            return;
        window->DockNode->TabBar->NextSelectedTabId = window->TabId;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

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

        DrawPreviewWindow();

        DrawCurrentLevelData();
    }

    private void DrawCurrentLevelData()
    {
        var windowFlags = ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin("CurrentLevelWindow", default, windowFlags))
        {
            if (WorldsWindow.SelectedWorldIndex <= _editor.RootJson.Worlds.Count - 1)
            {
                var world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];

                if (LevelsWindow.SelectedLevelIndex <= world.Levels.Count - 1)
                {
                    var level = world.Levels[LevelsWindow.SelectedLevelIndex];
                    DrawLayersInLevel(level);
                }
            }
        }

        ImGui.End();
    }

    private void DrawLayersInLevel(Level level)
    {
        ImGuiExt.SeparatorText("Layers");

        for (var i = 0; i < _editor.RootJson.LayerDefinitions.Count; i++)
        {
            if (level.LayerInstances.Any(x => x.LayerDefId == _editor.RootJson.LayerDefinitions[i].Uid))
                continue;
            var layerInstance = CreateLayerInstance(_editor.RootJson.LayerDefinitions[i], level);
            level.LayerInstances.Add(layerInstance);
        }

        DrawLayerInstances(level.LayerInstances, _editor.RootJson.LayerDefinitions);
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

    private void DrawPreviewWindow()
    {
        ImGui.SetNextWindowSize(new Num.Vector2(1024, 768), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(128, 128), new Num.Vector2(ImGuiExt.FLT_MAX, ImGuiExt.FLT_MAX));
        var centralNode = ImGuiInternal.DockBuilderGetCentralNode(_editor.ViewportDockSpaceId);
        ImGui.SetNextWindowDockID(centralNode->ID, ImGuiCond.FirstUseEver);
        if (ImGui.Begin(PreviewWindowTitle, default, ImGuiWindowFlags.None))
        {
            if (ImGui.IsWindowFocused())
            {
                MyEditorMain.ActiveInput = ActiveInput.EditorWindow;
            }

            GameWindow.EnsureTextureIsBound(ref _editorRenderTextureId, _editor._editorRenderTarget, _editor.ImGuiRenderer);
            var cursorScreenPosition = ImGui.GetCursorScreenPos();

            var editorMin = cursorScreenPosition;
            var editorMax = editorMin + new Num.Vector2(_editor._editorRenderTarget.Width, _editor._editorRenderTarget.Height);
            var dl = ImGui.GetWindowDrawList();

            dl->AddImage(
                (void*)_editorRenderTextureId.Value,
                editorMin,
                editorMax,
                Num.Vector2.Zero,
                Num.Vector2.One,
                Color.White.PackedValue
            );
            ImGui.InvisibleButton("InvButton", editorMax - editorMin);

            var windowViewportPosition = ImGui.GetWindowViewport()->Pos;
            var renderOffset = editorMin - windowViewportPosition;

            PreviewRenderViewportTransform = (
                Matrix3x2.CreateScale(1.0f, 1.0f) *
                Matrix3x2.CreateTranslation(renderOffset.X, renderOffset.Y)
            ).ToMatrix4x4();

            if (ImGui.IsWindowHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _camera.Position += -ImGui.GetIO()->MouseDelta.ToXNA() * 1.0f / _camera.Zoom;
            }

            if (ImGui.IsWindowHovered() && ImGui.GetIO()->MouseWheel != 0)
            {
                _camera.Zoom += 0.1f * ImGui.GetIO()->MouseWheel * _camera.Zoom;
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

        ImGui.End();
    }

    private void DrawMouseInfoOverlay()
    {
        var showOverlay = true;
        if (GameWindow.BeginOverlay("MouseOverlay", ref showOverlay))
        {
            var offset = Vector2.Zero;

            if (WorldsWindow.SelectedWorldIndex <= _editor.RootJson.Worlds.Count - 1)
            {
                var world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
                if (LevelsWindow.SelectedLevelIndex <= world.Levels.Count - 1)
                {
                    var level = world.Levels[LevelsWindow.SelectedLevelIndex];
                    offset = level.WorldPos;
                }
            }

            var mousePosition = _editor.InputHandler.MousePosition;
            ImGuiExt.PrintVector("Screen", mousePosition);

            var view = _camera.GetView();
            Matrix3x2.Invert(view, out var invertedView);
            var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
            ImGuiExt.PrintVector("World", mouseInWorld);

            ImGuiExt.PrintVector("Level", mouseInWorld - offset);

            var mouseCell = Entity.ToCell(mouseInWorld - offset);
            ImGuiExt.PrintVector("Grid", mouseCell);

            ImGui.Dummy(new Num.Vector2(200, 0));
        }

        ImGui.End();
    }

    private void DrawLayerInstances(List<LayerInstance> layerInstances, List<LayerDef> layerDefs)
    {
        var rowHeight = 40;
        if (ImGui.BeginTable("LayerInstances", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var layerToDelete = -1;
            for (var i = 0; i < layerInstances.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);

                var layerInstance = layerInstances[i];
                var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);

                var isSelected = _selectedLayerInstanceIndex == i;

                var typeColor = LayerDefWindow.GetLayerDefColor(layerDef?.LayerType ?? LayerType.IntGrid);

                var cursorPos = ImGui.GetCursorScreenPos();
                var avail = ImGui.GetContentRegionAvail();
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

                var buttonSize = new Num.Vector2(30, 30);
                ImGui.SetCursorScreenPos(cursorPos +
                                         new Num.Vector2(avail.X - buttonSize.X - ImGui.GetStyle()->FramePadding.X, rowHeight * 0.5f - buttonSize.Y * 0.5f));
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

        if (ImGuiExt.ColoredButton("Cleanup!"))
        {
            for (var i = layerInstances.Count - 1; i >= 0; i--)
            {
                var layerInstance = layerInstances[i];
                if (!GetLayerDefinition(layerInstance.LayerDefId, out var layerDef))
                {
                    Logs.LogWarn($"Removing layer instance \"{i}\" since there's no layer definition with id \"{layerInstance.LayerDefId}\"");
                    layerInstances.RemoveAt(i);
                    continue;
                }

                if (layerInstance.EntityInstances.Count > 0 && layerDef.LayerType != LayerType.Entities)
                {
                    Logs.LogWarn($"Removing entity instances since the layer isn't an entity layer");
                    layerInstance.EntityInstances.Clear();
                }

                if (layerInstance.IntGrid.Length > 0 && layerDef.LayerType != LayerType.IntGrid)
                {
                    Logs.LogWarn($"Clearing int grid values since the layer isn\'t of type IntGrid");
                    layerInstance.IntGrid = Array.Empty<int>();
                }

                for (var j = layerInstance.EntityInstances.Count - 1; j >= 0; j--)
                {
                    var entity = layerInstance.EntityInstances[j];
                    if (!GetEntityDef(entity.EntityDefId, out var entityDef))
                    {
                        Logs.LogWarn($"Removing entity instance since there\'s no entity definition with id \"{entity.EntityDefId}\"");
                        layerInstance.EntityInstances.RemoveAt(j);
                        continue;
                    }

                    for (var i1 = entity.FieldInstances.Count - 1; i1 >= 0; i1--)
                    {
                        var fieldInstance = entity.FieldInstances[i1];
                        if (!GetFieldDef(entityDef, fieldInstance.FieldDefId, out var fieldDef))
                        {
                            Logs.LogWarn($"Removing field instance there\'s no field definition with id \"{fieldInstance.FieldDefId}\"");
                            entity.FieldInstances.RemoveAt(i1);
                            continue;
                        }
                    }
                }
            }
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

                if (ImGui.BeginTable("IntGridTable", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
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
                        var min = cursorPos + new Num.Vector2(8, (_rowMinHeight - rectHeight) / 2);
                        var max = min + new Num.Vector2(32, rectHeight);
                        ImGuiExt.RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
                        var label = intGridValue.Value.ToString();
                        var textSize = ImGui.CalcTextSize(label);
                        var rectSize = max - min;
                        dl->AddText(min + new Num.Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2), Color.White.PackedValue,
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

                if (ImGui.BeginTable("EntityDefTable", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
                {
                    ImGui.TableSetupColumn("Value");

                    for (var i = 0; i < entityDefs.Count; i++)
                    {
                        var entityDef = entityDefs[i];
                        if (IsExcluded(entityDef, layerDef))
                        {
                            continue;
                        }

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
                            var uvMin = new Num.Vector2(1.0f / texture.Width * cellX * gridSize,
                                1.0f / texture.Height * cellY * gridSize);
                            var uvMax = uvMin + new Num.Vector2(gridSize / (float)texture.Width, gridSize / (float)texture.Height);
                            var iconSize = new Num.Vector2(32, 32);
                            var iconPos = cursorPos + iconSize / 2;
                            var rectPadding = new Num.Vector2(4, 4);
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
            /*entityDefs = entityDefs.Where(x => x.Tags.Intersect(layerDef.RequiredTags).Count() == layerDef.RequiredTags.Count).ToList();
            if (entityDefs.Count == 0)
            {
                ImGui.TextDisabled($"No entities matches the required tags: {string.Join(", ", layerDef.RequiredTags)}");
                break;
            }*/
        }

        if (layerDef.ExcludedTags.Count > 0)
        {
            var hasAnExcludedTag = entityDef.Tags.Intersect(layerDef.ExcludedTags).Any();
            if (hasAnExcludedTag)
            {
                return true;
            }
            /*entityDefs = entityDefs.Where(x => !x.Tags.Intersect(layerDef.ExcludedTags).Any()).ToList();
            if (entityDefs.Count == 0)
            {
                ImGui.TextDisabled($"There are no entities without any of the excluded tags: {string.Join(", ", layerDef.ExcludedTags)}");
                break;
            }*/
        }

        return false;
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        var commandBuffer = _editor.GraphicsDevice.AcquireCommandBuffer();
        var viewProjection = _camera.GetViewProjection(renderDestination.Width, renderDestination.Height);

        if (WorldsWindow.SelectedWorldIndex <= _editor.RootJson.Worlds.Count - 1)
        {
            var world = _editor.RootJson.Worlds[WorldsWindow.SelectedWorldIndex];
            foreach (var level in world.Levels)
            {
                var transform = Matrix3x2.CreateScale(level.Width, level.Height) *
                                Matrix3x2.CreateTranslation(level.WorldPos.X, level.WorldPos.Y);
                renderer.DrawSprite(renderer.BlankSprite, transform.ToMatrix4x4(), level.BackgroundColor);

                DrawLayerInstances(renderer, level);
            }
        }

        DrawMouse(renderer);

        DrawGrid(renderer, Color.Black * 0.1f, 1.0f / _camera.Zoom);

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection, PipelineType.AlphaBlend);

        _editor.GraphicsDevice.Submit(commandBuffer);
    }

    public static bool GetLayerDefinition(long layerDefUid, [NotNullWhen(true)] out LayerDef? layerDef)
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

    private void DrawLayerInstances(Renderer renderer, Level level)
    {
        for (var i = 0; i < level.LayerInstances.Count; i++)
        {
            var layer = level.LayerInstances[i];
            if (!layer.IsVisible)
                continue;

            var isSelected = _selectedLayerInstanceIndex == i;
            if (!GetLayerDefinition(layer.LayerDefId, out var layerDef))
                continue;

            if (layerDef.LayerType == LayerType.IntGrid)
            {
                DrawIntGridLayer(renderer, level, layerDef, layer, isSelected);
            }
            else if (layerDef.LayerType == LayerType.Entities)
            {
                DrawEntityLayer(renderer, level, layer, layerDef, isSelected);
            }
        }
    }

    public bool GetEntityDef(int uid, [NotNullWhen(true)] out EntityDefinition? entityDef)
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

    public bool GetTileSetDef(uint uid, [NotNullWhen(true)] out TileSetDef? tileSetDef)
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

    private void DrawEntityLayer(Renderer renderer, Level level, LayerInstance layer, LayerDef layerDef, bool isSelected)
    {
        for (var k = 0; k < layer.EntityInstances.Count; k++)
        {
            var entityInstance = layer.EntityInstances[k];
            var entityDefId = entityInstance.EntityDefId;
            if (!GetEntityDef(entityDefId, out var entityDef))
            {
                DrawWarningRect(renderer, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                continue;
            }

            GetTileSetDef(entityDef.TileSetDefId, out var tileSetDef);

            var sprite = renderer.BlankSprite;
            Matrix4x4 entityTransform;
            Color color;

            if (tileSetDef != null)
            {
                var texture = SplitWindow.GetTileSetTexture(tileSetDef.Path);
                var gridSize = _editor.RootJson.DefaultGridSize;
                var tileSize = new Point(
                    (int)(texture.Width / gridSize),
                    (int)(texture.Height / gridSize)
                );
                var cellX = tileSize.X > 0 ? entityDef.TileId % tileSize.X : 0;
                var cellY = tileSize.X > 0 ? (int)(entityDef.TileId / tileSize.X) : 0;
                sprite = new Sprite(texture, new Rectangle((int)(cellX * gridSize), (int)(cellY * gridSize), gridSize, gridSize));
                entityTransform = (
                    Matrix3x2.CreateScale(1f, 1f) *
                    Matrix3x2.CreateTranslation(
                        level.WorldPos.X + entityInstance.Position.X,
                        level.WorldPos.Y + entityInstance.Position.Y
                    )).ToMatrix4x4();
                color = Color.White;
            }
            else
            {
                entityTransform = (
                    Matrix3x2.CreateScale(layerDef.GridSize, layerDef.GridSize) *
                    Matrix3x2.CreateTranslation(
                        level.WorldPos.X + entityInstance.Position.X,
                        level.WorldPos.Y + entityInstance.Position.Y
                    )).ToMatrix4x4();
                color = entityDef.Color;
            }

            renderer.DrawSprite(sprite, entityTransform, isSelected ? color : color * 0.5f);

            // Draw blinking rect if there are field instances without matching field definitions
            for (var j = 0; j < entityInstance.FieldInstances.Count; j++)
            {
                if (!GetFieldDef(entityDef, entityInstance.FieldInstances[j].FieldDefId, out var fieldDef))
                {
                    DrawWarningRect(renderer, level.WorldPos, layerDef.GridSize, entityInstance.Position);
                }
            }
        }
    }

    private void DrawWarningRect(Renderer renderer, Vector2 worldPos, uint gridSize, Vector2 position)
    {
        var scale = gridSize + ((MathF.Sin(_editor.Time.TotalElapsedTime * 3f) + 1.0f) * 0.5f) * gridSize;
        var transform = (
            Matrix3x2.CreateTranslation(-0.5f, -0.5f) *
            Matrix3x2.CreateScale(scale, scale) *
            Matrix3x2.CreateTranslation(
                worldPos.X + position.X + gridSize * 0.5f,
                worldPos.Y + position.Y + gridSize * 0.5f
            )).ToMatrix4x4();
        renderer.DrawSprite(renderer.BlankSprite, transform, Color.Red.MultiplyAlpha(0.33f));
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

    private static void DrawIntGridLayer(Renderer renderer, Level level, LayerDef layerDef, LayerInstance layer, bool isSelected)
    {
        var cols = (int)(level.Width / layerDef.GridSize);
        var rows = (int)(level.Height / layerDef.GridSize);
        for (var j = 0; j < layer.IntGrid.Length; j++)
        {
            var cellValue = layer.IntGrid[j];
            if (cellValue != 0)
            {
                var cellTransform = Matrix3x2.CreateScale(layerDef.GridSize, layerDef.GridSize) *
                                    Matrix3x2.CreateTranslation(
                                        level.WorldPos.X + (j % cols) * layerDef.GridSize,
                                        level.WorldPos.Y + (j / cols) * layerDef.GridSize
                                    );

                var color = Color.Red;
                if (GetIntDef(layerDef, cellValue, out var intDef))
                {
                    color = intDef.Color;
                }

                renderer.DrawSprite(renderer.BlankSprite, cellTransform.ToMatrix4x4(), isSelected ? color : color * 0.5f);
            }
        }
    }

    private void DrawMouse(Renderer renderer)
    {
        /*if (!GetSelectedLevel(out var level))
            return;*/

        if (!GetSelectedLayerInstance(out var world, out var level, out var layerInstance, out var layerDef))
            return;

        if (!layerInstance.IsVisible)
            return;

        var (mouseInWorld, mouseInLevel, mouseInGrid) = GetMousePositions();
        var mouseSnappedToGrid = mouseInGrid * _editor.RootJson.DefaultGridSize + level.WorldPos;
        renderer.DrawRectOutline(mouseSnappedToGrid, mouseSnappedToGrid + Vector2.One * _editor.RootJson.DefaultGridSize, Color.Red, 2f);
    }

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

    private void DrawGrid(Renderer renderer, Color lineColor, float thickness)
    {
        if (!GetSelectedLevel(out var level))
        {
            return;
        }

        var gridSize = _editor.RootJson.DefaultGridSize;

        var startX = level.WorldPos.X;
        var endX = level.WorldPos.X + level.Size.X;
        var startY = level.WorldPos.Y;
        var endY = level.WorldPos.Y + level.Size.Y;

        // vertical lines
        for (var x = startX; x < endX; x += gridSize)
        {
            var lineStart = new Vector2(x, startY);
            var lineEnd = new Vector2(lineStart.X, endY);
            renderer.DrawLine(lineStart, lineEnd, lineColor, thickness);
        }

        // horizontal lines
        for (var y = startY; y < endY; y += gridSize)
        {
            var lineStart = new Vector2(startX, y);
            var lineEnd = new Vector2(endX, lineStart.Y);
            renderer.DrawLine(lineStart, lineEnd, lineColor, thickness);
        }
    }

    private (Vector2 world, Vector2 level, Point grid) GetMousePositions()
    {
        var mouseInWorld = GetMouseInWorld();
        var mouseInGrid = Point.Zero;
        var mouseInLevel = Vector2.Zero;

        if (GetSelectedLevel(out var level))
        {
            mouseInLevel = mouseInWorld - level.WorldPos;


            var gridSize = _editor.RootJson.DefaultGridSize;
            mouseInWorld -= level.WorldPos;
            var col = MathF.FloorToInt(mouseInWorld.X / gridSize);
            var row = MathF.FloorToInt(mouseInWorld.Y / gridSize);
            mouseInGrid = new Point(col, row);
        }

        return (mouseInWorld, mouseInLevel, mouseInGrid);
    }

    private Vector2 GetMouseInWorld()
    {
        var mousePosition = _editor.InputHandler.MousePosition;
        var view = _camera.GetView();
        Matrix3x2.Invert(view, out var invertedView);
        var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
        return mouseInWorld;
    }
}
