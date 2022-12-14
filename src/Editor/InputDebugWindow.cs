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
        ImGui.SetNextWindowSize(new Num.Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(200, 200), new Num.Vector2(800, 800));
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            var i = 0;
            void DrawBind(string label, Binds.ButtonBind bind)
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

            if (ImGui.BeginTable("Buttons", 5, tableFlags, new Num.Vector2(0, 150)))
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
            
            if (ImGui.BeginTable("Camera", 5, tableFlags, new Num.Vector2(0, 350)))
            {
                DrawTableHeaders();

                ImGui.TableHeadersRow();

                DrawBind("ZoomIn", Binds.Camera.ZoomIn);
                DrawBind("ZoomOut", Binds.Camera.ZoomOut);
                DrawBind("Up", Binds.Camera.Up);
                DrawBind("Down", Binds.Camera.Down);
                DrawBind("Forward", Binds.Camera.Forward);
                DrawBind("Back", Binds.Camera.Back);
                DrawBind("Right", Binds.Camera.Right);
                DrawBind("Left", Binds.Camera.Left);
                DrawBind("Pan", Binds.Camera.Pan);
                DrawBind("Reset", Binds.Camera.Reset);

                ImGui.EndTable();
            }
            
            if (ImGui.BeginTable("Player", 5, tableFlags, new Num.Vector2(0, 0)))
            {
                DrawTableHeaders();

                ImGui.TableHeadersRow();

                DrawBind("Right", Binds.Player.Right);
                DrawBind("Left", Binds.Player.Left);
                DrawBind("Jump", Binds.Player.Jump);
                DrawBind("Fire1", Binds.Player.Fire1);
                DrawBind("Respawn", Binds.Player.Respawn);
                DrawBind("MoveToMouse", Binds.Player.MoveToMouse);

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

            if (ImGui.BeginChild("Child1", new Num.Vector2(child1DefaultWidth + _sz1, 0), true))
            {
            }

            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("Child2", new Num.Vector2(contentAvail.X - (child1DefaultWidth + _sz1) - 5, 0), true))
            {
            }
            ImGui.EndChild();
        }

        ImGui.End();
    }
}
