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
    public const string WindowTitle = "RenderTargets";
    public DebugRenderTarget CurrentTarget = DebugRenderTarget.GameRender;

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
            GameWindow.EnsureTextureIsBound(ref _consolePtr, _editor.RenderTargets.ConsoleRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _menuPtr, _editor.RenderTargets.MenuRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _gamePtr, _editor.RenderTargets.GameRender, _editor.ImGuiRenderer);
            GameWindow.EnsureTextureIsBound(ref _compositePtr, _editor.RenderTargets.CompositeRender, _editor.ImGuiRenderer);

            EnumInspector.InspectEnum("CurrentTarget", ref CurrentTarget);

            var ptr = CurrentTarget switch
            {
                DebugRenderTarget.GameRender => _gamePtr,
                DebugRenderTarget.Lights => _lightPtr,
                DebugRenderTarget.Menu => _menuPtr,
                DebugRenderTarget.Console => _consolePtr,
                DebugRenderTarget.None => _compositePtr,
                _ => throw new ArgumentOutOfRangeException()
            };

            var contentAvail = ImGui.GetContentRegionAvail();

            var cursorPos = ImGui.GetCursorScreenPos();
            var imageSize = new Num.Vector2(contentAvail.X, contentAvail.X * (1080f / 1920f));
            var dl = ImGui.GetWindowDrawList();
            dl->AddRectFilled(cursorPos, cursorPos + imageSize, Color.White.PackedValue);
            ImGuiExt.FillWithStripes(ImGui.GetWindowDrawList(), new ImRect(cursorPos, cursorPos + imageSize));

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
