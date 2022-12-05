using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.Editor;

public unsafe class RenderTargetsWindow : ImGuiEditorWindow
{
    private MyEditorMain _editor;
    private IntPtr? _lightPtr;
    private IntPtr? _consolePtr;
    private IntPtr? _menuPtr;
    private IntPtr? _gamePtr;
    private IntPtr? _compositePtr;
    private IntPtr? _lightTargetPtr;
    public const string WindowTitle = "RenderTargets";
    public DebugRenderTarget CurrentTarget = DebugRenderTarget.GameRender;

    private Color _backgroundColor = Color.Black;
    private Color _stripesColor = new Color(0, 0, 0, 0.2f);

    public RenderTargetsWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var flags = ImGuiWindowFlags.NoCollapse;
        ImGui.SetNextWindowSize(new Num.Vector2(1920, 1080), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(480, 270), new Num.Vector2(1920, 1080));
        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen), flags))
        {
            GameWindow.EnsureTextureIsBound(ref _lightPtr, _editor.RenderTargets.LightSource, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _lightTargetPtr, _editor.RenderTargets.LightTarget, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _consolePtr, _editor.RenderTargets.ConsoleRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _menuPtr, _editor.RenderTargets.MenuRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _gamePtr, _editor.RenderTargets.GameRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _compositePtr, _editor.RenderTargets.CompositeRender, _editor.ImGuiRenderer);

            ImGui.BeginGroup();
            {
                var widthAvail = ImGui.GetContentRegionAvail().X;
                if (ImGui.BeginChild("TargetButtons", new Num.Vector2(widthAvail * 0.5f, 20), false, ImGuiWindowFlags.NoScrollbar))
                {
                    EnumInspector.InspectEnum("CurrentTarget", ref CurrentTarget);
                }

                ImGui.EndChild();

                ImGui.SameLine();
                if (ImGui.BeginChild("Colors", new Num.Vector2(widthAvail * 0.5f - ImGui.GetStyle()->ItemInnerSpacing.X, 20), false, ImGuiWindowFlags.NoScrollbar))
                {
                    SimpleTypeInspector.InspectColor("##BackgroundColor", ref _backgroundColor);
                    ImGui.SameLine();
                    SimpleTypeInspector.InspectColor("##StripesColor", ref _stripesColor);
                }

                ImGui.EndChild();
            }
            ImGui.EndGroup();

            var ptr = CurrentTarget switch
            {
                DebugRenderTarget.GameRender => _gamePtr,
                DebugRenderTarget.LightSource => _lightPtr,
                DebugRenderTarget.LightTarget => _lightTargetPtr,
                DebugRenderTarget.Menu => _menuPtr,
                DebugRenderTarget.Console => _consolePtr,
                DebugRenderTarget.None => _compositePtr,
                _ => throw new ArgumentOutOfRangeException()
            };

            var contentAvail = ImGui.GetContentRegionAvail();

            var cursorPos = ImGui.GetCursorScreenPos();
            var imageSize = new Num.Vector2(contentAvail.X, contentAvail.X * (1080f / 1920f));
            var dl = ImGui.GetWindowDrawList();
            dl->AddRectFilled(cursorPos, cursorPos + imageSize, _backgroundColor.PackedValue);
            ImGuiExt.FillWithStripes(ImGui.GetWindowDrawList(), new ImRect(cursorPos, cursorPos + imageSize), _stripesColor.PackedValue);

            ImGui.Image(
                ptr!.Value.ToPointer(),
                imageSize,
                new Num.Vector2(0, 0),
                new Num.Vector2(1, 1),
                new Num.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                new Num.Vector4(1.0f, 0, 0, 1f)
            );
        }

        ImGui.End();
    }
}
