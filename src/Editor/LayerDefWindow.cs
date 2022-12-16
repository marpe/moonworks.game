using Mochi.DearImGui;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class LayerDefWindow : SplitWindow
{
    private static int _rowMinHeight = 60;
    private int _selectedLayerDefIndex;
    private string _tempExcludedTag = "";
    private string _tempRequiredTag = "";
    public const string WindowTitle = "Layers";

    public LayerDefWindow(MyEditorMain editor) : base(WindowTitle, editor)
    {
    }


    public static Color GetLayerDefColor(LayerType type)
    {
        return type switch
        {
            LayerType.IntGrid => ImGuiExt.Colors[2],
            LayerType.Entities => ImGuiExt.Colors[3],
            LayerType.Tiles => ImGuiExt.Colors[4],
            LayerType.AutoLayer => ImGuiExt.Colors[5],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private void DrawLayerDefinitions()
    {
        if (ImGui.BeginTable("LayerDefinitions", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var layerToDelete = -1;
            for (var i = 0; i < RootJson.LayerDefinitions.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var layerDef = RootJson.LayerDefinitions[i];

                var isSelected = _selectedLayerDefIndex == i;
                var color = GetLayerDefColor(layerDef.LayerType);
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedLayerDefIndex = i;
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

                var labelColor = isSelected ? Color.White : color;
                Icon(LayerTypeIcon(layerDef.LayerType), color, _rowMinHeight);
                GiantLabel(layerDef.Identifier, labelColor, _rowMinHeight);

                ImGui.PopID();
            }

            if (layerToDelete != -1)
            {
                RootJson.LayerDefinitions.RemoveAt(layerToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawLeft()
    {
        DrawLayerDefinitions();
        if (ImGuiExt.ColoredButton("+ Add Layer Definition", new Vector2(-1, 0)))
        {
            RootJson.LayerDefinitions.Add(new LayerDef());
        }
    }

    protected override void DrawRight()
    {
        if (_selectedLayerDefIndex > RootJson.LayerDefinitions.Count - 1)
            return;

        var layerDef = RootJson.LayerDefinitions[_selectedLayerDefIndex];
        SimpleTypeInspector.InspectInputInt("Uid", ref layerDef.Uid);
        SimpleTypeInspector.InspectString("Identifier", ref layerDef.Identifier);
        var rangeSettings = new RangeSettings { MinValue = 16, MaxValue = 512, StepSize = 1, UseDragVersion = false };
        SimpleTypeInspector.InspectUInt("GridSize", ref layerDef.GridSize, rangeSettings);
        EnumInspector.InspectEnum("LayerType", ref layerDef.LayerType, true);

        if (layerDef.LayerType == LayerType.Entities)
        {
            DrawLayerDefTags(layerDef);
        }
        else if (layerDef.LayerType == LayerType.IntGrid)
        {
            SimpleTypeInspector.InspectInputUint("TileSetDefId", ref layerDef.TileSetDefId);
        }

        if (layerDef.LayerType == LayerType.IntGrid)
        {
            var tableFlags2 = ImGuiTableFlags.BordersH | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Hideable |
                              ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.SizingFixedFit |
                              ImGuiTableFlags.RowBg;

            var rowHeight = 60;

            DrawIntGridValues(tableFlags2, layerDef, rowHeight);

            DrawAutoRuleGroups(tableFlags2, layerDef, rowHeight);
        }
    }

    private static void DrawIntGridValues(ImGuiTableFlags tableFlags2, LayerDef layerDef, int rowHeight)
    {
        ImGui.PushID("IntGridValues");
        var valueToRemove = -1;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(ImGui.GetStyle()->CellPadding.X * 2f, ImGui.GetStyle()->CellPadding.Y * 2.5f));
        if (ImGui.BeginTable("IntGridValues", 4, tableFlags2, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.None, 32);
            ImGui.TableSetupColumn("Identifier", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.None, 20);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.NoHeaderLabel, 30);

            for (var i = 0; i < layerDef.IntGridValues.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn();
                DrawIntValue((i + 1).ToString(), new Vector2(rowHeight, rowHeight) * 0.6f, layerDef.IntGridValues[i].Color);
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));
                SimpleTypeInspector.InspectString("##Identifier", ref layerDef.IntGridValues[i].Identifier);
                ImGui.TableNextColumn();
                SimpleTypeInspector.InspectColor("##Color", ref layerDef.IntGridValues[i].Color);
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
                if (ImGuiExt.ColoredButton(FontAwesome6.Trash, Color.White, ImGuiExt.Colors[2], "Remove", new Vector2(30, 0),
                        new Vector2(ImGui.GetStyle()->FramePadding.X, 4)))
                {
                    valueToRemove = i;
                }

                ImGui.PopStyleVar();
                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.PopStyleVar();

        if (valueToRemove != -1)
        {
            layerDef.IntGridValues.RemoveAt(valueToRemove);
        }

        if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.White, ImGuiExt.Colors[0], new Vector2(-1, 0), "Add Value"))
        {
            var value = layerDef.IntGridValues.Count + 1;
            layerDef.IntGridValues.Add(new IntGridValue
            {
                Value = value,
                Color = ImGuiExt.Colors[value % ImGuiExt.Colors.Length],
                Identifier = "Identifier",
            });
        }

        ImGui.PopID();
    }

    private static void DrawAutoRuleGroups(ImGuiTableFlags tableFlags2, LayerDef layerDef, int rowHeight)
    {
        ImGui.PushID("AutoRules");
        var valueToRemove = -1;
        if (ImGui.BeginTable("AutoRuleGroups", 2, tableFlags2, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.None, 36);

            for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var group = layerDef.AutoRuleGroups[i];
                ImGui.SetNextItemWidth(-1);
                SimpleTypeInspector.InspectString("##Name", ref group.Name);

                if (group.Rules.Count > 0)
                {
                    if (ImGui.BeginTable("GroupRules", 2, tableFlags2, new Vector2(0, 0)))
                    {
                        ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch);

                        for (var k = 0; k < group.Rules.Count; k++)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.PushID(k);
                            var rule = group.Rules[k];
                            DrawRuleMatrix(rule, layerDef.IntGridValues);
                            ImGui.TableNextColumn();
                            DrawSizeCombo(ref rule.Size);

                            ImGui.SetNextItemWidth(-1);
                            SimpleTypeInspector.InspectInputUint("##NewTileId", ref _tempTileId);
                            ImGui.SetNextItemWidth(-1);
                            if (ImGuiExt.ColoredButton("+", ImGuiExt.Colors[1], new Vector2(-ImGuiExt.FLT_MIN, 0)))
                            {
                                if (!rule.TileIds.Contains((int)_tempTileId))
                                    rule.TileIds.Add((int)_tempTileId);
                            }

                            for (var m = 0; m < rule.TileIds.Count; m++)
                            {
                                ImGuiExt.ColoredButton(rule.TileIds[m].ToString());
                                if (m < rule.TileIds.Count - 1 && (m == 0 || m % 5 != 0))
                                    ImGui.SameLine();
                            }

                            ImGui.PopID();
                        }

                        ImGui.EndTable();
                    }
                }


                ImGui.TableNextColumn();

                if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.Orange, new Vector2(30, 0), "Add Rule"))
                {
                    group.Rules.Add(
                        new AutoRule
                        {
                            UId = IdGen.NewId,
                            BreakOnMatch = true,
                            IsActive = true,
                            Chance = 1.0f,
                        }
                    );
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (valueToRemove != -1)
        {
            layerDef.AutoRuleGroups.RemoveAt(valueToRemove);
        }

        if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.White, ImGuiExt.Colors[0], new Vector2(-1, 0), "Add Group"))
        {
            layerDef.AutoRuleGroups.Add(new AutoRuleGroup
            {
                Name = "New Group",
                Uid = IdGen.NewId,
                IsActive = true,
            });
        }

        ImGui.PopID();
    }

    private static void DrawRuleMatrix(AutoRule rule, List<IntGridValue> intGridValues)
    {
        while (rule.Pattern.Count > rule.Size * rule.Size)
        {
            rule.Pattern.RemoveAt(rule.Pattern.Count - 1);
        }

        for (var ii = 0; ii < rule.Size * rule.Size; ii++)
        {
            rule.Pattern.Add(0);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        for (var y = 0; y < rule.Size; y++)
        {
            for (var x = 0; x < rule.Size; x++)
            {
                var value = rule.Pattern[y * rule.Size + x];
                var color = intGridValues.FirstOrDefault(i => i.Value == value)?.Color ?? Color.Black;
                ImGuiExt.ColoredButton($"{value.ToString()}##{x}_{y}", color, new Vector2(30));
                if (x < rule.Size - 1)
                    ImGui.SameLine(0, 0);
            }
        }

        ImGui.PopStyleVar();
    }

    private static int[] _ruleMatrixSizes = new[] { 1, 3, 5, 7 };
    private static string[] _ruleMatrixLabels = new[] { "1x1", "3x3", "5x5", "7x7" };
    private static uint _tempTileId;

    private static void DrawSizeCombo(ref int size)
    {
        var currentIndex = Array.IndexOf(_ruleMatrixSizes, size);
        var label = _ruleMatrixLabels[currentIndex];
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##Size", label))
        {
            for (var i = 0; i < _ruleMatrixSizes.Length; i++)
            {
                var isSelected = i == currentIndex;
                if (ImGui.Selectable(_ruleMatrixLabels[i], isSelected, ImGuiSelectableFlags.None, default))
                {
                    currentIndex = i;
                    size = _ruleMatrixSizes[currentIndex];
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private static void DrawIntValue(string label, Vector2 size, Color color)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        var fillColor = color.MultiplyAlpha(0.33f);
        var outlineColor = color;

        ImGuiExt.RectWithOutline(dl, min, max, fillColor, outlineColor, 4f);

        var textSize = ImGui.CalcTextSize(label);
        var textPos = min + size * 0.5f - textSize * 0.5f;
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 18f, textPos, Color.White.PackedValue, label);
    }

    private void DrawLayerDefTags(LayerDef layerDef)
    {
        ImGuiExt.SeparatorText("Excluded Tags");
        ImGui.SetNextItemWidth(100);
        SimpleTypeInspector.InspectString("##TagExcluded", ref _tempExcludedTag);
        ImGui.SameLine();
        ImGui.BeginDisabled(_tempExcludedTag == "");
        if (ImGuiExt.ColoredButton("+##AddExcluded", Color.White, ImGuiExt.Colors[2], "Add Excluded Tag", new Vector2(0, ImGui.GetFrameHeight()),
                new Vector2(ImGui.GetStyle()->FramePadding.X, 2)))
        {
            layerDef.ExcludedTags.Add(_tempExcludedTag);
            _tempExcludedTag = "";
        }

        ImGui.EndDisabled();
        var exclTagToRemove = -1;
        for (var i = 0; i < layerDef.ExcludedTags.Count; i++)
        {
            if (ImGuiExt.ColoredButton(layerDef.ExcludedTags[i]))
                exclTagToRemove = i;
            if (i < layerDef.ExcludedTags.Count - 1)
                ImGui.SameLine();
        }

        if (exclTagToRemove != -1)
            layerDef.ExcludedTags.RemoveAt(exclTagToRemove);

        ImGuiExt.SeparatorText("Required Tags");

        ImGui.SetNextItemWidth(100);
        SimpleTypeInspector.InspectString("##TagRequired", ref _tempRequiredTag);
        ImGui.SameLine();
        ImGui.BeginDisabled(_tempRequiredTag == "");
        if (ImGuiExt.ColoredButton("+##AddRequired", Color.White, ImGuiExt.Colors[2], "Add Required Tag", new Vector2(0, ImGui.GetFrameHeight()),
                new Vector2(ImGui.GetStyle()->FramePadding.X, 2)))
        {
            layerDef.RequiredTags.Add(_tempRequiredTag);
            _tempRequiredTag = "";
        }

        ImGui.EndDisabled();
        var reqTagToRemove = -1;
        for (var i = 0; i < layerDef.RequiredTags.Count; i++)
        {
            if (ImGuiExt.ColoredButton(layerDef.RequiredTags[i]))
                reqTagToRemove = i;
            if (i < layerDef.RequiredTags.Count - 1)
                ImGui.SameLine();
        }

        if (reqTagToRemove != -1)
            layerDef.RequiredTags.RemoveAt(reqTagToRemove);
    }
}
