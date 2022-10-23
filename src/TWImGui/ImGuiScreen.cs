using ImGuiNET;
using MyGame.Utils;

namespace MyGame.TWImGui;

public class BlendStateEditor
{
    private static readonly string[] _blendOpNames;
    private static readonly string[] _blendFactorNames;

    static BlendStateEditor()
    {
        _blendOpNames = Enum.GetNames<BlendOp>();
        _blendFactorNames = Enum.GetNames<BlendFactor>();
    }

    private static bool AreEqual(ColorAttachmentBlendState a, ColorAttachmentBlendState b)
    {
        return a.BlendEnable == b.BlendEnable &&
               a.AlphaBlendOp == b.AlphaBlendOp &&
               a.ColorBlendOp == b.ColorBlendOp &&
               a.SourceColorBlendFactor == b.SourceColorBlendFactor &&
               a.SourceAlphaBlendFactor == b.SourceAlphaBlendFactor &&
               a.DestinationColorBlendFactor == b.DestinationColorBlendFactor &&
               a.DestinationAlphaBlendFactor == b.DestinationAlphaBlendFactor;
    }

    public static bool ComboStep(string label, ref int currentIndex, string[] items)
    {
        var result = false;

        ImGui.PushStyleColor(ImGuiCol.Button, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Color.Transparent.PackedValue);
        
        if (ImGui.Button(FontAwesome6.ChevronLeft + "##Left" + label))
        {
            currentIndex = (items.Length + currentIndex - 1) % items.Length;
            result = true;
        }

        ImGui.SameLine(0, 0);
        if (ImGui.Button(FontAwesome6.ChevronRight + "##Right" + label))
        {
            currentIndex = (items.Length + currentIndex + 1) % items.Length;
            result = true;
        }

        ImGui.PopStyleColor(3);
        
        ImGui.SameLine();
        result |= ImGui.Combo(label, ref currentIndex, items, items.Length);

        return result;
    }

    public static bool Draw(string name, ref ColorAttachmentBlendState state)
    {
        ImGui.PushID(name);
        var alphaBlendOpIndex = (int)state.AlphaBlendOp;
        var colorBlendOpIndex = (int)state.ColorBlendOp;
        var destColorBlendFactorIndex = (int)state.DestinationColorBlendFactor;
        var destAlphaBlendFactorIndex = (int)state.DestinationAlphaBlendFactor;
        var sourceColorBlendFactorIndex = (int)state.SourceColorBlendFactor;
        var sourceAlphaBlendFactorIndex = (int)state.SourceAlphaBlendFactor;
        var blendEnabled = state.BlendEnable;
        var prevState = state;
        ImGui.Checkbox("Enabled", ref blendEnabled);
        ComboStep("AlphaOp", ref alphaBlendOpIndex, _blendOpNames);
        ComboStep("ColorOp", ref colorBlendOpIndex, _blendOpNames);
        ComboStep("SourceColor", ref sourceColorBlendFactorIndex, _blendFactorNames);
        ComboStep("SourceAlpha", ref sourceAlphaBlendFactorIndex, _blendFactorNames);
        ComboStep("DestColor", ref destColorBlendFactorIndex, _blendFactorNames);
        ComboStep("DestAlpha", ref destAlphaBlendFactorIndex, _blendFactorNames);
        state.BlendEnable = blendEnabled;
        state.AlphaBlendOp = (BlendOp)alphaBlendOpIndex;
        state.ColorBlendOp = (BlendOp)colorBlendOpIndex;
        state.SourceColorBlendFactor = (BlendFactor)sourceColorBlendFactorIndex;
        state.SourceAlphaBlendFactor = (BlendFactor)sourceAlphaBlendFactorIndex;
        state.DestinationColorBlendFactor = (BlendFactor)destColorBlendFactorIndex;
        state.DestinationAlphaBlendFactor = (BlendFactor)destAlphaBlendFactorIndex;
        /// Blend equation is sourceColor * sourceBlend + destinationColor * destinationBlend
        ImGui.Text($"sourceColor * {state.SourceColorBlendFactor} + destColor * {state.DestinationColorBlendFactor}");
        ImGui.Text($"sourceAlpha * {state.SourceAlphaBlendFactor} + destAlpha * {state.DestinationAlphaBlendFactor}");
        if (ImGui.Button("AlphaBlend"))
        {
            state = ColorAttachmentBlendState.AlphaBlend;
        }

        ImGui.SameLine();
        if (ImGui.Button("NonPremultiplied"))
        {
            state = ColorAttachmentBlendState.NonPremultiplied;
        }

        ImGui.SameLine();
        if (ImGui.Button("Opaque"))
        {
            state = ColorAttachmentBlendState.Opaque;
        }

        ImGui.PopID();
        return !AreEqual(prevState, state);
    }
}

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
    private ulong _imGuiDrawCount;
    private Texture? _lastRender;
    private int _updateFps = 60;
    private float _updateRate = 1 / 60f;
    private float _lastRenderTime;

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
