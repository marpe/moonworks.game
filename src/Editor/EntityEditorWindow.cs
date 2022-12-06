using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public class NewEntityDefinition
{
    public NewFieldDefinition[] FieldDefinitions = Array.Empty<NewFieldDefinition>();
    public Color Color;
    public uint Width;
    public uint Height;
    public UPoint Size => new(Width, Height);
    public string Identifier = "";
    public float FillOpacity;
    public bool KeepAspectRatio;
    public bool ResizableX;
    public bool ResizableY;
    public bool ShowName;
    public uint TilesetId;
    public uint TileId;
    public double PivotX;
    public double PivotY;
    public string[] Tags = Array.Empty<string>();
}

public class NewFieldDefinition
{
    public string Identifier = "";
    public string Type = "";
}

public class NewWorld
{
    public NewEntityDefinition[] EntityDefinitions = Array.Empty<NewEntityDefinition>();
}

public unsafe class EntityEditorWindow : ImGuiEditorWindow
{
    private readonly MyEditorMain _editor;
    public const string WindowTitle = "Entity Editor";

    private bool _isDirty = false;

    private int _selectedEntityDefIndex = -1;

    private Color _refColor;
    private int _rowMinHeight = 60;

    private NewWorld _world = new();

    public EntityEditorWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }

    private static Color ParseColor(ReadOnlySpan<char> colorStr)
    {
        if (colorStr.Length > 0 && colorStr[0] == '#')
            return ColorExt.FromHex(colorStr.Slice(1));
        return Color.Black;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoCollapse;
        if (_isDirty)
            flags |= ImGuiWindowFlags.UnsavedDocument;

        void PushMenuStyle()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(ImGui.GetStyle()->FramePadding.X, ImGuiMainMenu.MAIN_MENU_PADDING_Y));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(ImGui.GetStyle()->ItemSpacing.X, ImGui.GetStyle()->FramePadding.Y * 2f));
        }

        void PopMenuStyle()
        {
        }

        ImGui.SetNextWindowSize(new Num.Vector2(800, 850), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(200, 200), new Num.Vector2(800, 850));
        if (ImGui.Begin(WindowTitle, default, flags))
        {
            DrawMainMenu();

            SetupDockSpace();
        }
        else
        {
            SetupDockSpace(true);
        }

        ImGui.End();

        // ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(8, 8));

        if (ImGui.Begin("EntityEditorList", default))
        {
            var result = ButtonGroup($"{FontAwesome6.Plus}", "Presets", 200);
            if (result == 0)
            {
                var def = new NewEntityDefinition()
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
                    Tags = Array.Empty<string>(),
                    FieldDefinitions = Array.Empty<NewFieldDefinition>(),
                };

                _refColor = Color.Green;
                var newArr = new NewEntityDefinition[_world.EntityDefinitions.Length + 1];
                Array.Copy(_world.EntityDefinitions, newArr, _world.EntityDefinitions.Length);
                newArr[_world.EntityDefinitions.Length] = def;
                _world.EntityDefinitions = newArr;
            }
            else if (result == 1)
            {
            }

            var entities = _world.EntityDefinitions;
            // ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(0, 0));
            // ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(0, 20));


            var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                             ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable |
                             ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;

            if (ImGui.BeginTable("EntityDefinitions", 1, tableFlags, new Num.Vector2(0, 0)))
            {
                ImGui.TableSetupColumn("Name");

                for (var i = 0; i < entities.Length; i++)
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                    ImGui.TableNextColumn();

                    ImGui.PushID(i);
                    var entityDef = entities[i];

                    var entityColor = entityDef.Color;
                    var isSelected = _selectedEntityDefIndex == i;

                    var (h, s, v) = ColorExt.RgbToHsv(entityColor);
                    var headerColor = ColorExt.HsvToRgb(h, s * 0.9f, v * 0.6f).MultiplyAlpha(isSelected ? 0.5f : 0);
                    var headerActiveColor = ColorExt.HsvToRgb(h, s, v).MultiplyAlpha(0.4f);
                    var headerHoverColor = ColorExt.HsvToRgb(h, s, v).MultiplyAlpha(0.4f);
                    var borderColor = ColorExt.HsvToRgb(h, s, v);

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(ImGui.GetStyle()->ItemSpacing.X, ImGui.GetStyle()->CellPadding.Y * 2));
                    ImGui.PushStyleColor(ImGuiCol.Header, headerColor.PackedValue);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerActiveColor.PackedValue);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, headerHoverColor.PackedValue);
                    ImGui.PushStyleColor(ImGuiCol.Border, borderColor.PackedValue);
                    var selectableFlags = ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap;
                    if (ImGui.Selectable("##Selectable", isSelected, selectableFlags, new Num.Vector2(0, _rowMinHeight)))
                    {
                        _selectedEntityDefIndex = i;
                        _refColor = entityColor;
                    }

                    ImGui.PopStyleColor(4);

                    if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                    {
                        ImGui.MenuItem("Copy", default);
                        ImGui.MenuItem("Cut", default);
                        ImGui.MenuItem("Dupliacte", default);
                        ImGui.MenuItem("Delete", default);
                        ImGui.EndPopup();
                    }

                    var buttonSize = 0.6f * _rowMinHeight;
                    ImGui.SameLine();
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,
                        new Num.Vector2(ImGui.GetStyle()->ItemInnerSpacing.X * 4f, ImGui.GetStyle()->ItemInnerSpacing.Y));
                    var cursorPosX = ImGui.GetCursorPosX();
                    var cursorPosY = ImGui.GetCursorPosY();
                    var contentAvail = ImGui.GetContentRegionAvail();
                    var buttonX = contentAvail.X * 0.5f - buttonSize - ImGui.GetStyle()->ItemInnerSpacing.X;
                    if (buttonX >= cursorPosX)
                    {
                        ImGui.SetCursorPosX(buttonX);
                        ImGui.SetCursorPosY(cursorPosY + (_rowMinHeight - buttonSize) / 2);
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                        ImGuiExt.ColoredButton("##Test", entityColor, new Num.Vector2(buttonSize, buttonSize));
                        ImGui.PopStyleVar();
                        ImGui.SameLine(0, ImGui.GetStyle()->ItemInnerSpacing.X);
                    }

                    ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                    ImGui.PushTextWrapPos();
                    var textSize = ImGui.CalcTextSize(entityDef.Identifier);
                    ImGui.SetCursorPosY(cursorPosY + (_rowMinHeight - (textSize.Y + ImGui.GetStyle()->ItemSpacing.Y)) / 2);

                    ImGui.TextColored(entityColor.ToNumerics(), entityDef.Identifier);
                    ImGui.PopFont();
                    ImGui.PopStyleVar();

                    ImGui.PopStyleVar();
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            SimpleTypeInspector.InspectInt("MinHeight", ref _rowMinHeight, new RangeSettings(0, 100, 1, false));

            // ImGui.PopStyleVar(2);

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }

        ImGui.End();

        if (ImGui.Begin("EntityEditorProps", default))
        {
            var entities = _world.EntityDefinitions;
            if (_selectedEntityDefIndex >= 0 && _selectedEntityDefIndex < entities.Length)
            {
                var entityDef = entities[_selectedEntityDefIndex];

                var origFramePadding = ImGui.GetStyle()->FramePadding;
                var origItemSpacing = ImGui.GetStyle()->ItemSpacing;
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, origFramePadding * 3f);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, origItemSpacing * 2f);
                ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.4f);

                var identifier = entityDef.Identifier;

                string HideIfNarrow(string label, float minWidth)
                {
                    return ImGui.GetContentRegionAvail().X < minWidth ? $"##{label}" : label;
                }

                var minWidth = 200;

                if (SimpleTypeInspector.InspectString(HideIfNarrow("Identifier", minWidth), ref identifier))
                {
                    entities[_selectedEntityDefIndex].Identifier = identifier;
                }

                var (ix, iy) = entityDef.Size;
                var (x, y) = ((int)ix, (int)iy);
                if (ImGuiExt.InspectPoint(HideIfNarrow("Size", minWidth), ref x, ref y))
                {
                    entityDef.Width = (uint)x;
                    entityDef.Height = (uint)y;
                }

                ImGuiExt.LabelPrefix(HideIfNarrow("Tags", minWidth));

                for (var i = 0; i < entityDef.Tags.Length; i++)
                {
                    var colorIndex = (2 + i) % ImGuiExt.Colors.Length;
                    if (ImGuiExt.ColoredButton(entityDef.Tags[i], ImGuiExt.Colors[colorIndex], new Num.Vector2(0, 26)))
                    {
                    }

                    ImGui.SameLine();
                }

                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(4, 2));
                ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
                if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.White, new Color(95, 111, 165), new Num.Vector2(26, 26), "Add Tag"))
                {
                }

                ImGui.PopFont();
                ImGui.PopStyleVar();

                if (SimpleTypeInspector.InspectColor(HideIfNarrow("Smart Color", minWidth), ref entityDef.Color, _refColor, ImGuiColorEditFlags.NoAlpha))
                {
                    entityDef.Color = entityDef.Color;
                }

                var (pivotX, pivotY) = (entityDef.PivotX, entityDef.PivotY);
                if (PivotPointEditor(HideIfNarrow("Pivot Point", minWidth), ref pivotX, ref pivotY, 40, entityDef.Color.PackedValue))
                {
                    entityDef.PivotX = pivotX;
                    entityDef.PivotY = pivotY;
                }

                ImGuiExt.SeparatorText("Fields");


                var result = ButtonGroup($"{FontAwesome6.Plus} Single Value", $"{FontAwesome6.Plus} Array", minWidth);
                if (result == 0)
                {
                    ImGui.OpenPopup("SingleValuePopup");
                }
                else if (result == 1)
                {
                }

                var itemSpacingY = ImGui.GetStyle()->ItemSpacing.Y;
                ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos() - new Num.Vector2(0, itemSpacingY), ImGuiCond.Always, Num.Vector2.Zero);
                if (ImGui.BeginPopupModal("SingleValuePopup", default, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize))
                {
                    var buttonSize = new Num.Vector2(100, 100);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Num.Vector2.Zero);
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
                    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Num.Vector2.Zero);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                    var fieldResult = false;
                    if (ImGui.BeginTable("FieldTypes", 4, ImGuiTableFlags.None, new Num.Vector2(400, 300)))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Integer", ImGuiExt.GetColor(0), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Float", ImGuiExt.GetColor(1), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Boolean", ImGuiExt.GetColor(2), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("String", ImGuiExt.GetColor(3), buttonSize);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Multilines", ImGuiExt.GetColor(4), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Color", ImGuiExt.GetColor(5), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Enum", ImGuiExt.GetColor(6), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("File path", ImGuiExt.GetColor(7), buttonSize);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Tile", ImGuiExt.GetColor(8), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Entity ref", ImGuiExt.GetColor(9), buttonSize);
                        ImGui.TableNextColumn();
                        fieldResult |= ImGuiExt.ColoredButton("Point", ImGuiExt.GetColor(10), buttonSize);

                        ImGui.EndTable();
                    }

                    if (fieldResult)
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.PopStyleVar(4);
                    ImGui.EndPopup();
                }


                ImGui.Separator();

                for (var i = 0; i < entityDef.FieldDefinitions.Length; i++)
                {
                    var field = entityDef.FieldDefinitions[i];
                    ImGui.Selectable(field.Identifier, false, ImGuiSelectableFlags.AllowItemOverlap, default);
                    ImGui.SameLine();
                    ImGui.Text(field.Type);
                }

                ImGui.PopStyleVar(2);
                ImGui.PopItemWidth();
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

        // ImGui.PopStyleVar();
    }

    private static int ButtonGroup(string firstLabel, string secondLabel, int minWidth)
    {
        var result = -1;
        var contentAvail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        var canFitOnOneLine = contentAvail.X >= minWidth;
        var buttonWidth = canFitOnOneLine ? contentAvail.X * 0.5f : -ImGuiExt.FLT_MIN;
        var buttonSize = new Num.Vector2(buttonWidth, 40);
        var buttonGap = canFitOnOneLine ? new Num.Vector2(2, 0) : Num.Vector2.Zero;
        if (ImGuiExt.ColoredButton(firstLabel, buttonSize))
        {
            result = 0;
        }

        if (canFitOnOneLine)
            ImGui.SameLine(0, buttonGap.X);
        if (ImGuiExt.ColoredButton(secondLabel, buttonSize - buttonGap))
        {
            result = 1;
        }

        ImGui.PopFont();
        ImGui.PopStyleVar();
        return result;
    }

    private static bool PivotPointEditor(string label, ref double pivotX, ref double pivotY, float size, uint color)
    {
        if (ImGuiInternal.GetCurrentWindow()->SkipItems)
            return false;

        var pivotAnchors = new Num.Vector2[]
        {
            new(0, 0),
            new(0.5f, 0),
            new(1, 0),

            new(0, 0.5f),
            new(0.5f, 0.5f),
            new(1, 0.5f),

            new(0, 1),
            new(0.5f, 1),
            new(1, 1),
        };

        ImGuiExt.LabelPrefix(label);

        var itemSpacing = ImGui.GetStyle()->ItemSpacing;
        var result = false;
        if (ImGui.BeginChild(label, new Num.Vector2(size + itemSpacing.X * 2.0f, size + itemSpacing.Y * 2f)))
        {
            var dl = ImGui.GetWindowDrawList();
            var cursor = ImGui.GetCursorScreenPos();
            var pivotRectSize = new Num.Vector2(size, size);
            var basePadding = new Num.Vector2(size / 4);
            var rectTopLeft = basePadding + cursor;
            var anchorRadius = MathF.Max(6, size / 8f);
            dl->AddRectFilled(rectTopLeft, rectTopLeft + pivotRectSize, color);
            dl->AddRect(rectTopLeft, rectTopLeft + pivotRectSize, Color.White.PackedValue);
            for (var i = 0; i < pivotAnchors.Length; i++)
            {
                var anchorCenter = pivotAnchors[i] * size;
                var isSelected = MathF.Approx((float)pivotX, pivotAnchors[i].X) && MathF.Approx((float)pivotY, pivotAnchors[i].Y);

                ImGui.SetCursorScreenPos(rectTopLeft + anchorCenter - Num.Vector2.One * anchorRadius);
                if (ImGui.InvisibleButton($"Anchor{i}", new Num.Vector2(anchorRadius * 2, anchorRadius * 2)))
                {
                    pivotX = pivotAnchors[i].X;
                    pivotY = pivotAnchors[i].Y;
                    result = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(pivotAnchors[i].ToString());
                }

                if (isSelected)
                {
                    var fillColor = Color.White;
                    var borderColor = Color.Blue;
                    dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius * 1.5f, fillColor.PackedValue);
                    dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius * 0.8f, borderColor.PackedValue);
                    dl->AddCircle(rectTopLeft + anchorCenter, anchorRadius * 1.5f, borderColor.PackedValue);
                }
                else
                {
                    var alpha = ImGui.IsItemHovered() ? 0.8f : 0.4f;
                    var fillColor = Color.White.MultiplyAlpha(alpha);
                    var borderColor = Color.Black.MultiplyAlpha(alpha);
                    dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius, fillColor.PackedValue);
                    dl->AddCircle(rectTopLeft + anchorCenter, anchorRadius, borderColor.PackedValue);
                }
            }
        }

        ImGui.EndChild();
        return result;
    }

    private void DrawMainMenu()
    {
        var beginMainMenu = ImGui.BeginMenuBar();
        if (!beginMainMenu)
            return;

        var result = ImGui.BeginMenu("File");

        if (result)
        {
            if (ImGui.MenuItem("Save", default))
            {
                var world = _editor.World;
                if (world.IsLoaded)
                {
                    var json = world.LDtk.LdtkRaw.ToJson();
                    var filename = "test.ldtk";
                    File.WriteAllText(filename, json);
                    Logs.LogInfo($"Saved to {filename}");
                }
            }

            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }

    private static void SetupDockSpace(bool keepAlive = false)
    {
        var dockspaceID = ImGui.GetID("EntityEditorDockspace");

        ImGuiWindowClass workspaceWindowClass;
        workspaceWindowClass.ClassId = dockspaceID;
        workspaceWindowClass.DockingAllowUnclassed = false;

        if (ImGuiInternal.DockBuilderGetNode(dockspaceID) == null)
        {
            var dockFlags = ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace | ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton;
            ImGuiInternal.DockBuilderAddNode(dockspaceID, (ImGuiDockNodeFlags)dockFlags);
            var contentAvail = ImGui.GetContentRegionAvail();
            var size = new Num.Vector2(MathF.Max(4.0f, contentAvail.X), MathF.Max(4.0f, contentAvail.Y));
            ImGuiInternal.DockBuilderSetNodeSize(dockspaceID, size);
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

        var flags = keepAlive ? ImGuiDockNodeFlags.KeepAliveOnly : ImGuiDockNodeFlags.None;
        ImGui.DockSpace(dockspaceID, new Num.Vector2(0.0f, 0.0f), flags, &workspaceWindowClass);
    }
}
