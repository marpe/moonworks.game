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
    const string RemapTilesPopup = "RemapTilesPopup";

    private static ImGuiTableFlags _horizontalBordersTableFlags = ImGuiTableFlags.BordersH | ImGuiTableFlags.BordersOuter |
                                                                  ImGuiTableFlags.Hideable | ImGuiTableFlags.PreciseWidths |
                                                                  ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg;

    private int _remapFromTileSetDefId;
    private int _remapFromTileSetDefIdx;
    private int[] _remappingTileIds = Array.Empty<int>();
    private int _remapIndex;
    private List<(int oldId, int newId)> _remapMap = new();
    private bool _isRemapping;


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
                var cursorPos = ImGui.GetCursorScreenPos();
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

                ImGui.SameLine(0, 0);
                var buttonSize = new Vector2(30, _rowMinHeight);
                ImGuiExt.TextButton(FontAwesome6.EllipsisVertical, "Drag to move", Color.White.PackedValue, buttonSize);
                if (ImGui.BeginDragDropSource())
                {
                    ImGui.SetDragDropPayload("LayerDefRow", &i, sizeof(int));
                    ImGui.Text($"Dragging layer {layerDef.Identifier}");
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("LayerDefRow");
                    if (payload != null)
                    {
                        var rowIndex = *(int*)payload->Data;
                        RootJson.LayerDefinitions[i] = RootJson.LayerDefinitions[rowIndex];
                        RootJson.LayerDefinitions[rowIndex] = layerDef;
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.SameLine();

                var labelColor = isSelected ? Color.White : color;
                Icon(LayerTypeIcon(layerDef.LayerType), color, _rowMinHeight);

                ImGui.SameLine();
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
            RootJson.LayerDefinitions.Add(new LayerDef() { Uid = GetNextId(RootJson.LayerDefinitions) });
        }
    }

    private int GetNextId(List<LayerDef> layerDefs)
    {
        var maxId = 0;
        for (var i = 0; i < layerDefs.Count; i++)
            if (maxId <= layerDefs[i].Uid)
                maxId = layerDefs[i].Uid + 1;
        return maxId;
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
            TileSetDefCombo.DrawTileSetDefCombo("TileSetDefId", ref layerDef.TileSetDefId, RootJson.TileSetDefinitions);

            if (TileSetDefCombo.DrawTileSetDefCombo("Remap", ref _remapFromTileSetDefId, RootJson.TileSetDefinitions))
            {
                _remapMap.Clear();
                _remappingTileIds = GetUsedTileIds(layerDef);
                _remapFromTileSetDefIdx = RootJson.TileSetDefinitions.FindIndex(x => x.Uid == _remapFromTileSetDefId);
                _isRemapping = true;
                _remapIndex = 0;
                ImGui.OpenPopup(RemapTilesPopup);
            }
        }

        if (layerDef.LayerType == LayerType.IntGrid)
        {
            var rowHeight = 60;

            DrawIntGridValues(_horizontalBordersTableFlags, layerDef, rowHeight);

            var tileSetDef = RootJson.TileSetDefinitions.FirstOrDefault(x => x.Uid == layerDef.TileSetDefId);

            if (tileSetDef != null)
            {
                DrawRemapTileId(tileSetDef, layerDef);
            }

            if (tileSetDef != null)
                DrawAutoRuleGroups(layerDef, tileSetDef);
        }
    }

    private void DrawRemapTileId(TileSetDef oldTileSetDef, LayerDef layerDef)
    {
        if (!_isRemapping)
            return;
        if (_remapIndex < _remappingTileIds.Length)
        {
            var oldTileId = _remappingTileIds[_remapIndex];

            var iconPos = ImGui.GetWindowPos() + new Vector2(ImGui.GetMainViewport()->WorkSize.X / 2, 50);
            var iconSize = new Vector2(oldTileSetDef.TileGridSize, oldTileSetDef.TileGridSize) * 4;

            var dl = ImGui.GetForegroundDrawList();
            if (SplitWindow.GetTileSetTexture(oldTileSetDef.Path, out var texture))
            {
                var sprite = LevelRenderer.GetTileSprite(texture, (uint)oldTileId, oldTileSetDef);
                ImGuiExt.RectWithOutline(
                    dl,
                    iconPos,
                    iconPos + iconSize,
                    ColorExt.FromPacked(ImGui.GetColorU32(ImGuiCol.FrameBg)),
                    Color.Black,
                    0
                );
                dl->AddImage(
                    (void*)sprite.TextureSlice.Texture.Handle,
                    iconPos,
                    iconPos + iconSize,
                    sprite.UV.TopLeft.ToNumerics(),
                    sprite.UV.BottomRight.ToNumerics()
                );
            }

            var newTileSetDef = RootJson.TileSetDefinitions[_remapFromTileSetDefIdx];
            if (TileSetIdPopup.DrawTileSetIdPopup(RemapTilesPopup, newTileSetDef, out var tileId))
            {
                _remapMap.Add((_remappingTileIds[_remapIndex], tileId));
                _remapIndex++;

                if (_remapIndex < _remappingTileIds.Length)
                {
                    ImGui.OpenPopup(RemapTilesPopup);
                }
                else
                {
                    layerDef.TileSetDefId = newTileSetDef.Uid;
                }
            }
        }
        else
        {
            for (var i = 0; i < _remapMap.Count; i++)
            {
                var (oldId, newId) = _remapMap[i];
                RemapTileId(oldId, newId);
            }

            _isRemapping = false;
        }
    }

    private void RemapTileId(int oldId, int newId)
    {
        var layerDef = RootJson.LayerDefinitions[_selectedLayerDefIndex];
        for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
        {
            var group = layerDef.AutoRuleGroups[i];
            for (var j = 0; j < group.Rules.Count; j++)
            {
                var rule = group.Rules[j];

                for (var k = 0; k < rule.TileIds.Count; k++)
                {
                    if (rule.TileIds[k] == oldId)
                        rule.TileIds[k] = newId;
                }
            }
        }
    }

    private static int[] GetUsedTileIds(LayerDef layerDef)
    {
        var tmpIdList = new HashSet<int>();
        for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
        {
            var group = layerDef.AutoRuleGroups[i];
            for (var j = 0; j < group.Rules.Count; j++)
            {
                var rule = group.Rules[j];
                for (var k = 0; k < rule.TileIds.Count; k++)
                {
                    tmpIdList.Add(rule.TileIds[k]);
                }
            }
        }

        return tmpIdList.ToArray();
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
                DrawIntValue(layerDef.IntGridValues[i].Value.ToString(), new Vector2(rowHeight, rowHeight) * 0.6f, layerDef.IntGridValues[i].Color);
                // SimpleTypeInspector.InspectInt("Value", ref layerDef.IntGridValues[i].Value, SimpleTypeInspector.UnsignedDefaultRangeSettings);
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

    private static void DrawAutoRuleGroups(LayerDef layerDef, TileSetDef tileSetDef)
    {
        void DragAndDropButton(string id, int index, Vector2 buttonSize, AutoRuleGroup group)
        {
            ImGuiExt.TextButton(FontAwesome6.EllipsisVertical, "Drag to move", Color.White.PackedValue, buttonSize);

            if (ImGui.BeginDragDropSource())
            {
                ImGui.SetDragDropPayload(id, &index, sizeof(int));
                ImGui.Text($"Dragging group {group.Name} ({group.Uid})");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(id);
                if (payload != null)
                {
                    var rowIndex = *(int*)payload->Data;
                    layerDef.AutoRuleGroups[index] = layerDef.AutoRuleGroups[rowIndex];
                    layerDef.AutoRuleGroups[rowIndex] = group;
                }

                ImGui.EndDragDropTarget();
            }
        }

        ImGui.PushID("AutoRules");
        var valueToRemove = -1;

        for (var i = 0; i < layerDef.AutoRuleGroups.Count; i++)
        {
            ImGui.PushID(i);
            var group = layerDef.AutoRuleGroups[i];

            DragAndDropButton($"AutoRuleGroups", i, new Vector2(30, ImGui.GetFrameHeight()), group);
            ImGui.SameLine();

            var foldoutId = ImGui.GetID($"AutoRuleGroup{i}");
            ImGuiExt.OpenFoldouts.TryGetValue(foldoutId, out var isOpen);
            var (icon, tooltip) = isOpen switch
            {
                true => (FontAwesome6.ChevronDown, "Collapsed"),
                _ => (FontAwesome6.ChevronRight, "Expand")
            };
            if (ImGuiExt.TextButton(icon, tooltip, Color.White.PackedValue, new Vector2(30, ImGui.GetFrameHeight())))
            {
                ImGuiExt.OpenFoldouts[foldoutId] = !isOpen;
            }

            ImGui.SameLine();

            ImGui.SetNextItemWidth(100);
            SimpleTypeInspector.InspectString("##Name", ref group.Name);

            ImGui.SameLine();
            if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.Orange, new Vector2(30, ImGui.GetFrameHeight()), "Add Rule"))
            {
                group.Rules.Add(
                    new AutoRule
                    {
                        Uid = GetNextId(group.Rules),
                        BreakOnMatch = true,
                        IsActive = true,
                        Chance = 1.0f,
                    }
                );
            }

            ImGui.SameLine();
            var (showHideTooltip, showHideIcon) = group.IsActive switch
            {
                true => ("Hide", FontAwesome6.Eye),
                _ => ("Show", FontAwesome6.EyeSlash),
            };
            if (ImGuiExt.ColoredButton(showHideIcon, Color.Black, new Vector2(30, ImGui.GetFrameHeight()), showHideTooltip))
            {
                group.IsActive = !group.IsActive;
            }

            ImGui.SameLine();
            if (ImGuiExt.ColoredButton(FontAwesome6.Trash, Color.Red, new Vector2(30, ImGui.GetFrameHeight()), "Remove Group"))
            {
                valueToRemove = i;
            }

            if (group.Rules.Count > 0 && isOpen)
            {
                DrawGroupRules($"Group{i}_Rule_Row", group, layerDef, tileSetDef);
            }

            ImGui.PopID();
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
                Uid = GetNextId(layerDef.AutoRuleGroups),
                IsActive = true,
            });
        }

        ImGui.PopID();
    }

    private static int GetNextId(List<AutoRuleGroup> groups)
    {
        var maxId = 0;
        for (var i = 0; i < groups.Count; i++)
            if (maxId <= groups[i].Uid)
                maxId = groups[i].Uid + 1;
        return maxId;
    }

    private static int GetNextId(List<AutoRule> rules)
    {
        var maxId = 0;
        for (var i = 0; i < rules.Count; i++)
            if (maxId <= rules[i].Uid)
                maxId = rules[i].Uid + 1;
        return maxId;
    }

    private static void DrawGroupRules(string id, AutoRuleGroup group, LayerDef layerDef, TileSetDef tileSetDef)
    {
        void DragAndDropButton(int index, AutoRule rule)
        {
            ImGuiExt.TextButton(FontAwesome6.EllipsisVertical, "Drag to move", Color.White.PackedValue, new Vector2(40, 40));

            if (ImGui.BeginDragDropSource())
            {
                ImGui.SetDragDropPayload(id, &index, sizeof(int));
                ImGui.Text($"Dragging rule {index}");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(id);
                if (payload != null)
                {
                    var rowIndex = *(int*)payload->Data;
                    group.Rules[index] = group.Rules[rowIndex];
                    group.Rules[rowIndex] = rule;
                }

                ImGui.EndDragDropTarget();
            }
        }

        if (ImGui.BeginTable("GroupRules", 3, _horizontalBordersTableFlags | ImGuiTableFlags.BordersV, new Vector2(0, 0)))
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

                DragAndDropButton(k, rule);

                ImGui.TableNextColumn();
                DrawRuleMatrix(rule, layerDef.IntGridValues);

                ImGui.TableNextColumn();

                DrawRuleProperties(layerDef, tileSetDef, rule);

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private static void DrawRuleProperties(LayerDef layerDef, TileSetDef tileSetDef, AutoRule rule)
    {
        DrawSizeCombo(ref rule.Size);
        ImGui.SetNextItemWidth(250);
        SimpleTypeInspector.InspectBool("BreakOnMatch", ref rule.BreakOnMatch);
        SimpleTypeInspector.InspectBool("IsActive", ref rule.IsActive);
        SimpleTypeInspector.InspectFloat("Chance", ref rule.Chance, new RangeSettings(0, 1.0f, 0.1f, false));

        var addTilePopupName = "AddTilePopup";
        if (ImGuiExt.ColoredButton("+", ImGuiExt.Colors[1], new Vector2(-ImGuiExt.FLT_MIN, ImGui.GetFrameHeight()), "Add TileId"))
        {
            ImGui.OpenPopup(addTilePopupName);
        }

        if (TileSetIdPopup.DrawTileSetIdPopup(addTilePopupName, tileSetDef, out var tileId))
        {
            if (!rule.TileIds.Contains(tileId))
                rule.TileIds.Add(tileId);
        }

        if (rule.TileIds.Count == 0)
        {
            ImGui.TextDisabled("No TileId have been assigned");
            return;
        }

        var replaceTileIdPopupName = "ReplaceTilePopUp";

        var tileIdToRemove = -1;
        var iconSize = new Vector2(layerDef.GridSize * 2f);
        for (var m = 0; m < rule.TileIds.Count; m++)
        {
            ImGui.PushID(m);
            var iconPos = ImGui.GetCursorScreenPos();
            if (ImGuiExt.DrawTileSetIcon("TileId", (uint)rule.TileIds[m], tileSetDef, iconPos, iconSize, false,
                    Color.White))
            {
                ImGui.OpenPopup(replaceTileIdPopupName);
            }

            if (TileSetIdPopup.DrawTileSetIdPopup(replaceTileIdPopupName, tileSetDef, out var newTileId))
            {
                rule.TileIds[m] = newTileId;
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                tileIdToRemove = m;
            }

            DrawZoomedTileTooltip($"#{rule.TileIds[m]}", (uint)rule.TileIds[m], tileSetDef, iconSize);

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

    private static void DrawZoomedTileTooltip(string label, uint tileId, TileSetDef tileSetDef, Vector2 iconSize)
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
        ImGuiExt.DrawTileSetIcon("TileIdZoomed", tileId, tileSetDef, ImGui.GetCursorScreenPos(), iconSize * 4, false, Color.White);

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
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), ImGui.GetFontSize(), position + new Vector2(0, rowHeight * 0.5f - ImGui.GetTextLineHeight() / 2),
            textColor.PackedValue, text, 0, default);
    }

    private static void DrawRuleMatrix(AutoRule rule, List<IntGridValue> intGridValues)
    {
        while (rule.Pattern.Count > rule.Size * rule.Size)
        {
            rule.Pattern.RemoveAt(rule.Pattern.Count - 1);
        }

        for (var ii = rule.Pattern.Count; ii < rule.Size * rule.Size; ii++)
        {
            rule.Pattern.Add(0);
        }

        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(3, 3));
        var spacing = ImGui.GetStyle()->ItemInnerSpacing.X;
        var buttonSize = Math.Max(20, (avail.X - spacing * (rule.Size - 1)) / rule.Size);
        var rowWidth = rule.Size * buttonSize + spacing * (rule.Size - 1);
        for (var y = 0; y < rule.Size; y++)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (int)((avail.X - rowWidth) / 2));
            ImGui.PushID(y);
            for (var x = 0; x < rule.Size; x++)
            {
                ImGui.PushID(x);
                var patternIndex = y * rule.Size + x;
                var patternValue = rule.Pattern[patternIndex];
                var intValue = intGridValues.FirstOrDefault(i => i.Value == patternValue || i.Value == -patternValue);
                var (buttonColor, textColor, label) = (intValue?.Color, patternValue) switch
                {
                    (not null, < 0) => (intValue.Color, Color.Red, FontAwesome6.Xmark),
                    (not null, _) => (intValue.Color, Color.White, "##PatternValue"),
                    (null, ANYTHING_TILE_ID) => (Color.White, Color.Black, FontAwesome6.Question),
                    (null, NOTHING_TILE_ID) => (Color.Black, ImGuiExt.Colors[2], FontAwesome6.XmarksLines),
                    _ => (new Color(20, 20, 20), Color.White, "##PatternValue"),
                };
                var popupName = "RulePopup";

                var cursorPos = ImGui.GetCursorScreenPos();

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                if (ImGuiExt.ColoredButton(label, textColor, buttonColor, new Vector2(buttonSize), null))
                {
                    ImGui.OpenPopup(popupName);
                }

                ImGui.PopStyleVar();

                if (x == rule.Size / 2 && y == rule.Size / 2)
                {
                    var dl = ImGui.GetWindowDrawList();
                    dl->AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), Color.Yellow.PackedValue, 0, ImDrawFlags.None, 1f);
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

    private static bool TagButton(string label)
    {
        return ImGuiExt.ColoredButton(
            label,
            Color.White, new Color(95, 111, 165),
            "Click to remove",
            new Vector2(0, ImGui.GetFrameHeight()),
            new Vector2(ImGui.GetStyle()->FramePadding.X, 2)
        );
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
        ImGui.PushID("ExcludedTags");
        for (var i = 0; i < layerDef.ExcludedTags.Count; i++)
        {
            ImGui.PushID(i);
            if (TagButton(layerDef.ExcludedTags[i]))
                exclTagToRemove = i;

            if (i < layerDef.ExcludedTags.Count - 1)
                ImGui.SameLine();
            ImGui.PopID();
        }

        ImGui.PopID();

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
        ImGui.PushID("RequiredTags");
        for (var i = 0; i < layerDef.RequiredTags.Count; i++)
        {
            ImGui.PushID(i);
            if (TagButton(layerDef.RequiredTags[i]))
                reqTagToRemove = i;

            if (i < layerDef.RequiredTags.Count - 1)
                ImGui.SameLine();
            ImGui.PopID();
        }

        ImGui.PopID();

        if (reqTagToRemove != -1)
            layerDef.RequiredTags.RemoveAt(reqTagToRemove);
    }
}
