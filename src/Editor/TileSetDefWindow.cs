﻿using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class TileSetDefWindow : SplitWindow
{
    private int _selectedTileSetDefinitionIndex;
    private int _rowMinHeight = 60;
    public const string WindowTitle = "TileSets";

    public TileSetDefWindow(MyEditorMain editor) : base(WindowTitle, editor)
    {
    }


    private void DrawTileSetDefinitions()
    {
        var avail = ImGui.GetContentRegionAvail();
        
        if (ImGui.BeginTable("TileSetDefinitions", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var tileSetToDelete = -1;
            for (var i = 0; i < RootJson.TileSetDefinitions.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();
                ImGui.PushID(i);
                
                var tilesetDef = RootJson.TileSetDefinitions[i];

                var isSelected = _selectedTileSetDefinitionIndex == i;
                var color = ImGuiExt.Colors[3];
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedTileSetDefinitionIndex = i;
                }

                ImGui.SameLine(0, 0);
                
                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        tileSetToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                var labelColor = isSelected ? Color.White : color;
                CenteredButton(color, _rowMinHeight);
                GiantLabel(tilesetDef.Identifier, labelColor, _rowMinHeight);

                ImGui.PopID();
            }

            if (tileSetToDelete != -1)
            {
                RootJson.TileSetDefinitions.RemoveAt(tileSetToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawLeft()
    {
        DrawTileSetDefinitions();
        if (ImGuiExt.ColoredButton("+ Add TileSet Definition", new Vector2(-1, 40)))
        {
            RootJson.TileSetDefinitions.Add(new TileSetDef() { Uid = GetNextId(RootJson.TileSetDefinitions) });
        }
    }

    private static int GetNextId(List<TileSetDef> tileSetDefs)
    {
        var maxId = 0;
        for (var i = 0; i < tileSetDefs.Count; i++)
            if (maxId <= tileSetDefs[i].Uid)
                maxId = tileSetDefs[i].Uid + 1;
        return maxId;
    }

    protected override void DrawRight()
    {
        if (_selectedTileSetDefinitionIndex > RootJson.TileSetDefinitions.Count - 1)
            return;
 
        var tileSetDef = RootJson.TileSetDefinitions[_selectedTileSetDefinitionIndex];

        SimpleTypeInspector.InspectInputInt("Uid", ref tileSetDef.Uid);
        SimpleTypeInspector.InspectString("Identifier", ref tileSetDef.Identifier);
        SimpleTypeInspector.InspectString("Path", ref tileSetDef.Path);
        var resolvedPath = GetPathRelativeToCwd(tileSetDef.Path);
        ImGui.TextDisabled(resolvedPath);
        SimpleTypeInspector.InspectInputUint("TileGridSize", ref tileSetDef.TileGridSize);
        SimpleTypeInspector.InspectInputInt("Padding", ref tileSetDef.Padding);
        SimpleTypeInspector.InspectInputInt("Spacing", ref tileSetDef.Spacing);

        if (GetTileSetTexture(tileSetDef.Path, out var texture))
        {
            var avail = ImGui.GetContentRegionAvail();
            var height = MathF.Max(1.0f, texture.Height) / MathF.Max(1.0f, texture.Width) * avail.X;
            var textureSize = new Vector2(avail.X, height);
            var min = ImGui.GetCursorScreenPos();
            var max = min + textureSize;
            ImGuiExt.FillWithStripes(ImGui.GetWindowDrawList(), new ImRect(min, max), Color.White.MultiplyAlpha(0.33f).PackedValue);
            ImGui.Image(
                (void*)texture.Handle,
                textureSize,
                Vector2.Zero, Vector2.One,
                Color.White.ToNumerics(),
                Color.Black.ToNumerics()
            );
            ImGui.SetCursorScreenPos(ImGui.GetItemRectMin());
            var popupId = "TileIdPopup";
            if (ImGui.InvisibleButton("OpenTileSetPopup", ImGui.GetItemRectSize()))
            {
                ImGui.OpenPopup(popupId);
            }

            if (TileSetIdPopup.DrawTileSetIdPopup(popupId, tileSetDef, out var tileId))
            {
            }
        }
        else
        {
            ImGui.TextColored(Color.Red.ToNumerics(), $"File not found: {resolvedPath}");
        }
    }
}
