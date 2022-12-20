using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class InputDebugWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    public const string WindowTitle = "Input Debug";

    private float _sz1 = 200;
    private float _sz2 = 200;

    public InputDebugWindow(MyEditorMain editor) : base(WindowTitle)
    {
        KeyboardShortcut = "^I";
        _editor = editor;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        void DrawTableHeaders()
        {
            ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.None, 200);
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.None | ImGuiTableColumnFlags.NoResize, 20);
            ImGui.TableSetupColumn("WasActive", ImGuiTableColumnFlags.None | ImGuiTableColumnFlags.NoResize, 20);
            ImGui.TableSetupColumn("WU", ImGuiTableColumnFlags.None | ImGuiTableColumnFlags.NoResize, 50);
            ImGui.TableSetupColumn("GU", ImGuiTableColumnFlags.None | ImGuiTableColumnFlags.NoResize, 50);
        }

        var flags = ImGuiWindowFlags.NoCollapse;
        ImGui.SetNextWindowSize(new Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(200, 200), new Vector2(800, 800));
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            var i = 0;
            void DrawBind(string label, Binds.ActionState bind)
            {
                ImGui.PushID(i);
                i++;

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(label);
                ImGui.TableNextColumn();
                SimpleTypeInspector.InspectBool("##Active", ref bind.Active);
                ImGui.TableNextColumn();
                SimpleTypeInspector.InspectBool("##WasActive", ref bind.WasActive);
                ImGui.TableNextColumn();
                SimpleTypeInspector.InspectULong("##WorldUpdateCount", ref bind.WorldUpdateCount);
                ImGui.TableNextColumn();
                SimpleTypeInspector.InspectULong("##GameUpdateCount", ref bind.GameUpdateCount);

                ImGui.PopID();
            }
            
            var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Hideable |
                             ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("Buttons", 5, tableFlags, new Vector2(0, 150)))
            {
                DrawTableHeaders();

                ImGui.TableHeadersRow();

                foreach (var (key, value) in Binds.Buttons)
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
            
            if (ImGui.BeginTable("Camera", 5, tableFlags, new Vector2(0, 350)))
            {
                DrawTableHeaders();

                ImGui.TableHeadersRow();

                DrawBind("ZoomIn", Binds.GetAction(Binds.InputAction.ZoomIn));
                DrawBind("ZoomOut", Binds.GetAction(Binds.InputAction.ZoomOut));
                DrawBind("Up", Binds.GetAction(Binds.InputAction.Up));
                DrawBind("Down", Binds.GetAction(Binds.InputAction.Down));
                DrawBind("Forward", Binds.GetAction(Binds.InputAction.Forward));
                DrawBind("Back", Binds.GetAction(Binds.InputAction.Back));
                DrawBind("Right", Binds.GetAction(Binds.InputAction.Right));
                DrawBind("Left", Binds.GetAction(Binds.InputAction.Left));
                DrawBind("Pan", Binds.GetAction(Binds.InputAction.Pan));
                DrawBind("Reset", Binds.GetAction(Binds.InputAction.Reset));

                ImGui.EndTable();
            }
            
            if (ImGui.BeginTable("Player", 5, tableFlags, new Vector2(0, 0)))
            {
                DrawTableHeaders();

                ImGui.TableHeadersRow();

                DrawBind("Right", Binds.GetAction(Binds.InputAction.Right));
                DrawBind("Left", Binds.GetAction(Binds.InputAction.Left));
                DrawBind("Jump", Binds.GetAction(Binds.InputAction.Jump));
                DrawBind("Fire1", Binds.GetAction(Binds.InputAction.Fire1));
                DrawBind("Respawn", Binds.GetAction(Binds.InputAction.Respawn));
                DrawBind("MoveToMouse", Binds.GetAction(Binds.InputAction.MoveToMouse));

                ImGui.EndTable();
            }

            var windowHeight = ImGui.GetContentRegionAvail().Y;
            var cursorPos = ImGui.GetCursorScreenPos();
            var contentAvail = ImGui.GetContentRegionAvail();
            var child1DefaultWidth = 150;

            var bb = new ImRect(
                cursorPos + new Vector2(child1DefaultWidth + _sz1, 0),
                cursorPos + new Vector2(child1DefaultWidth + _sz1 + 5, windowHeight)
            ); // bb.Min == child0->Pos, bb-Max child1->Pos


            if (ImGuiInternal.SplitterBehavior(bb, ImGui.GetID("Splitter"), ImGuiAxis.ImGuiAxis_X,
                    ImGuiExt.RefPtr(ref _sz1), ImGuiExt.RefPtr(ref _sz2),
                    10, 10, 4, 0.04f, ImGuiExt.GetStyleColor(ImGuiCol.WindowBg).PackedValue()))
            {
            }

            if (ImGui.BeginChild("Child1", new Vector2(child1DefaultWidth + _sz1, 0), true))
            {
            }

            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("Child2", new Vector2(contentAvail.X - (child1DefaultWidth + _sz1) - 5, 0), true))
            {
            }
            ImGui.EndChild();
        }

        ImGui.End();
    }
}
