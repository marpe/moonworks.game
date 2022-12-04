using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public unsafe class GameWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Game";
    public const string GameViewTitle = "GameView";
    private IntPtr? _gameRenderTextureId;
    public Matrix4x4 GameRenderViewportTransform;

    private float _gameRenderScale = 1f;
    private Num.Vector2 _gameRenderPosition = Num.Vector2.Zero;
    private MyEditorMain _editor;

    private bool _showDebug;

    public bool IsHoveringGame;

    [CVar("imgui.mouse_pan_and_zoom", "Toggle mouse pan & zoom control")]
    public static bool IsMousePanAndZoomEnabled = true;

    public bool IsPanZoomDirty => MathF.NotApprox(_gameRenderScale, 1.0f) || _gameRenderPosition != Num.Vector2.Zero; 

    public GameWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F1";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;
        var flags = ImGuiWindowFlags.NoCollapse; /*ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoScrollbar;*/

        var dockspaceID = ImGui.GetID("GameViewDockspace");

        ImGuiWindowClass workspaceWindowClass;
        workspaceWindowClass.ClassId = dockspaceID;
        workspaceWindowClass.DockingAllowUnclassed = false;

        ImGui.SetNextWindowSize(new Num.Vector2(1000, 800), ImGuiCond.FirstUseEver);
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(0, 0));
        ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags);

        if (ImGuiInternal.DockBuilderGetNode(dockspaceID) == null)
        {
            var dockFlags = ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace | ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton;
            ImGuiInternal.DockBuilderAddNode(dockspaceID, (ImGuiDockNodeFlags)dockFlags);
            ImGuiInternal.DockBuilderSetNodeSize(dockspaceID, ImGui.GetContentRegionAvail());

            uint upNode, downNode;
            ImGuiInternal.DockBuilderSplitNode(dockspaceID, ImGuiDir.Up, 0.05f, &upNode, &downNode);
            ImGuiInternal.DockBuilderDockWindow("GameToolbar", upNode);
            ImGuiInternal.DockBuilderDockWindow(GameViewTitle, downNode);
            ImGuiInternal.DockBuilderFinish(dockspaceID);
        }

        ImGui.DockSpace(dockspaceID, new Num.Vector2(0, 0), ImGuiDockNodeFlags.None, &workspaceWindowClass); // ImGuiDockNodeFlags.NoResize

        ImGui.End();
        ImGui.PopStyleVar();

        DrawGameView();
        DrawButtons();
    }

    private void DrawGameView()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
        /*ImGuiWindowClass windowClass;
        windowClass.ViewportFlagsOverrideSet = ImGuiViewportFlags.NoAutoMerge;
        ImGui.SetNextWindowClass(&windowClass);*/
        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse |ImGuiWindowFlags.NoScrollbar;
        ImGui.Begin(GameViewTitle, default, flags);
        ImGui.PopStyleVar();
        if (_gameRenderTextureId != null && _gameRenderTextureId != _editor.CompositeRender.Handle)
        {
            _editor.ImGuiRenderer.UnbindTexture(_gameRenderTextureId.Value);
            _gameRenderTextureId = null;
            Logger.LogInfo("Unbinding compositeRender texture");
        }

        if (_gameRenderTextureId == null)
        {
            Logger.LogInfo("Binding _compositeRender texture");
            _gameRenderTextureId = _editor.ImGuiRenderer.BindTexture(_editor.CompositeRender);
        }

        var windowSize = ImGui.GetWindowSize();
        var (viewportTransform, viewport) = Renderer.GetViewportTransform(windowSize.ToXNA().ToPoint(), _editor.CompositeRender.Size());

        var viewportPosition = new Num.Vector2(viewport.X, viewport.Y);
        var cursorScreenPosition = ImGui.GetCursorScreenPos();

        var viewportSize = viewport.Size.ToNumerics();
        var viewportHalfSize = viewportSize * 0.5f;

        var gameRenderMin = cursorScreenPosition + viewportPosition + viewportHalfSize - _gameRenderScale * viewportHalfSize +
                            _gameRenderScale * _gameRenderPosition;
        var gameRenderMax = gameRenderMin + _gameRenderScale * viewportSize;

        var dl = ImGui.GetWindowDrawList();
        dl->AddImage(
            (void*)_gameRenderTextureId.Value,
            gameRenderMin,
            gameRenderMax,
            Num.Vector2.Zero,
            Num.Vector2.One
        );

        ImGui.SetCursorScreenPos(gameRenderMin);

        ImGui.InvisibleButton(
            "GameRender",
            viewportSize * _gameRenderScale,
            ImGuiButtonFlags.MouseButtonLeft |
            ImGuiButtonFlags.MouseButtonMiddle |
            ImGuiButtonFlags.MouseButtonRight
        );

        // reset cursor position, otherwise imgui will complain since v1.89
        // where a check was added to prevent the window from being resized by just setting the cursor position 
        ImGui.SetCursorPos(ImGui.GetCursorStartPos());

        var isActive = ImGui.IsItemActive();
        var isHovered = ImGui.IsItemHovered();
        HandleInput(isActive, isHovered);

        SetGameRenderViewportTransform(gameRenderMin, viewportTransform);

        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();
        var windowPos = ImGui.GetWindowPos();
        var bb = new ImRect(windowPos + contentMin, windowPos + contentMax);
        IsHoveringGame = /*ImGui.IsWindowFocused() && */bb.Contains(ImGui.GetMousePos());

        if (ImGui.BeginPopupContextWindow("GameContextMenu"))
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
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
        }
        ImGui.End();
    }
    
    private void DrawButtons()
    {
        ImGui.Begin("GameToolbar");

        if (IsPanZoomDirty)
        {
            if (ImGuiExt.ColoredButton(FontAwesome6.MagnifyingGlass, ImGuiExt.Colors[0], "Reset pan & zoom"))
            {
                ResetPanAndZoom();
            }

            ImGui.SameLine();
        }

        var (icon, color, tooltip) = IsMousePanAndZoomEnabled switch
        {
            true => (FontAwesome6.ArrowPointer, Color.Green, "Disable mouse pan & zoom"),
            _ => (FontAwesome6.Lock, Color.Red, "Enable mouse pan & zoom")
        };
        if (ImGuiExt.ColoredButton(icon, color, tooltip))
        {
            IsMousePanAndZoomEnabled = !IsMousePanAndZoomEnabled;
        }

        var dockNode = ImGuiInternal.GetWindowDockNode();
        if (dockNode != null)
        {
            dockNode->LocalFlags = 0;
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoDockingOverMe);
            dockNode->LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoTabBar);
        }
        ImGui.End();
    }

    private void ResetPanAndZoom()
    {
        _gameRenderScale = 1.0f;
        _gameRenderPosition = Num.Vector2.Zero;
    }

    private void SetGameRenderViewportTransform(System.Numerics.Vector2 gameRenderMin, Matrix4x4 viewportTransform)
    {
        var windowViewportPosition = ImGui.GetWindowViewport()->Pos;
        var gameRenderOffset = gameRenderMin - windowViewportPosition;

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
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
            ImGui.IsMouseDown(ImGuiMouseButton.Middle) ||
            ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            ImGui.SetNextFrameWantCaptureKeyboard(false);
        }
    }

    private void DrawDebugOverlay()
    {
        if (!_showDebug)
            return;
        
        var windowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize |
                          /*ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus |*/ ImGuiWindowFlags.NoSavedSettings |
                          ImGuiWindowFlags.NoNav;

        ImGui.SetNextWindowBgAlpha(0.5f);

        var contentMin = ImGui.GetWindowContentRegionMin();
        var contentMax = ImGui.GetWindowContentRegionMax();

        var windowPos = ImGui.GetWindowPos();
        var windowPadding = 10f;
        var overlayPos = new Num.Vector2(
            windowPos.X + contentMax.X - windowPadding,
            windowPos.Y + contentMin.Y + windowPadding
        );
        var windowPosPivot = new Num.Vector2(
            1.0f,
            0
        );
        var flags = ImGuiCond.Always;

        ImGui.SetNextWindowPos(overlayPos, flags, windowPosPivot);
        ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);
        if (ImGui.Begin("GameWindowOverlay", ImGuiExt.RefPtr(ref _showDebug), windowFlags))
        {
            /*ImGui.Text(
                $"Min: {gameRenderMin.ToString()}, Max: {gameRenderMax.ToString()}\n" +
                $"IsHoveringGame: {IsHoveringGame.ToString()}\n" +
                $"GameRenderOffset: {gameRenderOffset.ToString()}");

            ImGui.Separator();*/
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);
            ImGui.PushFont(_editor.ImGuiRenderer.GetFont(ImGuiFont.Tiny));

            void PrintVector(string label, Vector2 v)
            {
                var avail = ImGui.GetContentRegionAvail();
                ImGui.Text(label);
                ImGui.SameLine(0.33f * avail.X);
                ImGui.Text($"{v.X:0.##}");
                ImGui.SameLine(0.66f * avail.X);
                ImGui.Text($"{v.Y:0.##}");
            }

            ImGuiExt.SeparatorText("Mouse", Color.White);
            var mousePosition = _editor.InputHandler.MousePosition;
            PrintVector("Pos", mousePosition);
            var view = _editor.GameScreen.Camera.GetView();
            Matrix3x2.Invert(view, out var invertedView);
            var mouseInWorld = Vector2.Transform(mousePosition, invertedView);
            PrintVector("World", mouseInWorld);
            var mouseCell = Entity.ToCell(mouseInWorld);
            PrintVector("Cel", mouseCell);

            var world = _editor.GameScreen.World;
            if (world != null)
            {
                var playerCell = world.Player.Cell;
                ImGuiExt.SeparatorText("Player", Color.White);
                PrintVector("Cell", playerCell);
                PrintVector("Pos", world.Player.Position.Current);
            }

            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Close", default)) _showDebug = !_showDebug;
                ImGui.EndPopup();
            }

            ImGui.Dummy(new Num.Vector2(200, 0));

            ImGui.PopFont();
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }
}
