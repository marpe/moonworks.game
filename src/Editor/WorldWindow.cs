using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Entities;
using MyGame.WorldsRoot;

namespace MyGame.Editor;

public unsafe class WorldWindow : ImGuiEditorWindow
{
    private class EntityInspector
    {
        public Entity Entity;
        public IInspector Inspector;

        public EntityInspector(Entity entity)
        {
            Entity = entity;
            Inspector = InspectorExt.GetGroupInspectorForTarget(entity);
        }
    }

    public const string WindowTitle = "World";
    private IInspector? _cameraInspector;
    private IInspector? _worldInspector;
    private RootJson? _prevRoot;
    private Level? _prevLevel;

    private ColorAttachmentBlendState _rimBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _lightBlendState = ColorAttachmentBlendState.Additive;
    private ColorAttachmentBlendState _customBlendState = ColorAttachmentBlendState.AlphaBlend;
    private bool _isSelectEntityDialogOpen;
    private int _selectedEntityIndex;
    private string _searchPattern = "";
    private string[] _entityNames = Array.Empty<string>();
    private Entity[] _entities = Array.Empty<Entity>();
    private EntityInspector? _selectedEntity;

    public WorldWindow() : base(WindowTitle)
    {
        KeyboardShortcut = "^W";
    }

    private static Num.Vector2 GetWorldPosInScreen(Vector2 position)
    {
        var editor = (MyEditorMain)Shared.Game;
        var posInGameWindow = World.GetWorldPosInScreen(position);
        var viewportTransform = editor.GameWindow.GameRenderView.GameRenderViewportTransform;
        var posInScreen = Vector2.Transform(posInGameWindow, viewportTransform) + ImGui.GetMainViewport()->Pos.ToXNA();
        return posInScreen.ToNumerics();
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

        var labelWidthRatio = 0.4f;
        var labelWidth = (int)(ImGui.GetContentRegionAvail().X * labelWidthRatio);
        ImGuiExt.PushLabelWidth(labelWidth);
        
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
                if (_prevRoot != Shared.Game.World.Root ||
                    _prevLevel != Shared.Game.World.Level ||
                    _worldInspector == null)
                {
                    _worldInspector = InspectorExt.GetGroupInspectorForTarget(world);
                    _selectedEntity = null;
                }

                _prevRoot = Shared.Game.World.Root;
                _prevLevel = Shared.Game.World.Level;

                _worldInspector.Draw();

                DrawPickEntityButton(world);

                DrawSelectEntityButton(world);

                DrawSelectedEntity();

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
        
        ImGuiExt.PopLabelWidth();

        ImGui.End();
    }

    private void DrawSelectedEntity()
    {
        if (_selectedEntity == null)
            return;

        if (_selectedEntity.Entity.IsDestroyed)
        {
            _selectedEntity = null;
            return;
        }

        _selectedEntity.Inspector.Draw();
    }

    private void DrawSelectEntityButton(World world)
    {
        ImGui.SameLine();
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

        if (ImGuiExt.DrawSearchDialog("Select Entity", "Select",
                ref _isSelectEntityDialogOpen, ref _selectedEntityIndex,
                _entityNames.AsSpan(), ref _searchPattern))
        {
            _isSelectEntityDialogOpen = false;
            var entity = _entities[_selectedEntityIndex];
            _selectedEntity = new EntityInspector(entity);
        }
    }

    private void DrawPickEntityButton(World world)
    {
        if (ImGuiExt.ColoredButton(FontAwesome6.EyeDropper + " Pick Entity", new Num.Vector2(ImGui.GetContentRegionAvail().X * 0.5f, 0)))
        {
            ImGui.OpenPopup("PickEntityOverlay");
        }

        DrawPickEntityOverlay(world);
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
        var gameRenderMin = editor.GameWindow.GameRenderView.GameRenderMin;
        var gameRenderMax = editor.GameWindow.GameRenderView.GameRenderMax;
        var gameRenderSize = gameRenderMax - gameRenderMin;
        ImGui.SetNextWindowPos(gameRenderMin, ImGuiCond.Always, Num.Vector2.Zero);
        ImGui.SetNextWindowSize(gameRenderSize, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0);
        ImGui.SetNextWindowViewport(ImGui.GetMainViewport()->ID);
        if (ImGui.BeginPopup("PickEntityOverlay", windowFlags))
        {
            // if (ImGui.IsWindowHovered())
            MyEditorMain.ActiveInput = ActiveInput.GameWindow;
            var mouseInWorld = World.GetMouseInWorld();
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
                if (ImGui.InvisibleButton("Entity" + i, size, (ImGuiButtonFlags)ImGuiButtonFlagsPrivate_.ImGuiButtonFlags_AllowItemOverlap))
                {
                    _selectedEntity = new EntityInspector(entity);
                    ImGui.CloseCurrentPopup();
                    break;
                }

                ImGui.SetItemAllowOverlap();
            }

            ImGui.EndPopup();
        }
    }
}
