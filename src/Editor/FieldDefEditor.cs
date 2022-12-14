using Mochi.DearImGui;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public static unsafe class FieldDefEditor
{
    private static bool _isArray;

    private static string GetFieldLabel(FieldType type)
    {
        return type switch
        {
            FieldType.Int => "0, 1, 2",
            FieldType.Float => "1.0",
            FieldType.String => "\"Ab\"",
            FieldType.Bool => FontAwesome6.Check,
            FieldType.Color => "Col",
            FieldType.Point => "X, Y",
            FieldType.Vector2 => "X, Y",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static void DrawFieldEditor(List<FieldDef> fieldDefs, ref int selectedFieldDefinitionIndex, Action<FieldDef>? defRemovedCallback = null)
    {
        ImGui.PushID("FieldEditor");
        ImGuiExt.SeparatorText("Fields");

        DrawAddButtons(200, fieldDefs);

        ImGui.Separator();

        if (fieldDefs.Count == 0)
        {
            ImGui.TextDisabled("No fields have been added");
        }
        else
        {

            var rowMinHeight = 40;

            if (ImGui.BeginTable("EntityFieldDefs", 1, SplitWindow.TableFlags, new Vector2(0, 0)))
            {
                ImGui.TableSetupColumn("Value");

                var fieldDefToDelete = -1;
                for (var i = 0; i < fieldDefs.Count; i++)
                {
                    ImGui.PushID(i);
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, rowMinHeight);
                    ImGui.TableNextColumn();

                    var fieldDef = fieldDefs[i];
                    var isSelected = i == selectedFieldDefinitionIndex;
                    var fieldColor = FieldDef.GetFieldColor(fieldDef.FieldType);
                    if (SplitWindow.GiantButton("##Selectable", isSelected, fieldColor, rowMinHeight))
                    {
                        selectedFieldDefinitionIndex = i;
                    }

                    if (ImGui.BeginPopupContextItem("Popup"))
                    {
                        ImGui.MenuItem("Copy", default);
                        ImGui.MenuItem("Cut", default);
                        ImGui.MenuItem("Duplicate", default);
                        if (ImGui.MenuItem("Delete", default))
                        {
                            fieldDefToDelete = i;
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.SameLine();
                    SplitWindow.GiantLabel(fieldDef.Identifier, Color.White, rowMinHeight);

                    DrawFieldTypeLabel(rowMinHeight, fieldDef, fieldColor);

                    ImGui.PopID();
                }

                if (fieldDefToDelete != -1)
                {
                    var fieldDef = fieldDefs[fieldDefToDelete];
                    fieldDefs.RemoveAt(fieldDefToDelete);
                    defRemovedCallback?.Invoke(fieldDef);
                }

                ImGui.EndTable();
            }

            ImGuiExt.SeparatorText("Selected Field");
            if (selectedFieldDefinitionIndex <= fieldDefs.Count - 1)
            {
                var fieldDef = fieldDefs[selectedFieldDefinitionIndex];
                SimpleTypeInspector.InspectInputInt("Uid", ref fieldDef.Uid);
                SimpleTypeInspector.InspectString("Identifier", ref fieldDef.Identifier);
                EnumInspector.InspectEnum("Type", ref fieldDef.FieldType);
            }
        }

        ImGui.PopID();
    }

    private static void DrawFieldTypeLabel(int rowMinHeight, FieldDef fieldDef, Color fieldColor)
    {
        var dl = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var lineHeight = Math.Max(ImGui.GetTextLineHeight(), rowMinHeight);
        var labelStr = GetFieldLabel(fieldDef.FieldType);
        var labelSize = ImGui.CalcTextSize(labelStr);
        var labelPos = cursorPos + new Vector2(
            ImGui.GetContentRegionAvail().X - labelSize.X - ImGui.GetStyle()->FramePadding.X - ImGui.GetStyle()->ItemSpacing.X,
            (int)(lineHeight * 0.5f - ImGui.GetTextLineHeight() * 0.5f));

        var rectPadding = new Num.Vector2(6, 4);
        var rectMin = labelPos - rectPadding;
        var rectMax = rectMin + labelSize + rectPadding * 2;
        ImGuiExt.RectWithOutline(dl, rectMin, rectMax, fieldColor.MultiplyAlpha(0.2f), fieldColor, 2f);
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16f, labelPos, Color.White.MultiplyAlpha(0.5f).PackedValue, labelStr);
    }

    private static void DrawAddButtons(int minWidth, List<FieldDef> fieldDefs)
    {
        var btnResult = SplitWindow.ButtonGroup($"{FontAwesome6.Plus} Single Value", $"{FontAwesome6.Plus} Array", minWidth);
        if (btnResult == 0)
        {
            _isArray = false;
            ImGui.OpenPopup("FieldTypePopup");
        }
        else if (btnResult == 1)
        {
            _isArray = true;
            ImGui.OpenPopup("FieldTypePopup");
        }

        ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos() - new Vector2(0, ImGui.GetStyle()->ItemSpacing.Y), ImGuiCond.Always, Vector2.Zero);
        if (DrawAddFieldDefPopup("FieldTypePopup", out var fieldType))
        {
            fieldDefs.Add(
                new FieldDef
                {
                    Uid = IdGen.NewId,
                    Identifier = "Field",
                    FieldType = fieldType,
                    IsArray = _isArray
                }
            );
        }
    }


    private static bool DrawAddFieldDefPopup(string label, out FieldType fieldType)
    {
        var result = false;
        fieldType = FieldType.Int;

        if (ImGui.BeginPopup(label))
        {
            var buttonSize = new Vector2(100, 100);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            if (ImGui.BeginTable("FieldTypes", 4, ImGuiTableFlags.None, new Vector2(400, 300)))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Integer", ImGuiExt.GetColor(), buttonSize))
                {
                    fieldType = FieldType.Int;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Float", ImGuiExt.GetColor(1), buttonSize))
                {
                    fieldType = FieldType.Float;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Boolean", ImGuiExt.GetColor(2), buttonSize))
                {
                    fieldType = FieldType.Bool;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("String", ImGuiExt.GetColor(3), buttonSize))
                {
                    fieldType = FieldType.String;
                    result = true;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Multilines", ImGuiExt.GetColor(4), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Color", ImGuiExt.GetColor(5), buttonSize))
                {
                    fieldType = FieldType.Color;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Enum", ImGuiExt.GetColor(6), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("File path", ImGuiExt.GetColor(7), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Tile", ImGuiExt.GetColor(8), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Entity ref", ImGuiExt.GetColor(9), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Point", ImGuiExt.GetColor(10), buttonSize))
                {
                    fieldType = FieldType.Point;
                    result = true;
                }

                ImGui.EndTable();
            }

            if (result)
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleVar(4);
            ImGui.EndPopup();
        }

        return result;
    }
}
