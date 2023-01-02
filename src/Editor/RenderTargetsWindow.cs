using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class RenderTargetsWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "RenderTargets";
    private int _selectedTargetIdx;

    private Color _backgroundColor = new Color(0.039f, 0.039f, 0.039f, 1.0f);
    private Color _stripesColor = new Color(1f, 1f, 1f, 0.165f);

    private readonly RenderTarget[] _renderTargets;
    private readonly string[] _renderTargetNames;

    public readonly GameRenderView GameRenderView;

    private bool _syncWithGame;
    private Color _borderColor = Color.Red;

    public RenderTargetsWindow(MyEditorMain editor) : base(WindowTitle)
    {
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

        GameRenderView = new GameRenderView();
        GameRenderView.DrawToolbarCallback = DrawToolbar;
    }

    private void DrawToolbar()
    {
        ImGui.SetNextItemWidth(200);
        
        ImGuiExt.ComboStep("##RenderTarget", true, ref _selectedTargetIdx, _renderTargetNames);
        ImGui.SameLine();

        ImGui.SameLine();
        SimpleTypeInspector.InspectColor("##BackgroundColor", ref _backgroundColor, "Background Color");

        ImGui.SameLine();
        SimpleTypeInspector.InspectColor("##StripesColor", ref _stripesColor, "Stripes Color");
        
        ImGui.SameLine();
        SimpleTypeInspector.InspectColor("##BorderColor", ref _borderColor, "Border Color");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        SimpleTypeInspector.InspectBool("Sync", ref _syncWithGame);
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var flags = ImGuiWindowFlags.NoNav |
                    ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoScrollbar;
        ImGui.SetNextWindowSize(new Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(480, 270), new Vector2(ImGuiExt.FLT_MAX, ImGuiExt.FLT_MAX));
        var result = ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags);

        if (result)
        {
            ImGui.PopStyleVar();
            DrawWindowContent();
        }

        if (!result)
        {
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }

    private void DrawWindowContent()
    {
        var dl = ImGui.GetWindowDrawList();
        var min = GameRenderView.GameRenderMin;
        var max = GameRenderView.GameRenderMax;
        dl->AddRectFilled(min, max, _backgroundColor.PackedValue);
        ImGuiExt.FillWithStripes(dl, new ImRect(min, max), _stripesColor.PackedValue);

        if (_syncWithGame)
        {
            var editor = (MyEditorMain)Shared.Game;
            GameRenderView.Zoom = editor.GameWindow.GameRenderView.Zoom;
            GameRenderView.Offset = editor.GameWindow.GameRenderView.Offset;
        }

        var label = $"{_renderTargets[_selectedTargetIdx].Size.X}x{_renderTargets[_selectedTargetIdx].Size.Y}";
        var labelSize = ImGui.CalcTextSize(label);
        var x = min.X + ((max.X - min.X) - labelSize.X) * 0.5f;
        var y = min.Y - labelSize.Y - 10;
        dl->AddText(ImGuiExt.GetFont(ImGuiFont.MediumBold), 16, new Vector2(x, y), Color.White.PackedValue, label, 0, default);
        
        GameRenderView.Draw("RenderTarget", _renderTargets[_selectedTargetIdx]);
        if (ImGui.IsItemHovered())
        {
            MyEditorMain.ActiveInput = ActiveInput.RenderTargetsWindow;
        } ;

        if (ImGui.BeginPopupContextWindow("WindowContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
        {
            if (ImGui.MenuItem("Reset pan & zoom", default))
                GameRenderView.ResetPanAndZoom();

            ImGui.EndPopup();
        }
    }
}
