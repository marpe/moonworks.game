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
    private MyGameMain _game;
    private float _alpha = 1.0f;
    private readonly Sampler _sampler;

    public ImGuiScreen(MyGameMain game)
    {
        _game = game;
        _sampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.PointClamp);
        var timer = Stopwatch.StartNew();
        _imGuiRenderer = new ImGuiRenderer(game);
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
            new ImGuiCallbackWindow("TestWindow", DrawTestWindow)
            {
                IsOpen = true
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

    public void Draw(SpriteBatch spriteBatch, Texture depthTexture, GraphicsPipeline pipeline, CommandBuffer commandBuffer,
        Texture swapchainTexture)
    {
        _imGuiRenderer.Begin((float)_game.Timestep.TotalSeconds);
        DrawInternal();
        var render = _imGuiRenderer.End();
        var sprite = new Sprite(render);
        spriteBatch.Start(new TextureSamplerBinding(sprite.Texture, _sampler));
        spriteBatch.Add(sprite, Color.White, 0, Matrix3x2.Identity);
        spriteBatch.PushVertexData(commandBuffer);

        commandBuffer.BeginRenderPass(new DepthStencilAttachmentInfo(depthTexture, new DepthStencilValue(0, 0)),
            new ColorAttachmentInfo(swapchainTexture, LoadOp.Load));
        commandBuffer.BindGraphicsPipeline(pipeline);
        var view = Matrix4x4.CreateLookAt(
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 0),
            Vector3.Up
        );
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0,
            swapchainTexture.Width,
            swapchainTexture.Height,
            0,
            0.0001f,
            4000f
        );
        var viewProjection = view * projection;
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);
        spriteBatch.Draw(commandBuffer, vertexParamOffset);
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
            ImGui.SliderFloat("Alpha", ref _alpha, 0, 1.0f);
        }

        ImGui.End();
    }

    private void DrawMenu()
    {
        var result = ImGui.BeginMainMenuBar();
        if (result)
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Quit"))
                {
                    _game.Quit();
                }

                ImGui.EndMenu();
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
