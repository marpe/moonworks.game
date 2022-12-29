using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Debug;
using MyGame.Entities;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class GameWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Game";
    public const string GameWindowTitle = "GameView";
    private const string ToolbarWindowName = "GameToolbar";

    private IntPtr? _gameRenderTextureId;
    public Matrix4x4 GameRenderViewportTransform;

    /// <summary>
    /// User zoom
    /// </summary>
    private float _gameRenderScale = 1f;

    /// <summary>
    /// User panning offset
    /// </summary>
    private Vector2 _gameRenderPosition = Vector2.Zero;

    private MyEditorMain _editor;

    private static bool _showDebug;

    [CVar("imgui.mouse_pan_and_zoom", "Toggle mouse pan & zoom control")]
    public static bool IsMousePanAndZoomEnabled = true;

    /// <summary>
    /// Used to set zoom to fill window height/width
    /// </summary>
    private Vector2 _gameViewWindowSize;

    public bool IsPanZoomDirty => MathF.NotApprox(_gameRenderScale, 1.0f) || _gameRenderPosition != Vector2.Zero;

    public GameWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F1";
        IsOpen = true;
    }

    public static void EnsureTextureIsBound([NotNull] ref IntPtr? ptr, Texture texture, ImGuiRenderer renderer)
    {
        if (texture.IsDisposed)
            throw new Exception("Attempted to bind a disposed texture");

        if (ptr != null && ptr != texture.Handle)
        {
            renderer.UnbindTexture(ptr.Value);
            ptr = null;
        }

        ptr ??= renderer.BindTexture(texture);
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        var windowClass = new ImGuiWindowClass();
        var result = ImGuiExt.BeginWorkspaceWindow(WindowTitle, "GameViewDockspace", InitializeDockSpace, null, ref windowClass);
        ImGui.PopStyleVar();

        if (result)
        {
            DrawGameView(windowClass);
            DrawToolbar(windowClass);
        }
    }

    private static void InitializeDockSpace(uint dockspaceID)
    {
        uint upNode, downNode;
        ImGuiInternal.DockBuilderSplitNode(dockspaceID, ImGuiDir.Up, 0.05f, &upNode, &downNode);
        ImGuiInternal.DockBuilderDockWindow(ToolbarWindowName, upNode);
        ImGuiInternal.DockBuilderDockWindow(GameWindowTitle, downNode);
    }

    private void DrawGameView(ImGuiWindowClass imGuiWindowClass)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        /*ImGuiWindowClass windowClass;
        windowClass.ViewportFlagsOverrideSet = ImGuiViewportFlags.NoAutoMerge;
        ImGui.SetNextWindowClass(&windowClass);*/
        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;
        ImGui.SetNextWindowClass(&imGuiWindowClass);
        if (ImGui.Begin(GameWindowTitle, default, flags))
        {
            ImGui.PopStyleVar();

            var contentMin = ImGui.GetWindowContentRegionMin();
            var contentMax = ImGui.GetWindowContentRegionMax();
            _gameViewWindowSize = contentMax - contentMin;

            EnsureTextureIsBound(ref _gameRenderTextureId, _editor.RenderTargets.CompositeRender.Target, _editor.ImGuiRenderer);

            var windowSize = ImGui.GetWindowSize();
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(windowSize.ToXNA().ToPoint(), _editor.RenderTargets.CompositeRender.Size);

            var viewportPosition = new Vector2(viewport.X, viewport.Y);
            var cursorScreenPosition = ImGui.GetCursorScreenPos();

            var viewportSize = viewport.Size.ToNumerics();
            var viewportHalfSize = viewportSize * 0.5f;

            var gameRenderMin = cursorScreenPosition + viewportPosition + viewportHalfSize - // this gets us to the center
                                _gameRenderScale * viewportHalfSize +
                                _gameRenderScale * _gameRenderPosition;
            var gameRenderMax = gameRenderMin + _gameRenderScale * viewportSize;

            var dl = ImGui.GetWindowDrawList();
            dl->AddImage(
                (void*)_gameRenderTextureId.Value,
                gameRenderMin,
                gameRenderMax,
                Vector2.Zero,
                Vector2.One,
                Color.White.PackedValue
            );

            ImGui.SetCursorScreenPos(gameRenderMin);

            var gameRenderSize = viewportSize * _gameRenderScale;
            ImGui.InvisibleButton(
                "GameRender",
                gameRenderSize.EnsureNotZero(),
                // ImGuiButtonFlags.MouseButtonLeft |
                // ImGuiButtonFlags.MouseButtonRight |
                ImGuiButtonFlags.MouseButtonMiddle
            );

            // reset cursor position, otherwise imgui will complain since v1.89
            // where a check was added to prevent the window from being resized by just setting the cursor position 
            ImGui.SetCursorPos(ImGui.GetCursorStartPos());

            var isActive = ImGui.IsItemActive();
            var isHovered = ImGui.IsItemHovered();

            if (isHovered)
            {
                MyEditorMain.ActiveInput = ActiveInput.GameWindow;
            }
            
            // draw border
            /*var isFocused = ImGui.IsItemFocused();
            var borderColor = (isActive, isFocused, isHovered) switch
            {
                (true, _, _) => Color.Green,
                (_, true, _) => Color.Blue,
                (_, _, true) => Color.Yellow,
                _ => Color.Gray
            };
            dl->AddRect(gameRenderMin, gameRenderMax, borderColor.PackedValue, 0, ImDrawFlags.None, 1f);*/

            HandleInput(isActive, isHovered);

            SetGameRenderViewportTransform(gameRenderMin, viewportTransform);

            var windowPos = ImGui.GetWindowPos();
            var bb = new ImRect(windowPos + contentMin, windowPos + contentMax);
            // IsHoveringGameWindow = /*ImGui.IsWindowFocused() && */bb.Contains(ImGui.GetMousePos());

            // exit relative mode on escape 
            if (Shared.Game.Inputs.Mouse.RelativeMode && ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                Shared.Game.Inputs.Mouse.RelativeMode = false;
            }

            if (ImGui.BeginPopupContextWindow("GameContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
            {
                ImGui.MenuItem("Show debug overlay", default, ImGuiExt.RefPtr(ref _showDebug));
                ImGui.MenuItem("Draw mouse debug", default, ImGuiExt.RefPtr(ref MouseDebug.DebugMouse));
                ImGui.MenuItem("Enable mouse pan & zoom", default, ImGuiExt.RefPtr(ref IsMousePanAndZoomEnabled));
                if (IsPanZoomDirty && ImGui.MenuItem("Reset pan & zoom", default))
                    ResetPanAndZoom();

                ImGui.EndPopup();
            }

            DrawDebugOverlay();

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
            }
        }

        ImGui.End();
    }

    private void DrawToolbar(ImGuiWindowClass imGuiWindowClass)
    {
        ImGui.SetNextWindowClass(&imGuiWindowClass);
        if (ImGui.Begin(ToolbarWindowName, default, ImGuiWindowFlags.NoScrollbar))
        {
            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlass, ImGuiExt.Colors[0], "Reset pan & zoom"))
            {
                ResetPanAndZoom();
            }

            ImGui.SameLine();

            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlassPlus, ImGuiExt.Colors[0], "Fill height"))
            {
                _gameRenderScale = _gameViewWindowSize.X / _gameViewWindowSize.Y;
            }

            ImGui.SameLine();

            ImGui.BeginChild("ZoomChild", new Vector2(60, 30));
            var tmpZoom = _gameRenderScale * 100 + 0.01f;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle()->FramePadding.X, ImGui.GetStyle()->FramePadding.Y + 2));
            if (SimpleTypeInspector.InspectFloat("##Zoom", ref tmpZoom, new RangeSettings(50, 1000, 1, true), "%.0f%%", ImGuiSliderFlags.AlwaysClamp))
            {
                _gameRenderScale = MathF.Exp(MathF.Lerp(MathF.Log(_gameRenderScale), MathF.Log(tmpZoom / 100f), 0.1f));
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.SameLine();

            var (icon, color, tooltip) = IsMousePanAndZoomEnabled switch
            {
                true => (FontAwesome6.ArrowPointer, Color.Green, "Disable mouse pan & zoom"),
                _ => (FontAwesome6.Lock, ImGuiExt.Colors[2], "Enable mouse pan & zoom")
            };
            if (ImGuiExt.ColoredButton(icon, color, tooltip))
            {
                IsMousePanAndZoomEnabled = !IsMousePanAndZoomEnabled;
            }

            var dockNode = ImGuiInternal.GetWindowDockNode();
            if (dockNode != null)
            {
                dockNode->LocalFlags = 0;
                // dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
                dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoResizeY);
            }
        }

        ImGui.End();
    }

    private void ResetPanAndZoom()
    {
        _gameRenderScale = 1.0f;
        _gameRenderPosition = Vector2.Zero;
    }

    private void SetGameRenderViewportTransform(Vector2 gameRenderMin, Matrix4x4 viewportTransform)
    {
        var gameRenderOffset = gameRenderMin - ImGui.GetWindowViewport()->Pos;

        viewportTransform.Decompose(out var viewportScale, out _, out _);

        GameRenderViewportTransform = (
            Matrix3x2.CreateScale(viewportScale.X * _gameRenderScale) *
            Matrix3x2.CreateTranslation(gameRenderOffset.X, gameRenderOffset.Y)
        ).ToMatrix4x4();
    }

    private void HandleInput(bool isActive, bool isHovered)
    {
        if (IsMousePanAndZoomEnabled)
        {
            // panning
            if (isActive && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                _gameRenderPosition += ImGui.GetIO()->MouseDelta * 1.0f / _gameRenderScale;
            }

            // zooming
            if (isHovered && ImGui.GetIO()->MouseWheel != 0)
            {
                _gameRenderScale += ImGui.GetIO()->MouseWheel * 0.1f * _gameRenderScale;
                if (_gameRenderScale < 1.0f)
                    _gameRenderScale = 1.0f;
            }
        }

        // imgui sets WantCaptureKeyboard when an item is active which we don't want for the game window
        if (ImGui.IsWindowHovered() &&
            (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
             ImGui.IsMouseDown(ImGuiMouseButton.Right)))
        {
            ImGui.SetWindowFocus(GameWindowTitle);
            ImGui.SetNextFrameWantCaptureKeyboard(false);
        }
    }

    public static bool BeginOverlay(string name, ref bool showDebug)
    {
        var windowFlags = ImGuiWindowFlags.NoDecoration |
                          ImGuiWindowFlags.NoDocking |
                          ImGuiWindowFlags.AlwaysAutoResize |
                          /*ImGuiWindowFlags.NoFocusOnAppearing |
                           ImGuiWindowFlags.NoBringToFrontOnFocus |*/
                          ImGuiWindowFlags.NoSavedSettings |
                          ImGuiWindowFlags.NoNav;

        ImGui.SetNextWindowBgAlpha(0.8f);

        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();

        var windowPos = ImGui.GetWindowPos();
        var windowPadding = 10f;
        var overlayPos = new Vector2(
            windowPos.X + contentMax.X - windowPadding,
            windowPos.Y + contentMin.Y + windowPadding
        );
        var windowPosPivot = new Vector2(
            1.0f,
            0
        );
        var flags = ImGuiCond.Always;

        ImGui.SetNextWindowPos(overlayPos, flags, windowPosPivot);
        ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);
        return ImGui.Begin(name, ImGuiExt.RefPtr(ref showDebug), windowFlags);
    }

    public static void DrawDebugOverlay()
    {
        if (!_showDebug)
            return;

        if (BeginOverlay("GameWindowOverlay", ref _showDebug))
        {
            /*ImGui.Text(
                $"Min: {gameRenderMin.ToString()}, Max: {gameRenderMax.ToString()}\n" +
                $"IsHoveringGame: {IsHoveringGame.ToString()}\n" +
                $"GameRenderOffset: {gameRenderOffset.ToString()}");
    
            ImGui.Separator();*/
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.Tiny));

            var editor = (MyEditorMain)Shared.Game;

            ImGuiExt.SeparatorText("Mouse", Color.White);
            var mousePosition = editor.InputHandler.MousePosition;
            ImGuiExt.PrintVector("Pos", mousePosition);
            var view = editor.Camera.GetView(0);
            Matrix3x2.Invert(view, out var invertedView);
            var mouseInWorld = MoonWorks.Math.Float.Vector2.Transform(mousePosition, invertedView);
            ImGuiExt.PrintVector("World", mouseInWorld);
            var mouseCell = Entity.ToCell(mouseInWorld);
            ImGuiExt.PrintVector("Cel", mouseCell);

            var world = editor.World;
            if (world.IsLoaded)
            {
                var player = world.Entities.First<Player>();
                var playerCell = player.Cell;
                ImGuiExt.SeparatorText("Player", Color.White);
                ImGuiExt.PrintVector("Cell", playerCell);
                ImGuiExt.PrintVector("Pos", player.Position.Current);
            }

            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Close", default)) _showDebug = !_showDebug;
                ImGui.EndPopup();
            }

            ImGui.Dummy(new Vector2(200, 0));

            ImGui.PopFont();
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }
}
