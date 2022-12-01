using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class LoadingScreenDebugWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "LoadingScreen Debug";
    private IInspector? _loadingScreenInspector;

    private static string[] _transitionTypeNames = Enum.GetNames<TransitionType>();

    public LoadingScreenDebugWindow() : base(WindowTitle)
    {
    }

    public override void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen)))
        {
            ImGui.Checkbox("Debug Loading", ImGuiExt.RefPtr(ref LoadingScreen.Debug));
            if (ImGui.SliderFloat("Progress", ImGuiExt.RefPtr(ref LoadingScreen.DebugProgress), 0, 1.0f, "%g"))
            {
            }

            ImGui.TextUnformatted($"State: {LoadingScreen.DebugState.ToString()}");

            var transitionType = (int)LoadingScreen.TransitionType;
            if (BlendStateEditor.ComboStep("TransitionType", ref transitionType, _transitionTypeNames))
            {
                LoadingScreen.TransitionType = (TransitionType)transitionType;

                _loadingScreenInspector = InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            }


            _loadingScreenInspector ??= InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            _loadingScreenInspector?.Draw();
        }

        ImGui.End();
    }
}
