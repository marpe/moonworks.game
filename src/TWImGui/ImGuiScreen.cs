using ImGuiNET;
using MyGame.Utils;

namespace MyGame.TWImGui;

public class ImGuiScreen
{
    internal SortedList<string, ImGuiWindow> Windows = new();

    public ImGuiWindow GetWindow(string windowName) => Windows[windowName];

    public Vector2 MousePositionInWorld;
    public Vector2 MousePosition;

    private readonly ImGuiRenderer _imGuiRenderer;
    private MyGameMain _game;
    private float _alpha = 1.0f;
    private readonly Sampler _sampler;
    private ulong _imGuiDrawCount;
    private Texture? _lastRender;
    private int _updateFps = 60;
    private float _updateRate = 1 / 60f;
    private float _lastRenderTime;
    private readonly string[] _blendStateNames;
    private List<ImGuiMenu> _menuItems = new();

    public ImGuiScreen(MyGameMain game)
    {
        _game = game;
        _sampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.PointClamp);
        var timer = Stopwatch.StartNew();
        _imGuiRenderer = new ImGuiRenderer(game);
        _blendStateNames = Enum.GetNames<BlendState>();
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        Logger.LogInfo($"ImGuiInit: {timer.ElapsedMilliseconds} ms");
        AddDefaultMenus();
    }

    private void AddDefaultMenus()
    {
        var file = new ImGuiMenu("File")
            .AddChild(new ImGuiMenu("Quit", "^Q", () => _game.Quit()));
        _menuItems.Add(file);
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
            new ImGuiCallbackWindow("ImGui Demo Window", ShowImGuiDemoWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F1"
            },
            new ImGuiCallbackWindow("TestWindow", DrawTestWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F2"
            }
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    public void Update()
    {
    }

    public void Draw(Texture depthTexture, CommandBuffer commandBuffer, Texture swapchainTexture)
    {
        if (_lastRender == null || _game.TotalElapsedTime - _lastRenderTime >= _updateRate)
        {
            _imGuiDrawCount++;
            _imGuiRenderer.Begin((float)_game.Timestep.TotalSeconds);
            DrawInternal();
            _lastRender = _imGuiRenderer.End();
            _lastRenderTime = _game.TotalElapsedTime;
        }

        var sprite = new Sprite(_lastRender);
        _game.SpriteBatch.AddSingle(commandBuffer, sprite, Color.White, 0, Matrix3x2.Identity);

        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(depthTexture, new DepthStencilValue(0, 0)),
            new ColorAttachmentInfo(swapchainTexture, LoadOp.Load)
        );
        _game.SpriteBatch.Draw(commandBuffer, swapchainTexture.Width, swapchainTexture.Height);
        commandBuffer.EndRenderPass();
    }

    private void DrawTestWindow(ImGuiWindow window)
    {
        if (!window.IsOpen)
            return;
        ImGui.SetNextWindowBgAlpha(_alpha);

        if (ImGuiExt.Begin(window.Title, ref window.IsOpen))
        {
            ImGui.Text("ImGui Window 1");
            ImGui.TextUnformatted($"FrameCount: {_game.FrameCount}");
            ImGui.TextUnformatted($"Total: {_game.TotalElapsedTime}");
            ImGui.TextUnformatted($"Elapsed: {_game.ElapsedTime}");
            ImGui.TextUnformatted($"RenderCount: {_game.RenderCount}");
            ImGui.TextUnformatted($"ImGuiDrawCount: {_imGuiDrawCount}");
            if (ImGui.SliderInt("UpdateFPS", ref _updateFps, 1, 120))
            {
                _updateRate = 1.0f / _updateFps;
            }

            ImGui.SliderFloat("Alpha", ref _alpha, 0, 1.0f);
            ImGui.Separator();
            var spriteBatchBlendStateIndex = (int)_game.SpriteBatch.BlendState;
            if (ImGui.BeginCombo("SpriteBatchBlendState", _blendStateNames[spriteBatchBlendStateIndex]))
            {
                for (var i = 0; i < _blendStateNames.Length; i++)
                {
                    var isSelected = i == spriteBatchBlendStateIndex;
                    if (ImGui.Selectable(_blendStateNames[i], isSelected))
                        _game.SpriteBatch.BlendState = (BlendState)i;
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            if (BlendStateEditor.Draw("SpriteBatch", ref _game.SpriteBatch.CustomBlendState))
            {
                _game.SpriteBatch.UpdateCustomBlendPipeline();
            }

            ImGui.Separator();
            var blendState = _imGuiRenderer.BlendState;
            if (BlendStateEditor.Draw("ImGui", ref blendState))
            {
                _imGuiRenderer.SetBlendState(blendState);
            }
        }

        ImGui.End();
    }

    private void DrawMenu(ImGuiMenu menu)
    {
        if (menu.Children.Count > 0)
        {
            if (ImGui.BeginMenu(menu.Text, menu.IsEnabled ?? true))
            {
                foreach (var child in menu.Children)
                {
                    DrawMenu(child);
                }

                ImGui.EndMenu();
            }
        }
        else
        {
            if (ImGui.MenuItem(menu.Text, menu.Shortcut))
            {
                menu.Callback?.Invoke();
            }
        }
    }

    private void CheckMenuShortcuts(ImGuiMenu menu)
    {
        if (!(menu.IsEnabled ?? true))
            return;

        if (ImGuiExt.IsKeyboardShortcutPressed(menu.Shortcut))
        {
            menu.Callback?.Invoke();
        }

        foreach (var child in menu.Children)
        {
            CheckMenuShortcuts(child);
        }
    }

    private void DrawMenu()
    {
        var result = ImGui.BeginMainMenuBar();
        if (result)
        {
            foreach (var menu in _menuItems)
            {
                DrawMenu(menu);
            }

            foreach (var menu in _menuItems)
            {
                CheckMenuShortcuts(menu);
            }

            if (ImGui.BeginMenu("Window"))
            {
                foreach (var (key, window) in Windows)
                {
                    ImGui.MenuItem(window.Title, window.KeyboardShortcut, ref window.IsOpen);
                }

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    private void DrawInternal()
    {
        if (ImGui.IsAnyItemHovered())
        {
            var cursor = ImGui.GetMouseCursor();
            if (cursor == ImGuiMouseCursor.Arrow)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        DrawMenu();

        var drawList = ImGui.GetBackgroundDrawList();

        DrawWindows();
    }

    private void DrawWindows()
    {
        foreach (var (key, window) in Windows)
        {
            if (!ImGui.GetIO().WantTextInput)
            {
                if (ImGuiExt.IsKeyboardShortcutPressed(window.KeyboardShortcut))
                    window.IsOpen = !window.IsOpen;
            }

            window.Draw();
        }
    }
}
