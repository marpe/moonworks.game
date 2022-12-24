using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

using Vector2 = Num.Vector2;

public static unsafe class FieldInstanceInspector
{
    private static bool GetFieldDef(int fieldDefId, [NotNullWhen(true)] out FieldDef? fieldDef, List<FieldDef> fieldDefs)
    {
        for (var i = 0; i < fieldDefs.Count; i++)
        {
            if (fieldDefs[i].Uid == fieldDefId)
            {
                fieldDef = fieldDefs[i];
                return true;
            }
        }

        fieldDef = null;
        return false;
    }


    public static void DrawFieldInstances(List<FieldInstance> fieldInstances, List<FieldDef> fieldDefs)
    {
        if (fieldDefs.Count == 0)
        {
            ImGui.TextDisabled("No fields have been defined");
            return;
        }

        for (var i = 0; i < fieldDefs.Count; i++)
        {
            if (fieldInstances.Any(x => x.FieldDefId == fieldDefs[i].Uid))
                continue;

            fieldInstances.Add(FieldDef.CreateFieldInstance(fieldDefs[i]));
        }

        for (var i = 0; i < fieldInstances.Count; i++)
        {
            ImGui.PushID(i);
            var fieldInstance = fieldInstances[i];
            if (!GetFieldDef(fieldInstance.FieldDefId, out var fieldDef, fieldDefs))
            {
                ImGui.Text($"Could not find a field definition with uid \"{fieldInstance.FieldDefId}\"");
                ImGui.PopID();
                continue;
            }

            EnsureFieldHasDefaultValue(fieldDef, fieldInstance);

            if (fieldDef.IsArray)
            {
                var list = (IList)fieldInstance.Value!;

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
                            var value = list[j] ?? FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, false);

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

                if (ImGuiExt.ColoredButton("+", new Vector2(-1, 0)))
                {
                    list.Add(FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, false));
                }
            }
            else
            {
                RangeSettings? rangeSettings = null;
                if (fieldDef.FieldType is FieldType.Int or FieldType.Float && Math.Abs(fieldDef.MinValue - fieldDef.MaxValue) > float.Epsilon)
                {
                    rangeSettings = new RangeSettings(
                        fieldDef.MinValue,
                        fieldDef.MaxValue,
                        (fieldDef.MaxValue - fieldDef.MinValue) / 500f,
                        false
                    );
                }

                SimpleTypeInspector.DrawSimpleInspector(fieldInstance.Value!.GetType(), fieldDef.Identifier, ref fieldInstance.Value, false, rangeSettings);
                if (ImGui.BeginPopupContextItem("FieldContextMenu"))
                {
                    if (ImGui.MenuItem("Reset to default value", default))
                    {
                        fieldInstance.Value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray);
                    }

                    ImGui.EndPopup();
                }
            }

            ImGui.PopID();
        }
    }

    private static void EnsureFieldHasDefaultValue(FieldDef fieldDef, FieldInstance fieldInstance)
    {
        if (fieldInstance.Value == null)
        {
            fieldInstance.Value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray);
        }

        var actualType = FieldDef.GetActualType(fieldDef.FieldType, fieldDef.IsArray);
        var instanceType = fieldInstance.Value.GetType();
        if (instanceType != actualType)
        {
            // TODO (marpe): Fix colors being deserialized as string
            if (actualType == typeof(Color) && fieldInstance.Value is string colorStr)
            {
                fieldInstance.Value = ColorExt.FromHex(colorStr.AsSpan(1));
            }
            else
            {
                fieldInstance.Value = Convert.ChangeType(fieldInstance.Value, actualType);
            }
        }
    }
}
