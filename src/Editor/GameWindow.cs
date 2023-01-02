using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Entities;
using Vector2 = System.Numerics.Vector2;

namespace MyGame.Editor;

public unsafe class GameWindow : ImGuiEditorWindow
{
    public const string WindowTitle = "Game";

    private MyEditorMain _editor;

    private static bool _showDebug;

    public GameRenderView GameRenderView;

    public GameWindow(MyEditorMain editor) : base(WindowTitle)
    {
        _editor = editor;
        KeyboardShortcut = "^F1";
        IsOpen = true;

        GameRenderView = new();
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
        var result = ImGui.Begin(WindowTitle, default, flags);
        
        if (result)
        {
            ImGui.PopStyleVar();
            DrawWindowContent();
            DrawDebugOverlay();
        }

        if (!result)
        {
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }

    private void DrawWindowContent()
    {
        GameRenderView.Draw("GameRender", _editor.RenderTargets.CompositeRender.Target);

        if (ImGui.BeginPopupContextWindow("GameContextMenu", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
        {
            ImGui.MenuItem("Enable mouse pan & zoom", default, ImGuiExt.RefPtr(ref GameRenderView.IsMousePanAndZoomEnabled));
            if (GameRenderView.IsPanZoomDirty && ImGui.MenuItem("Reset pan & zoom", default))
                GameRenderView.ResetPanAndZoom();

            ImGui.Separator();
            ImGui.MenuItem("Show debug overlay", default, ImGuiExt.RefPtr(ref _showDebug));
            ImGui.Separator();
            ImGui.MenuItem("Draw debug", default, ImGuiExt.RefPtr(ref World.Debug));
            if (World.Debug)
            {
                ImGui.MenuItem("Draw mouse debug", default, ImGuiExt.RefPtr(ref World.DebugMouse));
                ImGui.MenuItem("Draw camera debug", default, ImGuiExt.RefPtr(ref World.DebugCamera));
                ImGui.MenuItem("Draw level debug", default, ImGuiExt.RefPtr(ref World.DebugLevel));
            }

            ImGui.EndPopup();
        }
    }

    public static bool BeginOverlay(string name, ref bool showDebug, ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDecoration |
                                                                                                    ImGuiWindowFlags.NoDocking |
                                                                                                    ImGuiWindowFlags.AlwaysAutoResize |
                                                                                                    ImGuiWindowFlags.NoSavedSettings |
                                                                                                    ImGuiWindowFlags.NoNav,
        bool setViewport = true)
    {
        ImGui.SetNextWindowBgAlpha(0.8f);

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
        var condFlags = ImGuiCond.Always;

        if ((ImGui.GetCurrentContext()->NextWindowData.Flags & ImGuiNextWindowDataFlags.HasPos) == 0)
        {
            ImGui.SetNextWindowPos(overlayPos, condFlags, windowPosPivot);
        }

        if (setViewport)
            ImGui.SetNextWindowViewport(ImGui.GetWindowViewport()->ID);
        return ImGui.Begin(name, ImGuiExt.RefPtr(ref showDebug), windowFlags);
    }

    private static void DrawDebugOverlay()
    {
        if (!_showDebug)
            return;

        if (BeginOverlay("GameWindowOverlay", ref _showDebug))
        {
            /*ImGui.Text(
                $"Min: {gameRenderMin.ToString()}, Max: {gameRenderMax.ToString()}\n" +
                $"IsHoveringGame: {IsHoveringGame.ToString()}\n" +
                $"GameRenderOffset: {gameRenderOffset.ToString()}");
    
            ImGui.Separator();*/
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.PushFont(ImGuiExt.GetFont(ImGuiFont.Tiny));

            var editor = (MyEditorMain)Shared.Game;

            ImGuiExt.SeparatorText("Mouse", Color.White);
            var mousePosition = editor.InputHandler.MousePosition;
            ImGuiExt.PrintVector("Pos", mousePosition);
            var mouseInWorld = World.GetMouseInWorld();
            ImGuiExt.PrintVector("World", mouseInWorld);
            var mouseCell = Entity.ToCell(mouseInWorld);
            ImGuiExt.PrintVector("Cel", mouseCell);

            var world = editor.World;
            if (world.IsLoaded)
            {
                var player = world.Entities.First<Player>();
                var playerCell = player.Cell;
                ImGuiExt.SeparatorText("Player", Color.White);
                ImGuiExt.PrintVector("Cell", playerCell);
                ImGuiExt.PrintVector("Pos", player.Position.Current);
            }

            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Close", default)) _showDebug = !_showDebug;
                ImGui.EndPopup();
            }

            ImGui.Dummy(new Vector2(300, 0));

            ImGui.PopFont();
            ImGui.PopStyleVar();
        }

        ImGui.End();
    }
}
