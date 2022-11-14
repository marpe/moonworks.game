using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Graphics;
using MyGame.Input;
using MyGame.TWConsole;
using MyGame.TWImGui;
using Vector2 = System.Numerics.Vector2;

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
    private bool _doRender = true;
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

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, bool debugMode) : base(windowCreateInfo,
        frameLimiterSettings, debugMode)
    {
        _gameRender = Texture.CreateTexture2D(GraphicsDevice, 1920, 1080, TextureFormat.B8G8R8A8, TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

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
            new ImGuiEditorWorldWindow(),
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
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
            if (_gameRenderTextureId != null)
                _imGuiRenderer.UnbindTexture(_gameRenderTextureId.Value);
            _gameRenderTextureId = _imGuiRenderer.BindTexture(_gameRender);

            var avail = ImGui.GetContentRegionAvail();
            var width = ImGui.GetContentRegionAvail().X;
            var ar = (float)_gameRender.Height / _gameRender.Width;
            var height = width * ar;

            if (height > avail.Y)
            {
                height = avail.Y;
                width = avail.Y * 1.0f / ar;
            }

            var imageSize = new Num.Vector2(width, height);
            var padding = (avail - imageSize) / 2;

            ImGui.SetCursorPos(padding);
            ImGui.Image((void*)_gameRenderTextureId.Value, imageSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.One, new Num.Vector4(1.0f, 0, 0, 1.0f));

            if (ImGui.IsItemHovered())
            {
                ImGui.SetNextFrameWantCaptureMouse(false);
                ImGui.SetNextFrameWantCaptureKeyboard(false);
            }
        }

        ImGui.PopStyleVar();
        ImGui.End();
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
            ImGui.TextUnformatted($"FrameCount: {UpdateCount}");
            ImGui.TextUnformatted($"RenderCount: {DrawCount}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");

            if (ImGui.Button("Reload World", default))
            {
                GameScreen.LoadWorld();
            }

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
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, _mainMenuPaddingY));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
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
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, _mainMenuPaddingY));
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

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style->FramePadding.X, _mainMenuPaddingY));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style->ItemSpacing.X, style->FramePadding.Y * 2f));
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
        if (ImGui.IsAnyItemHovered())
        {
            var cursor = ImGui.GetMouseCursor();
            if (cursor == ImGuiMouseCursor.Arrow)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

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
            ImGuiInternal.DockBuilderDockWindow(ImGuiEditorWorldWindow.WindowTitle, dockDown);
            ImGuiInternal.DockBuilderFinish(dockId);
            _firstTime = false;
        }

        DrawMenu();

        var drawList = ImGui.GetBackgroundDrawList();

        DrawWindows(dockId);
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

        if (inputHandler.IsKeyPressed(KeyCode.F2))
        {
            IsHidden = !IsHidden;
        }

        if (!IsHidden)
        {
            var newState = InputState.Create(inputHandler);
            _inputStates.Add(newState);

            if ((int)UpdateCount % _updateRate == 0)
            {
                var inputState = InputState.Aggregate(_inputStates);
                _inputStates.Clear();
                var deltaTime = TotalElapsedTime - _prevElapsedTime;
                _imGuiRenderer.Update(deltaTime, inputState);
                _prevElapsedTime = TotalElapsedTime;
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

        if (!Renderer.BeginFrame(out var swapTexture))
            return;

        var windowSize = MainWindow.Size;
        if (_doRender)
        {
            TextureUtils.EnsureTextureSize(ref _gameRender, GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y);
            RenderGame(alpha, _gameRender);

            _imGuiDrawCount++;
            _imGuiRenderer.Begin();
            DrawInternal();
            _imGuiRenderer.End();
            _doRender = false;
        }

        Renderer.DrawSprite(_imGuiRenderer.RenderTarget, Matrix3x2.Identity, Color.White, 0);
        var view = SpriteBatch.GetViewProjection(0, 0, (uint)windowSize.X, (uint)windowSize.Y);
        Renderer.FlushBatches(swapTexture, view, Color.Black);
        Renderer.EndFrame();
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
