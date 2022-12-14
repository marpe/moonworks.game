using Mochi.DearImGui;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public class WorldsWindow : SplitWindow
{
    private int _rowMinHeight = 60;
    public static int SelectedWorldIndex;
    public const string WindowTitle = "Worlds";

    public WorldsWindow(MyEditorMain editor) : base(WindowTitle, editor)
    {
    }


    private void DrawWorlds()
    {
        if (ImGui.BeginTable("Worlds", 1, TableFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var worldToDelete = -1;
            for (var i = 0; i < Root.Worlds.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var world = Root.Worlds[i];


                var color = ImGuiExt.Colors[1];
                var isSelected = SelectedWorldIndex == i;
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    SelectedWorldIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        worldToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                CenteredButton(color, _rowMinHeight);

                GiantLabel(world.Identifier, Color.White, _rowMinHeight);

                ImGui.PopID();
            }

            if (worldToDelete != -1)
            {
                Root.Worlds.RemoveAt(worldToDelete);
            }

            ImGui.EndTable();
        }
    }

    protected override void DrawLeft()
    {
        DrawWorlds();
        if (ImGuiExt.ColoredButton("+ Add World", new Vector2(-1, 0)))
        {
            Root.Worlds.Add(new NewWorld());
        }
    }

    protected override void DrawRight()
    {
        if (SelectedWorldIndex <= Root.Worlds.Count - 1)
        {
            DrawSelectedWorldProperties(Root.Worlds[SelectedWorldIndex]);
        }
    }
    
    private void DrawSelectedWorldProperties(NewWorld world)
    {
        ImGui.PushID("World");

        SimpleTypeInspector.InspectString("Identifier", ref world.Identifier);

        ImGui.PopID();
    }
}
