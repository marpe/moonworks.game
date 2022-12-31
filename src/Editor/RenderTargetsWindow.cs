using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace MyGame.Editor;

public unsafe class RenderTargetsWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    public const string WindowTitle = "RenderTargets";
    private int _selectedTargetIdx = 0;

    private Color _backgroundColor = Color.Black;
    private Color _stripesColor = new Color(0, 0, 0, 0.2f);

    private readonly RenderTarget[] _renderTargets;
    private readonly string[] _renderTargetNames;
    
    /// <summary>
    /// User zoom
    /// </summary>
    private static float _gameRenderScale = 1f;

    /// <summary>
    /// User panning offset
    /// </summary>
    private static Vector2 _gameRenderPosition = Vector2.Zero;

    public RenderTargetsWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;

        var fields = editor.RenderTargets.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public);
        var renderTargetFields = fields.Where(x => x.FieldType == typeof(RenderTarget)).ToList();

        _renderTargets = new RenderTarget[renderTargetFields.Count];
        _renderTargetNames = new string[renderTargetFields.Count];
        for (var i = 0; i < renderTargetFields.Count; i++)
        {
            var renderTarget = renderTargetFields[i].GetValue(editor.RenderTargets) ?? throw new InvalidOperationException();
            _renderTargetNames[i] = renderTargetFields[i].Name;
            _renderTargets[i] = (RenderTarget)renderTarget;
        }
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.SetNextWindowSize(new Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(480, 270), new Vector2(1920, 1080));
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            ImGui.BeginGroup();
            {
                if (ImGui.BeginChild("TargetButtons", new Vector2(200, 20), false, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.SetNextItemWidth(-ImGuiExt.FLT_MIN);
                    if (ImGui.BeginCombo("##RenderTarget", _renderTargetNames[_selectedTargetIdx]))
                    {
                        for (var i = 0; i < _renderTargetNames.Length; i++)
                        {
                            var isSelected = i == _selectedTargetIdx;
                            if (ImGui.Selectable(_renderTargetNames[i], isSelected, ImGuiSelectableFlags.None, default))
                            {
                                _selectedTargetIdx = i;
                            }

                            if (isSelected)
                                ImGui.SetItemDefaultFocus();
                        }

                        ImGui.EndCombo();
                    }
                }

                ImGui.EndChild();

                ImGui.SameLine();
                if (ImGui.BeginChild("Colors", new Vector2(200, 20), false, ImGuiWindowFlags.NoScrollbar))
                {
                    SimpleTypeInspector.InspectColor("##BackgroundColor", ref _backgroundColor);
                    ImGuiExt.ItemTooltip("BackgroundColor");
                    ImGui.SameLine();
                    SimpleTypeInspector.InspectColor("##StripesColor", ref _stripesColor);
                    ImGuiExt.ItemTooltip("StripeColor");
                }

                ImGui.EndChild();
            }
            ImGui.EndGroup();

            var contentAvail = ImGui.GetContentRegionAvail();

            var cursorPosScreen = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(cursorPosScreen +
                                     _gameRenderScale * _gameRenderPosition);
            var cursorPos = ImGui.GetCursorScreenPos();
            var imageSize = new Vector2(contentAvail.X, contentAvail.X * (1080f / 1920f)) * _gameRenderScale;
            var dl = ImGui.GetWindowDrawList();
            dl->AddRectFilled(cursorPos, cursorPos + imageSize, _backgroundColor.PackedValue);
            ImGuiExt.FillWithStripes(ImGui.GetWindowDrawList(), new ImRect(cursorPos, cursorPos + imageSize), _stripesColor.PackedValue);

            nint? ptr = null;
            GameWindow.EnsureTextureIsBound(ref ptr, _renderTargets[_selectedTargetIdx], _editor.ImGuiRenderer);

            ImGui.Image(
                ptr.Value.ToPointer(),
                imageSize,
                new Vector2(0, 0),
                new Vector2(1, 1),
                new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                new Vector4(1.0f, 0, 0, 1f)
            );
            
            var isActive = ImGui.IsItemActive();
            var isHovered = ImGui.IsItemHovered();
            
            HandleInput(isActive, isHovered);
            
            if (ImGui.BeginPopupContextWindow("WindowContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
            {
                if (ImGui.MenuItem("Reset pan & zoom", default))
                    ResetPanAndZoom();

                ImGui.EndPopup();
            }
        }

        ImGui.End();
    }

    private void ResetPanAndZoom()
    {
        _gameRenderScale = 1.0f;
        _gameRenderPosition = Vector2.Zero;
    }


    private void HandleInput(bool isActive, bool isHovered)
    {
        // panning
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
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
}
