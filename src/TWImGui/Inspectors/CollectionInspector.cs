using ImGuiNET;

namespace MyGame.TWImGui.Inspectors;

public class CollectionInspector : Inspector
{
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
		var value = GetValue();
		if (value == null)
		{
			ImGuiExt.DrawLabelWithCenteredText(_name, "NULL");
			return;
		}

		if (value is not ICollection collection)
		{
			ImGuiExt.DrawLabelWithCenteredText(_name, "Value is not of type ICollection");
			return;
		}

		ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 0);
		ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
		if (ImGuiExt.BeginCollapsingHeader(_name, HeaderColor, ImGuiTreeNodeFlags.None))
		{
			DrawItemCount(collection.Count);

			if (ImGui.BeginTable("Items", 2, ImGuiExt.DefaultTableFlags, new Num.Vector2(0, 0)))
			{
				var keyLabel = collection is IDictionary ? "Key" : "#";

				ImGui.TableSetupColumn(keyLabel, ImGuiTableColumnFlags.WidthFixed, 20f);
				ImGui.TableSetupColumn("Value");

				ImGui.TableHeadersRow();

				if (collection is IDictionary dictionary)
				{
					foreach (var key in dictionary.Keys)
					{
						var keyStr = key.ToString() ?? throw new InvalidOperationException("Key cannot be null");
						DrawItem(keyStr, dictionary[key]);
					}
				}
				else
				{
					var i = 0;
					foreach (var item in collection)
					{
						var keyStr = i.ToString();
						DrawItem(keyStr, item);
						i++;
					}
				}

				ImGui.EndTable();
			}

			ImGuiExt.MediumVerticalSpace();

			ImGuiExt.EndCollapsingHeader();
		}
		else
		{
			DrawItemCount(collection.Count);
		}
		ImGui.PopStyleVar(2);
		ImGui.Spacing();
	}

	private void DrawItem(string key, object? item)
	{
		ImGui.PushID(key);

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
				ImGuiExt.PropRow("Character", fancyTextPart.Character.ToString());
				ImGui.EndTable();
			}
		}
		else if (item is Vector2 vector)
		{
			ImGui.Text($"{vector.X:0.##}, {vector.Y:0.##}");
		}
		else if (item is float fvalue)
		{
			ImGui.DragFloat("##Value", ref fvalue);
		}

		ImGui.PopID();
	}
}
