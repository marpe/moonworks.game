using Mochi.DearImGui;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class LayerDefWindow : SplitWindow
{
    public const int ANYTHING_TILE_ID = 10000;
    public const int NOTHING_TILE_ID = -ANYTHING_TILE_ID;
    private static int[] _ruleMatrixSizes = new[] { 1, 3, 5, 7 };
    private static string[] _ruleMatrixLabels = new[] { "1x1", "3x3", "5x5", "7x7" };
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

            var tileSetDef = RootJson.TileSetDefinitions.FirstOrDefault(x => x.Uid == layerDef.TileSetDefId);
            if (tileSetDef != null && tileSetDef.Path != "") // TODO (marpe): Check that the texture is loaded etc
            {
                DrawAutoRuleGroups(tableFlags2, layerDef, tileSetDef, rowHeight);
            }
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

    private static void DrawAutoRuleGroups(ImGuiTableFlags tableFlags2, LayerDef layerDef, TileSetDef tileSetDef, int rowHeight)
    {
        ImGui.PushID("AutoRules");
        var valueToRemove = -1;
        if (ImGui.BeginTable("AutoRuleGroups", 1, tableFlags2, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var group = layerDef.AutoRuleGroups[i];
                ImGui.SetNextItemWidth(-100);
                SimpleTypeInspector.InspectString("##Name", ref group.Name);

                ImGui.SameLine();
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

                ImGui.SameLine();
                if (ImGuiExt.ColoredButton(FontAwesome6.Trash, Color.Red, new Vector2(30, 0), "Remove Group"))
                {
                    valueToRemove = i;
                }

                if (group.Rules.Count > 0)
                {
                    if (ImGui.BeginTable("GroupRules", 3, tableFlags2 | ImGuiTableFlags.BordersV, new Vector2(0, 0)))
                    {
                        ImGui.TableSetupColumn("DragDrop", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch);

                        for (var k = 0; k < group.Rules.Count; k++)
                        {
                            var rule = group.Rules[k];
                            ImGui.PushID(k);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            ImGuiExt.TextButton(FontAwesome6.EllipsisVertical, "Drag to move", Color.White.PackedValue, new Vector2(40, 40));

                            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                            {
                                ImGui.SetDragDropPayload($"Group{i}_Rule_Row", &k, sizeof(int));
                                ImGui.Text($"Dragging rule {k}");
                                ImGui.EndDragDropSource();
                            }

                            if (ImGui.BeginDragDropTarget())
                            {
                                var payload = ImGui.AcceptDragDropPayload($"Group{i}_Rule_Row");
                                if (payload != null)
                                {
                                    var rowIndex = *(int*)payload->Data;
                                    group.Rules[k] = group.Rules[rowIndex];
                                    group.Rules[rowIndex] = rule;
                                }

                                ImGui.EndDragDropTarget();
                            }

                            ImGui.TableNextColumn();
                            DrawRuleMatrix(rule, layerDef.IntGridValues);
                            ImGui.TableNextColumn();
                            DrawSizeCombo(ref rule.Size);

                            ImGui.SetNextItemWidth(250);
                            SimpleTypeInspector.InspectBool("BreakOnMatch", ref rule.BreakOnMatch);
                            SimpleTypeInspector.InspectBool("IsActive", ref rule.IsActive);
                            SimpleTypeInspector.InspectFloat("Chance", ref rule.Chance, new RangeSettings(0, 1.0f, 0.1f, false));

                            if (ImGuiExt.ColoredButton("+", ImGuiExt.Colors[1], new Vector2(-ImGuiExt.FLT_MIN, 28), "Add TileId"))
                            {
                                ImGui.OpenPopup("TileIdPopup");
                            }

                            if (TileSetIdPopup.DrawTileSetIdPopup(layerDef, tileSetDef, out var tileId))
                            {
                                if (!rule.TileIds.Contains(tileId))
                                    rule.TileIds.Add(tileId);
                            }

                            if (rule.TileIds.Count == 0)
                            {
                                ImGui.TextDisabled("No TileId have been assigned");
                            }
                            else
                            {
                                var tileIdToRemove = -1;
                                var texture = GetTileSetTexture(tileSetDef.Path);
                                var iconSize = new Vector2(layerDef.GridSize * 2f);
                                for (var m = 0; m < rule.TileIds.Count; m++)
                                {
                                    ImGui.PushID(m);
                                    var iconPos = ImGui.GetCursorScreenPos();
                                    if (ImGuiExt.DrawTileSetIcon("TileId", layerDef.GridSize, texture, (uint)rule.TileIds[m], iconPos, iconSize, false,
                                            Color.White))
                                    {
                                        tileIdToRemove = m;
                                    }

                                    DrawZoomedTileTooltip(layerDef.GridSize, $"#{rule.TileIds[m]}", (uint)rule.TileIds[m], texture, iconSize);

                                    ImGui.PopID();

                                    ImGui.SameLine();
                                    if (ImGui.GetContentRegionAvail().X < iconSize.X + ImGui.GetStyle()->ItemSpacing.X)
                                        ImGui.NewLine();
                                }

                                if (tileIdToRemove != -1)
                                {
                                    rule.TileIds.RemoveAt(tileIdToRemove);
                                }
                            }

                            ImGui.PopID();
                        }

                        ImGui.EndTable();
                    }
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

    private static void DrawZoomedTileTooltip(uint gridSize, string label, uint tileId, Texture texture, Vector2 iconSize)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return;

        ImGui.SetNextWindowBgAlpha(1.0f);
        // ImGui.PushStyleColor(ImGuiCol.PopupBg, Color.Black.PackedValue);
        ImGui.BeginTooltip();

        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        var labelSize = ImGui.CalcTextSize(label);
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - labelSize.X) * 0.5f);
        ImGui.Text(label);
        ImGui.PopFont();
        ImGuiExt.DrawTileSetIcon("TileIdZoomed", gridSize, texture, tileId, ImGui.GetCursorScreenPos(), iconSize * 4, false, Color.White);

        /*
            var dl = ImGui.GetForegroundDrawList();
            var label = $"#{rule.TileIds[m]}";
            var labelSize = ImGui.CalcTextSize(label);
            var labelPos = cursorPosStart + avail - labelSize - new Vector2(10, 8);
            dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, labelPos + Vector2.One, Color.Black.PackedValue, label);
            dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, labelPos, Color.White.MultiplyAlpha(0.8f).PackedValue, label);*/
        ImGui.EndTooltip();
        // ImGui.PopStyleColor();
    }

    private static void AddText(string text, Vector2 position, int rowHeight, Color textColor)
    {
        var dl = ImGui.GetWindowDrawList();
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, position + new Vector2(0, rowHeight * 0.5f - ImGui.GetTextLineHeight() / 2),
            textColor.PackedValue, text, 0, default);
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

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(3, 3));
        for (var y = 0; y < rule.Size; y++)
        {
            ImGui.PushID(y);
            for (var x = 0; x < rule.Size; x++)
            {
                ImGui.PushID(x);
                var patternIndex = y * rule.Size + x;
                var patternValue = rule.Pattern[patternIndex];
                var intValue = intGridValues.FirstOrDefault(i => i.Value == patternValue || i.Value == -patternValue);
                var (buttonColor, textColor, label) = (intValue?.Color, patternValue) switch
                {
                    (not null, _) => (intValue.Color, Color.White, "##PatternValue"),
                    (null, ANYTHING_TILE_ID) => (Color.White, Color.Black, FontAwesome6.CircleQuestion),
                    (null, NOTHING_TILE_ID) => (Color.Black, ImGuiExt.Colors[2], FontAwesome6.CircleXmark),
                    _ => (new Color(20, 20, 20), Color.White, "##PatternValue"),
                };
                var popupName = "RulePopup";

                var cursorPos = ImGui.GetCursorScreenPos();
                var buttonSize = 40;
                if (ImGuiExt.ColoredButton(label, textColor, buttonColor, new Vector2(buttonSize), null))
                {
                    ImGui.OpenPopup(popupName);
                }

                DrawRuleTooltip(rule.Pattern[patternIndex], intValue);

                if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                {
                    rule.Pattern[patternIndex] = 0;
                }

                ImGui.SetNextWindowPos(cursorPos + new Vector2(0, buttonSize), ImGuiCond.Always, Vector2.Zero);
                DrawIntGridValuePopup(rule, intGridValues, popupName, patternValue, patternIndex);

                if (x < rule.Size - 1)
                    ImGui.SameLine(0);

                ImGui.PopID();
            }

            ImGui.PopID();
        }

        ImGui.PopStyleVar();
    }

    private static void DrawRuleTooltip(int patternValue, IntGridValue? intValue)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.BeginTooltip();

            if (intValue != null)
            {
                ImGui.Text("Tile should ");
                ImGui.SameLine();
                if (patternValue < 0)
                {
                    ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                    ImGui.TextColored(ImGuiExt.Colors[2].ToNumerics(), "NOT ");
                    ImGui.PopFont();
                    ImGui.SameLine();
                }

                ImGui.Text($"contain \"");
                ImGui.SameLine();
                ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                ImGui.TextColored(intValue.Color.ToNumerics(), $"{intValue.Identifier} ({intValue.Value})");
                ImGui.PopFont();
                ImGui.SameLine();
                ImGui.Text("\" to match");
            }
            else if (patternValue == ANYTHING_TILE_ID)
            {
                ImGui.Text("This tile should contain ");
                ImGui.SameLine();
                ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                ImGui.Text("ANY");
                ImGui.SameLine();
                ImGui.PopFont();
                ImGui.Text("value to match");
            }
            else if (patternValue == NOTHING_TILE_ID)
            {
                ImGui.Text("This tile should ");
                ImGui.SameLine();
                ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                ImGui.TextColored(ImGuiExt.Colors[2].ToNumerics(), "NOT");
                ImGui.SameLine();
                ImGui.PopFont();
                ImGui.Text(" contain ");
                ImGui.SameLine();
                ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                ImGui.Text("ANY");
                ImGui.SameLine();
                ImGui.PopFont();
                ImGui.Text(" value to match");
            }
            else
            {
                ImGui.Text("This tile doesn't matter");
            }

            ImGui.EndTooltip();
        }
    }

    private static void DrawIntGridValuePopup(AutoRule rule, List<IntGridValue> intGridValues, string popupName, int value, int patternIndex)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
        ImGui.SetNextWindowBgAlpha(1.0f);
        if (ImGui.BeginPopup(popupName, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var rowHeight = 36;
            if (ImGui.BeginTable("TileType", 1, TableFlags, new Vector2(200, 0)))
            {
                ImGui.TableSetupColumn("Value");
                for (var k = 0; k < intGridValues.Count; k++)
                {
                    ImGui.PushID(k);
                    var intGridValue = intGridValues[k];
                    var isSelected = value == intGridValue.Value || value == -intGridValue.Value;
                    if (DrawIntGridValueRow(intGridValue.Value, intGridValue.Identifier, isSelected, intGridValue.Color, rowHeight, out var selectedValue))
                    {
                        rule.Pattern[patternIndex] = selectedValue;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.PopID();
                }

                // draw anything/nothing

                ImGui.PushID(ANYTHING_TILE_ID);

                if (DrawIntGridValueRow(ANYTHING_TILE_ID, "Anything", value == ANYTHING_TILE_ID, Color.White, rowHeight, out var selected1))
                {
                    rule.Pattern[patternIndex] = selected1;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopID();
                ImGui.PushID(NOTHING_TILE_ID);
                if (DrawIntGridValueRow(NOTHING_TILE_ID, "Nothing", value == NOTHING_TILE_ID, Color.DarkGray, rowHeight, out var selected2))
                {
                    rule.Pattern[patternIndex] = selected2;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopID();

                ImGui.EndTable();
            }

            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(2);
    }


    private static bool DrawIntGridValueRow(int currValue, string identifier, bool isSelected, Color color, int rowHeight, out int selectedValue)
    {
        var result = false;
        selectedValue = -1;
        ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
        ImGui.TableNextColumn();
        var cursorPos = ImGui.GetCursorScreenPos();

        if (GiantButton("##Selectable", isSelected, color, rowHeight))
        {
            result = true;
            selectedValue = currValue;
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            result = true;
            selectedValue = -currValue;
        }

        var currValueStr = currValue.ToString();
        var leftPadding = new Vector2(20, 0);
        AddText(currValueStr, cursorPos + leftPadding, rowHeight, color);
        AddText(identifier, cursorPos + leftPadding + new Vector2(45, 0) + new Vector2(20, 0), rowHeight, color);
        return result;
    }

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
