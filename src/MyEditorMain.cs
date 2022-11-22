using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Editor;
using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame;

public unsafe class MyEditorMain : MyGameMain
{
    private Texture _gameRender;

    [CVar("imgui.hidden", "Toggle ImGui screen")]
    public static bool IsHidden = true;

    private readonly string[] _blendStateNames;

    private readonly ImGuiRenderer _imGuiRenderer;
    private readonly Sampler _sampler;
    private readonly float _alpha = 1.0f;
    private bool _doRender = false;
    private ulong _imGuiDrawCount;
    private readonly List<InputState> _inputStates = new();
    private readonly float _mainMenuPaddingY = 6f;
    private readonly List<ImGuiMenu> _menuItems = new();
    private int _updateRate = 1;
    internal SortedList<string, ImGuiEditorWindow> Windows = new();
    private float _prevElapsedTime;
    private bool _firstTime = true;
    private static readonly string _debugWindowName = "MyGame Debug";
    private static string _imguiDemoWindowName = "ImGui Demo Window";
    private string _gameWindowName = "Game";
    private IntPtr? _gameRenderTextureId;
    private Texture _imGuiRenderTarget;
    
    [CVar("screenshot", "Save a screenshot")]
    public static bool Screenshot;

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep, bool debugMode) : base(
        windowCreateInfo,
        frameLimiterSettings, targetTimestep, debugMode)
    {
        var sz = MyGameMain.DesignResolution;
        _gameRender = Texture.CreateTexture2D(GraphicsDevice, sz.X, sz.Y, TextureFormat.B8G8R8A8, TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);
        _imGuiRenderTarget =
            Texture.CreateTexture2D(GraphicsDevice, sz.X, sz.Y, TextureFormat.B8G8R8A8, TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

        var timer = Stopwatch.StartNew();
        _sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);
        _imGuiRenderer = new ImGuiRenderer(this);
        _blendStateNames = Enum.GetNames<BlendState>();
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        AddDefaultMenus();
        Logger.LogInfo($"ImGuiInit: {timer.ElapsedMilliseconds} ms");
    }

    private void AddDefaultMenus()
    {
        var file = new ImGuiMenu("File")
            .AddChild(new ImGuiMenu("Quit", "^Q", () => Quit()));
        _menuItems.Add(file);
        var imgui = new ImGuiMenu("ImGui")
            .AddChild(new ImGuiMenu("Debug Inspectors", null, () => { ImGuiExt.DebugInspectors = !ImGuiExt.DebugInspectors; }));
        _menuItems.Add(imgui);
    }

    private static void ShowImGuiDemoWindow(ImGuiEditorWindow window)
    {
        if (!window.IsOpen)
            return;

        fixed (bool* isOpen = &window.IsOpen)
        {
            ImGui.ShowDemoWindow(isOpen);
        }
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiEditorWindow[]
        {
            new ImGuiEditorCallbackWindow(_debugWindowName, DrawDebugWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F1",
            },
            new ImGuiEditorCallbackWindow(_imguiDemoWindowName, ShowImGuiDemoWindow)
            {
                IsOpen = false,
                KeyboardShortcut = "^F2",
            },
            new ImGuiEditorCallbackWindow(_gameWindowName, DrawGameWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F3",
            },
            new WorldWindow(),
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    protected override void SetInputViewport()
    {
        if (IsHidden)
            base.SetInputViewport();
    }

    private void DrawGameWindow(ImGuiEditorWindow window)
    {
        if (!window.IsOpen)
        {
            return;
        }

        var flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
        if (ImGui.Begin(window.Title, ImGuiExt.RefPtr(ref window.IsOpen), flags))
        {
            if (_gameRenderTextureId != null && _gameRenderTextureId != _gameRender.Handle)
            {
                _imGuiRenderer.UnbindTexture(_gameRenderTextureId.Value);
                _gameRenderTextureId = null;
                Logger.LogInfo("Unbinding gameRender texture");
            }

            if (_gameRenderTextureId == null)
            {
                Logger.LogInfo("Binding gameRender texture");
                _gameRenderTextureId = _imGuiRenderer.BindTexture(_gameRender);
            }

            var windowSize = ImGui.GetWindowSize();
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(new Point((int)windowSize.X, (int)windowSize.Y), DesignResolution);

            ImGui.SetCursorPos(new Num.Vector2(viewport.X, viewport.Y));
            var cursorScreenPos = ImGui.GetCursorScreenPos();
            var borderColor = new Num.Vector4(1.0f, 0, 0, 0.0f);
            ImGui.Image((void*)_gameRenderTextureId.Value, new Num.Vector2(viewport.Width, viewport.Height), Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.One,
                borderColor);

            ImGui.SetCursorPos(ImGui.GetCursorStartPos());

            var viewportPos = ImGui.GetWindowViewport()->Pos;
            var renderOffset = cursorScreenPos - viewportPos;
            viewportTransform.Decompose(out var scale, out _, out _);
            viewportTransform = (Matrix3x2.CreateScale(scale.X, scale.Y) *
                                 Matrix3x2.CreateTranslation(renderOffset.X, renderOffset.Y)).ToMatrix4x4();

            InputHandler.SetViewportTransform(viewportTransform);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetNextFrameWantCaptureMouse(false);
                ImGui.SetNextFrameWantCaptureKeyboard(false);
            }
        }

        ImGui.PopStyleVar();
        ImGui.End();
    }

    private static string LoadingIndicator(bool isLoading)
    {
        if (!isLoading)
            return "";
        var n = (int)(Shared.Game.Time.TotalElapsedTime * 4 % 4);
        return " " + n switch
        {
            0 => FontAwesome6.ArrowRight,
            1 => FontAwesome6.ArrowDown,
            2 => FontAwesome6.ArrowLeft,
            _ => FontAwesome6.ArrowUp,
        };
    }

    private void DrawDebugWindow(ImGuiEditorWindow window)
    {
        if (!window.IsOpen)
        {
            return;
        }

        ImGui.SetNextWindowBgAlpha(_alpha);

        if (ImGuiExt.Begin(window.Title, ref window.IsOpen))
        {
            var io = ImGui.GetIO();
            ImGui.TextUnformatted($"Nav: {(io->NavActive ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureMouse: {(io->WantCaptureMouse ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureKeyboard: {(io->WantCaptureKeyboard ? "Y" : "N")}");
            ImGui.TextUnformatted($"DrawFps: {Time.DrawFps}");
            ImGui.TextUnformatted($"UpdateFps: {Time.UpdateFps}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");

            ImGui.TextUnformatted($"NumDrawCalls: {Renderer.SpriteBatch.DrawCalls}, AddedSprites: {Renderer.SpriteBatch.LastNumAddedSprites}");

            ImGui.BeginDisabled(Shared.LoadingScreen.IsLoading);
            if (ImGui.Button("Reload World" + LoadingIndicator(Shared.LoadingScreen.IsLoading), default))
            {
                GameScreen.Restart();
            }

            ImGui.EndDisabled();

            ImGui.SliderFloat("ShakeSpeed", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeSpeed), 0, 500, default);
            ImGui.SliderFloat("ShakeAmount", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeAmount), 0, 500, default);
            ImGui.SliderInt("UpdateRate", ImGuiExt.RefPtr(ref _updateRate), 1, 10, default);
        }

        ImGui.End();
    }

    private void DrawMenu(ImGuiMenu menu, int depth = 0)
    {
        if (menu.Children.Count > 0)
        {
            if (depth == 0)
            {
                var style = ImGui.GetStyle();
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style->FramePadding.X, _mainMenuPaddingY));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
            }

            var result = ImGui.BeginMenu(menu.Text, menu.IsEnabled ?? true);
            if (depth == 0)
            {
                ImGui.PopStyleVar(2);
            }

            if (result)
            {
                foreach (var child in menu.Children)
                {
                    DrawMenu(child, depth + 1);
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
        {
            return;
        }

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
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style->FramePadding.X, _mainMenuPaddingY));
        var result = ImGui.BeginMainMenuBar();
        ImGui.PopStyleVar();
        if (!result)
        {
            return;
        }

        foreach (var menu in _menuItems)
        {
            DrawMenu(menu);
        }

        foreach (var menu in _menuItems)
        {
            CheckMenuShortcuts(menu);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style->FramePadding.X, _mainMenuPaddingY));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
        var windowMenu = ImGui.BeginMenu("Window");
        ImGui.PopStyleVar(2);
        if (windowMenu)
        {
            foreach (var (key, window) in Windows)
            {
                ImGui.MenuItem(window.Title, window.KeyboardShortcut, ImGuiExt.RefPtr(ref window.IsOpen));
            }

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    private void DrawInternal()
    {
        var mainViewport = ImGui.GetMainViewport();
        var dockId = ImGui.DockSpaceOverViewport(mainViewport, ImGuiDockNodeFlags.PassthruCentralNode);

        if (_firstTime)
        {
            ImGuiInternal.DockBuilderRemoveNodeChildNodes(dockId);

            var leftWidth = 0.15f;
            var dockLeft = ImGuiInternal.DockBuilderSplitNode(dockId, ImGuiDir.Left, leftWidth, null, &dockId);
            var rightWidth = leftWidth / (1.0f - leftWidth); // 1.0f / (1.0f - leftWidth) - 1.0f;
            var dockDown = ImGuiInternal.DockBuilderSplitNode(dockId, ImGuiDir.Right, rightWidth, null, &dockId);
            ImGuiInternal.DockBuilderDockWindow(_debugWindowName, dockLeft);
            ImGuiInternal.DockBuilderDockWindow(WorldWindow.WindowTitle, dockDown);
            ImGuiInternal.DockBuilderFinish(dockId);
            _firstTime = false;
        }

        DrawMenu();

        var drawList = ImGui.GetBackgroundDrawList();

        DrawWindows(dockId);

        SetMouseCursor();
    }

    private void SetMouseCursor()
    {
        if (!ImGui.IsAnyItemHovered())
            return;

        if (ImGui.GetMouseCursor() != ImGuiMouseCursor.Arrow)
            return;

        var cursor = ImGui.GetCurrentContext()->HoveredIdDisabled switch
        {
            true => ImGuiMouseCursor.NotAllowed,
            _ => ImGuiMouseCursor.Hand
        };

        ImGui.SetMouseCursor(cursor);
    }

    private void DrawWindows(uint dockId)
    {
        foreach (var (key, window) in Windows)
        {
            if (!ImGui.GetIO()->WantTextInput)
            {
                if (ImGuiExt.IsKeyboardShortcutPressed(window.KeyboardShortcut))
                {
                    window.IsOpen = !window.IsOpen;
                }
            }

            ImGui.SetNextWindowDockID(dockId, ImGuiCond.FirstUseEver);
            window.Draw();
        }
    }

    protected override void Update(TimeSpan dt)
    {
        var inputHandler = InputHandler;

        if (!IsHidden)
        {
            var newState = InputState.Create(inputHandler);
            _inputStates.Add(newState);

            if ((int)Time.UpdateCount % _updateRate == 0)
            {
                var inputState = InputState.Aggregate(_inputStates);
                _inputStates.Clear();
                var deltaTime = Time.TotalElapsedTime - _prevElapsedTime;
                _imGuiRenderer.Update(deltaTime, _imGuiRenderTarget.Size(), inputState);
                _prevElapsedTime = Time.TotalElapsedTime;
                _doRender = true;
            }

            var io = ImGui.GetIO();
            if (io->WantCaptureKeyboard)
                InputHandler.KeyboardEnabled = false;
            if (io->WantCaptureMouse)
                InputHandler.MouseEnabled = false;
        }

        base.Update(dt);
    }

    protected override void Draw(double alpha)
    {
        if (IsHidden)
        {
            base.Draw(alpha);
            return;
        }

        if (MainWindow.IsMinimized)
            return;

        if (Screenshot)
        {
            SaveRender(GraphicsDevice, _gameRender);
            Logger.LogInfo("Render saved!");
            Screenshot = false;
        }

        if (_doRender)
        {
            RenderGame(alpha, _gameRender);

            _imGuiDrawCount++;
            _imGuiRenderer.Begin();
            DrawInternal();
            TextureUtils.EnsureTextureSize(ref _imGuiRenderTarget, GraphicsDevice, MainWindow.Size);
            _imGuiRenderer.End(_imGuiRenderTarget);
            _doRender = false;
        }

        var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

        if (swapTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        if (_imGuiDrawCount > 0)
            Renderer.DrawSprite(_imGuiRenderTarget, Matrix4x4.Identity, Color.White);
        else
            Renderer.DrawPoint(swapTexture.Size() / 2, Color.Transparent, 10f);
        Renderer.Flush(commandBuffer, swapTexture, Color.Black, null);
        Renderer.Submit(commandBuffer);
    }

    private static void SaveRender(GraphicsDevice device, Texture render)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var buffer = Buffer.Create<byte>(device, BufferUsageFlags.Index, render.Width * render.Height * sizeof(uint));
        commandBuffer.CopyTextureToBuffer(render, buffer);
        device.Submit(commandBuffer);
        device.Wait();
        var pixels = new byte[buffer.Size];
        buffer.GetData(pixels, (uint)pixels.Length);
        Texture.SavePNG("test.png", (int)render.Width, (int)render.Height, render.Format, pixels);
    }

    protected override void Destroy()
    {
        base.Destroy();

        var fileName = ImGuiExt.StringFromPtr(ImGui.GetIO()->IniFilename);
        ImGui.SaveIniSettingsToDisk(fileName);
        Logger.LogInfo($"Saved ImGui Settings to \"{fileName}\"");
        _imGuiRenderer.Dispose();
    }
}
