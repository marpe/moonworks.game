using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public unsafe class EntityEditorWindow : ImGuiEditorWindow
{
    private readonly MyEditorMain _editor;
    public const string WindowTitle = "Entity Editor";

    private bool _isDirty = false;

    private int _selectedEntityDefIndex = 0;

    public EntityEditorWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoCollapse;
        if (_isDirty)
            flags |= ImGuiWindowFlags.UnsavedDocument;

        ImGui.SetNextWindowSize(new Num.Vector2(1000, 800), ImGuiCond.FirstUseEver);
        ImGui.Begin(WindowTitle, default, flags);

        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                ImGui.MenuItem("Test", default);
                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        var dockspaceID = ImGui.GetID("EntityEditorDockspace");

        ImGuiWindowClass workspaceWindowClass;
        workspaceWindowClass.ClassId = dockspaceID;
        workspaceWindowClass.DockingAllowUnclassed = false;

        if (ImGuiInternal.DockBuilderGetNode(dockspaceID) == null)
        {
            var dockFlags = ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace | ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton;
            ImGuiInternal.DockBuilderAddNode(dockspaceID, (ImGuiDockNodeFlags)dockFlags);
            ImGuiInternal.DockBuilderSetNodeSize(dockspaceID, ImGui.GetContentRegionAvail());
            //
            var rightDockID = 0u;
            var leftDockID = 0u;
            ImGuiInternal.DockBuilderSplitNode(dockspaceID, ImGuiDir.Left, 0.5f, &leftDockID, &rightDockID);
            // Dock viewport
            var pLeftNode = ImGuiInternal.DockBuilderGetNode(leftDockID);
            pLeftNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe |
                                                          ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            var pRightNode = ImGuiInternal.DockBuilderGetNode(rightDockID);
            pRightNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe |
                                                           ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            ImGuiInternal.DockBuilderDockWindow("EntityEditorList", leftDockID);
            ImGuiInternal.DockBuilderDockWindow("EntityEditorProps", rightDockID);
            //
            ImGuiInternal.DockBuilderFinish(dockspaceID);
        }

        ImGui.DockSpace(dockspaceID, new Num.Vector2(0.0f, 0.0f), ImGuiDockNodeFlags.None, &workspaceWindowClass);

        ImGui.End();

        ImGui.Begin("EntityEditorList", default);


        {
            var world = _editor.GameScreen.World;
            if (world != null)
            {
                var entities = world.LdtkRaw.Defs.Entities;
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(0, 20));
                for (var i = 0; i < entities.Length; i++)
                {
                    var entityDef = entities[i];
                    if (ImGui.MenuItem(entityDef.Identifier, default, _selectedEntityDefIndex == i))
                    {
                        _selectedEntityDefIndex = i;
                    }
                }

                ImGui.PopStyleVar();
            }

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }
        ImGui.End();

        ImGui.Begin("EntityEditorProps", default);
        {
            var world = _editor.GameScreen.World;
            if (world != null)
            {
                var entities = world.LdtkRaw.Defs.Entities;
                if (_selectedEntityDefIndex >= 0 && _selectedEntityDefIndex < entities.Length)
                {
                    var entityDef = entities[_selectedEntityDefIndex];

                    var identifier = entityDef.Identifier;

                    var cursor = ImGui.GetCursorPos();
                    
                    if (SimpleTypeInspector.InspectString("Identifier", ref identifier))
                    {
                        entities[_selectedEntityDefIndex].Identifier = identifier;
                    }
                    
                    ImGui.SetCursorPos(cursor);
                    ImGui.NewLine();

                    for (var i = 0; i < entityDef.FieldDefs.Length; i++)
                    {
                        var field = entityDef.FieldDefs[i];
                        ImGui.Selectable(field.Identifier, false, ImGuiSelectableFlags.AllowItemOverlap, default);
                        ImGui.SameLine();
                        ImGui.Text(field.Type);
                    }
                }
            }

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }
        ImGui.End();
    }
}
