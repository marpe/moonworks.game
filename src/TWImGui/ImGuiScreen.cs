using ImGuiNET;

namespace MyGame.TWImGui;

public class ImGuiScreen
{
    internal SortedList<string, ImGuiWindow> Windows = new();

    public ImGuiWindow GetWindow(string windowName) => Windows[windowName];

    public Vector2 MousePositionInWorld;
    public Vector2 MousePosition;

    private ImGuiWindow _imGuiDemoWindow = new ImGuiCallbackWindow("ImGui Demo Window", ShowImGuiDemoWindow)
    {
        IsOpen = true
    };

    private readonly ImGuiRenderer _imGuiRenderer;
    private Game _game;

    public ImGuiScreen(Game game)
    {
        _game = game;
        var timer = Stopwatch.StartNew();
        _imGuiRenderer = new ImGuiRenderer(game);
        _imGuiRenderer.RebuildFontAtlas();
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        Logger.LogInfo($"ImGuiInit: {timer.ElapsedMilliseconds} ms");
    }

    private static void ShowImGuiDemoWindow(ImGuiWindow window)
    {
        if (!window.IsOpen)
            return;
        ImGui.ShowDemoWindow(ref window.IsOpen);
    }

    private void AddDefaultWindows()
    {
        var windows = new[]
        {
            _imGuiDemoWindow,
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    public void Update()
    {
        _imGuiRenderer.UpdateInput();
    }

    public void Draw(CommandBuffer commandBuffer, Texture swapchainTexture)
    {
        _imGuiRenderer.Begin((float)_game.Timestep.TotalSeconds);
        DrawInternal();
        _imGuiRenderer.End(commandBuffer, swapchainTexture);
    }

    private void DrawInternal()
    {
        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        var drawList = ImGui.GetBackgroundDrawList();
        
        ImGui.Begin("ImGuiWindow1");
        ImGui.Text("ImGui Window 1");
        ImGui.End();

        foreach (var (key, window) in Windows)
        {
            var keyboardShortcut = window.KeyboardShortcut;
            if (keyboardShortcut != null && keyboardShortcut.Length > 0 && !ImGui.GetIO().WantCaptureKeyboard)
            {
                var result = true;
                for (var j = 0; j < keyboardShortcut.Length; j++)
                {
                    if (keyboardShortcut[j] == '^')
                        result = result && (ImGui.IsKeyDown((int)KeyCode.LeftControl) ||
                                            ImGui.IsKeyDown((int)KeyCode.RightControl));
                    else if (keyboardShortcut[j] == '+')
                        result = result && (ImGui.IsKeyDown((int)KeyCode.LeftShift) ||
                                            ImGui.IsKeyDown((int)KeyCode.RightShift));
                    else if (keyboardShortcut[j] == '!')
                        result = result && (ImGui.IsKeyDown((int)KeyCode.LeftAlt) ||
                                            ImGui.IsKeyDown((int)KeyCode.RightAlt));
                    else
                        result = result && ImGui.IsKeyPressed((int)Enum.Parse<KeyCode>(keyboardShortcut.AsSpan().Slice(j, 1)));
                }

                if (result)
                    window.IsOpen = !window.IsOpen;
            }

            window.Draw();
        }
    }
}
