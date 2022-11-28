using Mochi.DearImGui;

namespace MyGame.TWImGui.Inspectors;

public unsafe class CollectionInspector : Inspector
{
    private Dictionary<object, IInspector> _inspectors = new();
    private HashSet<object> _inactiveItems = new();

    public Color HeaderColor { get; set; } = Color.Indigo;

    public static void DrawItemCount(int count)
    {
        ImGui.SameLine();
        var itemCountLabel = $"({count} items)";
        var itemCountLabelSize = ImGui.CalcTextSize(itemCountLabel);
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - itemCountLabelSize.X);
        ImGui.Text(itemCountLabel);
    }

    public override void Draw()
    {
        if (ImGuiExt.DebugInspectors)
            DrawDebug();

        var value = GetValue();
        if (value == null)
        {
            ImGuiExt.DrawLabelWithCenteredText(_name, "NULL");
            return;
        }

        if (value is not ICollection collection)
        {
            throw new InvalidOperationException("Value is not of type ICollection");
        }

        PushStyle();
        if (ImGuiExt.BeginCollapsingHeader(_name, HeaderColor, ImGuiTreeNodeFlags.None))
        {
            foreach (var item in _inspectors.Keys)
                _inactiveItems.Add(item);

            DrawItemCount(collection.Count);

            if (ImGui.BeginTable("Items", 2, ImGuiExt.DefaultTableFlags, new Num.Vector2(0, 0)))
            {
                var keyLabel = collection is IDictionary ? "Key" : "#";

                ImGui.TableSetupColumn(keyLabel, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide, 20f);
                ImGui.TableSetupColumn("Value");

                ImGui.TableHeadersRow();

                IEnumerable enumerable;

                {
                    if (collection is IDictionary dictionary)
                        enumerable = dictionary.Keys;
                    else
                        enumerable = collection;
                }

                var i = 0;
                foreach (var item in enumerable)
                {
                    ImGui.PushID(i);

                    if (collection is IDictionary dictionary)
                    {
                        var keyStr = item.ToString() ?? "";
                        DrawItem(keyStr, dictionary[item]);
                    }
                    else
                    {
                        var keyStr = i.ToString();
                        DrawItem(keyStr, item);
                    }

                    i++;
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            foreach (var item in _inactiveItems)
                _inspectors.Remove(item);
            _inactiveItems.Clear();

            ImGuiExt.MediumVerticalSpace();

            ImGuiExt.EndCollapsingHeader();
        }
        else
        {
            DrawItemCount(collection.Count);
        }

        PopStyle();
        ImGui.Spacing();
    }

    private static void PushStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
    }

    private static void PopStyle()
    {
        ImGui.PopStyleVar(2);
    }

    private void DrawItem(string key, object? item)
    {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.Text(key);

        ImGui.TableSetColumnIndex(1);

        if (item == null)
        {
            ImGui.TextDisabled("NULL");
        }
        else if (item is FancyTextPart fancyTextPart)
        {
            if (ImGuiExt.BeginPropTable("FancyText"))
            {
                ImGuiExt.PropRow("Alpha", $"{fancyTextPart.Alpha:0.00}");
                ImGuiExt.PropRow("Character", fancyTextPart.Character);
                ImGui.EndTable();
            }
        }
        else if (item is Vector2 vector)
        {
            ImGui.Text($"{vector.X:0.##}, {vector.Y:0.##}");
        }
        else if (item is float fvalue)
        {
            ImGui.DragFloat("##Value", ImGuiExt.RefPtr(ref fvalue), default, default, default, default);
        }
        else if (item is Enemy)
        {
            if (!_inspectors.ContainsKey(item))
                _inspectors.Add(item, InspectorExt.GetGroupInspectorForTarget(item));

            PopStyle();
            _inspectors[item].Draw();
            PushStyle();

            _inactiveItems.Remove(item);
        }
        else
        {
            ImGui.TextUnformatted(item.ToString());
        }
    }
}
