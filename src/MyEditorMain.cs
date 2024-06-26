using System.Threading;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Debug;
using MyGame.Editor;
using MyGame.WorldsRoot;
using NumVector2 = System.Numerics.Vector2;

namespace MyGame;

public enum ActiveInput
{
    None,
    Game,
    GameWindow,
    EditorWindow,
    RenderTargetsWindow
}

public unsafe class MyEditorMain : MyGameMain
{
    [CVar("imgui.hidden", "Toggle ImGui screen")]
    public static bool IsHidden = true;

    public ImGuiRenderer ImGuiRenderer;

    private ulong _imGuiDrawCount;
    private readonly List<ImGuiMenu> _menuItems = new();
    private SortedList<string, ImGuiEditorWindow> _imGuiWindows = new();
    private Texture _imGuiRenderTarget;

    private bool _screenshot;

    private int _imGuiUpdateCount;
    private FileWatcher _fileWatcher;

    private Buffer _screenshotBuffer;
    private byte[] _screenshotPixels;

    public GameWindow GameWindow;
    private DebugWindow _debugWindow;
    private ImGuiDemoWindow _demoWindow;
    private Task _screenshotTask;

    private Stopwatch _imguiRenderStopwatch = new();
    private Stopwatch _renderStopwatch = new();
    public float _imGuiRenderDurationMs;
    public float _renderDurationMs;

    public EditorWindow EditorWindow;

    public WorldsRoot.RootJson RootJson = new();
    public uint ViewportDockSpaceId;

    public static ActiveInput PrevActiveInput = ActiveInput.None;
    public static ActiveInput ActiveInput = ActiveInput.None;
    public string Filepath = "";

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep, bool debugMode) : base(
        windowCreateInfo,
        frameLimiterSettings, targetTimestep, debugMode)
    {
        var sw = Stopwatch.StartNew();
        var windowSize = MainWindow.Size;
        _imGuiRenderTarget = Texture.CreateTexture2D(
            GraphicsDevice,
            (uint)windowSize.X,
            (uint)windowSize.Y,
            TextureFormat.B8G8R8A8,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget
        );

        _demoWindow = new ImGuiDemoWindow();

        GameWindow = new GameWindow(this);
        _debugWindow = new DebugWindow(this);

        _renderTargetsWindow = new RenderTargetsWindow(this);

        EditorWindow = new EditorWindow(this) { IsOpen = true };

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

        LoadIcons(ImGuiRenderer);

        LoadWorld(ContentPaths.worlds.worlds_json);

        sw.StopAndLog("MyEditorMain");
    }

    public static bool ResetDock = true;
    private RenderTargetsWindow _renderTargetsWindow;
    private int _lastUpdateCount;

    private static void LoadIcons(ImGuiRenderer renderer)
    {
        var iconPaths = typeof(ContentPaths.icons).GetFields()
            .Select(f => f.GetRawConstantValue())
            .Cast<string>()
            .ToArray();


        foreach (var path in iconPaths)
        {
            var texture = Shared.Content.Load<TextureAsset>(path).TextureSlice.Texture;
            renderer.BindTexture(texture, true);
        }
    }

    private void OnFileChanged(FileEvent e)
    {
        var extension = Path.GetExtension(e.FullPath);
        if (extension is not (".json" or ".png" or ".aseprite" or ".spv"))
            return;
        Logs.LogInfo($"File changed: {e.FullPath}, {e.ChangeType}");
        var relativePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, e.FullPath);
        if (extension == ".json")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading world on thread: {Thread.CurrentThread.ManagedThreadId}");

                var rootJson = Shared.Content.Load<RootJson>(relativePath, true);

                // start the same level we're currently on
                Action? onComplete = null;
                if (World.IsLoaded)
                {
                    var levelIdentifier = World.Level.Identifier;
                    onComplete = () => { World.StartLevel(levelIdentifier); };
                }
                else if (EditorWindow.GetSelectedLevel(out var level))
                {
                    var levelIdentifier = level.Identifier;
                    onComplete = () => { World.StartLevel(levelIdentifier); };
                }
                else
                {
                    onComplete = () => { World.NextLevel(); };
                }

                QueueSetRoot(rootJson, relativePath, onComplete);
            });
        }
        else if (extension == ".png")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading png texture on thread: {Thread.CurrentThread.ManagedThreadId}");
                Shared.Content.Load<TextureAsset>(relativePath, true);
                QueueAction(() => { Logs.LogInfo($"Texture added from thread: {Thread.CurrentThread.ManagedThreadId}"); });
            });
        }
        else if (extension == ".aseprite")
        {
            Task.Run(() =>
            {
                Logs.LogInfo($"Started loading aseprite texture on thread: {Thread.CurrentThread.ManagedThreadId}");
                Shared.Content.Load<AsepriteAsset>(relativePath, true);
                QueueAction(() => { Logs.LogInfo($"Texture added from thread: {Thread.CurrentThread.ManagedThreadId}"); });
            });
        }
        else if (extension == ".spv")
        {
            QueueAction(() =>
            {
                foreach (var (type, pipeline) in Renderer.Pipelines)
                {
                    var isMatch = pipeline.FragmentShaderPath == relativePath ||
                                  pipeline.VertexShaderPath == relativePath;

                    if (isMatch)
                    {
                        Renderer.Pipelines[type] = Pipelines.Factories[type].Invoke(GraphicsDevice, Renderer.Pipelines[type].CreateInfo.AttachmentInfo.ColorAttachmentDescriptions[0].BlendState);
                        Logs.LogInfo($"Reloaded Pipeline: {type.ToString()}");
                    }
                }
            });
        }
    }

    private void SetWindowBorder()
    {
        SDL.SDL_SetWindowBordered(MainWindow.Handle, MainWindow.IsBorderless ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);
    }

    private void LoadWorld(string path)
    {
        if (File.Exists(path))
        {
            RootJson = Shared.Content.Load<RootJson>(path, true);
            Filepath = path;
            Logs.LogInfo($"World loaded: {path}");
        }
    }

    private void SaveWorld()
    {
        var json = JsonConvert.SerializeObject(RootJson, Formatting.Indented, ContentManager.JsonSerializerSettings);
        var filename = ContentPaths.worlds.worlds_json;
        File.WriteAllText(filename, json);
        Logs.LogInfo($"Saved to {filename}");
    }

    private void AddDefaultMenus()
    {
        var file = new ImGuiMenu("File")
            .AddChild(new ImGuiMenu("Save", "^S", () => SaveWorld()))
            .AddChild(new ImGuiMenu("Load", "^O", () => LoadWorld(ContentPaths.worlds.worlds_json)))
            .AddChild(new ImGuiMenu("Quit", "^Q", () => Quit()));
        _menuItems.Add(file);
        var imgui = new ImGuiMenu("ImGui")
            .AddChild(new ImGuiMenu("Debug Inspectors", null, () => { ImGuiExt.DebugInspectors = !ImGuiExt.DebugInspectors; }, null,
                () => ImGuiExt.DebugInspectors))
            .AddChild(new ImGuiMenu("Borderless Window", null, () => SetWindowBorder(), null, () => MainWindow.IsBorderless))
            .AddChild(new ImGuiMenu("Show ImGui Demo Window", "^F2", () => _demoWindow.IsOpen = !_demoWindow.IsOpen, null, () => _demoWindow.IsOpen));
        _menuItems.Add(imgui);

        var editor = new ImGuiMenu("Editor")
            .AddChild(new ImGuiMenu("Maximize", null, () => MainWindow.IsMaximized = true))
            .AddChild(new ImGuiMenu("Restore", null, () => SDL.SDL_RestoreWindow(MainWindow.Handle)))
            .AddChild(new ImGuiMenu("Minimize", null, () => MainWindow.IsMinimized = true));
        _menuItems.Add(editor);
    }

    private void AddDefaultWindows()
    {
        var windows = new ImGuiEditorWindow[]
        {
            new LoadingScreenDebugWindow(),
            new WorldWindow() { IsOpen = false },
            GameWindow,
            _debugWindow,
            _demoWindow,
            _renderTargetsWindow,
            new InputDebugWindow(this),
            EditorWindow,
        };
        foreach (var window in windows)
        {
            _imGuiWindows.Add(window.Title, window);
        }
    }

    protected override void SetInputViewport()
    {
        switch (ActiveInput)
        {
            case ActiveInput.None:
                break;
            case ActiveInput.Game:
                base.SetInputViewport();
                break;
            case ActiveInput.GameWindow:
                InputHandler.SetViewportTransform(GameWindow.GameRenderView.GameRenderViewportTransform);
                break;
            case ActiveInput.RenderTargetsWindow:
                InputHandler.SetViewportTransform(_renderTargetsWindow.GameRenderView.GameRenderViewportTransform);
                break;
            case ActiveInput.EditorWindow:
                break;
            default:
                throw new ArgumentOutOfRangeException();
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
        var flags = ImGuiDockNodeFlags.PassthruCentralNode;
        flags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton);
        flags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton);

        var localFlags = (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton);
        localFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton);

        ViewportDockSpaceId = ImGui.DockSpaceOverViewport(mainViewport, flags);

        if (ResetDock)
        {
            ImGuiInternal.DockBuilderRemoveNodeChildNodes(ViewportDockSpaceId);

            var leftWidth = 0.2f;
            var rightWidth = 0.2f;
            uint dockLeftId;
            uint dockCenterId;
            uint dockRightId;

            ImGuiInternal.DockBuilderSplitNode(ViewportDockSpaceId, ImGuiDir.Left, leftWidth, &dockLeftId, &dockCenterId);
            ImGuiInternal.DockBuilderSplitNode(dockCenterId, ImGuiDir.Right, rightWidth, &dockRightId, null);

            var leftNode = ImGuiInternal.DockBuilderGetNode(dockLeftId);
            var rightNode = ImGuiInternal.DockBuilderGetNode(dockRightId);

            leftNode->LocalFlags |= localFlags;
            rightNode->LocalFlags |= localFlags;

            ImGuiInternal.DockBuilderDockWindow(DebugWindow.WindowTitle, dockLeftId);
            ImGuiInternal.DockBuilderDockWindow(LoadingScreenDebugWindow.WindowTitle, dockLeftId);
            ImGuiInternal.DockBuilderDockWindow(EditorWindow.WindowTitle, dockLeftId);
            ImGuiInternal.DockBuilderDockWindow(WorldWindow.WindowTitle, dockRightId);
            ImGuiInternal.DockBuilderDockWindow(InputDebugWindow.WindowTitle, dockRightId);

            ImGuiInternal.DockBuilderFinish(ViewportDockSpaceId);
        }

        ResetDock = false;

        return ViewportDockSpaceId;
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
            ImGuiRenderer.Update((float)dt.TotalSeconds, _imGuiRenderTarget.Size(), InputHandler);
            _imGuiUpdateCount++;
        }

        var wasHidden = IsHidden;

        InputHandler.KeyboardEnabled = !ImGui.GetIO()->WantCaptureKeyboard;
        InputHandler.MouseEnabled = ActiveInput is
            ActiveInput.GameWindow or
            ActiveInput.RenderTargetsWindow or
            ActiveInput.Game;
        var wasMouseEnabled = InputHandler.MouseEnabled;
        var wasKeyboardEnabled = InputHandler.KeyboardEnabled;
        base.Update(dt);

        ShowOrHideChildWindows(wasHidden);
    }

    private static void ShowOrHideChildWindows(bool wasHidden)
    {
        // show/hide child windows when showing/hiding imgui
        if (wasHidden == IsHidden)
            return;

        var platformIO = ImGui.GetPlatformIO();
        for (var i = 1; i < platformIO->Viewports.Size; i++)
        {
            ImGuiViewport* vp = platformIO->Viewports[i];
            var window = vp->ViewportData().Window;
            if (IsHidden)
                SDL.SDL_HideWindow(window.Handle);
            else
                SDL.SDL_ShowWindow(window.Handle);
        }
    }

    protected override void Draw(double alpha)
    {
        if (IsHidden)
        {
            ActiveInput = ActiveInput.Game;
            base.Draw(alpha);
            return;
        }

        if (MainWindow.IsMinimized)
            return;

        _renderStopwatch.Restart();
        var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

        RenderGame(ref commandBuffer, alpha, RenderTargets.CompositeRender);

        if (_lastUpdateCount < _imGuiUpdateCount)
        {
            _lastUpdateCount = _imGuiUpdateCount;
            PrevActiveInput = ActiveInput;
            ActiveInput = ActiveInput.None;

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

        if (swapTexture == null)
        {
            Logs.LogError("Could not acquire swapchain texture");
            return;
        }

        _swapSize = swapTexture.Size();

        if (_screenshot)
        {
            commandBuffer.CopyTextureToBuffer(RenderTargets.CompositeRender.Target, _screenshotBuffer);
        }

        if (_imGuiDrawCount > 0)
        {
            Renderer.DrawSprite(_imGuiRenderTarget, Matrix4x4.Identity, Color.White);
            Renderer.RunRenderPass(ref commandBuffer, swapTexture, Color.Black, null, true);
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

                // TODO: Fix saving of screenshots
                /*
                Texture.SavePNG(
                    filename,
                    (int)RenderTargets.CompositeRender.Width,
                    (int)RenderTargets.CompositeRender.Height,
                    RenderTargets.CompositeRender.Target.Format,
                    _screenshotPixels
                );*/
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
