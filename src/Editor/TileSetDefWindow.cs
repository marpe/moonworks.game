﻿using Mochi.DearImGui;
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
        if (ImGui.BeginTable("TileSetDefinitions", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var tileSetToDelete = -1;
            for (var i = 0; i < Root.TileSetDefinitions.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var tilesetDef = Root.TileSetDefinitions[i];

                var isSelected = _selectedTileSetDefinitionIndex == i;
                var color = ImGuiExt.Colors[3];
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedTileSetDefinitionIndex = i;
                }

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
                Root.TileSetDefinitions.RemoveAt(tileSetToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawLeft()
    {
        DrawTileSetDefinitions();
        if (ImGuiExt.ColoredButton("+ Add TileSet Definition", new Vector2(-1, 0)))
        {
            Root.TileSetDefinitions.Add(new TileSetDef());
        }
    }

    protected override void DrawRight()
    {
        if (_selectedTileSetDefinitionIndex <= Root.TileSetDefinitions.Count - 1)
        {
            var tileSetDef = Root.TileSetDefinitions[_selectedTileSetDefinitionIndex];

            SimpleTypeInspector.InspectInputInt("Uid", ref tileSetDef.Uid);
            SimpleTypeInspector.InspectString("Identifier", ref tileSetDef.Identifier);
            SimpleTypeInspector.InspectString("Path", ref tileSetDef.Path);

            if (tileSetDef.Path != "")
            {
                var texture = GetTileSetTexture(tileSetDef.Path);
                var avail = ImGui.GetContentRegionAvail();
                var height = MathF.Max(1.0f, texture.Height) / MathF.Max(1.0f, texture.Width) * avail.X;
                ImGui.Image((void*)texture.Handle, new Vector2(avail.X, height), Vector2.Zero, Vector2.One, Color.White.ToNumerics(),
                    Color.Black.ToNumerics());
            }
        }
    }
}