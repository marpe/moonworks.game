using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Cameras;
using MyGame.WorldsRoot;
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

    // TODO (marpe): Cleanup
    public bool IsFocused;
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

    public void Update(float deltaSeconds)
    {
        if (_editor.InputHandler.IsMouseButtonDown(MouseButtonCode.Left))
        {
            if (WorldsWindow.SelectedWorldIndex <= _editor.WorldsRoot.Worlds.Count - 1)
            {
                var world = _editor.WorldsRoot.Worlds[WorldsWindow.SelectedWorldIndex];
                if (LevelsWindow.SelectedLevelIndex <= world.Levels.Count - 1)
                {
                    var level = world.Levels[LevelsWindow.SelectedLevelIndex];
                    if (_selectedLayerInstanceIndex <= level.LayerInstances.Count - 1)
                    {
                        var layerInstance = level.LayerInstances[_selectedLayerInstanceIndex];
                        var layerDef = _editor.WorldsRoot.LayerDefinitions.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);

                        if (layerDef != null)
                        {
                            var snappedMousePos = GetMousePosition();
                            var mouseCell = (snappedMousePos - level.WorldPos) / layerDef.GridSize;
                            var cols = (int)(level.Width / layerDef.GridSize);
                            var rows = (int)(level.Height / layerDef.GridSize);

                            if (mouseCell.X >= 0 && mouseCell.X < cols && mouseCell.Y >= 0 && mouseCell.Y < rows)
                            {
                                var cellIndex = (int)mouseCell.Y * cols + (int)mouseCell.X;
                                if (layerDef.LayerType == LayerType.IntGrid && cellIndex <= layerInstance.IntGrid.Length - 1 &&
                                    _selectedIntGridValueIndex <= layerDef.IntGridValues.Count - 1)
                                {
                                    layerInstance.IntGrid[cellIndex] = layerDef.IntGridValues[_selectedIntGridValueIndex].Value;
                                }
                                else if (layerDef.LayerType == LayerType.Entities && _editor.InputHandler.IsMouseButtonPressed(MouseButtonCode.Left))
                                {
                                    var entityDef = _editor.WorldsRoot.EntityDefinitions[_selectedEntityDefinitionIndex];

                                    var instance = new EntityInstance()
                                    {
                                        Position = (mouseCell * layerDef.GridSize).ToPoint(),
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
                                }
                            }
                        }
                    }
                }
            }
        }
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
            if (WorldsWindow.SelectedWorldIndex <= _editor.WorldsRoot.Worlds.Count - 1)
            {
                var world = _editor.WorldsRoot.Worlds[WorldsWindow.SelectedWorldIndex];

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

        for (var i = 0; i < _editor.WorldsRoot.LayerDefinitions.Count; i++)
        {
            if (level.LayerInstances.Any(x => x.LayerDefId == _editor.WorldsRoot.LayerDefinitions[i].Uid))
                continue;
            var layerInstance = CreateLayerInstance(_editor.WorldsRoot.LayerDefinitions[i], level);
            level.LayerInstances.Add(layerInstance);
        }

        DrawLayerInstances(level.LayerInstances, _editor.WorldsRoot.LayerDefinitions);
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
        if (ImGui.Begin(PreviewWindowTitle))
        {
            IsFocused = ImGui.IsWindowFocused();

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

            var windowViewportPosition = ImGui.GetWindowViewport()->Pos;
            var renderOffset = editorMin - windowViewportPosition;

            PreviewRenderViewportTransform = (
                Matrix3x2.CreateScale(1.0f, 1.0f) *
                Matrix3x2.CreateTranslation(renderOffset.X, renderOffset.Y)
            ).ToMatrix4x4();

            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _camera.Position += -ImGui.GetIO()->MouseDelta.ToXNA() * 1.0f / _camera.Zoom;
            }

            if (ImGui.GetIO()->MouseWheel != 0)
            {
                _camera.Zoom += 0.1f * ImGui.GetIO()->MouseWheel * _camera.Zoom;
            }

            var showOverlay = true;
            if (GameWindow.BeginOverlay("MoseOverlay", ref showOverlay))
            {
                var offset = Vector2.Zero;

                if (WorldsWindow.SelectedWorldIndex <= _editor.WorldsRoot.Worlds.Count - 1)
                {
                    var world = _editor.WorldsRoot.Worlds[WorldsWindow.SelectedWorldIndex];
                    if (LevelsWindow.SelectedLevelIndex <= world.Levels.Count - 1)
                    {
                        var level = world.Levels[LevelsWindow.SelectedLevelIndex];
                        offset = level.WorldPos;
                    }
                }

                var mousePosition = _editor.InputHandler.MousePosition;
                ImGuiExt.PrintVector("Pos", mousePosition);

                var view = _camera.GetView();
                Matrix3x2.Invert(view, out var invertedView);
                var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
                ImGuiExt.PrintVector("World", mouseInWorld);

                ImGuiExt.PrintVector("Level", mouseInWorld - offset);

                var mouseCell = Entity.ToCell(mouseInWorld - offset);
                ImGuiExt.PrintVector("Cel", mouseCell);

                ImGui.Dummy(new Num.Vector2(200, 0));
            }

            ImGui.End();
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
                DrawSelectedLayerBrushes(layerDef, _editor.WorldsRoot, ref _selectedIntGridValueIndex, ref _selectedEntityDefinitionIndex);
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

    private static void DrawSelectedLayerBrushes(LayerDef layerDef, WorldsRoot.WorldsRoot root, ref int selectedIntGridValueIndex,
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

                var entityDefs = root.EntityDefinitions;

                if (entityDefs.Count == 0)
                {
                    ImGui.TextDisabled("There are no entities defined");
                    break;
                }

                if (layerDef.RequiredTags.Count > 0)
                {
                    entityDefs = entityDefs.Where(x => x.Tags.Intersect(layerDef.RequiredTags).Count() == layerDef.RequiredTags.Count).ToList();
                    if (entityDefs.Count == 0)
                    {
                        ImGui.TextDisabled($"No entities matches the required tags: {string.Join(", ", layerDef.RequiredTags)}");
                        break;
                    }
                }

                if (layerDef.ExcludedTags.Count > 0)
                {
                    entityDefs = entityDefs.Where(x => !x.Tags.Intersect(layerDef.ExcludedTags).Any()).ToList();
                    if (entityDefs.Count == 0)
                    {
                        ImGui.TextDisabled($"There are no entities without any of the excluded tags: {string.Join(", ", layerDef.ExcludedTags)}");
                        break;
                    }
                }

                var tileSetDefs = root.TileSetDefinitions;
                var gridSize = root.DefaultGridSize;

                if (ImGui.BeginTable("EntityDefTable", 1, SplitWindow.TableFlags, new Num.Vector2(0, 0)))
                {
                    ImGui.TableSetupColumn("Value");

                    for (var i = 0; i < entityDefs.Count; i++)
                    {
                        var entityDef = entityDefs[i];
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

                        var tileSet = tileSetDefs.FirstOrDefault(x => x.Uid == entityDef.TilesetId);
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

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        var commandBuffer = _editor.GraphicsDevice.AcquireCommandBuffer();
        var viewProjection = _camera.GetViewProjection(renderDestination.Width, renderDestination.Height);

        if (WorldsWindow.SelectedWorldIndex <= _editor.WorldsRoot.Worlds.Count - 1)
        {
            var world = _editor.WorldsRoot.Worlds[WorldsWindow.SelectedWorldIndex];
            foreach (var level in world.Levels)
            {
                var transform = Matrix3x2.CreateScale(level.Width, level.Height) *
                                Matrix3x2.CreateTranslation(level.WorldPos.X, level.WorldPos.Y);
                renderer.DrawSprite(renderer.BlankSprite, transform.ToMatrix4x4(), level.BackgroundColor);

                foreach (var layer in level.LayerInstances)
                {
                    var layerDef = _editor.WorldsRoot.LayerDefinitions.FirstOrDefault(x => x.Uid == layer.LayerDefId);
                    if (layerDef == null)
                        continue;
                    if (layerDef.LayerType == LayerType.IntGrid)
                    {
                        var cols = (int)(level.Width / layerDef.GridSize);
                        var rows = (int)(level.Height / layerDef.GridSize);
                        for (var i = 0; i < layer.IntGrid.Length; i++)
                        {
                            var cellValue = layer.IntGrid[i];
                            if (cellValue != 0)
                            {
                                var cellTransform = Matrix3x2.CreateScale(layerDef.GridSize, layerDef.GridSize) *
                                                    Matrix3x2.CreateTranslation(
                                                        level.WorldPos.X + (i % cols) * layerDef.GridSize,
                                                        level.WorldPos.Y + (i / cols) * layerDef.GridSize
                                                    );
                                var intDef = layerDef.IntGridValues.First(x => x.Value == cellValue);
                                renderer.DrawSprite(renderer.BlankSprite, cellTransform.ToMatrix4x4(), intDef.Color);
                            }
                        }
                    }
                    else if (layerDef.LayerType == LayerType.Entities)
                    {
                        foreach (var entityInstance in layer.EntityInstances)
                        {
                            var entityDef = _editor.WorldsRoot.EntityDefinitions.FirstOrDefault(x => x.Uid == entityInstance.EntityDefId);
                            if (entityDef == null)
                                continue;
                            var entityTransform = Matrix3x2.CreateScale(layerDef.GridSize, layerDef.GridSize) *
                                                  Matrix3x2.CreateTranslation(
                                                      level.WorldPos.X + entityInstance.Position.X,
                                                      level.WorldPos.Y + entityInstance.Position.Y
                                                  );
                            renderer.DrawSprite(renderer.BlankSprite, entityTransform.ToMatrix4x4(), entityDef.Color);
                        }
                    }
                }
            }
        }

        var snappedMousePos = GetMousePosition();

        renderer.DrawRectOutline(snappedMousePos, snappedMousePos + new Vector2(16, 16), Color.Red, 2f);

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);

        _editor.GraphicsDevice.Submit(commandBuffer);
    }

    private Vector2 GetMousePosition()
    {
        var mousePosition = _editor.InputHandler.MousePosition;
        var view = _camera.GetView();
        Matrix3x2.Invert(view, out var invertedView);
        var mouseInWorld = Vector2.Transform(mousePosition, invertedView);

        var offset = Vector2.Zero;
        if (WorldsWindow.SelectedWorldIndex <= _editor.WorldsRoot.Worlds.Count - 1)
        {
            var world = _editor.WorldsRoot.Worlds[WorldsWindow.SelectedWorldIndex];
            if (LevelsWindow.SelectedLevelIndex <= world.Levels.Count - 1)
            {
                var level = world.Levels[LevelsWindow.SelectedLevelIndex];
                offset = level.WorldPos;
            }
        }

        var gridSize = 16;
        return new Vector2(
            MathF.Floor((mouseInWorld.X - offset.X /* - gridSize * 0.5f*/) / gridSize) * gridSize,
            MathF.Floor((mouseInWorld.Y - offset.Y /* - gridSize * 0.5f*/) / gridSize) * gridSize
        ) + offset;
    }
}
