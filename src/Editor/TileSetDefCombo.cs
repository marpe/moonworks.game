using Mochi.DearImGui;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

using Vector2 = Num.Vector2;

public static unsafe class TileSetDefCombo
{
    public static void DrawTileSetDefCombo(string id, ref uint tileSetDefId, List<TileSetDef> tileSetDefinitions)
    {
        if (tileSetDefinitions.Count == 0)
        {
            ImGui.TextDisabled("No tilesets have been added");
            return;
        }

        var tmpId = tileSetDefId;
        var currentTileSetDef = tileSetDefinitions.FirstOrDefault(x => x.Uid == tmpId);
        if (currentTileSetDef == null && tileSetDefId != 0)
        {
            ImGui.TextColored(ImGuiExt.Colors[2].ToNumerics(), $"Could not find a TileSet with Id \"{tileSetDefId}\"");
            if (ImGuiExt.ColoredButton("Ok", ImGuiExt.Colors[2], new Vector2(-1, 0)))
            {
                tileSetDefId = (uint)tileSetDefinitions.First().Uid;
            }

            return;
        }

        var currentIndex = 0;
        for (var i = 0; i < tileSetDefinitions.Count; i++)
        {
            if (tileSetDefinitions[i].Uid == tileSetDefId)
                currentIndex = i;
        }

        var label = tileSetDefinitions[currentIndex].Identifier;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo(ImGuiExt.LabelPrefix(id), label))
        {
            for (var i = 0; i < tileSetDefinitions.Count; i++)
            {
                var isSelected = i == currentIndex;
                if (ImGui.Selectable(tileSetDefinitions[i].Identifier, isSelected, ImGuiSelectableFlags.None, default))
                {
                    tileSetDefId = (uint)tileSetDefinitions[i].Uid;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }
}
