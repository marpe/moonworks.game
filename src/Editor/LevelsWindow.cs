using Mochi.DearImGui;
using MyGame.WorldsRoot;
using FieldInstance = MyGame.WorldsRoot.FieldInstance;
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
                var color = ImGuiExt.Colors[1];
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    SelectedLevelIndex = i;
                }

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

        FieldDefEditor.DrawFieldEditor(RootJson.LevelFieldDefinitions, ref _selectedFieldDefinitionIndex, fieldDefRemoved);
    }

    private int GetNextId(List<Level> levels)
    {
        var maxId = 0;
        for (var i = 0; i < levels.Count; i++)
            if (maxId < levels[i].Uid)
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

        ImGui.PushItemWidth(ImGui.GetWindowWidth());

        var level = world.Levels[SelectedLevelIndex];

        SimpleTypeInspector.InspectString("Identifier", ref level.Identifier);
        var rangeSettings = new RangeSettings(16, 16000, 1f, true);
        if (SimpleTypeInspector.InspectUInt("Width", ref level.Width, rangeSettings))
        {
            ResizeLayers(level, RootJson.LayerDefinitions);
        }

        if (SimpleTypeInspector.InspectUInt("Height", ref level.Height, rangeSettings))
        {
            ResizeLayers(level, RootJson.LayerDefinitions);
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
            }
        }

        SimpleTypeInspector.InspectPoint("WorldPos", ref level.WorldPos);
        SimpleTypeInspector.InspectColor("BackgroundColor", ref level.BackgroundColor);

        DrawFieldInstances(level.FieldInstances, RootJson.LevelFieldDefinitions);

        ImGui.PopItemWidth();
    }

    private static void ResizeLayers(Level level, List<LayerDef> layerDefs)
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
                var cols = level.Width / layerDef.GridSize;
                var rows = level.Height / layerDef.GridSize;
                Array.Resize(ref layer.IntGrid, (int)(cols * rows));
            }
            else
            {
                Array.Resize(ref layer.IntGrid, 0);
            }
        }
    }

    private static void DrawFieldInstances(List<FieldInstance> fieldInstances, List<FieldDef> fieldDefs)
    {
        ImGuiExt.SeparatorText("Fields");

        if (fieldDefs.Count == 0)
        {
            ImGui.TextDisabled("No fields have been defined");
            return;
        }

        for (var i = 0; i < fieldDefs.Count; i++)
        {
            if (fieldInstances.Any(x => x.FieldDefId == fieldDefs[i].Uid))
                continue;

            fieldInstances.Add(CreateFieldInstance(fieldDefs[i]));
        }

        for (var i = 0; i < fieldInstances.Count; i++)
        {
            ImGui.PushID(i);
            var fieldInstance = fieldInstances[i];
            var fieldDef = fieldDefs.FirstOrDefault(x => x.Uid == fieldInstance.FieldDefId);
            if (fieldDef == null)
            {
                ImGui.Text($"Could not find a level field definition with uid \"{fieldInstance.FieldDefId}\"");
                ImGui.PopID();
                continue;
            }

            if (fieldInstance.Value == null || fieldInstance.Value.GetType() != FieldDef.GetActualType(fieldDef.FieldType, fieldDef.IsArray))
            {
                if (fieldDef.IsArray)
                {
                    fieldInstance.Value = FieldDef.GetDefaultValue(fieldDef.FieldType, fieldDef.IsArray);
                }
                else
                {
                    fieldInstance.Value = fieldDef.DefaultValue ?? FieldDef.GetDefaultValue(fieldDef.FieldType, fieldDef.IsArray);
                }
            }

            if (fieldDef.IsArray)
            {
                var list = (IList)fieldInstance.Value;

                ImGuiExt.LabelPrefix(fieldDef.Identifier);
                ImGui.NewLine();

                if (list.Count == 0)
                {
                    ImGui.TextDisabled("There are no elements in the array");
                }
                else
                {
                    var indexToRemove = -1;
                    if (ImGui.BeginTable("Value", 2, ImGuiTableFlags.BordersH | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.SizingFixedFit,
                            new Vector2(0, 0)))
                    {
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.NoResize);

                        for (var j = 0; j < list.Count; j++)
                        {
                            ImGui.PushID(j);
                            var value = list[j];
                            if (value == null)
                            {
                                value = fieldDef.DefaultValue ?? FieldDef.GetDefaultValue(fieldDef.FieldType, false);
                            }

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.SetNextItemWidth(-1);
                            if (SimpleTypeInspector.DrawSimpleInspector(value.GetType(), "##Value", ref value))
                            {
                                list[j] = value;
                            }

                            ImGui.TableNextColumn();

                            if (ImGuiExt.ColoredButton(FontAwesome6.Trash, ImGuiExt.Colors[2], "Remove"))
                            {
                                indexToRemove = j;
                            }

                            ImGui.PopID();
                        }

                        ImGui.EndTable();
                    }

                    if (indexToRemove != -1)
                    {
                        list.RemoveAt(indexToRemove);
                    }
                }

                if (ImGuiExt.ColoredButton("+", new Num.Vector2(-1, 0)))
                {
                    list.Add(FieldDef.GetDefaultValue(fieldDef.FieldType, false));
                }
            }
            else
            {
                SimpleTypeInspector.DrawSimpleInspector(fieldInstance.Value.GetType(), fieldDef.Identifier, ref fieldInstance.Value);
            }

            ImGui.PopID();
        }
    }

    private static FieldInstance CreateFieldInstance(FieldDef fieldDef)
    {
        return new FieldInstance
        {
            Value = FieldDef.GetDefaultValue(fieldDef.FieldType, fieldDef.IsArray),
            FieldDefId = fieldDef.Uid
        };
    }
}
