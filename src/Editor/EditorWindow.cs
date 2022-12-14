using Mochi.DearImGui;
using MyGame.Cameras;
using MyGame.WorldsRoot;
using FieldInstance = MyGame.WorldsRoot.FieldInstance;
using LayerInstance = MyGame.WorldsRoot.LayerInstance;
using Level = MyGame.WorldsRoot.Level;
using Vector2 = System.Numerics.Vector2;

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

    public EditorWindow(MyEditorMain editor) : base(WindowTitle)
    {
        KeyboardShortcut = "^E";
        _editor = editor;
    }

    public void Update(float deltaSeconds)
    {
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        DrawPreviewWindow();

        DrawCurrentLevelData();
    }

    private void DrawCurrentLevelData()
    {
        if (ImGui.Begin(WindowTitle))
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
        if (ImGui.Begin(PreviewWindowTitle))
        {
            GameWindow.EnsureTextureIsBound(ref _editorRenderTextureId, _editor._editorRenderTarget, _editor.ImGuiRenderer);
            var cursorScreenPosition = ImGui.GetCursorScreenPos();

            var editorMin = cursorScreenPosition;
            var editorMax = editorMin + new Vector2(_editor._editorRenderTarget.Width, _editor._editorRenderTarget.Height);
            var dl = ImGui.GetWindowDrawList();
            dl->AddImage(
                (void*)_editorRenderTextureId.Value,
                editorMin,
                editorMax,
                Vector2.Zero,
                Vector2.One,
                Color.White.PackedValue
            );

            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _camera.Position += -ImGui.GetIO()->MouseDelta.ToXNA() * 1.0f / _camera.Zoom;
            }

            if (ImGui.GetIO()->MouseWheel != 0)
            {
                _camera.Zoom += 0.1f * ImGui.GetIO()->MouseWheel * _camera.Zoom;
            }
        }

        ImGui.End();
    }

    private void DrawLayerInstances(List<LayerInstance> layerInstances, List<LayerDef> layerDefs)
    {
        var rowHeight = 40;
        if (ImGui.BeginTable("LayerInstances", 1, SplitWindow.TableFlags, new Vector2(0, 0)))
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

                if (ImGui.BeginTable("IntGridTable", 1, SplitWindow.TableFlags, new Vector2(0, 0)))
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
                        var min = cursorPos + new Vector2(8, (_rowMinHeight - rectHeight) / 2);
                        var max = min + new Vector2(32, rectHeight);
                        ImGuiExt.RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
                        var label = intGridValue.Value.ToString();
                        var textSize = ImGui.CalcTextSize(label);
                        var rectSize = max - min;
                        dl->AddText(min + new Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2), Color.White.PackedValue,
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

                if (ImGui.BeginTable("EntityDefTable", 1, SplitWindow.TableFlags, new Vector2(0, 0)))
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
                            var uvMin = new Vector2(1.0f / texture.Width * cellX * gridSize,
                                1.0f / texture.Height * cellY * gridSize);
                            var uvMax = uvMin + new Vector2(gridSize / (float)texture.Width, gridSize / (float)texture.Height);
                            var iconSize = new Vector2(32, 32);
                            var iconPos = cursorPos + iconSize / 2;
                            var rectPadding = new Vector2(4, 4);
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
            }
        }

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);

        _editor.GraphicsDevice.Submit(commandBuffer);
    }
}
