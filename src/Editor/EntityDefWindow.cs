using Mochi.DearImGui;
using MyGame.WorldsRoot;
using EntityDefinition = MyGame.WorldsRoot.EntityDefinition;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class EntityDefWindow : SplitWindow
{
    private Color _refColor;
    private int _selectedEntityDefinitionIndex;
    private static int _rowMinHeight = 60;
    private int _selectedFieldDefinitionIndex;
    public const string WindowTitle = "Entities";

    public EntityDefWindow(MyEditorMain editor) : base(WindowTitle, editor)
    {
    }

    protected override void DrawLeft()
    {
        var btnResult = ButtonGroup($"{FontAwesome6.Plus}", "Presets", 200);
        if (btnResult == 0)
        {
            var def = new EntityDefinition
            {
                Color = Color.Green,
                Height = 16,
                Width = 16,
                Identifier = "NewEntity",
                FillOpacity = 0.08f,
                KeepAspectRatio = true,
                ResizableX = false,
                ResizableY = false,

                ShowName = false,
                TilesetId = 0,
                TileId = 0,
                PivotX = 0.5,
                PivotY = 0.5,
                Tags = new(),
                FieldDefinitions = new(),
            };

            _refColor = Color.Green;
            Root.EntityDefinitions.Add(def);
        }
        else if (btnResult == 1)
        {
        }

        DrawEntityDefTable(Root.EntityDefinitions, ref _selectedEntityDefinitionIndex, Root.TileSetDefinitions, Root.DefaultGridSize);

        SimpleTypeInspector.InspectInt("MinHeight", ref _rowMinHeight, new RangeSettings(ImGui.GetFrameHeightWithSpacing(), 100, 1, false));
    }

    private static void DrawEntityDefTable(List<EntityDefinition> entityDefs, ref int selectedEntityDefinitionIndex, List<TileSetDef> tileSetDefs, int gridSize)
    {
        if (ImGui.BeginTable("EntityDefTable", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Value");

            var entityDefToDelete = -1;
            for (var i = 0; i < entityDefs.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();
                var entityDef = entityDefs[i];
                ImGui.PushID(i);
                var isSelected = selectedEntityDefinitionIndex == i;
                var cursorPos = ImGui.GetCursorScreenPos();
                if (GiantButton("##Selectable", isSelected, entityDef.Color, _rowMinHeight))
                {
                    selectedEntityDefinitionIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        entityDefToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                var tileSet = tileSetDefs.FirstOrDefault(x => x.Uid == entityDef.TilesetId);
                if (tileSet != null)
                {
                    var dl = ImGui.GetWindowDrawList();
                    var texture = GetTileSetTexture(tileSet.Path);

                    // TODO (marpe): Replace gridSize with whatever grid size is being used for the tileset
                    var tileSize = new Point((int)(texture.Width / gridSize), (int)(texture.Height / gridSize));
                    var cellX = tileSize.X > 0 ? entityDef.TileId % tileSize.X : 0;
                    var cellY = tileSize.X > 0 ? (int)(entityDef.TileId / tileSize.X) : 0;
                    var uvMin = new Vector2(1.0f / texture.Width * cellX * gridSize,
                        1.0f / texture.Height * cellY * gridSize);
                    var uvMax = uvMin + new Vector2(gridSize / (float)texture.Width, gridSize / (float)texture.Height);
                    var lineHeight = Math.Max(_rowMinHeight, ImGui.GetTextLineHeight());
                    var iconSize = new Vector2(lineHeight, lineHeight) * 0.5f;
                    var iconPos = cursorPos + new Vector2(30, (int)((lineHeight - ImGui.GetStyle()->FramePadding.Y) / 2)) - iconSize / 2;
                    var padding = new Vector2(4, 4);
                    ImGuiExt.RectWithOutline(
                        dl,
                        iconPos - padding,
                        iconPos + iconSize + padding * 2,
                        entityDef.Color.MultiplyAlpha(0.2f),
                        entityDef.Color.MultiplyAlpha(0.6f),
                        2f
                    );
                    dl->AddImage(
                        (void*)texture.Handle,
                        iconPos,
                        iconPos + iconSize,
                        uvMin,
                        uvMax
                    );
                }

                ImGui.SameLine(0, 70f);

                GiantLabel(entityDef.Identifier, entityDef.Color, _rowMinHeight);

                ImGui.PopID();
            }

            if (entityDefToDelete != -1)
            {
                entityDefs.RemoveAt(entityDefToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawRight()
    {
        var entities = Root.EntityDefinitions;
        if (_selectedEntityDefinitionIndex >= 0 && _selectedEntityDefinitionIndex < entities.Count)
        {
            var entityDef = entities[_selectedEntityDefinitionIndex];

            SimpleTypeInspector.InspectString("Identifier", ref entityDef.Identifier);
            SimpleTypeInspector.InspectInputUint("TilesetId", ref entityDef.TilesetId);
            SimpleTypeInspector.InspectInputUint("TileId", ref entityDef.TileId);

            var (ix, iy) = entityDef.Size;
            var (x, y) = ((int)ix, (int)iy);
            if (ImGuiExt.InspectPoint("Size", ref x, ref y))
            {
                entityDef.Width = (uint)x;
                entityDef.Height = (uint)y;
            }

            DrawTags(entityDef);

            if (SimpleTypeInspector.InspectColor("Smart Color", ref entityDef.Color, _refColor, ImGuiColorEditFlags.NoAlpha))
            {
                entityDef.Color = entityDef.Color;
            }

            var (pivotX, pivotY) = (entityDef.PivotX, entityDef.PivotY);
            if (ImGuiExt.PivotPointEditor("Pivot Point", ref pivotX, ref pivotY, 40, entityDef.Color.PackedValue))
            {
                entityDef.PivotX = pivotX;
                entityDef.PivotY = pivotY;
            }

            FieldDefEditor.DrawFieldEditor(entityDef.FieldDefinitions, ref _selectedFieldDefinitionIndex);
        }
    }

    private static void DrawTags(EntityDefinition entityDef)
    {
        ImGuiExt.LabelPrefix("Tags");

        var tagToRemove = -1;
        for (var i = 0; i < entityDef.Tags.Count; i++)
        {
            // var colorIndex = (2 + i) % ImGuiExt.Colors.Length;
            var tag = entityDef.Tags[i];

            var textSize = ImGui.CalcTextSize(tag);
            var fieldWidth = textSize.X + 2 * ImGui.GetStyle()->FramePadding.X;
            if (fieldWidth + 30 > ImGui.GetContentRegionAvail().X)
                ImGui.NewLine();
            ImGui.SetNextItemWidth(fieldWidth);
            if (SimpleTypeInspector.InspectString($"##Tag{i}", ref tag))
            {
                entityDef.Tags[i] = tag;
            }

            ImGui.SameLine(0, 0);

            if (ImGuiExt.ColoredButton($"{FontAwesome6.Trash}##DeleteTag{i}", ImGuiExt.Colors[2], new Vector2(0, 26)))
            {
                tagToRemove = i;
            }

            ImGui.SameLine();
            if (ImGui.GetContentRegionAvail().X < 30)
                ImGui.NewLine();
        }

        if (tagToRemove != -1)
        {
            entityDef.Tags.RemoveAt(tagToRemove);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.White, new Color(95, 111, 165), new Vector2(26, 26), "Add Tag"))
        {
            entityDef.Tags.Add("");
        }

        ImGui.PopFont();
        ImGui.PopStyleVar();
    }
}
