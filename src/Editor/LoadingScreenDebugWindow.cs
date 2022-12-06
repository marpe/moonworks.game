using Mochi.DearImGui;

namespace MyGame.Editor;

public unsafe class LoadingScreenDebugWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "LoadingScreen Debug";
    private IInspector? _loadingScreenInspector;

    private static string[] _transitionTypeNames = Enum.GetNames<TransitionType>();

    private float _deltaSeconds = 1f / 30f;
    private float _sleepDurationInSeconds = 3f;
    private EnumInspector? _stateInspector;

    public LoadingScreenDebugWindow() : base(WindowTitle)
    {
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

        if (ImGui.Begin(WindowTitle, ImGuiExt.RefPtr(ref IsOpen)))
        {
            var transitionType = (int)LoadingScreen.TransitionType;
            ImGuiExt.LabelPrefix("TransitionType");
            ImGui.Dummy(Num.Vector2.Zero);

            if (BlendStateEditor.ComboStep("##TransitionType", true, ref transitionType, _transitionTypeNames))
            {
                LoadingScreen.TransitionType = (TransitionType)transitionType;

                _loadingScreenInspector = InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            }

            var progress = Shared.LoadingScreen.Progress;
            ImGui.BeginDisabled();
            SimpleTypeInspector.InspectFloat("Progress", ref progress, new RangeSettings(0, 1, 0.01f, false));

            ImGui.AlignTextToFramePadding();

            var tmpState = Shared.LoadingScreen.State;
            ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.Tiny));
            EnumInspector.InspectEnum("State", ref tmpState);
            ImGui.PopFont();

            ImGui.Text($"QueueCount: {Shared.LoadingScreen.QueueCount}");
            ImGui.EndDisabled();

            SimpleTypeInspector.InspectBool("Manual update", ref LoadingScreen.Debug);

            if (LoadingScreen.Debug)
            {
                SimpleTypeInspector.InspectFloat("DeltaSeconds", ref _deltaSeconds, new RangeSettings(1f / 120f, 1f / 5f, 0.01f, false));
                if (ImGuiExt.ColoredButton("Step", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
                {
                    Shared.LoadingScreen.UpdateState(_deltaSeconds);
                }
            }

            SimpleTypeInspector.InspectFloat("Sleep Duration (s)", ref _sleepDurationInSeconds, new RangeSettings(0, 5f, 0.25f, false));

            _loadingScreenInspector ??= InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            _loadingScreenInspector?.Draw();

            var label = Shared.LoadingScreen.State == TransitionState.Hidden ? "Start Loading" : "Loading";
            if (ImGui.Button(label, new Num.Vector2(-ImGuiExt.FLT_MIN, 30)))
            {
                LoadingScreen.TestLoad(_sleepDurationInSeconds);
            }

            if (Shared.LoadingScreen.State != TransitionState.Hidden)
            {
                var t = Shared.LoadingScreen.State == TransitionState.TransitionOff
                    ? 0.5f + (1.0f - Shared.LoadingScreen.Progress) * 0.5f
                    : Shared.LoadingScreen.Progress * 0.5f;
                ImGui.SetNextItemWidth(-1);
                ImGui.ProgressBar(t, new Num.Vector2(0, 6f), "");
            }

            // ImGuiExt.BufferingBar("LoadingProgressBar", Shared.LoadingScreen.Progress, new Num.Vector2(size.X, 4), 0xff2e2e2e, ImGuiExt.Colors[0].PackedValue, 2f);

            /*ImGui.SameLine();
            var labelSize = ImGui.CalcTextSize(label);
            var itemSize = ImGui.GetItemRectSize();
            var spinnerPos = new Num.Vector2(
                cursorPos.X + itemSize.X * 0.5f + labelSize.X * 0.5f + ImGui.GetStyle()->ItemInnerSpacing.X,
                cursorPos.Y + itemSize.Y * 0.5f - ImGui.GetFontSize() * 0.5f
            );
            ImGui.SetCursorScreenPos(spinnerPos);
            if(Shared.LoadingScreen.State != TransitionState.Hidden)
                ImGuiExt.Spinner("ProgressSpinner", ImGui.GetFontSize() / 2 - ImGui.GetStyle()->FramePadding.Y, 2, Color.White.PackedValue, 0.5f);*/
        }

        ImGui.End();
    }
}
