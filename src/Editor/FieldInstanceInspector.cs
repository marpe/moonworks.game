using Mochi.DearImGui;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

using Vector2 = Num.Vector2;

public static unsafe class FieldInstanceInspector
{
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

            fieldInstances.Add(CreateFieldInstance(fieldDefs[i]));
        }

        for (var i = 0; i < fieldInstances.Count; i++)
        {
            ImGui.PushID(i);
            var fieldInstance = fieldInstances[i];
            var fieldDef = fieldDefs.FirstOrDefault(x => x.Uid == fieldInstance.FieldDefId);
            if (fieldDef == null)
            {
                ImGui.Text($"Could not find a field definition with uid \"{fieldInstance.FieldDefId}\"");
                ImGui.PopID();
                continue;
            }

            var actualType = FieldDef.GetActualType(fieldDef.FieldType, fieldDef.IsArray);
            if (fieldInstance.Value == null)
            {
                if (fieldDef.IsArray)
                {
                    fieldInstance.Value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray);
                }
                else
                {
                    fieldInstance.Value = fieldDef.DefaultValue ?? FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray);
                }
            }

            if (fieldInstance.Value.GetType() != actualType)
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
                                value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, false);
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

                if (ImGuiExt.ColoredButton("+", new Vector2(-1, 0)))
                {
                    list.Add(FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, false));
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
            Value = FieldDef.GetDefaultValue(fieldDef.DefaultValue, fieldDef.FieldType, fieldDef.IsArray),
            FieldDefId = fieldDef.Uid
        };
    }
}
