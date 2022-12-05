using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public unsafe class InputDebugWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    public const string WindowTitle = "Input Debug";

    private float _sz1 = 200;
    private float _sz2 = 200;

    public InputDebugWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.NoCollapse;
        ImGui.SetNextWindowSize(new Num.Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(200, 200), new Num.Vector2(800, 800));
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            var i = 0;

            var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Hideable |
                             ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("EntityDefinitions", 5, tableFlags, new Num.Vector2(0, 350)))
            {
                ImGui.TableSetupColumn("Key");
                ImGui.TableSetupColumn("Active");
                ImGui.TableSetupColumn("WasActive");
                ImGui.TableSetupColumn("WU");
                ImGui.TableSetupColumn("GU");

                foreach (var (key, value) in BindHandler.Buttons)
                {
                    ImGui.PushID(i);
                    i++;
                    
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(key.ToString());
                    ImGui.TableNextColumn();
                    SimpleTypeInspector.InspectBool("##Active", ref value.Active);
                    ImGui.TableNextColumn();
                    SimpleTypeInspector.InspectBool("##WasActive", ref value.WasActive);
                    ImGui.TableNextColumn();
                    SimpleTypeInspector.InspectULong("##WorldUpdateCount", ref value.WorldUpdateCount);
                    ImGui.TableNextColumn();
                    SimpleTypeInspector.InspectULong("##GameUpdateCount", ref value.GameUpdateCount);
                    
                    ImGui.PopID();
                }
                
                ImGui.EndTable();
            }

            var windowHeight = ImGui.GetContentRegionAvail().Y;
            var cursorPos = ImGui.GetCursorScreenPos();
            var contentAvail = ImGui.GetContentRegionAvail();
            var child1DefaultWidth = 150;
            
            var bb = new ImRect(
                cursorPos + new Num.Vector2(child1DefaultWidth + _sz1, 0),
                cursorPos + new Num.Vector2(child1DefaultWidth + _sz1 + 5, windowHeight)
            ); // bb.Min == child0->Pos, bb-Max child1->Pos


            if (ImGuiInternal.SplitterBehavior(bb, ImGui.GetID("Splitter"), ImGuiAxis.ImGuiAxis_X, 
                    ImGuiExt.RefPtr(ref _sz1), ImGuiExt.RefPtr(ref _sz2),
                    10, 10, 4, 0.04f, ImGuiExt.GetStyleColor(ImGuiCol.WindowBg).PackedValue()))
            {
            }

            ImGui.BeginChild("Child1", new Num.Vector2(child1DefaultWidth + _sz1, 0), true);
            ImGui.Text("Inside child 1");
            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginChild("Child2", new Num.Vector2(contentAvail.X - (child1DefaultWidth + _sz1) - 5, 0), true);
            ImGui.Text("Inside child 2");
            ImGui.EndChild();
        }

        ImGui.End();
    }
}
