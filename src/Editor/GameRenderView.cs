using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class GameRenderView
{
    [CVar("imgui.mouse_pan_and_zoom", "Toggle mouse pan & zoom control")]
    public static bool IsMousePanAndZoomEnabled = true;

    public bool IsPanZoomDirty => MathF.NotApprox(Zoom, 1.0f) || Offset != Vector2.Zero;

    public Matrix4x4 GameRenderViewportTransform;

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
    
    public void Draw(Texture texture)
    {
        var imGuiCursor = ImGui.GetCursorScreenPos();

        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        var contentSize = contentMax - contentMin;

        ImGuiUtils.DrawGame(texture, contentSize, Zoom, Offset, out var min, out var max, out var viewportSize, out var viewportInvTransform);

        var isActive = ImGui.IsItemActive();
        var isHovered = ImGui.IsItemHovered();

        GameRenderMin = min;
        GameRenderMax = max;
        GameRenderViewportTransform = viewportInvTransform;

        HandleInput(isActive, isHovered);
        
        ImGui.SetCursorScreenPos(imGuiCursor);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        DrawToolbar(contentSize, viewportSize);
        ImGui.PopStyleVar();
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
            // ImGui.SetWindowFocus(GameWindowTitle);
            ImGui.SetNextFrameWantCaptureKeyboard(false);
        }
    }

    private void DrawToolbar(Vector2 renderSize, Vector2 viewportSize)
    {
        var toolbarHeight = ImGui.GetStyle()->WindowPadding.Y * 2 + ImGui.GetFrameHeightWithSpacing();
        if (ImGui.BeginChild("Toolbar", new Vector2(ImGui.GetContentRegionAvail().X, toolbarHeight), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlass, ImGuiExt.Colors[0], "Reset pan & zoom"))
            {
                ResetPanAndZoom();
            }

            ImGui.SameLine();

            //////////////////

            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlassPlus, ImGuiExt.Colors[0], "Fill height"))
            {
                Zoom = renderSize.Y / viewportSize.Y;
            }

            ImGui.SameLine();

            //////////////////

            ImGui.BeginChild("ZoomChild", new Vector2(60, 30));
            var tmpZoom = Zoom * 100 + 0.01f;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle()->FramePadding.X, ImGui.GetStyle()->FramePadding.Y + 2));
            if (SimpleTypeInspector.InspectFloat("##Zoom", ref tmpZoom, new RangeSettings(50, 1000, 1, true), "%.0f%%", ImGuiSliderFlags.AlwaysClamp))
            {
                Zoom = MathF.Exp(MathF.Lerp(MathF.Log(Zoom), MathF.Log(tmpZoom / 100f), 0.1f));
            }

            ImGui.PopStyleVar();
            ImGui.EndChild();

            ImGui.SameLine();

            //////////////////

            ImGui.SetNextItemWidth(100);
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
