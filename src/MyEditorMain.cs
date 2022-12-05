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
    private readonly List<ImGuiMenu> _menuItems = new();
    public int UpdateRate = 1;
    private SortedList<string, ImGuiEditorWindow> _imGuiWindows = new();
    private bool _firstTime = true;
    private Texture _imGuiRenderTarget;

    private bool _screenshot;

    private int _imGuiUpdateCount;
    private FileWatcher _fileWatcher;

    private Buffer _screenshotBuffer;
    private byte[] _screenshotPixels;

    private GameWindow _gameWindow;
    private DebugWindow _debugWindow;
    private ImGuiDemoWindow _demoWindow;
    private Task _screenshotTask;

    private Stopwatch _imguiRenderStopwatch = new();
    private Stopwatch _renderGameStopwatch = new();
    private Stopwatch _renderStopwatch = new();
    private Stopwatch _gameUpdateStopwatch = new();
    public float _imGuiRenderDurationMs;
    public float _renderGameDurationMs;
    public float _renderDurationMs;
    public float _gameUpdateDurationMs;

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep, bool debugMode) : base(
        windowCreateInfo,
        frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();
        var windowSize = MainWindow.Size;
        _imGuiRenderTarget = Texture.CreateTexture2D(
            GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y, TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget
        );

        _demoWindow = new ImGuiDemoWindow();

        _gameWindow = new GameWindow(this);
        _debugWindow = new DebugWindow(this);

        var imguiSw = Stopwatch.StartNew();
        ImGuiRenderer = new ImGuiRenderer(this);
        ImGuiThemes.DarkTheme();
        AddDefaultWindows();
        AddDefaultMenus();
        imguiSw.StopAndLog("ImGuiRenderer");

        // screenshot setup
        {
            _screenshotTask = Task.CompletedTask;
            _screenshotBuffer = Buffer.Create<byte>(
                GraphicsDevice,
                BufferUsageFlags.Index,
                RenderTargets.GameRender.Width * RenderTargets.GameRender.Height * sizeof(uint)
            );
            _screenshotPixels = new byte[_screenshotBuffer.Size];
        }

        _fileWatcher = new FileWatcher("Content", "*", OnFileChanged);

        sw.StopAndLog("MyEditorMain");
    }

    private void OnFileChanged(FileEvent e)
    {
        Logs.LogInfo($"File changed: {e.FullPath}, {e.ChangeType}");
        var relativePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, e.FullPath);
        var extension = Path.GetExtension(relativePath);
        if (extension == ".ldtk")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading world on thread: {Thread.CurrentThread.ManagedThreadId}");

                var ldtk = ContentManager.LoadLDtk(relativePath);

                // TODO (marpe): Fix
                var ldtkAsset = new LDtkAsset();
                ldtkAsset.LdtkRaw = ldtk;
                
                // start the same level we're currently on
                Action? onComplete = null;
                if (GameScreen.World.IsLoaded)
                {
                    var levelIdentifier = GameScreen.World.Level.Identifier;
                    onComplete = () => { GameScreen.World.StartLevel(levelIdentifier); };
                }

                GameScreen.QueueSetLdtk(ldtkAsset, onComplete);
            });
        }
        else if (extension == ".aseprite")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading aseprite texture on thread: {Thread.CurrentThread.ManagedThreadId}");
                var texture = TextureUtils.LoadAseprite(GraphicsDevice, e.FullPath);
                GameScreen.QueueAction(() =>
                {
                    Shared.Content.ReplaceTexture(relativePath, texture);
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

    private void SetWindowBorder()
    {
        SDL.SDL_SetWindowBordered(MainWindow.Handle, MainWindow.IsBorderless ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);
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
                () => ImGuiRenderer.UsePointSampler))
            .AddChild(new ImGuiMenu("Borderless Window", null, () => SetWindowBorder(), null, () => MainWindow.IsBorderless))
            .AddChild(new ImGuiMenu("Show ImGui Demo Window", "^F2", () => _demoWindow.IsOpen = !_demoWindow.IsOpen, null, () => _demoWindow.IsOpen));
        _menuItems.Add(imgui);
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiEditorWindow[]
        {
            new LoadingScreenDebugWindow(),
            new WorldWindow(),
            new EntityEditorWindow(this),
            _gameWindow,
            _debugWindow,
            _demoWindow,
            new RenderTargetsWindow(this),
            new InputDebugWindow(this),
        };
        foreach (var window in windows)
        {
            _imGuiWindows.Add(window.Title, window);
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

    private void DrawInternal()
    {
        var mainViewport = ImGui.GetMainViewport();

        var dockId = SetupDockSpace(mainViewport);

        if (MainWindow.IsBorderless)
        {
            ImGuiBorderlessTitle.Draw(MainWindow, this);
        }

        ImGuiMainMenu.DrawMenu(_menuItems, _imGuiWindows);

        DrawWindows(dockId);

        SetMouseCursor();
    }

    private uint SetupDockSpace(ImGuiViewport* mainViewport)
    {
        var dockId = ImGui.DockSpaceOverViewport(mainViewport, ImGuiDockNodeFlags.PassthruCentralNode);

        if (_firstTime)
        {
            ImGuiInternal.DockBuilderRemoveNodeChildNodes(dockId);

            var leftWidth = 0.15f;
            var dockLeft = ImGuiInternal.DockBuilderSplitNode(dockId, ImGuiDir.Left, leftWidth, null, &dockId);

            uint topLeft;
            uint bottomLeft;
            var leftSplit = ImGuiInternal.DockBuilderSplitNode(dockLeft, ImGuiDir.Up, 0.5f, &topLeft, &bottomLeft);

            var rightWidth = leftWidth / (1.0f - leftWidth); // 1.0f / (1.0f - leftWidth) - 1.0f;
            var dockRight = ImGuiInternal.DockBuilderSplitNode(dockId, ImGuiDir.Right, rightWidth, null, &dockId);
            ImGuiInternal.DockBuilderDockWindow(DebugWindow.WindowTitle, topLeft);
            ImGuiInternal.DockBuilderDockWindow(LoadingScreenDebugWindow.WindowTitle, topLeft);
            ImGuiInternal.DockBuilderDockWindow(EntityEditorWindow.WindowTitle, bottomLeft);
            ImGuiInternal.DockBuilderDockWindow(WorldWindow.WindowTitle, dockRight);

            ImGuiInternal.DockBuilderFinish(dockId);
            _firstTime = false;
        }

        return dockId;
    }

    private void SetMouseCursor()
    {
        if (!ImGui.IsAnyItemHovered())
            return;

        if (ImGui.GetMouseCursor() != ImGuiMouseCursor.Arrow)
            return;

        if (_gameWindow.IsHoveringGameWindow)
        {
            var hoveredWindow = ImGui.GetCurrentContext()->HoveredWindow;
            if (hoveredWindow != null && ImGuiExt.StringFromPtr(hoveredWindow->Name) == GameWindow.GameViewTitle)
                return;
        }

        var cursor = ImGui.GetCurrentContext()->HoveredIdDisabled switch
        {
            true => ImGuiMouseCursor.NotAllowed,
            _ => ImGuiMouseCursor.Hand
        };

        ImGui.SetMouseCursor(cursor);
    }

    private void DrawWindows(uint dockId)
    {
        foreach (var (_, window) in _imGuiWindows)
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
            if (io->NavActive || (_gameWindow.IsOpen && !_gameWindow.IsHoveringGameWindow))
                InputHandler.MouseEnabled = false;

            _imGuiUpdateCount++;
        }

        var wasHidden = IsHidden;

        _gameUpdateStopwatch.Restart();
        base.Update(dt);
        _gameUpdateStopwatch.Stop();
        _gameUpdateDurationMs = _gameUpdateStopwatch.GetElapsedMilliseconds();

        // show/hide child windows when showing/hiding imgui
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

        _renderStopwatch.Restart();
        _renderGameStopwatch.Restart();
        RenderGame(alpha, RenderTargets.CompositeRender);
        _renderGameStopwatch.Stop();
        _renderGameDurationMs = _renderGameStopwatch.GetElapsedMilliseconds();

        if (((int)Time.UpdateCount % UpdateRate == 0) && _imGuiUpdateCount > 0)
        {
            _imguiRenderStopwatch.Restart();
            _imGuiDrawCount++;
            ImGuiRenderer.Begin();
            DrawInternal();
            var previousSize = _imGuiRenderTarget.Size();
            if (TextureUtils.EnsureTextureSize(ref _imGuiRenderTarget, GraphicsDevice, MainWindow.Size))
            {
                Logs.LogInfo($"ImGuiRenderTarget resized {previousSize.ToString()} -> {_imGuiRenderTarget.Size().ToString()}");
            }

            ImGuiRenderer.End(_imGuiRenderTarget);
            _imguiRenderStopwatch.Stop();
            _imGuiRenderDurationMs = _imguiRenderStopwatch.GetElapsedMilliseconds();
        }

        var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

        if (swapTexture == null)
        {
            Logs.LogError("Could not acquire swapchain texture");
            return;
        }

        _swapSize = swapTexture.Size();

        if (_screenshot)
        {
            commandBuffer.CopyTextureToBuffer(RenderTargets.LightTarget.Target, _screenshotBuffer);
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

        if (_screenshot)
        {
            Logs.LogInfo("Saving screenshot...");

            _screenshotTask = _screenshotTask.ContinueWith((_) =>
            {
                GraphicsDevice.Wait();
                _screenshotBuffer.GetData(_screenshotPixels, _screenshotBuffer.Size);
                var i = 1;
                var dirname = "screenshots";
                Directory.CreateDirectory(dirname);
                var filename = Path.Combine(dirname, "screenshot_0.png");
                while (File.Exists(filename) && i < 100)
                {
                    filename = Path.Combine(dirname, "screenshot_" + (i++) + ".png");
                }

                Texture.SavePNG(
                    filename,
                    (int)RenderTargets.CompositeRender.Width,
                    (int)RenderTargets.CompositeRender.Height,
                    RenderTargets.CompositeRender.Target.Format,
                    _screenshotPixels
                );
                Logs.LogInfo($"Screenshot saved to {filename}");
            });

            _screenshot = false;
        }

        _renderStopwatch.Stop();
        _renderDurationMs = _renderStopwatch.GetElapsedMilliseconds();
    }

    protected override void Destroy()
    {
        base.Destroy();

        var fileName = ImGuiExt.StringFromPtr(ImGui.GetIO()->IniFilename);
        ImGui.SaveIniSettingsToDisk(fileName);
        Logs.LogInfo($"Saved ImGui Settings to \"{fileName}\"");
        ImGuiRenderer.Dispose();
        _fileWatcher.Dispose();
        _screenshotBuffer.Dispose();
    }

    [ConsoleHandler("screenshot", "Save a screenshot")]
    public static void Screenshot()
    {
        ((MyEditorMain)Shared.Game)._screenshot = true;
    }
}
