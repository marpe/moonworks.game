using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class GameRenderView
{
    [CVar("imgui.mouse_pan_and_zoom", "Toggle mouse pan & zoom control")]
    public static bool IsMousePanAndZoomEnabled = true;

    public bool IsPanZoomDirty => MathF.NotApprox(Zoom, 1.0f) || Offset != Vector2.Zero;

    public Matrix3x2 GameRenderViewportTransform;

    public Vector2 GameRenderMin;
    public Vector2 GameRenderMax;

    /// <summary>
    /// User zoom
    /// </summary>
    public float Zoom { get; set; } = 1f;

    private static int[] _gameRenderScales = new[] { 1, 2, 4, 8, 16, 32 };

    /// <summary>
    /// User panning offset
    /// </summary>
    public Vector2 Offset { get; set; } = Vector2.Zero;

    public Action? DrawToolbarCallback;

    private bool _usePointFiltering = true;
    private Vector2 _contentSize;
    private Vector2 _viewportSize;
    private Vector2 _textureSize;

    public void Draw(string id, Texture texture)
    {
        var imGuiCursor = ImGui.GetCursorScreenPos();

        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        _contentSize = contentMax - contentMin;

        DrawToolbar();

        ImGui.SetCursorScreenPos(imGuiCursor);

        _textureSize = texture.SizeVec().ToNumerics();
        ImGuiUtils.DrawGame(texture, _contentSize, Zoom, Offset, _usePointFiltering, out var min, out var max, out var viewportSize,
            out var viewportInvTransform);

        _viewportSize = viewportSize;

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(
            id,
            (max - min).EnsureNotZero(),
            ImGuiButtonFlags.MouseButtonMiddle
        );

        var isActive = ImGui.IsItemActive();
        var isHovered = ImGui.IsItemHovered();

        var borderColor = isHovered ? Color.LightGreen : Color.Green;
        DrawBorder(max, min, borderColor);

        GameRenderMin = min;
        GameRenderMax = max;
        GameRenderViewportTransform = viewportInvTransform;

        var label = $"{texture.Width}x{texture.Height}";
        var labelSize = ImGui.CalcTextSize(label);
        var x = min.X + ((max.X - min.X) - labelSize.X) * 0.5f;
        var y = min.Y - labelSize.Y - 10;
        var dl = ImGui.GetWindowDrawList();
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), ImGui.GetFontSize(), new Vector2(x, y), Color.White.PackedValue, label, 0, default);

        HandleInput(isActive, isHovered);
    }

    private static void DrawBorder(Vector2 max, Vector2 min, Color color, float thickness = 2)
    {
        var dl = ImGui.GetWindowDrawList();
        var borderSize = max - min;
        var thicknessOverTwo = (int)(thickness * 0.5f);
        var contentRect = ImGui.GetCurrentContext()->CurrentWindow->ContentRegionRect;
        var contentRectMin = contentRect.Min + new Vector2(1, 0) + new Vector2(thicknessOverTwo);
        var contentRectMax = contentRect.Max + new Vector2(-1, -1) - new Vector2(thicknessOverTwo);
        var contentRectSize = contentRectMax - contentRectMin;
        var r1 = new Rectangle((int)contentRectMin.X, (int)contentRectMin.Y, (int)contentRectSize.X, (int)contentRectSize.Y);
        var r2 = new Rectangle((int)(min.X), (int)min.Y, (int)borderSize.X, (int)borderSize.Y);
        r2.Inflate(thicknessOverTwo, thicknessOverTwo);
        var rect = Rectangle.Intersect(r1, r2);
        var borderMin = rect.MinVec().ToNumerics();
        var borderMax = rect.MaxVec().ToNumerics();
        if (thickness % 2 == 0)
        {
            borderMin -= new Vector2(0.5f);
            borderMax += new Vector2(0.5f);
        }

        dl->AddRect(borderMin, borderMax, color.PackedValue, 0, ImDrawFlags.None, thickness);
    }

    private void HandleInput(bool isActive, bool isHovered)
    {
        if (IsMousePanAndZoomEnabled)
        {
            // panning
            if (isActive && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                Offset += ImGui.GetIO()->MouseDelta * 1.0f / Zoom;
            }

            // zooming
            if (isHovered && ImGui.GetIO()->MouseWheel != 0)
            {
                Zoom += ImGui.GetIO()->MouseWheel * 0.1f * Zoom;
                if (Zoom < 1.0f)
                    Zoom = 1.0f;
            }
        }

        if (isHovered)
        {
            MyEditorMain.ActiveInput = ActiveInput.GameWindow;
        }

        // exit relative mode on escape 
        if (Shared.Game.Inputs.Mouse.RelativeMode && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Shared.Game.Inputs.Mouse.RelativeMode = false;
        }

        // imgui sets WantCaptureKeyboard when an item is active which we don't want for the game window
        if (ImGui.IsWindowHovered() &&
            (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Right)))
        {
            ImGui.SetWindowFocus();
            ImGui.SetNextFrameWantCaptureKeyboard(false);
        }
    }

    public void DrawToolbar()
    {
        var toolbarHeight = ImGui.GetStyle()->WindowPadding.Y * 2 + ImGui.GetFrameHeightWithSpacing();
        if (ImGui.BeginChild("Toolbar", new Vector2(ImGui.GetContentRegionAvail().X, toolbarHeight), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlass, ImGuiExt.Colors[0], "Fit"))
            {
                ResetPanAndZoom();
            }

            ImGui.SameLine();

            //////////////////

            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlassPlus, ImGuiExt.Colors[0], "Fill height"))
            {
                Zoom = _contentSize.Y / _viewportSize.Y;
            }

            ImGui.SameLine();

            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlassMinus, ImGuiExt.Colors[0], "Zoom to original size"))
            {
                var scale = MathF.Min(
                    _textureSize.X / _viewportSize.X,
                    _textureSize.Y / _viewportSize.Y
                );
                Zoom = scale;
            }


            ImGui.SameLine();

            //////////////////

            // ImGui.BeginChild("ZoomChild", new Vector2(60, 30));
            var tmpZoom = Zoom * 100 + 0.01f;
            ImGui.SetNextItemWidth(40);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle()->FramePadding.X, ImGui.GetStyle()->FramePadding.Y + 2));
            if (SimpleTypeInspector.InspectFloat("##Zoom", ref tmpZoom, new RangeSettings(50, 1000, 1, true), "%.0f%%", ImGuiSliderFlags.AlwaysClamp))
            {
                Zoom = MathF.Exp(MathF.Lerp(MathF.Log(Zoom), MathF.Log(tmpZoom / 100f), 0.1f));
            }

            ImGui.PopStyleVar();
            // ImGui.EndChild();

            ImGui.SameLine();

            //////////////////

            ImGui.SetNextItemWidth(50);
            DrawScaleCombo();

            ImGui.SameLine();

            //////////////////

            var (icon, color, tooltip) = IsMousePanAndZoomEnabled switch
            {
                true => (FontAwesome6.ArrowPointer, Color.Green, "Disable mouse pan & zoom"),
                _ => (FontAwesome6.Lock, ImGuiExt.Colors[2], "Enable mouse pan & zoom")
            };
            if (ImGuiExt.ColoredButton(icon, color, tooltip))
            {
                IsMousePanAndZoomEnabled = !IsMousePanAndZoomEnabled;
            }

            ImGui.SameLine();

            //////////////////

            SimpleTypeInspector.InspectBool("Point Filtering", ref _usePointFiltering, -1);

            ImGui.SameLine();

            //////////////////

            DrawToolbarCallback?.Invoke();
        }

        ImGui.EndChild();
    }

    private void DrawScaleCombo()
    {
        var currentIndex = Array.IndexOf(_gameRenderScales, (int)Zoom);
        if (currentIndex == -1)
            currentIndex = 3;
        var label = _gameRenderScales[currentIndex].ToString();
        if (ImGui.BeginCombo("##Size", label))
        {
            for (var i = 0; i < _gameRenderScales.Length; i++)
            {
                var isSelected = i == currentIndex;
                if (ImGui.Selectable(_gameRenderScales[i].ToString(), isSelected, ImGuiSelectableFlags.None, default))
                {
                    currentIndex = i;
                    Zoom = _gameRenderScales[currentIndex];
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    public void ResetPanAndZoom()
    {
        Zoom = 1.0f;
        Offset = Vector2.Zero;
    }
}
