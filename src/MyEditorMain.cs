using System.Threading;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Editor;
using NumVector2 = System.Numerics.Vector2;

namespace MyGame;

public unsafe class MyEditorMain : MyGameMain
{
    private Texture _gameRender;

    [CVar("imgui.hidden", "Toggle ImGui screen")]
    public static bool IsHidden = true;

    private readonly string[] _blendStateNames;

    private readonly ImGuiRenderer _imGuiRenderer;
    public ImGuiRenderer ImGuiRenderer => _imGuiRenderer;

    private readonly Sampler _sampler;
    private readonly float _alpha = 1.0f;
    private ulong _imGuiDrawCount;
    private readonly float _mainMenuPaddingY = 6f;
    private readonly List<ImGuiMenu> _menuItems = new();
    private int _updateRate = 1;
    internal SortedList<string, ImGuiEditorWindow> Windows = new();
    private bool _firstTime = true;
    private static readonly string _debugWindowName = "MyGame Debug";
    private static string _imguiDemoWindowName = "ImGui Demo Window";
    private string _gameWindowName = "Game";
    private IntPtr? _gameRenderTextureId;
    private Texture _imGuiRenderTarget;

    [CVar("screenshot", "Save a screenshot")]
    public static bool Screenshot;

    private static string[] _transitionTypeNames = Enum.GetNames<TransitionType>();
    private IInspector? _loadingScreenInspector;
    private string _loadingDebugWindowName = "LoadingDebug";
    private bool IsHoveringGameWindow;
    private Matrix4x4 _gameRenderViewportTransform;
    private NumVector2 _gameRenderOffset;
    private int _imGuiUpdateCount;
    private FileWatcher _fileWatcher;

    public MyEditorMain(WindowCreateInfo windowCreateInfo, FrameLimiterSettings frameLimiterSettings, int targetTimestep, bool debugMode) : base(
        windowCreateInfo,
        frameLimiterSettings, targetTimestep, debugMode)
    {
        var sz = DesignResolution;
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
            new ImGuiEditorCallbackWindow(_loadingDebugWindowName, DrawLoadingDebugWindow)
            {
                IsOpen = true,
                KeyboardShortcut = "^F4",
            },
            new WorldWindow(),
        };
        foreach (var window in windows)
        {
            Windows.Add(window.Title, window);
        }
    }

    private void DrawLoadingDebugWindow(ImGuiEditorWindow window)
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (ImGui.Begin(window.Title, ImGuiExt.RefPtr(ref window.IsOpen)))
        {
            ImGui.Checkbox("Debug Loading", ImGuiExt.RefPtr(ref LoadingScreen.Debug));
            if (ImGui.SliderFloat("Progress", ImGuiExt.RefPtr(ref LoadingScreen.DebugProgress), 0, 1.0f, "%g"))
            {
            }

            ImGui.TextUnformatted($"State: {LoadingScreen.DebugState.ToString()}");

            var transitionType = (int)LoadingScreen.TransitionType;
            if (BlendStateEditor.ComboStep("TransitionType", ref transitionType, _transitionTypeNames))
            {
                LoadingScreen.TransitionType = (TransitionType)transitionType;

                _loadingScreenInspector = InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            }


            _loadingScreenInspector ??= InspectorExt.GetGroupInspectorForTarget(LoadingScreen.SceneTransitions[LoadingScreen.TransitionType]);
            _loadingScreenInspector?.Draw();
        }

        ImGui.End();
    }

    protected override void SetInputViewport()
    {
        if (IsHidden)
            base.SetInputViewport();
    }

    private void DrawGameWindow(ImGuiEditorWindow window)
    {
        IsHoveringGameWindow = false;

        if (!window.IsOpen)
            return;

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
            _gameRenderOffset = cursorScreenPos - viewportPos;
            IsHoveringGameWindow = ImGui.IsWindowHovered();

            viewportTransform.Decompose(out var scale, out _, out _);
            _gameRenderViewportTransform = (Matrix3x2.CreateScale(scale.X, scale.Y) *
                                            Matrix3x2.CreateTranslation(_gameRenderOffset.X, _gameRenderOffset.Y)).ToMatrix4x4();
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
            ImGui.TextUnformatted($"MousePos: {InputHandler.MousePosition.ToString()}");
            ImGui.TextUnformatted($"MouseEnabled: {InputHandler.MouseEnabled.ToString()}");
            ImGui.TextUnformatted($"Nav: {(io->NavActive ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureMouse: {(io->WantCaptureMouse ? "Y" : "N")}");
            ImGui.TextUnformatted($"WantCaptureKeyboard: {(io->WantCaptureKeyboard ? "Y" : "N")}");
            ImGui.TextUnformatted($"DrawFps: {Time.DrawFps}");
            ImGui.TextUnformatted($"UpdateFps: {Time.UpdateFps}");
            ImGui.TextUnformatted($"Framerate: {(1000f / io->Framerate):0.##} ms/frame, FPS: {io->Framerate:0.##}");

            ImGui.TextUnformatted($"NumDrawCalls: {Renderer.SpriteBatch.MaxDrawCalls}, AddedSprites: {Renderer.SpriteBatch.MaxNumAddedSprites}");

            ImGui.BeginDisabled(Shared.LoadingScreen.IsLoading);
            if (ImGui.Button("Reload World" + LoadingIndicator(Shared.LoadingScreen.IsLoading), default))
            {
                GameScreen.Restart();
            }

            ImGui.EndDisabled();

            ImGui.SliderFloat("ShakeSpeed", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeSpeed), 0, 500, default);
            ImGui.SliderFloat("ShakeAmount", ImGuiExt.RefPtr(ref FancyTextComponent.ShakeAmount), 0, 500, default);
            ImGui.SliderInt("UpdateRate", ImGuiExt.RefPtr(ref _updateRate), 1, 10, default);

            ImGui.Separator();

            if (Shared.Game.GameScreen.World != null)
            {
                var player = Shared.Game.GameScreen.World.Player;

                if (ImGuiExt.BeginCollapsingHeader("Ground", Color.LightBlue))
                {
                    ImGui.Text("GroundCollisions");

                    foreach (var collision in player.Mover.GroundCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("GroundCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGui.Text("ContinuedGroundCollisions");

                    foreach (var collision in player.Mover.ContinuedGroundCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("ContinuedGroundCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGuiExt.EndCollapsingHeader();
                }

                if (ImGuiExt.BeginCollapsingHeader("Move", Color.LightBlue))
                {
                    ImGui.Text("MoveCollisions");

                    foreach (var collision in player.Mover.MoveCollisions)
                    {
                        if (ImGuiExt.BeginPropTable("MoveCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGui.Text("ContinuedMoveCollisions");
                    for (var i = 0; i < player.Mover.ContinuedMoveCollisions.Count; i++)
                    {
                        var collision = player.Mover.ContinuedMoveCollisions[i];
                        if (ImGuiExt.BeginPropTable("ContinuedMoveCollisions"))
                        {
                            DrawCollision(collision);
                            ImGui.EndTable();
                        }
                    }

                    ImGuiExt.EndCollapsingHeader();
                }
            }

            /*_mainMenuInspector ??= InspectorExt.GetInspectorForTarget(Shared.Menus.MainMenuScreen);
            _mainMenuInspector.Draw();
            ImGui.SliderFloat("GoalPosition", ImGuiExt.RefPtr(ref Shared.Menus.MainMenuScreen.Spring.EquilibriumPosition), -1, 1, default);
            public Spring Spring = new();
            public Vector2 Position;
            public Vector2 Scale = Vector2.One;
            public float MoveOffset = 500;
            public Vector2 Size = new Vector2(50, 25);
            public float ScaleFactor = 2f;
            public Vector2 InitialPosition = new Vector2(960, 100);*/
        }

        ImGui.End();
    }

    private static void DrawCollision(CollisionResult collision)
    {
        ImGuiExt.PropRow("Direction", collision.Direction.ToString());
        ImGuiExt.PropRow("PreviousPosition", collision.PreviousPosition.ToString());
        ImGuiExt.PropRow("Position", collision.Position.ToString());
        ImGuiExt.PropRow("Intersection", collision.Intersection.ToString());
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
            foreach (var (_, window) in Windows)
            {
                ImGui.MenuItem(window.Title, window.KeyboardShortcut, ImGuiExt.RefPtr(ref window.IsOpen));
            }

            ImGui.EndMenu();
        }

        DrawMainMenuButtons();

        ImGui.EndMainMenuBar();
    }

    private void DrawMainMenuButtons()
    {
        var max = ImGui.GetContentRegionMax();
        ImGui.SetCursorPosX(max.X / 2 - 29);

        var (icon, color, tooltip) = GameScreen.IsPaused switch
        {
            true => (FontAwesome6.Play, Color.Green, "Play"),
            _ => (FontAwesome6.Pause, Color.Yellow, "Pause")
        };

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
            ImGuiInternal.DockBuilderDockWindow(_debugWindowName, dockLeft);
            ImGuiInternal.DockBuilderDockWindow(_loadingDebugWindowName, dockLeft);
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

    private static bool IsHoveringGame()
    {
        var hoveredWindow = ImGui.GetCurrentContext()->HoveredWindow;
        if (hoveredWindow == null)
            return true;
        return ImGuiExt.StringFromPtr(hoveredWindow->Name) == "Game";
    }

    protected override void Update(TimeSpan dt)
    {
        if (!IsHidden)
        {
            _imGuiRenderer.Update((float)dt.TotalSeconds, _imGuiRenderTarget.Size(), InputState.Create(InputHandler));

            var io = ImGui.GetIO();
            if (io->WantCaptureKeyboard)
                InputHandler.KeyboardEnabled = false;
            if (io->NavActive || !IsHoveringGame())
                InputHandler.MouseEnabled = false;

            _imGuiUpdateCount++;
        }

        var wasHidden = IsHidden;

        InputHandler.SetViewportTransform(IsHoveringGameWindow ? _gameRenderViewportTransform : Matrix4x4.Identity);

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

        // TODO (marpe): Move
        if (Screenshot)
        {
            SaveRender(GraphicsDevice, _gameRender);
            Logger.LogInfo("Render saved!");
            Screenshot = false;
        }

        RenderGame(alpha, _gameRender);

        if (((int)Time.UpdateCount % _updateRate == 0) && _imGuiUpdateCount > 0)
        {
            _imGuiDrawCount++;
            _imGuiRenderer.Begin();
            DrawInternal();
            TextureUtils.EnsureTextureSize(ref _imGuiRenderTarget, GraphicsDevice, MainWindow.Size);
            _imGuiRenderer.End(_imGuiRenderTarget);
        }

        var (commandBuffer, swapTexture) = Renderer.AcquireSwapchainTexture();

        if (swapTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        {
            var (viewportTransform, viewport) = Renderer.GetViewportTransform(
                swapTexture.Size(),
                DesignResolution
            );
            var view = Matrix4x4.CreateTranslation(0, 0, -1000);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, swapTexture.Width, swapTexture.Height, 0, 0.0001f, 10000f);

            Renderer.DrawSprite(_gameRender, viewportTransform, Color.White);
            Renderer.Flush(commandBuffer, swapTexture, Color.Black, view * projection);
        }

        if (_imGuiDrawCount > 0)
        {
            Renderer.DrawSprite(_imGuiRenderTarget, Matrix4x4.Identity, Color.White);
            Renderer.Flush(commandBuffer, swapTexture, null, null);
        }

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
        _fileWatcher.Dispose();
    }
}
