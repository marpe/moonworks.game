﻿using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
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

        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoScrollbar;

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
            
            var windowSize = ImGui.GetWindowSize();
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(windowSize.ToXNA().ToPoint(), _editor.CompositeRender.Size());

            var viewportPosition = new Vector2(viewport.X, viewport.Y);
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
                Vector2.Zero,
                Vector2.One
            );

            ImGui.SetCursorScreenPos(gameRenderMin);

            ImGui.InvisibleButton(
                "GameRender",
                viewportSize * _gameRenderScale,
                ImGuiButtonFlags.MouseButtonLeft |
                ImGuiButtonFlags.MouseButtonMiddle |
                ImGuiButtonFlags.MouseButtonRight |
                (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap
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
            
            ImGui.SetItemAllowOverlap();
            DrawButtons();

            var gameRenderOffset = SetGameRenderViewportTransform(gameRenderMin, viewportTransform);

            DrawDebugOverlay(gameRenderMin, gameRenderMax, gameRenderOffset);

            if (ImGui.BeginPopupContextWindow("GameContextMenu"))
            {
                ImGui.MenuItem("Show window debug", default, ImGuiExt.RefPtr(ref _showDebug));
                ImGui.EndPopup();
            }
        }

        ImGui.End();
    }

    private void DrawButtons()
    {
        var startPos = ImGui.GetCursorStartPos();
        ImGui.SetCursorPos(startPos);
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
    }

    private void DrawDebugOverlay(Vector2 gameRenderMin, Vector2 gameRenderMax, Vector2 gameRenderOffset)
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
        var overlayPos = new Vector2(
            windowPos.X + contentMax.X - windowPadding,
            windowPos.Y + contentMin.Y + windowPadding
        );
        var windowPosPivot = new Vector2(
            1.0f,
            0
        );
        ImGui.SetNextWindowPos(overlayPos, ImGuiCond.Always, windowPosPivot);
        ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);
        if (ImGui.Begin("GameWindowOverlay", ImGuiExt.RefPtr(ref _showDebug), windowFlags))
        {
            ImGui.Text(
                $"Min: {gameRenderMin.ToString()}, Max: {gameRenderMax.ToString()}\n" +
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