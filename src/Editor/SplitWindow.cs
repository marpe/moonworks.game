using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.WorldsRoot;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace MyGame.Editor;

public abstract unsafe class SplitWindow : ImGuiEditorWindow
{
    private string _leftTitle;
    private string _rightTitle;
    private string _dockSpaceId;
    protected MyEditorMain Editor;
    private Action _drawLeft;
    private Action _drawRight;

    public static ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                                               ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable |
                                               ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;

    private readonly Action<uint> _initializeDockSpace;

    protected WorldsRoot.RootJson RootJson => Editor.RootJson;
    private static Dictionary<string, string> _cachedPaths = new();
    private uint _rightDockID;
    private uint _leftDockID;

    private bool _shouldForceDock = false;

    protected virtual void PushStyles()
    {
        var origFramePadding = ImGui.GetStyle()->FramePadding;
        var origItemSpacing = ImGui.GetStyle()->ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, origFramePadding * 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, origItemSpacing * 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushItemWidth(ImGui.GetWindowWidth());
    }

    protected virtual void PopStyles()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopItemWidth();
    }

    protected SplitWindow(string title, MyEditorMain editor) : base(title)
    {
        Editor = editor;
        _leftTitle = $"{title}_Left";
        _rightTitle = $"{title}_Right";
        _dockSpaceId = $"{title}_DockSpace";

        _drawLeft = () =>
        {
            PushStyles();
            DrawLeft();
            PopStyles();
        };
        _drawRight = () =>
        {
            PushStyles();
            DrawRight();
            PopStyles();
        };
        _initializeDockSpace = InitializeDockSpace;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var windowClass = new ImGuiWindowClass();

        _shouldForceDock |= _leftDockID == 0 || _rightDockID == 0;
        var result = ImGuiExt.BeginWorkspaceWindow(Title, _dockSpaceId, _initializeDockSpace, null, ref windowClass, ImGuiDockNodeFlags.None, _shouldForceDock);
        _shouldForceDock = false;

        if (result)
        {
            DrawSplit(_leftTitle, _leftDockID, _drawLeft, windowClass, ref _shouldForceDock);
            DrawSplit(_rightTitle, _rightDockID, _drawRight, windowClass, ref _shouldForceDock);
            if (_shouldForceDock)
                EditorWindow.ResetDock = true;
        }
    }

    private void InitializeDockSpace(uint dockSpaceID)
    {
        _rightDockID = 0u;
        _leftDockID = 0u;
        ImGuiInternal.DockBuilderSplitNode(dockSpaceID, ImGuiDir.Left, 0.5f, ImGuiExt.RefPtr(ref _leftDockID), ImGuiExt.RefPtr(ref _rightDockID));

        ImGuiInternal.DockBuilderDockWindow(_leftTitle, _leftDockID);
        ImGuiInternal.DockBuilderDockWindow(_rightTitle, _rightDockID);
    }

    private static void DrawSplit(string title, uint dockId, Action drawContent, ImGuiWindowClass windowClass, ref bool shouldDock)
    {
        ImGui.SetNextWindowClass(&windowClass);
        var windowFlags = ImGuiWindowFlags.NoCollapse |
                          ImGuiWindowFlags.NoTitleBar |
                          ImGuiWindowFlags.NoDecoration |
                          ImGuiWindowFlags.NoSavedSettings;
        ImGui.SetNextWindowSize(new Vector2(400, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowDockID(dockId, ImGuiCond.Always);
        if (ImGui.Begin(title, default, windowFlags))
        {
            drawContent();

            if (ImGui.BeginPopupContextWindow("WindowContextMenu"))
            {
                if (ImGui.MenuItem("Dock", default))
                {
                    shouldDock = true;
                }

                ImGui.EndPopup();
            }
        }

        SetDockSpaceFlags();
        ImGui.End();
    }

    private static void SetDockSpaceFlags()
    {
        var dockNode = ImGuiInternal.GetWindowDockNode();
        if (dockNode != null)
        {
            dockNode->LocalFlags = 0;
            // dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingSplitMe);
            // dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
        }
    }

    protected abstract void DrawLeft();
    protected abstract void DrawRight();

    public static Texture GetTileSetTexture(string tileSetPath)
    {
        if (!_cachedPaths.TryGetValue(tileSetPath, out var path))
        {
            var worldFileDir = Path.GetDirectoryName(ContentPaths.worlds.worlds_json);
            path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, Path.Join(worldFileDir, tileSetPath));
            _cachedPaths.Add(tileSetPath, path);
        }

        var editor = (MyEditorMain)Shared.Game;
        var texture = Shared.Content.Load<Texture>(path);
        editor.ImGuiRenderer.BindTexture(texture);
        return texture;
    }

    public static bool GiantButton(string label, bool isSelected, Color color, int rowMinHeight)
    {
        var (h, s, v) = ColorExt.RgbToHsv(color);
        var headerColor = ColorExt.HsvToRgb(h, s * 0.9f, v * 0.6f).MultiplyAlpha(isSelected ? 0.5f : 0);
        var headerActiveColor = ColorExt.HsvToRgb(h, s, v).MultiplyAlpha(0.4f);
        var headerHoverColor = ColorExt.HsvToRgb(h, s, v).MultiplyAlpha(0.4f);
        var borderColor = ColorExt.HsvToRgb(h, s, v);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle()->ItemSpacing.X, ImGui.GetStyle()->CellPadding.Y * 2));
        ImGui.PushStyleColor(ImGuiCol.Header, headerColor.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerActiveColor.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, headerHoverColor.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.Border, borderColor.PackedValue);
        var selectableFlags = ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap;
        var result = ImGui.Selectable(label, isSelected, selectableFlags, new Vector2(0, rowMinHeight));
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar();
        return result;
    }

    public static void Icon(string iconPath, Color color, int rowHeight)
    {
        var buttonSize = 0.6f * rowHeight;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,
            new Vector2(ImGui.GetStyle()->ItemInnerSpacing.X * 4f, ImGui.GetStyle()->ItemInnerSpacing.Y));
        var cursorPosX = ImGui.GetCursorPosX();
        var cursorPosY = ImGui.GetCursorPosY();
        var contentAvail = ImGui.GetContentRegionAvail();
        var buttonX = Math.Max(contentAvail.X * 0.5f - buttonSize - ImGui.GetStyle()->ItemInnerSpacing.X, cursorPosX);
        /*if (buttonX - 20 >= cursorPosX)
        {*/
        ImGui.SetCursorPosX(buttonX);
        ImGui.SetCursorPosY(cursorPosY + (rowHeight - buttonSize) / 2);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.Image(
            (void*)Shared.Content.Load<Texture>(iconPath).Handle,
            new Vector2(buttonSize, buttonSize),
            Vector2.Zero, Vector2.One,
            Color.White.ToNumerics(),
            color.ToNumerics()
        );
        ImGui.PopStyleVar();
        // }

        ImGui.PopStyleVar();
    }

    public static void CenteredButton(Color color, int rowHeight)
    {
        var buttonSize = 0.6f * rowHeight;
        ImGui.SameLine();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,
            new Vector2(ImGui.GetStyle()->ItemInnerSpacing.X * 4f, ImGui.GetStyle()->ItemInnerSpacing.Y));
        var cursorPosX = ImGui.GetCursorPosX();
        var cursorPosY = ImGui.GetCursorPosY();
        var contentAvail = ImGui.GetContentRegionAvail();
        var buttonX = contentAvail.X * 0.5f - buttonSize - ImGui.GetStyle()->ItemInnerSpacing.X;
        if (buttonX >= cursorPosX)
        {
            ImGui.SetCursorPosX(buttonX);
            ImGui.SetCursorPosY(cursorPosY + (rowHeight - buttonSize) / 2);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGuiExt.ColoredButton("##Test", color, new Vector2(buttonSize, buttonSize));
            ImGui.PopStyleVar();
            ImGui.SameLine(0, ImGui.GetStyle()->ItemInnerSpacing.X);
        }

        ImGui.PopStyleVar();
    }

    public static void GiantLabel(string label, Color color, int rowHeight)
    {
        /*if (!ImGui.IsItemVisible())
            return;*/

        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(ImGui.GetStyle()->ItemInnerSpacing.X * 4f, ImGui.GetStyle()->ItemInnerSpacing.Y));

        var lineHeight = Math.Max(rowHeight, (int)ImGui.GetTextLineHeight());

        var cursorStart = ImGui.GetCursorScreenPos();
        var cursorPosY = ImGui.GetCursorPosY();
        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        ImGui.PushTextWrapPos();
        var textSize = ImGui.CalcTextSize(label);
        ImGui.SetCursorPosY(cursorPosY + lineHeight * 0.5f - textSize.Y * 0.5f);

        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        ImGui.Dummy(new Vector2(avail.X, 0));
        ImGui.SameLine();

        var clipRectMin = new Vector2(cursorScreenPos.X, cursorStart.Y - ImGui.GetStyle()->CellPadding.Y);
        var clipRectMax = clipRectMin + new Vector2(avail.X, lineHeight + ImGui.GetStyle()->CellPadding.Y * 2f);

        var dl = ImGui.GetWindowDrawList();
        var clipRect = new Vector4(clipRectMin.X, clipRectMin.Y, clipRectMax.X, clipRectMax.Y);
        dl->AddText(ImGui.GetFont(), ImGui.GetFontSize(), cursorScreenPos, color.PackedValue, label, avail.X, ImGuiExt.RefPtr(ref clipRect));
        // ImGui.TextColored(color.ToNumerics(), label);

        ImGui.PopFont();
        ImGui.PopTextWrapPos();

        ImGui.PopStyleVar();
    }

    public static int ButtonGroup(string firstLabel, string secondLabel, int minWidth)
    {
        var result = -1;
        var contentAvail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.MediumBold));
        var canFitOnOneLine = contentAvail.X >= minWidth;
        var buttonWidth = canFitOnOneLine ? contentAvail.X * 0.5f : -ImGuiExt.FLT_MIN;
        var buttonSize = new Vector2(buttonWidth, 40);
        var buttonGap = canFitOnOneLine ? new Vector2(2, 0) : Vector2.Zero;
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

    public static string LayerTypeIcon(LayerType layerType)
    {
        return layerType switch
        {
            LayerType.IntGrid => ContentPaths.icons.intGrid_png,
            LayerType.Entities => ContentPaths.icons.entity_png,
            LayerType.Tiles => ContentPaths.icons.tile_png,
            LayerType.AutoLayer => ContentPaths.icons.autoLayer_png,
            _ => throw new ArgumentOutOfRangeException(nameof(layerType), layerType, null)
        };
    }
}
