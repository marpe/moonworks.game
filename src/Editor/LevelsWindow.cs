using Mochi.DearImGui;
using MyGame.WorldsRoot;
using Level = MyGame.WorldsRoot.Level;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public class LevelsWindow : SplitWindow
{
    public const string WindowTitle = "Levels";
    private int _rowMinHeight = 60;
    public static int SelectedLevelIndex;
    private int _selectedFieldDefinitionIndex;

    public LevelsWindow(MyEditorMain editor) : base(WindowTitle, editor)
    {
    }

    private void DrawLevels()
    {
        if (WorldsWindow.SelectedWorldIndex > RootJson.Worlds.Count - 1)
        {
            ImGui.TextDisabled("Select a world");
            return;
        }

        var world = RootJson.Worlds[WorldsWindow.SelectedWorldIndex];

        if (world.Levels.Count == 0)
        {
            ImGui.TextDisabled("This world contains no levels");
            return;
        }

        if (ImGui.BeginTable("Levels", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var levelToDelete = -1;
            for (var i = 0; i < world.Levels.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();
                ImGui.PushID(i);

                var level = world.Levels[i];

                var isSelected = SelectedLevelIndex == i;
                var color = level.BackgroundColor;
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    SelectedLevelIndex = i;
                }

                // ImGui.SameLine(0, 0);

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        levelToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                var labelColor = isSelected ? Color.White : color;
                CenteredButton(color, _rowMinHeight);
                GiantLabel(level.Identifier, labelColor, _rowMinHeight);

                ImGui.PopID();
            }

            if (levelToDelete != -1)
            {
                world.Levels.RemoveAt(levelToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawLeft()
    {
        DrawLevels();

        if (WorldsWindow.SelectedWorldIndex > RootJson.Worlds.Count - 1)
        {
            ImGui.TextDisabled("Select a world");
            return;
        }

        if (ImGuiExt.ColoredButton("+ Add Level", new Vector2(-1, 0)))
        {
            RootJson.Worlds[WorldsWindow.SelectedWorldIndex].Levels.Add(new Level()
            {
                Uid = GetNextId(RootJson.Worlds[WorldsWindow.SelectedWorldIndex].Levels)
            });
        }

        var fieldDefRemoved = (FieldDef def) =>
        {
            foreach (var world in RootJson.Worlds)
            {
                foreach (var level in world.Levels)
                {
                    for (var i = level.FieldInstances.Count - 1; i >= 0; i--)
                    {
                        if (level.FieldInstances[i].FieldDefId == def.Uid)
                        {
                            level.FieldInstances.RemoveAt(i);
                        }
                    }
                }
            }
        };

        ImGuiExt.SeparatorText("Custom Level Field Definitions");

        FieldDefEditor.DrawFieldEditor(RootJson.LevelFieldDefinitions, ref _selectedFieldDefinitionIndex, fieldDefRemoved);
    }

    private int GetNextId(List<Level> levels)
    {
        var maxId = 0;
        for (var i = 0; i < levels.Count; i++)
            if (maxId <= levels[i].Uid)
                maxId = levels[i].Uid + 1;
        return maxId;
    }

    protected override void DrawRight()
    {
        if (WorldsWindow.SelectedWorldIndex > RootJson.Worlds.Count - 1)
            return;

        var world = RootJson.Worlds[WorldsWindow.SelectedWorldIndex];

        if (SelectedLevelIndex > world.Levels.Count - 1)
        {
            ImGui.TextDisabled("Select a level");
            return;
        }

        var level = world.Levels[SelectedLevelIndex];

        SimpleTypeInspector.InspectString("Identifier", ref level.Identifier);
        var rangeSettings = new RangeSettings(16, 16000, 1f, true);
        var oldSize = level.Size;
        if (SimpleTypeInspector.InspectUInt("Width", ref level.Width, rangeSettings))
        {
            ResizeLayers(level, oldSize, RootJson.LayerDefinitions);
        }

        if (SimpleTypeInspector.InspectUInt("Height", ref level.Height, rangeSettings))
        {
            ResizeLayers(level, oldSize, RootJson.LayerDefinitions);
        }

        var layerDef = RootJson.LayerDefinitions.FirstOrDefault();
        if (layerDef != null)
        {
            var gridSize = new UPoint(level.Width / layerDef.GridSize, level.Height / layerDef.GridSize);
            var minGridSize = (uint)(rangeSettings.MinValue / layerDef.GridSize);
            var maxGridSize = (uint)(rangeSettings.MaxValue / layerDef.GridSize);
            if (SimpleTypeInspector.InspectUPoint("Cells", ref gridSize, 1.0f, (int)minGridSize, (int)maxGridSize))
            {
                level.Width = Math.Clamp(gridSize.X * layerDef.GridSize, (uint)rangeSettings.MinValue, (uint)rangeSettings.MaxValue);
                level.Height = Math.Clamp(gridSize.Y * layerDef.GridSize, (uint)rangeSettings.MinValue, (uint)rangeSettings.MaxValue);

                ResizeLayers(level, oldSize, RootJson.LayerDefinitions);
            }
        }

        SimpleTypeInspector.InspectPoint("WorldPos", ref level.WorldPos);
        SimpleTypeInspector.InspectColor("BackgroundColor", ref level.BackgroundColor);

        ImGuiExt.SeparatorText("Custom Level Fields");

        FieldInstanceInspector.DrawFieldInstances(level.FieldInstances, RootJson.LevelFieldDefinitions);
    }

    private static void ResizeLayers(Level level, UPoint oldSize, List<LayerDef> layerDefs)
    {
        for (var i = 0; i < level.LayerInstances.Count; i++)
        {
            var layer = level.LayerInstances[i];
            var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layer.LayerDefId);
            if (layerDef == null)
            {
                Logs.LogError($"Could not find a layer definition with id \"{layer.LayerDefId}\"");
                continue;
            }

            if (layerDef.LayerType == LayerType.IntGrid)
            {
                var oldGrid = oldSize / layerDef.GridSize;
                var newGrid = level.Size / layerDef.GridSize;
                ResizeLayer(ref layer.IntGrid, oldGrid, newGrid, Point.Zero);
            }
            else
            {
                Array.Resize(ref layer.IntGrid, 0);
            }
            // TODO (marpe): Cleanup entities outside level etc
        }
    }

    private static void ResizeLayer(ref int[] intGrid, UPoint oldGrid, UPoint newGrid, Point moveDelta)
    {
        if (oldGrid == newGrid && intGrid.Length == newGrid.X * newGrid.Y)
            return;

        var old = new int[intGrid.Length];
        Array.Copy(intGrid, old, intGrid.Length);

        intGrid = new int[newGrid.X * newGrid.Y];

        for (var y = 0; y <= oldGrid.Y - 1; y++)
        {
            for (var x = 0; x <= oldGrid.X - 1; x++)
            {
                var newCx = x + moveDelta.X;
                var newCy = y + moveDelta.Y;
                var newGridId = newCy * newGrid.X + newCx;
                if (newCx >= 0 && newCx < newGrid.X &&
                    newCy >= 0 && newCy < newGrid.Y &&
                    newGridId >= 0 && newGridId < intGrid.Length)
                {
                    var oldGridId = y * oldGrid.X + x;
                    if (oldGridId >= 0 && oldGridId < old.Length)
                    {
                        intGrid[newGridId] = old[oldGridId];
                    }
                }
            }
        }
    }

    public static void ResizeLevel(Level level, UPoint oldSize, UPoint newSize, Point moveDelta, uint gridSize)
    {
        if (oldSize == newSize)
            return;

        var oldGrid = oldSize / gridSize;
        var newGrid = newSize / gridSize;

        if (oldGrid == newGrid)
            return;

        for (var i = 0; i < level.LayerInstances.Count; i++)
        {
            var layer = level.LayerInstances[i];
            if (layer.IntGrid.Length > 0)
                ResizeLayer(ref layer.IntGrid, oldGrid, newGrid, moveDelta);

            if (moveDelta.X == 0 && moveDelta.Y == 0)
                continue;

            for (var j = 0; j < layer.AutoLayerTiles.Count; j++)
            {
                var tile = layer.AutoLayerTiles[j];
                tile.Cell = new UPoint((uint)(tile.Cell.X + moveDelta.X), (uint)(tile.Cell.Y + moveDelta.Y));
                layer.AutoLayerTiles[j] = tile;
            }

            for (var j = 0; j < layer.EntityInstances.Count; j++)
            {
                var entity = layer.EntityInstances[j];
                entity.Position += moveDelta * gridSize;
            }
        }
    }
}
