using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Graphics;
using MyGame.TWConsole;
using MyGame.TWImGui;
using ImGuiWindow = MyGame.TWImGui.ImGuiWindow;

namespace MyGame.Screens;

public unsafe class ImGuiScreen
{
    [CVar("imgui.hidden", "Toggle ImGui screen")]
    public static bool IsHidden = true;

    private readonly string[] _blendStateNames;

    private readonly ImGuiRenderer _imGuiRenderer;
    private readonly Sampler _sampler;
    private readonly float _alpha = 1.0f;
    private bool _doRender;
    private readonly MyGameMain _game;
    private ulong _imGuiDrawCount;
    private readonly List<InputState> _inputStates = new();
    private readonly float _mainMenuPaddingY = 6f;
    private readonly List<ImGuiMenu> _menuItems = new();
    private int _updateRate = 1;
    internal SortedList<string, ImGuiWindow> Windows = new();
    private float _prevElapsedTime;
    private bool _firstTime = true;
    private static readonly string _debugWindowName = "MyGame Debug";
    private static string _imguiDemoWindowName = "ImGui Demo Window";

    public ImGuiScreen(MyGameMain game)
    {
        _game = game;
        var timer = Stopwatch.StartNew();
        _sampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.PointClamp);
        _imGuiRenderer = new ImGuiRenderer(game);
        _blendStateNames = Enum.GetNames<BlendState>();
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        AddDefaultMenus();
        Logger.LogInfo($"ImGuiInit: {timer.ElapsedMilliseconds} ms");
    }

    private void AddDefaultMenus()
    {
        var file = new ImGuiMenu("File")
            .AddChild(new ImGuiMenu("Quit", "^Q", () => _game.Quit()));
        _menuItems.Add(file);
        var imgui = new ImGuiMenu("ImGui")
            .AddChild(new ImGuiMenu("Debug Inspectors", null, () => { ImGuiExt.DebugInspectors = !ImGuiExt.DebugInspectors; }));
        _menuItems.Add(imgui);
    }


    private static void ShowImGuiDemoWindow(ImGuiWindow window)
    {
        if (!window.IsOpen)
        {
            return;
        }

        fixed (bool* isOpen = &window.IsOpen)
        {
            ImGui.ShowDemoWindow(isOpen);
        }
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiWindow[]
        {
            new ImGuiCallbackWindow(_debugWindowName, DrawDebugWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F1",
            },
            new ImGuiCallbackWindow(_imguiDemoWindowName, ShowImGuiDemoWindow)
            {
                IsOpen = false,
                KeyboardShortcut = "^F2",
            },
            new ImGuiWorldWindow(),
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var inputHandler = _game.InputHandler;

        if (inputHandler.IsKeyPressed(KeyCode.F2))
        {
            IsHidden = !IsHidden;
        }

        if (IsHidden)
        {
            return;
        }

        var newState = InputState.Create(inputHandler, allowKeyboardInput, allowMouseInput);
        _inputStates.Add(newState);

        if ((int)_game.UpdateCount % _updateRate == 0)
        {
            var inputState = InputState.Aggregate(_inputStates);
            _inputStates.Clear();
            var deltaTime = _game.TotalElapsedTime - _prevElapsedTime;
            _imGuiRenderer.Update(deltaTime, inputState);
            _prevElapsedTime = _game.TotalElapsedTime;
            _doRender = true;
        }
    }

    public void Draw(Renderer renderer)
    {
        if (IsHidden)
        {
            return;
        }

        if (_doRender)
        {
            _imGuiDrawCount++;
            _imGuiRenderer.Begin();
            DrawInternal();
            _imGuiRenderer.End();
            _doRender = false;
        }

        if (_imGuiDrawCount == 0)
        {
            return;
        }

        var sprite = new Sprite(_imGuiRenderer.RenderTarget);
        renderer.DrawSprite(sprite, Matrix3x2.Identity, Color.White, 0);

        /*var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.FlushBatches(swap, viewProjection);*/
    }

    private void DrawDebugWindow(ImGuiWindow window)
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
            ImGui.TextUnformatted($"FrameCount: {_game.UpdateCount}");
            ImGui.TextUnformatted($"RenderCount: {_game.DrawCount}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");

            if (ImGui.Button("Reload World", default))
            {
                _game.GameScreen.LoadWorld();
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
            ImGuiInternal.DockBuilderDockWindow(ImGuiWorldWindow.WindowTitle, dockDown);
            ImGuiInternal.DockBuilderFinish(dockId);
            _firstTime = false;
        }

        DrawMenu();

        var drawList = ImGui.GetBackgroundDrawList();

        DrawWindows(dockId);

        if (!_game.ConsoleScreen.IsHidden)
        {
            /*
            var isModalOpen = true;
            ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);
            ImGui.OpenPopup("InputBlocker");
            var flags = ImGuiWindowFlags.NoInputs |
                        ImGuiWindowFlags.NoDecoration |
                        ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoSavedSettings |
                        ImGuiWindowFlags.NoBackground;
            if (ImGui.BeginPopupModal("InputBlocker", ref isModalOpen, flags))
            {
                ImGui.Text("This modal is for blocking input...");
                ImGui.EndPopup();
            }
            */
        }
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

    public void Destroy()
    {
        var fileName = ImGuiExt.StringFromPtr(ImGui.GetIO()->IniFilename);
        ImGui.SaveIniSettingsToDisk(fileName);
        Logger.LogInfo($"Saved ImGui Settings to \"{fileName}\"");
        _imGuiRenderer.Dispose();
    }
}
