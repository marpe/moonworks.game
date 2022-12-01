using System.Threading;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Editor;
using NumVector2 = System.Numerics.Vector2;

namespace MyGame;

public unsafe class MyEditorMain : MyGameMain
{
    [CVar("imgui.hidden", "Toggle ImGui screen")]
    public static bool IsHidden = true;

    public ImGuiRenderer ImGuiRenderer;

    private ulong _imGuiDrawCount;
    private readonly float _mainMenuPaddingY = 6f;
    private readonly List<ImGuiMenu> _menuItems = new();
    public int UpdateRate = 1;
    internal SortedList<string, ImGuiEditorWindow> Windows = new();
    private bool _firstTime = true;
    private Texture _imGuiRenderTarget;

    [CVar("screenshot", "Save a screenshot")]
    public static bool Screenshot;

    private int _imGuiUpdateCount;
    private FileWatcher _fileWatcher;

    private Buffer _screenshotBuffer;
    private byte[] _screenshotPixels;

    private GameWindow _gameWindow;
    private DebugWindow _debugWindow;

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep, bool debugMode) : base(
        windowCreateInfo,
        frameLimiterSettings, targetTimestep, debugMode)
    {
        var windowSize = MainWindow.Size;
        _imGuiRenderTarget = Texture.CreateTexture2D(GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

        _gameWindow = new GameWindow(this);
        _debugWindow = new DebugWindow(this);

        var timer = Stopwatch.StartNew();
        ImGuiRenderer = new ImGuiRenderer(this);
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        AddDefaultMenus();

        _screenshotBuffer = Buffer.Create<byte>(GraphicsDevice, BufferUsageFlags.Index, GameRender.Width * GameRender.Height * sizeof(uint));
        _screenshotPixels = new byte[_screenshotBuffer.Size];

        _fileWatcher = new FileWatcher("Content", "*", OnFileChanged);

        Logger.LogInfo($"ImGuiInit: {timer.ElapsedMilliseconds} ms");
    }

    private void OnFileChanged(FileEvent e)
    {
        Logs.LogInfo($"File changed: {e.FullPath}, {e.ChangeType}");
        var extension = Path.GetExtension(e.FullPath);
        if (extension == ".ldtk")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading world on thread: {Thread.CurrentThread.ManagedThreadId}");
                var world = new World(GameScreen, e.FullPath);
                GameScreen.QueueSetWorld(world);
                if (GameScreen.World != null)
                {
                    var levelIdentifier = GameScreen.World.Level.Identifier;
                    GameScreen.QueueAction(() =>
                    {
                        world.StartLevel(levelIdentifier);
                    });
                }
            });
        }
        else if (extension == ".aseprite")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading aseprite texture on thread: {Thread.CurrentThread.ManagedThreadId}");
                var texture = TextureUtils.LoadAseprite(GraphicsDevice, e.FullPath);
                var relativePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, e.FullPath);
                GameScreen.QueueAction(() =>
                {
                    GameScreen.Content.AddTexture(relativePath, texture);
                    Logs.LogInfo($"Texture added from thread: {Thread.CurrentThread.ManagedThreadId}");
                });
            });
        }
        else if (extension == ".spv")
        {
            Renderer.Pipelines[PipelineType.RimLight].Pipeline.Dispose();
            Renderer.Pipelines[PipelineType.RimLight] = Pipelines.CreateRimLightPipeline(GraphicsDevice);
        }
    }

    private void AddDefaultMenus()
    {
        var file = new ImGuiMenu("File")
            .AddChild(new ImGuiMenu("Quit", "^Q", () => Quit()));
        _menuItems.Add(file);
        var imgui = new ImGuiMenu("ImGui")
            .AddChild(new ImGuiMenu("Debug Inspectors", null, () => { ImGuiExt.DebugInspectors = !ImGuiExt.DebugInspectors; }, null,
                () => ImGuiExt.DebugInspectors))
            .AddChild(new ImGuiMenu("Use Point Sampler", null, () => { ImGuiRenderer.UsePointSampler = !ImGuiRenderer.UsePointSampler; }, null,
                () => ImGuiRenderer.UsePointSampler));
        _menuItems.Add(imgui);
    }

    private static void ShowImGuiDemoWindow(ImGuiEditorWindow window)
    {
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiEditorWindow[]
        {
            new LoadingScreenDebugWindow(),
            new WorldWindow(),
            _gameWindow,
            _debugWindow,
            new ImGuiDemoWindow(),
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    protected override void SetInputViewport()
    {
        if (!IsHidden && _gameWindow.IsOpen)
        {
            InputHandler.SetViewportTransform(_gameWindow.GameRenderViewportTransform);
        }
        else
        {
            base.SetInputViewport();
        }
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
            if (ImGui.MenuItem(menu.Text, menu.Shortcut, menu.IsSelectedCallback?.Invoke() ?? false))
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
            foreach (var (_, window) in Windows)
            {
                ImGui.MenuItem(window.Title, window.KeyboardShortcut, ImGuiExt.RefPtr(ref window.IsOpen));
            }

            ImGui.EndMenu();
        }

        DrawMainMenuButtons();

        ImGui.EndMainMenuBar();
    }

    private static string LoadingIndicator(string labelWhenNotLoading, bool isLoading)
    {
        if (!isLoading)
            return labelWhenNotLoading;
        var n = (int)(Shared.Game.Time.TotalElapsedTime * 4 % 4);
        return n switch
        {
            0 => FontAwesome6.ArrowRight,
            1 => FontAwesome6.ArrowDown,
            2 => FontAwesome6.ArrowLeft,
            _ => FontAwesome6.ArrowUp,
        };
    }

    private void DrawMainMenuButtons()
    {
        var max = ImGui.GetContentRegionMax();
        var numButtons = 3;
        var buttonWidth = 29;
        ImGui.SetCursorPosX(max.X / 2 - numButtons * buttonWidth);

        var (icon, color, tooltip) = GameScreen.IsPaused switch
        {
            true => (FontAwesome6.Play, Color.Green, "Play"),
            _ => (FontAwesome6.Pause, Color.Yellow, "Pause")
        };

        ImGui.BeginDisabled(Shared.LoadingScreen.IsLoading);
        if (ImGuiExt.ColoredButton(LoadingIndicator(FontAwesome6.ArrowsRotate, Shared.LoadingScreen.IsLoading), Color.Blue, "Reload World"))
        {
            GameScreen.Restart();
        }

        ImGui.EndDisabled();

        if (ImGuiExt.ColoredButton(icon, color, tooltip))
        {
            GameScreen.IsPaused = !GameScreen.IsPaused;
        }

        if (ImGuiExt.ColoredButton(FontAwesome6.ForwardStep, Color.Orange, "Step"))
        {
            GameScreen.IsPaused = GameScreen.IsStepping = true;
        }
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
            ImGuiInternal.DockBuilderDockWindow(DebugWindow.WindowTitle, dockLeft);
            ImGuiInternal.DockBuilderDockWindow(LoadingScreenDebugWindow.WindowTitle, dockLeft);
            ImGuiInternal.DockBuilderDockWindow(WorldWindow.WindowTitle, dockDown);
            ImGuiInternal.DockBuilderFinish(dockId);
            _firstTime = false;
        }

        DrawMenu();

        DrawWindows(dockId);

        SetMouseCursor();
    }

    private void SetMouseCursor()
    {
        if (!ImGui.IsAnyItemHovered())
            return;

        if (ImGui.GetMouseCursor() != ImGuiMouseCursor.Arrow)
            return;

        if (_gameWindow.IsHoveringGame && _gameWindow.IsWindowFocused)
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
        if (!IsHidden)
        {
            ImGuiRenderer.Update((float)dt.TotalSeconds, _imGuiRenderTarget.Size(), InputState.Create(InputHandler));

            var io = ImGui.GetIO();
            if (io->WantCaptureKeyboard)
                InputHandler.KeyboardEnabled = false;
            if (io->NavActive || (_gameWindow.IsOpen && !_gameWindow.IsHoveringGame))
                InputHandler.MouseEnabled = false;

            _imGuiUpdateCount++;
        }

        var wasHidden = IsHidden;

        base.Update(dt);

        if (wasHidden != IsHidden)
        {
            var platformIO = ImGui.GetPlatformIO();
            for (var i = 1; i < platformIO->Viewports.Size; i++)
            {
                ImGuiViewport* vp = platformIO->Viewports[i];
                var window = vp->Window();
                if (IsHidden)
                    SDL.SDL_HideWindow(window.Handle);
                else
                    SDL.SDL_ShowWindow(window.Handle);
            }
        }
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

        RenderGame(alpha, CompositeRender);

        if (((int)Time.UpdateCount % UpdateRate == 0) && _imGuiUpdateCount > 0)
        {
            _imGuiDrawCount++;
            ImGuiRenderer.Begin();
            DrawInternal();
            TextureUtils.EnsureTextureSize(ref _imGuiRenderTarget, GraphicsDevice, MainWindow.Size);
            ImGuiRenderer.End(_imGuiRenderTarget);
        }

        var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

        if (swapTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        if (Screenshot)
        {
            commandBuffer.CopyTextureToBuffer(CompositeRender, _screenshotBuffer);
        }

        /*{
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(swapTexture.Size(), CompositeRender.Size());
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(CompositeRender, viewportTransform, Color.White);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, view * projection);
        }*/

        if (_imGuiDrawCount > 0)
        {
            Renderer.DrawSprite(_imGuiRenderTarget, Matrix4x4.Identity, Color.White);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, null);
        }

        Renderer.Submit(ref commandBuffer);

        if (Screenshot)
        {
            Logs.LogInfo("Saving screenshot...");

            Task.Run(() =>
            {
                GraphicsDevice.Wait();
                _screenshotBuffer.GetData(_screenshotPixels, _screenshotBuffer.Size);
                var filename = "test.png";
                Texture.SavePNG(filename, (int)CompositeRender.Width, (int)CompositeRender.Height, CompositeRender.Format, _screenshotPixels);
                Logger.LogInfo($"Screenshot saved to {filename}");
            });

            Screenshot = false;
        }
    }

    protected override void Destroy()
    {
        base.Destroy();

        var fileName = ImGuiExt.StringFromPtr(ImGui.GetIO()->IniFilename);
        ImGui.SaveIniSettingsToDisk(fileName);
        Logger.LogInfo($"Saved ImGui Settings to \"{fileName}\"");
        ImGuiRenderer.Dispose();
        _fileWatcher.Dispose();
        _screenshotBuffer.Dispose();
    }
}
