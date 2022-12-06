using Mochi.DearImGui;
using RefreshCS;

namespace MyGame.Editor;

public unsafe class WorldWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "World";
    private IInspector? _cameraInspector;
    private IInspector? _worldInspector;
    private World? _prevWorld;

    private ColorAttachmentBlendState _rimBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _lightBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _customBlendState = ColorAttachmentBlendState.AlphaBlend;

    public WorldWindow() : base(WindowTitle)
    {
        KeyboardShortcut = "^W";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        ImGui.Begin(Title, ImGuiExt.RefPtr(ref IsOpen));

        var world = Shared.Game.World;
        if (!world.IsLoaded)
        {
            ImGui.TextDisabled("World is not loaded");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("Tabs"))
        {
            if (ImGui.BeginTabItem("Rendering"))
            {
                EnumInspector.InspectEnum("TargetBlitPipeline", ref Shared.Game.World.LightsToDestinationBlend, false);
                
                ImGuiExt.SeparatorText("Main");
                if (BlendStateEditor.Draw("MainRimLightBlendState", ref _lightBlendState))
                {
                    Shared.Game.Renderer.Pipelines[PipelineType.Light].Pipeline.Dispose();
                    Shared.Game.Renderer.Pipelines[PipelineType.Light] = Pipelines.CreateLightPipeline(Shared.Game.GraphicsDevice, _lightBlendState);
                    Logs.LogInfo("Recreated main pipeline");
                }
                
                ImGuiExt.SeparatorText("Rim");
                if (BlendStateEditor.Draw("RimLightBlendState", ref _rimBlendState))
                {
                    Shared.Game.Renderer.Pipelines[PipelineType.RimLight].Pipeline.Dispose();
                    Shared.Game.Renderer.Pipelines[PipelineType.RimLight] = Pipelines.CreateRimLightPipeline(Shared.Game.GraphicsDevice, _rimBlendState);
                    Logs.LogInfo("Recreated rim pipeline");
                }
                
                ImGuiExt.SeparatorText("Custom");
                if (BlendStateEditor.Draw("CustomBlendState", ref _customBlendState))
                {
                    Shared.Game.Renderer.Pipelines[PipelineType.CustomBlendState].Pipeline.Dispose();
                    Shared.Game.Renderer.Pipelines[PipelineType.CustomBlendState] = Pipelines.CreateSpritePipeline(Shared.Game.GraphicsDevice, _customBlendState);
                    Logs.LogInfo("Recreated rim pipeline");
                }

                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("World"))
            {
                // var refreshInspector = ImGuiExt.ColoredButton("Refresh");
                if (_prevWorld != world || _worldInspector == null) /* || refreshInspector)*/
                {
                    _worldInspector = InspectorExt.GetGroupInspectorForTarget(world);
                }

                _prevWorld = world;
                _worldInspector.Draw();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Camera"))
            {
                _cameraInspector ??= InspectorExt.GetGroupInspectorForTarget(Shared.Game.Camera);
                _cameraInspector.Draw();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
