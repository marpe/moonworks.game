using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public unsafe class GameWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Game";
    private IntPtr? _gameRenderTextureId;
    public Matrix4x4 GameRenderViewportTransform;

    private float _gameRenderScale = 1f;
    private Num.Vector2 _gameRenderPosition = Num.Vector2.Zero;
    private MyEditorMain _editor;

    private bool _showDebug;

    public bool IsHoveringGame = false;
    public bool IsWindowFocused;

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

        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoScrollbar;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
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

            /*ImGui.Image(
                (void*)_gameRenderTextureId.Value,
                viewportSize * _gameRenderScale,
                Vector2.Zero,
                Vector2.One,
                Num.Vector4.One,
                Num.Vector4.Zero
            );*/

            var isActive = ImGui.IsItemActive();
            var isHovered = ImGui.IsItemHovered();
            HandleInput(isActive, isHovered);

            var gameRenderOffset = SetGameRenderViewportTransform(gameRenderMin, viewportTransform);

            DrawDebugOverlay();

            var contentMin = ImGui.GetWindowContentRegionMin();
            var contentMax = ImGui.GetWindowContentRegionMax();
            var windowPos = ImGui.GetWindowPos();
            var bb = new ImRect(windowPos + contentMin, windowPos + contentMax);
            IsHoveringGame = /*ImGui.IsWindowFocused() && */bb.Contains(ImGui.GetMousePos());

            IsWindowFocused = ImGui.IsWindowFocused();

            if (ImGui.BeginPopupContextWindow("GameContextMenu"))
            {
                ImGui.MenuItem("Show debug overlay", default, ImGuiExt.RefPtr(ref _showDebug));
                ImGui.MenuItem("Draw mouse debug", default, ImGuiExt.RefPtr(ref World.DebugMouse));
                if (ImGui.MenuItem("Reset pan & zoom", default))
                    ResetPanAndZoom();

                ImGui.EndPopup();
            }
        }
        else
        {
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }

    private void ResetPanAndZoom()
    {
        _gameRenderScale = 1.0f;
        _gameRenderPosition = Num.Vector2.Zero;
    }

    private Num.Vector2 SetGameRenderViewportTransform(Num.Vector2 gameRenderMin, Matrix4x4 viewportTransform)
    {
        var windowViewportPosition = ImGui.GetWindowViewport()->Pos;
        var gameRenderOffset = gameRenderMin - windowViewportPosition;

        viewportTransform.Decompose(out var viewportScale, out _, out _);

        GameRenderViewportTransform = (
            Matrix3x2.CreateScale(viewportScale.X * _gameRenderScale) *
            Matrix3x2.CreateTranslation(gameRenderOffset.X, gameRenderOffset.Y)
        ).ToMatrix4x4();
        return gameRenderOffset;
    }

    private void HandleInput(bool isActive, bool isHovered)
    {
        if (isActive && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
        {
            _gameRenderPosition += ImGui.GetMouseDragDelta(ImGuiMouseButton.Middle) * 1.0f / _gameRenderScale;
            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Middle);
        }

        if (isHovered && ImGui.GetIO()->MouseWheel != 0)
        {
            _gameRenderScale += ImGui.GetIO()->MouseWheel * 0.1f * _gameRenderScale;
            if (_gameRenderScale < 1.0f)
                _gameRenderScale = 1.0f;
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
                          ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav;

        ImGui.SetNextWindowBgAlpha(0.5f);

        var cursorStart = ImGui.GetCursorStartPos();
        ImGui.SetCursorPos(cursorStart);
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
        ImGui.SetNextWindowPos(overlayPos, ImGuiCond.Appearing, windowPosPivot);
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
