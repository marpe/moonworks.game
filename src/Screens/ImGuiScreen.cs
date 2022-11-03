using ImGuiNET;
using MyGame.Graphics;
using MyGame.TWImGui;
using SDL2;

namespace MyGame.Screens;

public class ImGuiScreen
{
    internal SortedList<string, ImGuiWindow> Windows = new();

    private readonly ImGuiRenderer _imGuiRenderer;
    private MyGameMain _game;
    private float _alpha = 1.0f;
    private readonly Sampler _sampler;
    private ulong _imGuiDrawCount;
    private int _updateFps = 60;
    private float _updateRate = 1 / 60f;
    private float _lastUpdateTime;
    private readonly string[] _blendStateNames;
    private List<ImGuiMenu> _menuItems = new();
    private float _mainMenuPaddingY = 6f;
    private bool _doRender;
    private InputState _inputState = new();
    public bool IsHidden { get; private set; } = true;

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
    }


    private static void ShowImGuiDemoWindow(ImGuiWindow window)
    {
        if (!window.IsOpen)
            return;
        ImGui.ShowDemoWindow(ref window.IsOpen);
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiWindow[]
        {
            new ImGuiCallbackWindow("ImGui Demo Window", ShowImGuiDemoWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F1"
            },
            new ImGuiWorldWindow()
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    private void DrawTestWindow2(ImGuiWindow window)
    {
        if (!window.IsOpen)
            return;

        if (ImGuiExt.Begin(window.Title, ref window.IsOpen))
        {
            ImGui.TextUnformatted("LALLALALALAA");
        }

        ImGui.End();
    }

    public void Update(float deltaSeconds, bool allowKeyboardInput, bool allowMouseInput)
    {
        var inputHandler = _game.InputHandler;

        if (inputHandler.IsKeyPressed(KeyCode.F2))
        {
            IsHidden = !IsHidden;
        }

        if (_game.TotalElapsedTime - _lastUpdateTime < _updateRate)
            return;

        _lastUpdateTime = _game.TotalElapsedTime;

        if (IsHidden)
            allowKeyboardInput = allowMouseInput = false;

        // TODO (marpe): Accumulate input events until next draw?
        InputState.Clear(ref _inputState);

        if (allowKeyboardInput)
        {
            Array.Resize(ref _inputState.TextInput, inputHandler.TextInput.Count);
            _inputState.NumTextInputChars = inputHandler.TextInput.Count;
            for (var i = 0; i < _inputState.NumTextInputChars; i++)
            {
                _inputState.TextInput[i] = inputHandler.TextInput[i];
            }

            for (var i = 0; i < _inputState.KeyboardState.Length; i++)
            {
                if (Enum.IsDefined((KeyCode)i))
                    _inputState.KeyboardState[i] = inputHandler.IsKeyDown((KeyCode)i);
            }
        }

        if (allowMouseInput)
        {
            SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
            _inputState.GlobalMousePosition = new Vector2(globalMouseX, globalMouseY);
            _inputState.MouseWheelDelta = inputHandler.MouseWheelDelta;
            for (var i = 0; i < 3; i++)
            {
                _inputState.MouseState[i] = inputHandler.IsMouseButtonDown((MouseButtonCode)i);
            }
        }

        _imGuiRenderer.Update(deltaSeconds, _inputState);
        _doRender = true;
    }

    public void Draw(Renderer renderer)
    {
        if (IsHidden)
            return;

        if (_doRender)
        {
            _imGuiDrawCount++;
            _imGuiRenderer.Begin();
            DrawInternal();
            _imGuiRenderer.End();
            _doRender = false;
        }

        if (_imGuiDrawCount == 0)
            return;

        var sprite = new Sprite(_imGuiRenderer.RenderTarget);
        renderer.DrawSprite(sprite, Matrix3x2.Identity, Color.White, 0);

        /*var swap = renderer.SwapTexture;
        var viewProjection = SpriteBatch.GetViewProjection(0, 0, swap.Width, swap.Height);
        renderer.FlushBatches(swap, viewProjection);*/
    }

    private void DrawTestWindow(ImGuiWindow window)
    {
        if (!window.IsOpen)
            return;
        ImGui.SetNextWindowBgAlpha(_alpha);

        if (ImGuiExt.Begin(window.Title, ref window.IsOpen))
        {
            ImGui.Text("ImGui Window 1");
            ImGui.Separator();
            ImGui.SliderFloat("MenuPadding", ref _mainMenuPaddingY, 0, 100f);
            ImGui.Separator();
            ImGui.TextUnformatted($"Nav: {ImGui.GetIO().NavActive}");
            ImGui.TextUnformatted($"FrameCount: {_game.UpdateCount}");
            ImGui.TextUnformatted($"Total: {_game.TotalElapsedTime}");
            ImGui.TextUnformatted($"Elapsed: {_game.ElapsedTime}");
            ImGui.TextUnformatted($"RenderCount: {_game.DrawCount}");
            ImGui.TextUnformatted($"ImGuiDrawCount: {_imGuiDrawCount}");
            if (ImGui.SliderInt("UpdateFPS", ref _updateFps, 1, 120))
            {
                _updateRate = 1.0f / _updateFps;
            }

            ImGui.SliderFloat("Alpha", ref _alpha, 0, 1.0f);
            ImGui.Separator();
            var spriteBatchBlendStateIndex = (int)_game.Renderer.BlendState;
            if (ImGui.BeginCombo("SpriteBatchBlendState", _blendStateNames[spriteBatchBlendStateIndex]))
            {
                for (var i = 0; i < _blendStateNames.Length; i++)
                {
                    var isSelected = i == spriteBatchBlendStateIndex;
                    if (ImGui.Selectable(_blendStateNames[i], isSelected))
                        _game.Renderer.BlendState = (BlendState)i;
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            if (BlendStateEditor.Draw("SpriteBatch", ref _game.Renderer.CustomBlendState))
            {
                _game.Renderer.UpdateCustomBlendPipeline();
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

    private void DrawMenu(ImGuiMenu menu, int depth = 0)
    {
        if (menu.Children.Count > 0)
        {
            if (depth == 0)
            {
                var style = ImGui.GetStyle();
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style.FramePadding.X, _mainMenuPaddingY));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(style.ItemSpacing.X, style.FramePadding.Y * 2f));
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
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style.FramePadding.X, _mainMenuPaddingY));
        var result = ImGui.BeginMainMenuBar();
        ImGui.PopStyleVar();
        if (!result)
            return;

        foreach (var menu in _menuItems)
        {
            DrawMenu(menu);
        }

        foreach (var menu in _menuItems)
        {
            CheckMenuShortcuts(menu);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(style.FramePadding.X, _mainMenuPaddingY));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Num.Vector2(style.ItemSpacing.X, style.FramePadding.Y * 2f));
        var windowMenu = ImGui.BeginMenu("Window");
        ImGui.PopStyleVar(2);
        if (windowMenu)
        {
            foreach (var (key, window) in Windows)
            {
                ImGui.MenuItem(window.Title, window.KeyboardShortcut, ref window.IsOpen);
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
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        DrawMenu();

        var drawList = ImGui.GetBackgroundDrawList();

        DrawWindows();

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

    public void Destroy()
    {
        ImGui.SaveIniSettingsToDisk(ImGui.GetIO().IniFilename);
        _imGuiRenderer.Dispose();
    }
}
