using Mochi.DearImGui;
using MyGame.Entities;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

public unsafe class WorldWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "World";
    private IInspector? _cameraInspector;
    private IInspector? _worldInspector;
    private RootJson? _prevRoot;

    private ColorAttachmentBlendState _rimBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _lightBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _customBlendState = ColorAttachmentBlendState.AlphaBlend;
    private bool _isSelectEntityDialogOpen;
    private int _selectedEntityIndex;
    private string _searchPattern = "";
    private string[] _entityNames = Array.Empty<string>();
    private Entity[] _entities = Array.Empty<Entity>();
    private IInspector? _selectedEntityInspector;
    private bool _isPickingEntity;

    public WorldWindow() : base(WindowTitle)
    {
        KeyboardShortcut = "^W";
    }

    private static Num.Vector2 GetWorldPosInScreen(Vector2 position)
    {
        var editor = (MyEditorMain)Shared.Game;
        var view = editor.Camera.GetView(0);
        var posInGameWindow = Vector2.Transform(position, view);
        var viewportTransform = editor.GameWindow.GameRenderViewportTransform;
        var posInScreen = Vector2.Transform(posInGameWindow, viewportTransform) + ImGui.GetMainViewport()->Pos.ToXNA();
        return posInScreen.ToNumerics();
    }

    private static Vector2 GetMouseInWorld()
    {
        var mousePosition = Shared.Game.InputHandler.MousePosition;
        var view = Shared.Game.Camera.GetView(0);
        Matrix3x2.Invert(view, out var invertedView);
        return Vector2.Transform(mousePosition, invertedView);
    }

    public override void Draw()
    {
        if (!IsOpen)
            return;

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
                    Shared.Game.Renderer.Pipelines[PipelineType.CustomBlendState] =
                        Pipelines.CreateSpritePipeline(Shared.Game.GraphicsDevice, _customBlendState);
                    Logs.LogInfo("Recreated rim pipeline");
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("World"))
            {
                // var refreshInspector = ImGuiExt.ColoredButton("Refresh");
                if (_prevRoot != Shared.Game.World.Root || _worldInspector == null) /* || refreshInspector)*/
                {
                    _worldInspector = InspectorExt.GetGroupInspectorForTarget(world);
                    _selectedEntityInspector = null;
                }

                _prevRoot = Shared.Game.World.Root;
                _worldInspector.Draw();

                DrawPickEntityButton(world);

                DrawSelectEntityButton(world);

                _selectedEntityInspector?.Draw();

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

    private void DrawSelectEntityButton(World world)
    {
        if (ImGuiExt.ColoredButton(FontAwesome6.HandPointer + " Select Entity", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            var entityNames = new List<string>();
            var entities = new List<Entity>();
            world.Entities.ForEach((e) =>
            {
                entityNames.Add(e.GetType().Name);
                entities.Add(e);
            });
            _entityNames = entityNames.ToArray();
            _entities = entities.ToArray();
            _isSelectEntityDialogOpen = true;
        }

        if (ImGuiExt.DrawSearchDialog("SelectEntityDialog", "Select",
                ref _isSelectEntityDialogOpen, ref _selectedEntityIndex,
                _entityNames.AsSpan(), ref _searchPattern))
        {
            _isSelectEntityDialogOpen = false;
            var entity = _entities[_selectedEntityIndex];
            _selectedEntityInspector = InspectorExt.GetGroupInspectorForTarget(entity);
        }
    }

    private void DrawPickEntityButton(World world)
    {
        if (ImGuiExt.ColoredButton(FontAwesome6.EyeDropper + " Pick Entity", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
        {
            _isPickingEntity = true;
        }

        if (_isPickingEntity)
        {
            DrawPickEntityOverlay(world);
        }
    }

    private void DrawPickEntityOverlay(World world)
    {
        var windowFlags = ImGuiWindowFlags.NoDecoration |
                          ImGuiWindowFlags.NoDocking |
                          ImGuiWindowFlags.AlwaysAutoResize |
                          ImGuiWindowFlags.NoSavedSettings |
                          ImGuiWindowFlags.NoNav |
                          ImGuiWindowFlags.NoBackground |
                          ImGuiWindowFlags.NoScrollbar |
                          ImGuiWindowFlags.NoScrollWithMouse;

        var editor = (MyEditorMain)Shared.Game;
        ImGui.SetNextWindowPos(editor.GameWindow.GameRenderMin, ImGuiCond.Always, Num.Vector2.Zero);
        ImGui.SetNextWindowSize(editor.GameWindow.GameRenderSize, ImGuiCond.Always);
        if (GameWindow.BeginOverlay("PickEntityOverlay", ref _isPickingEntity, windowFlags, false))
        {
            // if (ImGui.IsWindowHovered())
            MyEditorMain.ActiveInput = ActiveInput.GameWindow;
            var mouseInWorld = GetMouseInWorld();
            var mouseInScreen = GetWorldPosInScreen(mouseInWorld);

            var dl = ImGui.GetWindowDrawList();
            dl->AddRect(mouseInScreen - new Num.Vector2(5, 5), mouseInScreen + new Num.Vector2(5, 5), Color.Green.PackedValue, 0, ImDrawFlags.None,
                4f);
            var hoveredEntities = new List<Entity>();

            dl->AddRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail(), Color.Red.PackedValue, 0,
                ImDrawFlags.None, 4f);

            world.Entities.ForEach(e =>
            {
                if (e.Bounds.Contains(mouseInWorld))
                {
                    hoveredEntities.Add(e);
                }
            });

            var i = 0;
            foreach (var entity in hoveredEntities)
            {
                var min = GetWorldPosInScreen(entity.Bounds.Min);
                var max = GetWorldPosInScreen(entity.Bounds.Max);
                dl->AddRect(min, max, Color.Cyan.PackedValue, 0, ImDrawFlags.None, 4f);
                ImGui.SetCursorScreenPos(min);
                var size = max - min;
                if (ImGui.InvisibleButton("Entity" + i, size))
                {
                    _selectedEntityInspector = InspectorExt.GetGroupInspectorForTarget(entity);
                    _isPickingEntity = false;
                    break;
                }
            }
        }

        ImGui.End();
    }
}
