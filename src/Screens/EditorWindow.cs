using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Cameras;
using MyGame.Editor;
using MyGame.WorldsRoot;
using FieldInstance = MyGame.WorldsRoot.FieldInstance;
using LayerInstance = MyGame.WorldsRoot.LayerInstance;
using Level = MyGame.WorldsRoot.Level;

namespace MyGame.Screens;

public unsafe class EditorWindow : ImGuiEditorWindow
{
    public const string EditorViewWindowTitle = "EditorView";
    public const string EditorDockspaceNodeId = "EditorDockspace";
    public const string EntityEditorWindowTitle = "Entit Editor";
    public const string EntityEditorDockspaceId = "EntityEditorDockspace";
    public const string TablesWindowTitle = "TablesEditorView";
    public const string TablesLeftWindowTitle = "TablesLeftWindow";
    public const string TablesRightWindowTitle = "TablesRightWindow";
    public WorldsRoot.WorldsRoot WorldsRoot = new();
    private MyEditorMain _editor;
    private IntPtr? _editorRenderTextureId;
    private int _selectedLayerDefIndex;
    private int _selectedLevelIndex;
    private int _selectedWorldIndex;
    private int _selectedLayerInstanceIndex;
    private int _selectedTileSetDefinitionIndex;
    private Camera _camera = new(1920, 1080);
    private bool _isDirty;
    private string _tempRequiredTag = "";
    private string _tempExcludedTag = "";
    private int _selectedIntGridValueIndex;
    int _rowMinHeight = 60;
    private int _selectedEntityDefinitionIndex;

    ImGuiTableFlags _tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                                  ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable |
                                  ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;

    private Color _refColor;

    public EditorWindow(MyEditorMain editor) : base(EditorViewWindowTitle)
    {
        KeyboardShortcut = "^E";
        var filename = ContentPaths.worlds.worlds_json;
        if (File.Exists(filename))
        {
            var json = File.ReadAllText(filename);
            WorldsRoot = JsonConvert.DeserializeObject<WorldsRoot.WorldsRoot>(json, ContentManager.JsonSerializerSettings) ??
                         throw new InvalidOperationException();
        }

        _editor = editor;
    }

    public void Update(float deltaSeconds)
    {
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
                var json = JsonConvert.SerializeObject(WorldsRoot, Formatting.Indented, ContentManager.JsonSerializerSettings);
                var filename = ContentPaths.worlds.worlds_json;
                File.WriteAllText(filename, json);
                Logs.LogInfo($"Saved to {filename}");
            }

            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoCollapse;
        if (_isDirty)
            flags |= ImGuiWindowFlags.UnsavedDocument;

        ImGui.SetNextWindowSize(new Num.Vector2(800, 850), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(200, 200), new Num.Vector2(800, 850));
        if (ImGui.Begin(TablesWindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            DrawMainMenu();

            SetupDockSpace(EditorDockspaceNodeId, TablesLeftWindowTitle, TablesRightWindowTitle);
        }
        else
        {
            SetupDockSpace(EditorDockspaceNodeId, TablesLeftWindowTitle, TablesRightWindowTitle, true);
        }

        ImGui.End();

        DrawEntityEditor();

        DrawEditorWindow();

        DrawLeftTables();

        DrawRightTables();
    }

    private void DrawEntityEditor()
    {
        if (_selectedWorldIndex > WorldsRoot.Worlds.Count - 1)
            return;

        var flags = ImGuiWindowFlags.NoCollapse;
        if (_isDirty)
            flags |= ImGuiWindowFlags.UnsavedDocument;

        var world = WorldsRoot.Worlds[_selectedWorldIndex];
        var listWindowTitle = "EntityEditorList";
        var propsWindowTitle = "EntityEditorProps";

        ImGui.SetNextWindowSize(new Num.Vector2(800, 850), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(200, 200), new Num.Vector2(800, 850));
        if (ImGui.Begin(EntityEditorWindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            SetupDockSpace(EntityEditorDockspaceId, listWindowTitle, propsWindowTitle);
        }
        else
        {
            SetupDockSpace(EntityEditorDockspaceId, listWindowTitle, propsWindowTitle, true);
        }

        ImGui.End();

        if (ImGui.Begin(listWindowTitle))
        {
            var result = ButtonGroup($"{FontAwesome6.Plus}", "Presets", 200);
            if (result == 0)
            {
                var def = new WorldsRoot.EntityDefinition()
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
                WorldsRoot.EntityDefinitions.Add(def);
            }
            else if (result == 1)
            {
            }

            var entities = WorldsRoot.EntityDefinitions;
            // ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(0, 0));
            // ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(0, 20));

            if (ImGui.BeginTable("EntityDefinitions", 1, _tableFlags, new Num.Vector2(0, 0)))
            {
                ImGui.TableSetupColumn("Name");

                for (var i = 0; i < entities.Count; i++)
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                    ImGui.TableNextColumn();

                    ImGui.PushID(i);
                    var entityDef = entities[i];

                    var color = entityDef.Color;
                    var isSelected = _selectedEntityDefinitionIndex == i;

                    if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                    {
                        _selectedEntityDefinitionIndex = i;
                        _refColor = color;
                    }

                    if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                    {
                        ImGui.MenuItem("Copy", default);
                        ImGui.MenuItem("Cut", default);
                        ImGui.MenuItem("Duplicate", default);
                        ImGui.MenuItem("Delete", default);
                        ImGui.EndPopup();
                    }

                    CenteredButton(color, _rowMinHeight);

                    GiantLabel(entityDef.Identifier, color, _rowMinHeight);

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
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }

        ImGui.End();

        if (ImGui.Begin(propsWindowTitle))
        {
            var entities = WorldsRoot.EntityDefinitions;
            if (_selectedEntityDefinitionIndex >= 0 && _selectedEntityDefinitionIndex < entities.Count)
            {
                var entityDef = entities[_selectedEntityDefinitionIndex];

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
                    entities[_selectedEntityDefinitionIndex].Identifier = identifier;
                }

                var (ix, iy) = entityDef.Size;
                var (x, y) = ((int)ix, (int)iy);
                if (ImGuiExt.InspectPoint(HideIfNarrow("Size", minWidth), ref x, ref y))
                {
                    entityDef.Width = (uint)x;
                    entityDef.Height = (uint)y;
                }

                ImGuiExt.LabelPrefix(HideIfNarrow("Tags", minWidth));

                for (var i = 0; i < entityDef.Tags.Count; i++)
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
                if (DrawAddFieldDefModal("SingleValuePopup", out var fieldType))
                {
                }

                ImGui.Separator();

                for (var i = 0; i < entityDef.FieldDefinitions.Count; i++)
                {
                    var field = entityDef.FieldDefinitions[i];
                    ImGui.Selectable(field.Identifier, false, ImGuiSelectableFlags.AllowItemOverlap, default);
                    ImGui.SameLine();
                    ImGui.Text(field.FieldType.ToString());
                }

                ImGui.PopStyleVar(2);
                ImGui.PopItemWidth();
            }

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }

        ImGui.End();
    }

    public static bool DrawAddFieldDefModal(string label, out FieldType fieldType)
    {
        var result = false;
        fieldType = FieldType.Int;

        if (ImGui.BeginPopupModal(label, default, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize))
        {
            var buttonSize = new Num.Vector2(100, 100);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Num.Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Num.Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            if (ImGui.BeginTable("FieldTypes", 4, ImGuiTableFlags.None, new Num.Vector2(400, 300)))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Integer", ImGuiExt.GetColor(), buttonSize))
                {
                    fieldType = FieldType.Int;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Float", ImGuiExt.GetColor(1), buttonSize))
                {
                    fieldType = FieldType.Float;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Boolean", ImGuiExt.GetColor(2), buttonSize))
                {
                    fieldType = FieldType.Bool;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("String", ImGuiExt.GetColor(3), buttonSize))
                {
                    fieldType = FieldType.String;
                    result = true;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Multilines", ImGuiExt.GetColor(4), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Color", ImGuiExt.GetColor(5), buttonSize))
                {
                    fieldType = FieldType.Color;
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Enum", ImGuiExt.GetColor(6), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("File path", ImGuiExt.GetColor(7), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Tile", ImGuiExt.GetColor(8), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Entity ref", ImGuiExt.GetColor(9), buttonSize))
                {
                    result = true;
                }

                ImGui.TableNextColumn();
                if (ImGuiExt.ColoredButton("Point", ImGuiExt.GetColor(10), buttonSize))
                {
                    fieldType = FieldType.Point;
                    result = true;
                }

                ImGui.EndTable();
            }

            if (result)
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopStyleVar(4);
            ImGui.EndPopup();
        }

        return result;
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

    private void DrawRightTables()
    {
        if (ImGui.Begin(TablesRightWindowTitle))
        {
            if (_selectedWorldIndex <= WorldsRoot.Worlds.Count - 1)
            {
                var world = WorldsRoot.Worlds[_selectedWorldIndex];

                DrawSelectedWorldProperties(world);

                if (_selectedLevelIndex <= world.Levels.Count - 1)
                {
                    var level = world.Levels[_selectedLevelIndex];
                    DrawLevelPropertyEditor(level);
                }
            }

            if (_selectedTileSetDefinitionIndex <= WorldsRoot.TileSetDefinitions.Count - 1)
            {
                var tileSetDef = WorldsRoot.TileSetDefinitions[_selectedTileSetDefinitionIndex];
                if (ImGuiExt.BeginCollapsingHeader("TileSetDefinition", ImGuiExt.Colors[0], ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont.Tiny))
                {
                    ImGui.PushID("TileSetDefinition");
                    SimpleTypeInspector.InspectInputInt("Uid", ref tileSetDef.Uid);
                    SimpleTypeInspector.InspectString("Identifier", ref tileSetDef.Identifier);
                    SimpleTypeInspector.InspectString("Path", ref tileSetDef.Path);

                    if (tileSetDef.Path != "")
                    {
                        var texture = GetTileSetTexture(tileSetDef.Path);
                        var avail = ImGui.GetContentRegionAvail();
                        var height = MathF.Max(1.0f, texture.Height) / MathF.Max(1.0f, texture.Width) * avail.X;
                        ImGui.Image((void*)texture.Handle, new Num.Vector2(avail.X, height), Num.Vector2.Zero, Num.Vector2.One, Color.White.ToNumerics(),
                            Color.Black.ToNumerics());
                    }

                    ImGui.PopID();
                    ImGuiExt.EndCollapsingHeader();
                }
            }

            if (_selectedLayerDefIndex <= WorldsRoot.LayerDefinitions.Count - 1)
            {
                if (ImGuiExt.BeginCollapsingHeader("LayerDefinition", ImGuiExt.Colors[0], ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont.Tiny))
                {
                    ImGui.PushID("LayerDef");
                    var layerDef = WorldsRoot.LayerDefinitions[_selectedLayerDefIndex];
                    SimpleTypeInspector.InspectInputInt("Uid", ref layerDef.Uid);
                    SimpleTypeInspector.InspectString("Identifier", ref layerDef.Identifier);
                    var rangeSettings = new RangeSettings { MinValue = 16, MaxValue = 512, StepSize = 1, UseDragVersion = false };
                    SimpleTypeInspector.InspectUInt("GridSize", ref layerDef.GridSize, rangeSettings);
                    EnumInspector.InspectEnum("LayerType", ref layerDef.LayerType, true);

                    if (ImGuiExt.BeginCollapsingHeader("Tags", ImGuiExt.Colors[3], ImGuiTreeNodeFlags.None, ImGuiFont.Tiny))
                    {
                        DrawLayerDefTags(layerDef);
                        ImGuiExt.EndCollapsingHeader();
                    }


                    if (layerDef.LayerType == LayerType.IntGrid)
                    {
                        var tableFlags2 = ImGuiTableFlags.BordersH | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Hideable |
                                          ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.SizingFixedFit |
                                          ImGuiTableFlags.RowBg;

                        if (ImGui.BeginTable("IntGridValues", 4, tableFlags2, new Num.Vector2(0, 100)))
                        {
                            ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.None, 32);
                            ImGui.TableSetupColumn("Identifier", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.None, 50);
                            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.NoHeaderLabel, 40);

                            var valueToRemove = -1;
                            for (var i = 0; i < layerDef.IntGridValues.Count; i++)
                            {
                                ImGui.PushID(i);
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                var dl = ImGui.GetWindowDrawList();
                                var min = ImGui.GetCursorScreenPos();
                                dl->AddRectFilled(min, min + new Num.Vector2(32, ImGui.GetFrameHeightWithSpacing()),
                                    layerDef.IntGridValues[i].Color.MultiplyAlpha(0.33f).PackedValue, 4f);
                                dl->AddRect(min, min + new Num.Vector2(32, ImGui.GetFrameHeightWithSpacing()), layerDef.IntGridValues[i].Color.PackedValue, 4f);
                                var label = (i + 1).ToString();
                                var textSize = ImGui.CalcTextSize(label);
                                var textPos = min + new Num.Vector2(14 - textSize.X * 0.5f, 4);
                                dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 18f, textPos, Color.White.PackedValue, label);
                                ImGui.TableNextColumn();
                                ImGui.SetNextItemWidth(-1);
                                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(8f, 5f));
                                SimpleTypeInspector.InspectString("##Identifier", ref layerDef.IntGridValues[i].Identifier);
                                ImGui.TableNextColumn();
                                SimpleTypeInspector.InspectColor("##Color", ref layerDef.IntGridValues[i].Color);
                                ImGui.PopStyleVar();
                                ImGui.TableNextColumn();
                                if (ImGuiExt.ColoredButton(FontAwesome6.Trash, Color.White, ImGuiExt.Colors[2], "Remove", new Num.Vector2(40, 0),
                                        new Num.Vector2(ImGui.GetStyle()->FramePadding.X, 4)))
                                {
                                    valueToRemove = i;
                                }

                                ImGui.PopID();
                            }

                            ImGui.EndTable();

                            if (valueToRemove != -1)
                            {
                                layerDef.IntGridValues.RemoveAt(valueToRemove);
                            }

                            if (ImGuiExt.ColoredButton(FontAwesome6.Plus, Color.White, ImGuiExt.Colors[0], new Num.Vector2(-1, 0), "Add"))
                            {
                                var value = layerDef.IntGridValues.Count + 1;
                                layerDef.IntGridValues.Add(new IntGridValue
                                {
                                    Value = value,
                                    Color = ImGuiExt.Colors[value % ImGuiExt.Colors.Length],
                                    Identifier = "Identifier",
                                });
                            }
                        }
                    }

                    ImGui.PopID();
                    ImGuiExt.EndCollapsingHeader();
                }
            }
        }

        SetDockingFlags();

        ImGui.End();
    }

    private void DrawSelectedWorldProperties(NewWorld world)
    {
        ImGui.PushID("World");

        SimpleTypeInspector.InspectString("Identifier", ref world.Identifier);

        ImGui.PopID();
    }

    private void DrawLevelPropertyEditor(Level level)
    {
        if (ImGuiExt.BeginCollapsingHeader("Level", ImGuiExt.Colors[0], ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont.Tiny))
        {
            ImGui.PushID("Level");
            SimpleTypeInspector.InspectString("Identifier", ref level.Identifier);
            var rangeSettings = new RangeSettings(16, 16000, 1, false);
            if (SimpleTypeInspector.InspectUInt("Width", ref level.Width, rangeSettings))
            {
                ResizeLayers(level, WorldsRoot.LayerDefinitions);
            }

            if (SimpleTypeInspector.InspectUInt("Height", ref level.Height, rangeSettings))
            {
                ResizeLayers(level, WorldsRoot.LayerDefinitions);
            }

            var layerDef = WorldsRoot.LayerDefinitions.FirstOrDefault();
            if (layerDef != null)
            {
                ImGui.BeginDisabled();
                var gridSize = new Point((int)(level.Width / layerDef.GridSize), (int)(level.Height / layerDef.GridSize));
                SimpleTypeInspector.InspectPoint("Cells", ref gridSize);
                ImGui.EndDisabled();
            }

            SimpleTypeInspector.InspectPoint("WorldPos", ref level.WorldPos);
            SimpleTypeInspector.InspectColor("BackgroundColor", ref level.BackgroundColor);

            DrawFieldInstances(level.FieldInstances, WorldsRoot.LevelFieldDefinitions);

            ImGuiExt.SeparatorText("Layers");

            for (var i = 0; i < WorldsRoot.LayerDefinitions.Count; i++)
            {
                if (level.LayerInstances.Any(x => x.LayerDefId == WorldsRoot.LayerDefinitions[i].Uid))
                    continue;
                var layerInstance = CreateLayerInstance(WorldsRoot.LayerDefinitions[i], level);
                level.LayerInstances.Add(layerInstance);
            }

            DrawLayerInstances(level.LayerInstances, WorldsRoot.LayerDefinitions);

            ImGuiExt.SeparatorText("Selected Layer");

            DrawSelectedLayerInstance(level);

            ImGui.PopID();
            ImGuiExt.EndCollapsingHeader();
        }
    }

    private static void RectWithOutline(ImDrawList* dl, Num.Vector2 min, Num.Vector2 max, Color fillColor, Color outlineColor, float rounding = 4.0f)
    {
        dl->AddRectFilled(min, max, fillColor.PackedValue, rounding);
        dl->AddRect(min, max, outlineColor.PackedValue, rounding);
    }

    private void DrawSelectedLayerInstance(Level level)
    {
        if (_selectedLayerInstanceIndex <= level.LayerInstances.Count - 1)
        {
            var layerInstance = level.LayerInstances[_selectedLayerInstanceIndex];
            ImGui.PushID("SelectedLayerInstance");

            var layerDef = WorldsRoot.LayerDefinitions.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);
            if (layerDef == null)
            {
                ImGui.TextColored(Color.Red.ToNumerics(), $"Couldn't find a layer definition with Uid \"{layerInstance.LayerDefId}\"");
            }
            else
            {
                switch (layerDef.LayerType)
                {
                    case LayerType.IntGrid:
                        if (ImGui.BeginTable("IntGridTable", 1, _tableFlags, new Num.Vector2(0, 0)))
                        {
                            ImGui.TableSetupColumn("Name");

                            for (var i = 0; i < layerDef.IntGridValues.Count; i++)
                            {
                                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                                ImGui.TableNextColumn();

                                ImGui.PushID(i);

                                var cursorPos = ImGui.GetCursorScreenPos();

                                var isSelected = _selectedIntGridValueIndex == i;
                                var intGridValue = layerDef.IntGridValues[i];
                                if (GiantButton("##Selectable", isSelected, intGridValue.Color, _rowMinHeight))
                                {
                                    _selectedIntGridValueIndex = i;
                                }

                                var dl = ImGui.GetWindowDrawList();
                                var rectHeight = _rowMinHeight * 0.6f;
                                var min = cursorPos + new Num.Vector2(8, (_rowMinHeight - rectHeight) / 2);
                                var max = min + new Num.Vector2(32, rectHeight);
                                RectWithOutline(dl, min, max, intGridValue.Color.MultiplyAlpha(0.33f), intGridValue.Color);
                                var label = intGridValue.Value.ToString();
                                var textSize = ImGui.CalcTextSize(label);
                                var rectSize = max - min;
                                dl->AddText(min + new Num.Vector2((rectSize.X - textSize.X) / 2, (rectSize.Y - textSize.Y) / 2), Color.White.PackedValue,
                                    label);

                                ImGui.SameLine(60);

                                GiantLabel(intGridValue.Identifier, intGridValue.Color, _rowMinHeight);

                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }

                        break;
                    case LayerType.Entities:
                        if (ImGui.BeginTable("EntityDefTable", 1, _tableFlags, new Num.Vector2(0, 100)))
                        {
                            ImGui.TableSetupColumn("Value");

                            for (var i = 0; i < WorldsRoot.EntityDefinitions.Count; i++)
                            {
                                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                                ImGui.TableNextColumn();
                                var entityDef = WorldsRoot.EntityDefinitions[i];
                                ImGui.PushID(i);
                                var isSelected = _selectedEntityDefinitionIndex == i;
                                var cursorPos = ImGui.GetCursorScreenPos();
                                if (GiantButton("##Selectable", isSelected, entityDef.Color, _rowMinHeight))
                                {
                                    _selectedEntityDefinitionIndex = i;
                                }

                                var tileSet = WorldsRoot.TileSetDefinitions.FirstOrDefault(x => x.Uid == entityDef.TilesetId);
                                if (tileSet != null)
                                {
                                    var dl = ImGui.GetWindowDrawList();
                                    var texture = GetTileSetTexture(tileSet.Path);

                                    var tileSize = new Point((int)(texture.Width / layerDef.GridSize), (int)(texture.Height / layerDef.GridSize));
                                    var cellX = tileSize.X > 0 ? entityDef.TileId % tileSize.X : 0;
                                    var cellY = tileSize.X > 0 ? (int)(entityDef.TileId / tileSize.X) : 0;
                                    var uvMin = new Num.Vector2(1.0f / texture.Width * cellX * layerDef.GridSize,
                                        1.0f / texture.Height * cellY * layerDef.GridSize);
                                    var uvMax = uvMin + new Num.Vector2(layerDef.GridSize / (float)texture.Width, layerDef.GridSize / (float)texture.Height);
                                    var iconSize = new Num.Vector2(32, 32);
                                    var iconPos = cursorPos + iconSize / 2;
                                    dl->AddImage(
                                        (void*)texture.Handle,
                                        iconPos,
                                        iconPos + iconSize,
                                        uvMin,
                                        uvMax
                                    );
                                    dl->AddRect(
                                        iconPos,
                                        iconPos + iconSize,
                                        ImGuiExt.Colors[0].PackedValue
                                    );
                                }

                                ImGui.SameLine(0, 60f);

                                GiantLabel(entityDef.Identifier, entityDef.Color, _rowMinHeight);

                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }

                        if (_selectedEntityDefinitionIndex <= WorldsRoot.EntityDefinitions.Count - 1)
                        {
                            var entityDef = WorldsRoot.EntityDefinitions[_selectedEntityDefinitionIndex];
                            if (ImGuiExt.BeginCollapsingHeader("Entity Definition", ImGuiExt.Colors[5], ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont.Tiny))
                            {
                                ImGui.PushID("EntityDef");

                                SimpleTypeInspector.InspectString("Identifier", ref entityDef.Identifier);
                                SimpleTypeInspector.InspectColor("Color", ref entityDef.Color);
                                SimpleTypeInspector.InspectInputUint("TilesetId", ref entityDef.TilesetId);
                                SimpleTypeInspector.InspectInputUint("TileId", ref entityDef.TileId);

                                ImGuiExt.SeparatorText("Field Definitions");

                                if (ImGui.BeginTable("EntityFieldDefs", 1, _tableFlags, new Num.Vector2(0, 0)))
                                {
                                    ImGui.TableSetupColumn("Value");

                                    for (var i = 0; i < entityDef.FieldDefinitions.Count; i++)
                                    {
                                        ImGui.PushID(i);
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        var fieldDef = entityDef.FieldDefinitions[i];
                                        SimpleTypeInspector.InspectInputInt("Uid", ref fieldDef.Uid);
                                        SimpleTypeInspector.InspectString("Identifier", ref fieldDef.Identifier);
                                        EnumInspector.InspectEnum("Type", ref fieldDef.FieldType);
                                        SimpleTypeInspector.InspectBool("IsArray", ref fieldDef.IsArray);
                                        ImGui.PopID();
                                    }

                                    ImGui.EndTable();
                                }

                                if (ImGuiExt.ColoredButton($"{FontAwesome6.Plus} Add Field", new Num.Vector2(-1, 0)))
                                {
                                    ImGui.OpenPopup("SingleValuePopup");
                                }

                                var itemSpacingY = ImGui.GetStyle()->ItemSpacing.Y;
                                ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos() - new Num.Vector2(0, itemSpacingY), ImGuiCond.Always, Num.Vector2.Zero);
                                if (DrawAddFieldDefModal("SingleValuePopup", out var fieldType))
                                {
                                    entityDef.FieldDefinitions.Add(new FieldDef
                                    {
                                        Uid = IdGen.NewId,
                                        Identifier = "Field",
                                        FieldType = fieldType,
                                        IsArray = false,
                                    });
                                }

                                ImGui.PopID();
                                ImGuiExt.EndCollapsingHeader();
                            }
                        }

                        break;
                    case LayerType.Tiles:
                        break;
                    case LayerType.AutoLayer:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            ImGui.PopID();
        }
    }

    private Texture GetTileSetTexture(string tileSetPath)
    {
        var worldFileDir = Path.GetDirectoryName(ContentPaths.worlds.worlds_json);
        var path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, Path.Join(worldFileDir, tileSetPath));
        Texture texture;

        if (Shared.Content.HasTexture(path))
        {
            texture = Shared.Content.GetTexture(path);
            _editor.ImGuiRenderer.BindTexture(texture);
            return texture;
        }

        if ((path.EndsWith(".png") || path.EndsWith(".aseprite")) && File.Exists(path))
        {
            Shared.Content.LoadAndAddTextures(new[] { path });
            texture = Shared.Content.GetTexture(path);
            _editor.ImGuiRenderer.BindTexture(texture);
            return texture;
        }

        _editor.ImGuiRenderer.BindTexture(_editor.Renderer.BlankSprite.Texture);
        return _editor.Renderer.BlankSprite.Texture;
    }

    private static bool GiantButton(string label, bool isSelected, Color color, int rowMinHeight)
    {
        var (h, s, v) = ColorExt.RgbToHsv(color);
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
        var result = ImGui.Selectable(label, isSelected, selectableFlags, new System.Numerics.Vector2(0, rowMinHeight));
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar();
        return result;
    }

    private void DrawLayerDefTags(LayerDef layerDef)
    {
        ImGuiExt.SeparatorText("Excluded Tags");
        ImGui.SetNextItemWidth(100);
        SimpleTypeInspector.InspectString("##TagExcluded", ref _tempExcludedTag);
        ImGui.SameLine();
        ImGui.BeginDisabled(_tempExcludedTag == "");
        if (ImGuiExt.ColoredButton("+##AddExcluded", Color.White, ImGuiExt.Colors[2], "Add Excluded Tag", new Num.Vector2(0, ImGui.GetFrameHeight()),
                new Num.Vector2(ImGui.GetStyle()->FramePadding.X, 2)))
        {
            layerDef.ExcludedTags.Add(_tempExcludedTag);
            _tempExcludedTag = "";
        }

        ImGui.EndDisabled();
        var exclTagToRemove = -1;
        for (var i = 0; i < layerDef.ExcludedTags.Count; i++)
        {
            if (ImGuiExt.ColoredButton(layerDef.ExcludedTags[i]))
                exclTagToRemove = i;
            if (i < layerDef.ExcludedTags.Count - 1)
                ImGui.SameLine();
        }

        if (exclTagToRemove != -1)
            layerDef.ExcludedTags.RemoveAt(exclTagToRemove);

        ImGuiExt.SeparatorText("Required Tags");

        ImGui.SetNextItemWidth(100);
        SimpleTypeInspector.InspectString("##TagRequired", ref _tempRequiredTag);
        ImGui.SameLine();
        ImGui.BeginDisabled(_tempRequiredTag == "");
        if (ImGuiExt.ColoredButton("+##AddRequired", Color.White, ImGuiExt.Colors[2], "Add Required Tag", new Num.Vector2(0, ImGui.GetFrameHeight()),
                new Num.Vector2(ImGui.GetStyle()->FramePadding.X, 2)))
        {
            layerDef.RequiredTags.Add(_tempRequiredTag);
            _tempRequiredTag = "";
        }

        ImGui.EndDisabled();
        var reqTagToRemove = -1;
        for (var i = 0; i < layerDef.RequiredTags.Count; i++)
        {
            if (ImGuiExt.ColoredButton(layerDef.RequiredTags[i]))
                reqTagToRemove = i;
            if (i < layerDef.RequiredTags.Count - 1)
                ImGui.SameLine();
        }

        if (reqTagToRemove != -1)
            layerDef.RequiredTags.RemoveAt(reqTagToRemove);
    }

    private void ResizeLayers(Level level, List<LayerDef> layerDefs)
    {
        for (var i = 0; i < level.LayerInstances.Count; i++)
        {
            var layer = level.LayerInstances[i];
            var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layer.LayerDefId);
            if (layerDef == null)
            {
                Logs.LogError($"Could not find a layer definition with id \"{layer.LayerDefId}\"");
                continue;
            }

            var cols = level.Width / layerDef.GridSize;
            var rows = level.Height / layerDef.GridSize;
            Array.Resize(ref layer.IntGrid, (int)(cols * rows));
        }
    }

    private static LayerInstance CreateLayerInstance(LayerDef layerDef, Level level)
    {
        var cols = level.Width / layerDef.GridSize;
        var rows = level.Height / layerDef.GridSize;
        return new LayerInstance
        {
            LayerDefId = layerDef.Uid,
            IntGrid = new int[cols * rows],
        };
    }

    private static void DrawFieldInstances(List<FieldInstance> fieldInstances, List<FieldDef> fieldDefs)
    {
        for (var i = 0; i < fieldDefs.Count; i++)
        {
            if (fieldInstances.Any(x => x.FieldDefId == fieldDefs[i].Uid))
                continue;

            fieldInstances.Add(CreateFieldInstance(fieldDefs[i]));
        }

        for (var i = 0; i < fieldInstances.Count; i++)
        {
            ImGui.PushID(i);
            var fieldInstance = fieldInstances[i];
            var fieldDef = fieldDefs.FirstOrDefault(x => x.Uid == fieldInstance.FieldDefId);
            if (fieldDef == null)
            {
                ImGui.Text($"Could not find a level field definition with uid \"{fieldInstance.FieldDefId}\"");
                continue;
            }

            if (fieldDef.IsArray)
            {
                if (ImGui.BeginTable("Value", 1, ImGuiTableFlags.None, new Num.Vector2(0, 100)))
                {
                    ImGui.TableSetupColumn("Value");

                    var list = (IList)fieldInstance.Value;
                    for (var j = 0; j < list.Count; j++)
                    {
                        ImGui.PushID(j);
                        var value = list[j];
                        if (value == null)
                        {
                            value = GetDefaultValue(fieldDef.FieldType);
                        }

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        InspectField(fieldDef, value);
                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }
            }
            else
            {
                InspectField(fieldDef, fieldInstance.Value);
            }

            ImGui.PopID();
        }
    }

    private static FieldInstance CreateFieldInstance(FieldDef fieldDef)
    {
        return new FieldInstance
        {
            Value = GetDefaultValue(fieldDef.FieldType),
            FieldDefId = fieldDef.Uid
        };
    }

    private static object GetDefaultValue(FieldType type)
    {
        return type switch
        {
            FieldType.Int => default(int),
            FieldType.Float => default(float),
            FieldType.String => "",
            FieldType.Bool => false,
            FieldType.Color => Color.White,
            FieldType.Point => Point.Zero,
            FieldType.Vector2 => Vector2.Zero,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static void InspectField(FieldDef fieldDef, object value)
    {
        switch (fieldDef.FieldType)
        {
            case FieldType.Int:
                var intValue = (int)value;
                SimpleTypeInspector.InspectInt(fieldDef.Identifier, ref intValue, SimpleTypeInspector.DefaultRangeSettings);
                break;
            case FieldType.Float:
                var floatValue = (float)value;
                SimpleTypeInspector.InspectFloat(fieldDef.Identifier, ref floatValue, SimpleTypeInspector.DefaultRangeSettings);
                break;
            case FieldType.String:
                var strValue = (string)value;
                SimpleTypeInspector.InspectString(fieldDef.Identifier, ref strValue);
                break;
            case FieldType.Bool:
                var boolValue = (bool)value;
                SimpleTypeInspector.InspectBool(fieldDef.Identifier, ref boolValue);
                break;
            case FieldType.Color:
                var colorValue = (Color)value;
                SimpleTypeInspector.InspectColor(fieldDef.Identifier, ref colorValue);
                break;
            case FieldType.Point:
                var pointValue = (Point)value;
                SimpleTypeInspector.InspectPoint(fieldDef.Identifier, ref pointValue);
                break;
            case FieldType.Vector2:
                var vector2Value = (Vector2)value;
                SimpleTypeInspector.InspectVector2(fieldDef.Identifier, ref vector2Value);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static void SetDockingFlags()
    {
        var dockNode = ImGuiInternal.GetWindowDockNode();
        if (dockNode != null)
        {
            dockNode->LocalFlags = 0;
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe);
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
        }
    }

    private void DrawLeftTables()
    {
        if (ImGui.Begin(TablesLeftWindowTitle, default, ImGuiWindowFlags.None))
        {
            DrawWorlds();
            if (ImGuiExt.ColoredButton("+ Add World", new Num.Vector2(-1, 0)))
            {
                WorldsRoot.Worlds.Add(new WorldsRoot.NewWorld());
            }

            ImGui.Separator();

            DrawLevels();
            if (WorldsRoot.Worlds.Count > 0)
            {
                if (ImGuiExt.ColoredButton("+ Add Level", new Num.Vector2(-1, 0)))
                {
                    WorldsRoot.Worlds[_selectedWorldIndex].Levels.Add(new WorldsRoot.Level());
                }
            }

            ImGui.Separator();

            DrawLayerDefinitions();
            if (ImGuiExt.ColoredButton("+ Add Layer Definition", new Num.Vector2(-1, 0)))
            {
                WorldsRoot.LayerDefinitions.Add(new WorldsRoot.LayerDef());
            }

            ImGui.Separator();

            DrawTileSetDefinitions();
            if (ImGuiExt.ColoredButton("+ Add TileSet Definition", new Num.Vector2(-1, 0)))
            {
                WorldsRoot.TileSetDefinitions.Add(new WorldsRoot.TileSetDef());
            }
        }

        SetDockingFlags();

        ImGui.End();
    }

    private void DrawEditorWindow()
    {
        if (ImGui.Begin(EditorViewWindowTitle))
        {
            GameWindow.EnsureTextureIsBound(ref _editorRenderTextureId, _editor._editorRenderTarget, _editor.ImGuiRenderer);
            var cursorScreenPosition = ImGui.GetCursorScreenPos();

            var editorMin = cursorScreenPosition;
            var editorMax = editorMin + new Num.Vector2(_editor._editorRenderTarget.Width, _editor._editorRenderTarget.Height);
            var dl = ImGui.GetWindowDrawList();
            dl->AddImage(
                (void*)_editorRenderTextureId.Value,
                editorMin,
                editorMax,
                Num.Vector2.Zero,
                Num.Vector2.One,
                Color.White.PackedValue
            );

            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _camera.Position += -ImGui.GetIO()->MouseDelta.ToXNA() * 1.0f / _camera.Zoom;
            }

            if (ImGui.GetIO()->MouseWheel != 0)
            {
                _camera.Zoom += 0.1f * ImGui.GetIO()->MouseWheel * _camera.Zoom;
            }
        }

        ImGui.End();
    }

    private void DrawTileSetDefinitions()
    {
        if (ImGui.BeginTable("TileSetDefinitions", 1, _tableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var tileSetToDelete = -1;
            for (var i = 0; i < WorldsRoot.TileSetDefinitions.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var tilesetDef = WorldsRoot.TileSetDefinitions[i];

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
                WorldsRoot.TileSetDefinitions.RemoveAt(tileSetToDelete);
            }

            ImGui.EndTable();
        }
    }

    private void DrawWorlds()
    {
        if (ImGui.BeginTable("Worlds", 1, _tableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var worldToDelete = -1;
            for (var i = 0; i < WorldsRoot.Worlds.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var world = WorldsRoot.Worlds[i];


                var color = ImGuiExt.Colors[1];
                var isSelected = _selectedWorldIndex == i;
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedWorldIndex = i;
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
                WorldsRoot.Worlds.RemoveAt(worldToDelete);
            }

            ImGui.EndTable();
        }
    }

    private void CenteredButton(Color color, int rowHeight)
    {
        var buttonSize = 0.6f * rowHeight;
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
            ImGui.SetCursorPosY(cursorPosY + (rowHeight - buttonSize) / 2);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGuiExt.ColoredButton("##Test", color, new Num.Vector2(buttonSize, buttonSize));
            ImGui.PopStyleVar();
            ImGui.SameLine(0, ImGui.GetStyle()->ItemInnerSpacing.X);
        }

        ImGui.PopStyleVar();
    }

    private static void GiantLabel(string label, Color color, int rowHeight)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Num.Vector2(ImGui.GetStyle()->ItemInnerSpacing.X * 4f, ImGui.GetStyle()->ItemInnerSpacing.Y));

        var cursorPosY = ImGui.GetCursorPosY();
        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        ImGui.PushTextWrapPos();
        var textSize = ImGui.CalcTextSize(label);
        ImGui.SetCursorPosY(cursorPosY + (rowHeight - (textSize.Y + ImGui.GetStyle()->ItemSpacing.Y)) / 2);
        ImGui.TextColored(color.ToNumerics(), label);
        ImGui.PopFont();

        ImGui.PopStyleVar();
    }

    private void DrawLevels()
    {
        var world = WorldsRoot.Worlds[_selectedWorldIndex];

        if (ImGui.BeginTable("Levels", 1, _tableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var levelToDelete = -1;
            for (var i = 0; i < world.Levels.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var level = world.Levels[i];

                var isSelected = _selectedLevelIndex == i;
                var color = ImGuiExt.Colors[1];
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedLevelIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        levelToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                var labelColor = isSelected ? Color.White : color;
                CenteredButton(color, _rowMinHeight);
                GiantLabel(level.Identifier, labelColor, _rowMinHeight);

                ImGui.PopID();
            }

            if (levelToDelete != -1)
            {
                world.Levels.RemoveAt(levelToDelete);
            }

            ImGui.EndTable();
        }
    }

    private void DrawLayerInstances(List<LayerInstance> layerInstances, List<LayerDef> layerDefs)
    {
        var rowHeight = 40;
        if (ImGui.BeginTable("LayerInstances", 1, _tableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var layerToDelete = -1;
            for (var i = 0; i < layerInstances.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);

                var layerInstance = layerInstances[i];
                var layerDef = layerDefs.FirstOrDefault(x => x.Uid == layerInstance.LayerDefId);

                var isSelected = _selectedLayerInstanceIndex == i;

                var typeColor = layerDef?.LayerType switch
                {
                    LayerType.Tiles => ImGuiExt.Colors[0],
                    LayerType.IntGrid => ImGuiExt.Colors[1],
                    LayerType.Entities => ImGuiExt.Colors[2],
                    LayerType.AutoLayer => ImGuiExt.Colors[3],
                    _ => ImGuiExt.Colors[2]
                };

                if (GiantButton("##Selectable", isSelected, typeColor.MultiplyAlpha(0.66f), rowHeight))
                {
                    _selectedLayerInstanceIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        layerToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                string label;
                Color color;
                if (layerDef != null)
                {
                    label = layerDef.Identifier;
                    color = Color.White.MultiplyAlpha(0.9f);
                }
                else
                {
                    label = $"Couldn't find a LayerDefinition with Uid \"{layerInstance.LayerDefId}\"";
                    color = isSelected ? Color.White : Color.Red.MultiplyAlpha(0.9f);
                }

                var labelColor = isSelected ? Color.White : color;
                CenteredButton(color, rowHeight);
                GiantLabel(label, labelColor, rowHeight);

                ImGui.PopID();
            }

            if (layerToDelete != -1)
            {
                layerInstances.RemoveAt(layerToDelete);
            }

            ImGui.EndTable();
        }
    }

    private void DrawLayerDefinitions()
    {
        if (ImGui.BeginTable("LayerDefinitions", 1, _tableFlags, new Num.Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Name");

            var layerToDelete = -1;
            for (var i = 0; i < WorldsRoot.LayerDefinitions.Count; i++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, _rowMinHeight);
                ImGui.TableNextColumn();

                ImGui.PushID(i);
                var layerDef = WorldsRoot.LayerDefinitions[i];

                var isSelected = _selectedLayerDefIndex == i;
                var color = ImGuiExt.Colors[4];
                if (GiantButton("##Selectable", isSelected, color, _rowMinHeight))
                {
                    _selectedLayerDefIndex = i;
                }

                if (ImGui.BeginPopupContextItem("Popup")) //ImGui.OpenPopupOnItemClick("Popup"))
                {
                    ImGui.MenuItem("Copy", default);
                    ImGui.MenuItem("Cut", default);
                    ImGui.MenuItem("Duplicate", default);
                    if (ImGui.MenuItem("Delete", default))
                    {
                        layerToDelete = i;
                    }

                    ImGui.EndPopup();
                }

                var labelColor = isSelected ? Color.White : color;
                CenteredButton(color, _rowMinHeight);
                GiantLabel(layerDef.Identifier, labelColor, _rowMinHeight);

                ImGui.PopID();
            }

            if (layerToDelete != -1)
            {
                WorldsRoot.LayerDefinitions.RemoveAt(layerToDelete);
            }

            ImGui.EndTable();
        }
    }

    public void Draw(Renderer renderer, Texture renderDestination, double alpha)
    {
        var commandBuffer = _editor.GraphicsDevice.AcquireCommandBuffer();
        var viewProjection = _camera.GetViewProjection(renderDestination.Width, renderDestination.Height);

        foreach (var world in WorldsRoot.Worlds)
        {
            foreach (var level in world.Levels)
            {
                var transform = Matrix3x2.CreateScale(level.Width, level.Height) *
                                Matrix3x2.CreateTranslation(level.WorldPos.X, level.WorldPos.Y);
                renderer.DrawSprite(renderer.BlankSprite, transform.ToMatrix4x4(), level.BackgroundColor);
            }
        }

        renderer.RunRenderPass(ref commandBuffer, renderDestination, Color.Black, viewProjection);

        _editor.GraphicsDevice.Submit(commandBuffer);
    }

    private static void SetupDockSpace(string dockspaceId, string leftWindowTitle, string rightWindowTitle, bool keepAlive = false)
    {
        var dockspaceID = ImGui.GetID(dockspaceId);

        ImGuiWindowClass workspaceWindowClass;
        workspaceWindowClass.ClassId = dockspaceID;
        workspaceWindowClass.DockingAllowUnclassed = false;

        if (ImGuiInternal.DockBuilderGetNode(dockspaceID) == null)
        {
            var dockFlags = ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton;
            ImGuiInternal.DockBuilderAddNode(dockspaceID, (ImGuiDockNodeFlags)dockFlags);
            var contentAvail = ImGui.GetContentRegionAvail();
            var size = new Num.Vector2(MathF.Max(4.0f, contentAvail.X), MathF.Max(4.0f, contentAvail.Y));
            ImGuiInternal.DockBuilderSetNodeSize(dockspaceID, size);
            //
            var rightDockID = 0u;
            var leftDockID = 0u;
            var parentID = ImGuiInternal.DockBuilderSplitNode(dockspaceID, ImGuiDir.Left, 0.5f, &leftDockID, &rightDockID);

            /*var parentNode = ImGuiInternal.DockBuilderGetNode(parentID);
            parentNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe |
                                                           ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);*/

            // Dock viewport
            /*var pLeftNode = ImGuiInternal.DockBuilderGetNode(leftDockID);
            pLeftNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe |
                                                          ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            var pRightNode = ImGuiInternal.DockBuilderGetNode(rightDockID);
            pRightNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe |
                                                           ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);*/
            
            ImGuiInternal.DockBuilderDockWindow(leftWindowTitle, leftDockID);
            ImGuiInternal.DockBuilderDockWindow(rightWindowTitle, rightDockID);
            //
            ImGuiInternal.DockBuilderFinish(dockspaceID);
        }

        var flags = keepAlive ? ImGuiDockNodeFlags.KeepAliveOnly : ImGuiDockNodeFlags.None | ImGuiDockNodeFlags.NoSplit;
        ImGui.DockSpace(dockspaceID, new Num.Vector2(0.0f, 0.0f), flags, &workspaceWindowClass);
    }
}
