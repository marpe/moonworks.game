using Mochi.DearImGui;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class GameWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Game";
    private IntPtr? _gameRenderTextureId;
    public Matrix4x4 GameRenderViewportTransform;

    private float _gameRenderScale = 1f;
    private Vector2 _gameRenderPosition = Vector2.Zero;
    private MyEditorMain _editor;

    private bool _showDebug;

    public GameWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F3";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse;

        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
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

            HandleInput();

            var dl = ImGui.GetWindowDrawList();

            var windowSize = ImGui.GetWindowSize();
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(windowSize.ToXNA().ToPoint(), _editor.CompositeRender.Size());

            var viewportPosition = new Vector2(viewport.X, viewport.Y);
            var cursorScreenPosition = ImGui.GetCursorScreenPos();

            var viewportSize = viewport.Size.ToNumerics();
            var viewportHalfSize = viewportSize * 0.5f;

            var gameRenderMin = cursorScreenPosition + viewportPosition + viewportHalfSize - _gameRenderScale * viewportHalfSize +
                                _gameRenderScale * _gameRenderPosition;
            var gameRenderMax = gameRenderMin + _gameRenderScale * viewportSize;
            dl->AddImage(
                (void*)_gameRenderTextureId.Value,
                gameRenderMin,
                gameRenderMax,
                Vector2.Zero,
                Vector2.One
            );

            var gameRenderOffset = SetGameRenderViewportTransform(gameRenderMin, viewportTransform);

            DrawButtons();
            DrawDebugOverlay(gameRenderMin, gameRenderMax, gameRenderOffset);

            if (ImGui.BeginPopupContextWindow("GameContextMenu"))
            {
                ImGui.Selectable("Show window debug", ImGuiExt.RefPtr(ref _showDebug), ImGuiSelectableFlags.None, default);
                ImGui.EndPopup();
            }
        }

        ImGui.End();
    }

    private void DrawButtons()
    {
        if (ImGuiExt.ColoredButton(FontAwesome6.ArrowsRotate, Color.Black, "Reset pan & zoom"))
        {
            _gameRenderScale = 1.0f;
            _gameRenderPosition = Vector2.Zero;
        }
    }

    private Vector2 SetGameRenderViewportTransform(Vector2 gameRenderMin, Matrix4x4 viewportTransform)
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

    private void HandleInput()
    {
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
        {
            _gameRenderPosition += ImGui.GetMouseDragDelta(ImGuiMouseButton.Middle) * 1.0f / _gameRenderScale;
            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Middle);
        }

        if (ImGui.GetIO()->MouseWheel != 0)
        {
            _gameRenderScale += ImGui.GetIO()->MouseWheel * 0.1f * _gameRenderScale;
            if (_gameRenderScale < 1.0f)
                _gameRenderScale = 1.0f;
        }
    }

    private void DrawDebugOverlay(Vector2 gameRenderMin, Vector2 gameRenderMax, Vector2 gameRenderOffset)
    {
        if (!_showDebug)
            return;

        var windowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize |
                          ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

        var windowViewportPosition = ImGui.GetWindowViewport()->Pos;
        ImGui.SetNextWindowBgAlpha(0.35f);
        if (ImGui.Begin("GameWindowOverlay", ImGuiExt.RefPtr(ref _showDebug), windowFlags))
        {
            ImGui.Text(
                $"Min: {gameRenderMin.ToString()}, Max: {gameRenderMax.ToString()}\n" +
                $"WinViewport: {windowViewportPosition.ToString()}\n" +
                $"GameRenderOffset: {gameRenderOffset.ToString()}");

            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Close", default)) _showDebug = !_showDebug;
                ImGui.EndPopup();
            }
        }

        ImGui.End();
    }
}
