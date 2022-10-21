using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using SDL2;

namespace MyGame.TWImGui;

public enum ImGuiFont
{
    Tiny,
    Small,
    Medium,
    Default = Medium,
}

public class ImGuiRenderer
{
    private readonly Dictionary<IntPtr, Texture> _loadedTextures = new();
    private readonly Dictionary<ImGuiFont, ImFontPtr> _fonts = new();

    private readonly Num.Vector2 _scaleFactor = Num.Vector2.One;

    private bool _frameBegun;

    private int _textureIdCounter;
    private IntPtr? _fontAtlasTextureId;

    private MoonWorks.Graphics.Buffer? _indexBuffer;
    private uint _indexBufferSize;

    private MoonWorks.Graphics.Buffer? _vertexBuffer;
    private uint _vertexBufferSize;

    public bool IsDisposed { get; private set; }

    private readonly Dictionary<ImGuiMouseCursor, IntPtr> _mouseCursors = new();
    private ImGuiMouseCursor _lastCursor = ImGuiMouseCursor.None;

    private readonly Game _game;
    private readonly Sampler _textureSampler;
    private readonly GraphicsPipeline _pipeline;

    private Platform_CreateWindow _createWindow;
    private Platform_DestroyWindow _destroyWindow;
    private Platform_GetWindowPos _getWindowPos;
    private Platform_ShowWindow _showWindow;
    private Platform_SetWindowPos _setWindowPos;
    private Platform_SetWindowSize _setWindowSize;
    private Platform_GetWindowSize _getWindowSize;
    private Platform_SetWindowFocus _setWindowFocus;
    private Platform_GetWindowFocus _getWindowFocus;
    private Platform_GetWindowMinimized _getWindowMinimized;
    private Platform_SetWindowTitle _setWindowTitle;

    public ImGuiRenderer(Game game)
    {
        _game = game;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        SetupInput();

        var io = ImGui.GetIO();

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        _mouseCursors.Add(ImGuiMouseCursor.Arrow, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW));
        _mouseCursors.Add(ImGuiMouseCursor.TextInput, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeAll, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNS, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeEW, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNESW, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNWSE, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE));
        _mouseCursors.Add(ImGuiMouseCursor.Hand, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND));
        _mouseCursors.Add(ImGuiMouseCursor.NotAllowed, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO));

        _textureSampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.LinearClamp);
        _pipeline = SetupPipeline(game.GraphicsDevice);

        Inputs.TextInput += OnTextInput;

        BuildFontAtlas();

        SetupMultiViewport(_game.MainWindow);
    }

    private unsafe void SetupMultiViewport(Window mainWindow)
    {
        var platformIO = ImGui.GetPlatformIO();
        var mainViewport = ImGui.GetMainViewport();
        mainViewport.PlatformHandle = mainWindow.Handle;
        var gcHandle = GCHandle.Alloc(mainWindow);
        mainViewport.PlatformUserData = (IntPtr)gcHandle;
        SDL.SDL_SysWMinfo info = new();
        SDL.SDL_VERSION(out info.version);
        if (SDL.SDL_bool.SDL_TRUE == SDL.SDL_GetWindowWMInfo(mainWindow.Handle, ref info))
        {
            mainViewport.PlatformHandleRaw = info.info.win.window;
        }

        _createWindow = CreateWindow;
        _destroyWindow = DestroyWindow;
        _getWindowPos = GetWindowPos;
        _setWindowPos = SetWindowPos;
        _setWindowSize = SetWindowSize;
        _getWindowSize = GetWindowSize;
        _showWindow = ShowWindow;
        _setWindowFocus = SetWindowFocus;
        _getWindowFocus = GetWindowFocus;
        _getWindowMinimized = GetWindowMinimized;
        _setWindowTitle = SetWindowTitle;

        platformIO.Platform_CreateWindow = Marshal.GetFunctionPointerForDelegate(_createWindow);
        platformIO.Platform_DestroyWindow = Marshal.GetFunctionPointerForDelegate(_destroyWindow);
        platformIO.Platform_ShowWindow = Marshal.GetFunctionPointerForDelegate(_showWindow);
        platformIO.Platform_SetWindowPos = Marshal.GetFunctionPointerForDelegate(_setWindowPos);
        platformIO.Platform_SetWindowSize = Marshal.GetFunctionPointerForDelegate(_setWindowSize);
        platformIO.Platform_SetWindowFocus = Marshal.GetFunctionPointerForDelegate(_setWindowFocus);
        platformIO.Platform_GetWindowFocus = Marshal.GetFunctionPointerForDelegate(_getWindowFocus);
        platformIO.Platform_GetWindowMinimized = Marshal.GetFunctionPointerForDelegate(_getWindowMinimized);
        platformIO.Platform_SetWindowTitle = Marshal.GetFunctionPointerForDelegate(_setWindowTitle);

        ImGuiNative.ImGuiPlatformIO_Set_Platform_GetWindowPos(platformIO.NativePtr, Marshal.GetFunctionPointerForDelegate(_getWindowPos));
        ImGuiNative.ImGuiPlatformIO_Set_Platform_GetWindowSize(platformIO.NativePtr, Marshal.GetFunctionPointerForDelegate(_getWindowSize));

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io.NativePtr->BackendPlatformName = (byte*)new FixedAsciiString("MoonWorks.SDL").DataPtr;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
        io.BackendFlags |= ImGuiBackendFlags.PlatformHasViewports;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasViewports;
        // io.BackendFlags |= ImGuiBackendFlags.HasMouseHoveredViewport;

        SDL.SDL_SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");

        UpdateMonitors();
    }

    private static GraphicsPipeline SetupPipeline(GraphicsDevice graphicsDevice)
    {
        var vertexShader = new ShaderModule(graphicsDevice, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.Imgui.SpriteVertSpv));
        var fragmentShader =
            new ShaderModule(graphicsDevice, Path.Combine(MyGameMain.ContentRoot, ContentPaths.Shaders.Imgui.SpriteFragSpv));

        var myVertexBindings = new VertexBinding[]
        {
            VertexBinding.Create<PositionTextureColorVertex>()
        };

        var myVertexAttributes = new VertexAttribute[]
        {
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.Position), 0),
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.Color), 2),
        };

        var myVertexInputState = new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes
        };

        var pipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.NonPremultiplied)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0),
            FragmentShaderInfo = GraphicsShaderInfo.Create(fragmentShader, "main", 1),
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = myVertexInputState,
        };

        return new GraphicsPipeline(
            graphicsDevice,
            pipelineCreateInfo
        );
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void Dispose(bool isDisposing)
    {
        if (IsDisposed)
            return;

        if (isDisposing)
        {
            foreach (var texture in _loadedTextures)
            {
                texture.Value.Dispose();
            }

            _loadedTextures.Clear();

            foreach (var font in _fonts)
            {
                font.Value.Destroy();
            }

            _fonts.Clear();

            foreach (var cursor in _mouseCursors)
            {
                SDL.SDL_FreeCursor(cursor.Value);
            }

            Inputs.TextInput -= OnTextInput;
        }

        IsDisposed = true;
    }

    private void SetupInput()
    {
        var io = ImGui.GetIO();

        io.KeyMap[(int)ImGuiKey.Tab] = (int)KeyCode.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)KeyCode.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)KeyCode.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)KeyCode.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)KeyCode.Down;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)KeyCode.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)KeyCode.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)KeyCode.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)KeyCode.End;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)KeyCode.Delete;
        io.KeyMap[(int)ImGuiKey.Space] = (int)KeyCode.Space;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)KeyCode.Backspace;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)KeyCode.Return;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)KeyCode.Escape;
        io.KeyMap[(int)ImGuiKey.A] = (int)KeyCode.A;
        io.KeyMap[(int)ImGuiKey.C] = (int)KeyCode.C;
        io.KeyMap[(int)ImGuiKey.V] = (int)KeyCode.V;
        io.KeyMap[(int)ImGuiKey.X] = (int)KeyCode.X;
        io.KeyMap[(int)ImGuiKey.Y] = (int)KeyCode.Y;
        io.KeyMap[(int)ImGuiKey.Z] = (int)KeyCode.Z;
    }

    public ImFontPtr GetFont(ImGuiFont font)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        return _fonts[font];
    }

    private void OnTextInput(char c)
    {
        if (c == '\t') return;

        ImGui.GetIO().AddInputCharacter(c);
    }

    public void Begin(float deltaTimeInSeconds)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        if (_frameBegun)
            throw new InvalidOperationException("Begin was called twice");

        _frameBegun = true;

        var io = ImGui.GetIO();
        io.DisplaySize = new Num.Vector2(
            _game.MainWindow.Width / _scaleFactor.X,
            _game.MainWindow.Height / _scaleFactor.Y
        );
        io.DisplayFramebufferScale = _scaleFactor;

        // TODO (marpe): Check if this is needed
        var platformIo = ImGui.GetPlatformIO();
        SDL.SDL_GetWindowPosition(_game.MainWindow.Handle, out var x, out var y);
        platformIo.Viewports[0].Pos = new Num.Vector2(x, y);
        platformIo.Viewports[0].Size = new Num.Vector2(_game.MainWindow.Width, _game.MainWindow.Height);

        io.DeltaTime = deltaTimeInSeconds;
        UpdateInput();
        UpdateMouseCursor();
        UpdateWindows();
        UpdateMonitors();
        ImGui.NewFrame();
    }

    private void UpdateWindows()
    {
        var viewports = ImGui.GetPlatformIO().Viewports;
        for (var i = 1; i < viewports.Size; i++)
        {
            var vp = viewports[i];
            var window = WindowFromUserData(vp.PlatformUserData);
            UpdateWindow(window, vp);
        }
    }

    private void UpdateWindow(Window window, ImGuiViewportPtr viewport)
    {
        var id = SDL.SDL_GetWindowID(window.Handle);

        while (SDL.SDL_PollEvent(out var evt) == 1)
        {
            if (evt.type != SDL.SDL_EventType.SDL_WINDOWEVENT)
                continue;

            if (id != evt.window.windowID)
                continue;

            Logger.LogInfo($"Received Event: {evt.window.type}");
            switch (evt.window.windowEvent)
            {
                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                    ImGui.GetIO().AddFocusEvent(true);
                    break;
                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                    ImGui.GetIO().AddFocusEvent(false);
                    break;
                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                    viewport.PlatformRequestResize = true;
                    break;
                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
                    viewport.PlatformRequestMove = true;
                    break;
                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                    viewport.PlatformRequestClose = true;
                    break;
                default:
                    Logger.LogWarn($"Unhandled window event: {evt.window.windowEvent}");
                    break;
            }
        }
    }

    private unsafe void UpdateMonitors()
    {
        var platformIO = ImGui.GetPlatformIO();
        Marshal.FreeHGlobal(platformIO.NativePtr->Monitors.Data);
        var numMonitors = SDL.SDL_GetNumVideoDisplays();
        var data = Marshal.AllocHGlobal(Unsafe.SizeOf<ImGuiPlatformMonitor>() * numMonitors);
        platformIO.NativePtr->Monitors = new ImVector(numMonitors, numMonitors, data);

        for (var i = 0; i < numMonitors; i++)
        {
            var result = SDL.SDL_GetDisplayUsableBounds(i, out var r);
            if (result < 0)
            {
                Logger.LogError($"SDL_GetDisplayUsableBounds failed: {SDL.SDL_GetError()}");
            }

            var monitor = platformIO.Monitors[i];
            monitor.DpiScale = 1f;
            monitor.MainPos = new Num.Vector2(r.x, r.y);
            monitor.MainSize = new Num.Vector2(r.w, r.h);
            monitor.WorkPos = new Num.Vector2(r.x, r.y);
            monitor.WorkSize = new Num.Vector2(r.w, r.h);
        }
    }

    private void UpdateMouseCursor()
    {
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) != 0)
        {
            return;
        }

        var cursor = ImGui.GetMouseCursor();

        if (_lastCursor == cursor)
        {
            return;
        }

        if (io.MouseDrawCursor || cursor == ImGuiMouseCursor.None)
        {
            SDL.SDL_ShowCursor((int)SDL.SDL_bool.SDL_FALSE);
        }
        else
        {
            SDL.SDL_ShowCursor((int)SDL.SDL_bool.SDL_TRUE);
            SDL.SDL_SetCursor(_mouseCursors[cursor]);
        }

        _lastCursor = cursor;
    }


    public void End(CommandBuffer commandBuffer, Texture swapchainTexture)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        if (!_frameBegun)
            throw new InvalidOperationException("Begin has not been called");

        _frameBegun = false;
        ImGui.Render();

        Render(commandBuffer, swapchainTexture, ImGui.GetDrawData());

        // Update and Render additional Platform Windows
        var io = ImGui.GetIO();
        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();

            var platformIO = ImGui.GetPlatformIO();
            for (var i = 1; i < platformIO.Viewports.Size; i++)
            {
                var vp = platformIO.Viewports[i];
                var window = WindowFromUserData(vp.PlatformUserData);
                var windowCommandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
                var windowSwapchainTexture = windowCommandBuffer.AcquireSwapchainTexture(window);
                if (windowSwapchainTexture == null)
                {
                    Logger.LogError("Couldn't acquire swapchain texture");
                    continue;
                }

                Render(windowCommandBuffer, windowSwapchainTexture, vp.DrawData);
                _game.GraphicsDevice.Submit(windowCommandBuffer);
            }
        }
    }

    private static Window WindowFromUserData(IntPtr userData)
    {
        return (Window)(GCHandle.FromIntPtr(userData).Target ?? throw new InvalidOperationException("UserData was null"));
    }

    private void Render(CommandBuffer commandBuffer, Texture swapchainTexture, ImDrawDataPtr drawData)
    {
        UpdateBuffers(_game.GraphicsDevice, commandBuffer, drawData);
        commandBuffer.BeginRenderPass(
            new ColorAttachmentInfo(swapchainTexture, LoadOp.Load)
        );
        RenderDrawData(commandBuffer, drawData);
        commandBuffer.EndRenderPass();
    }

    private void UpdateBuffers(GraphicsDevice graphicsDevice, CommandBuffer commandBuffer, ImDrawDataPtr drawData)
    {
        var totalVtxBufferSize =
            (uint)(drawData.TotalVtxCount * Unsafe.SizeOf<PositionTextureColorVertex>()); // Unsafe.SizeOf<ImDrawVert>());
        if (totalVtxBufferSize > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();

            _vertexBufferSize = (uint)(drawData.TotalVtxCount * Unsafe.SizeOf<PositionTextureColorVertex>());
            _vertexBuffer = new MoonWorks.Graphics.Buffer(graphicsDevice, BufferUsageFlags.Vertex, _vertexBufferSize);
        }

        var totalIdxBufferSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
        if (totalIdxBufferSize > _indexBufferSize)
        {
            _indexBuffer?.Dispose();

            _indexBufferSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
            _indexBuffer = new MoonWorks.Graphics.Buffer(graphicsDevice, BufferUsageFlags.Index, _indexBufferSize);
        }

        var vtxOffset = 0u;
        var idxOffset = 0u;
        var vtxStride = Unsafe.SizeOf<PositionTextureColorVertex>();
        var idxStride = sizeof(ushort);

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdListsRange[n];
            var imVtxBufferSize = (uint)(cmdList.VtxBuffer.Size * vtxStride);
            var imIdxBufferSize = (uint)(cmdList.IdxBuffer.Size * idxStride);
            commandBuffer.SetBufferData(_vertexBuffer, cmdList.VtxBuffer.Data, vtxOffset, imVtxBufferSize);
            commandBuffer.SetBufferData(_indexBuffer, cmdList.IdxBuffer.Data, idxOffset, imIdxBufferSize);
            vtxOffset += imVtxBufferSize;
            idxOffset += imIdxBufferSize;
        }
    }


    private void RenderDrawData(CommandBuffer commandBuffer, ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;

        commandBuffer.BindGraphicsPipeline(_pipeline);

        commandBuffer.SetViewport(new Viewport
        {
            X = 0,
            Y = 0,
            W = drawData.DisplaySize.X,
            H = drawData.DisplaySize.Y,
            MaxDepth = 1,
            MinDepth = 0
        });

        var viewProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
            drawData.DisplayPos.X,
            drawData.DisplayPos.X + drawData.DisplaySize.X,
            drawData.DisplayPos.Y + drawData.DisplaySize.Y,
            drawData.DisplayPos.Y,
            -1f,
            1f
        );
        var vtxUniformsOffset = commandBuffer.PushVertexShaderUniforms(viewProjectionMatrix);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.Sixteen);

        var vtxOffset = 0u;
        var idxOffset = 0u;

        // Will project scissor/clipping rectangles into framebuffer space
        var clipOffset = drawData.DisplayPos; // (0,0) unless using multi-viewports
        var clipScale = drawData.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdListsRange[n];

            for (var cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                var drawCmd = cmdList.CmdBuffer[cmdi];

                if (!_loadedTextures.ContainsKey(drawCmd.TextureId))
                {
                    throw new InvalidOperationException(
                        $"Could not find a texture with id '{drawCmd.TextureId}', check your bindings"
                    );
                }

                var textureSamplerBindings = new TextureSamplerBinding(_loadedTextures[drawCmd.TextureId], _textureSampler);
                commandBuffer.BindFragmentSamplers(textureSamplerBindings);

                // Project scissor/clipping rectangles into framebuffer space
                var clipMin = new Vector2((drawCmd.ClipRect.X - clipOffset.X) * clipScale.X,
                    (drawCmd.ClipRect.Y - clipOffset.Y) * clipScale.Y);
                var clipMax = new Vector2((drawCmd.ClipRect.Z - clipOffset.X) * clipScale.X,
                    (drawCmd.ClipRect.W - clipOffset.Y) * clipScale.Y);

                // Clamp to viewport as vkCmdSetScissor() won't accept values that are off bounds
                if (clipMin.X < 0.0f)
                {
                    clipMin.X = 0.0f;
                }

                if (clipMin.Y < 0.0f)
                {
                    clipMin.Y = 0.0f;
                }

                if (clipMax.X > drawData.DisplaySize.X)
                {
                    clipMax.X = drawData.DisplaySize.X;
                }

                if (clipMax.Y > drawData.DisplaySize.Y)
                {
                    clipMax.Y = drawData.DisplaySize.Y;
                }

                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                    continue;

                // Apply scissor/clipping rectangle
                var scissor = new Rect()
                {
                    X = (int)clipMin.X,
                    Y = (int)clipMin.Y,
                    W = (int)(clipMax.X - clipMin.X),
                    H = (int)(clipMax.Y - clipMin.Y)
                };
                commandBuffer.SetScissor(scissor);

                commandBuffer.DrawIndexedPrimitives(
                    vtxOffset + drawCmd.VtxOffset,
                    idxOffset + drawCmd.IdxOffset,
                    drawCmd.ElemCount / 3,
                    vtxUniformsOffset,
                    0
                );
            }

            vtxOffset += (uint)cmdList.VtxBuffer.Size;
            idxOffset += (uint)cmdList.IdxBuffer.Size;
        }
    }

    private void UpdateInput()
    {
        var io = ImGui.GetIO();

        for (var i = 0; i < io.KeysDown.Count; i++)
        {
            if (!Enum.IsDefined((KeyCode)i))
                continue;
            io.KeysDown[i] = _game.Inputs.Keyboard.IsDown((KeyCode)i);
        }

        io.KeyShift = _game.Inputs.Keyboard.IsDown(KeyCode.LeftShift) ||
                      _game.Inputs.Keyboard.IsDown(KeyCode.RightShift);
        io.KeyCtrl = _game.Inputs.Keyboard.IsDown(KeyCode.LeftControl) ||
                     _game.Inputs.Keyboard.IsDown(KeyCode.RightControl);
        io.KeyAlt = _game.Inputs.Keyboard.IsDown(KeyCode.LeftAlt) ||
                    _game.Inputs.Keyboard.IsDown(KeyCode.RightAlt);
        io.KeySuper = _game.Inputs.Keyboard.IsDown(KeyCode.LeftMeta) ||
                      _game.Inputs.Keyboard.IsDown(KeyCode.RightMeta);

        SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
        io.MousePos = new Num.Vector2(globalMouseX, globalMouseY);
        // io.MousePos = new Num.Vector2(_game.Inputs.Mouse.X, _game.Inputs.Mouse.Y);

        io.MouseDown[0] = _game.Inputs.Mouse.LeftButton.IsDown;
        io.MouseDown[1] = _game.Inputs.Mouse.RightButton.IsDown;
        io.MouseDown[2] = _game.Inputs.Mouse.MiddleButton.IsDown;

        io.MouseWheel = _game.Inputs.Mouse.WheelDelta;
    }

    public IntPtr BindTexture(Texture texture)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        var id = new IntPtr(++_textureIdCounter);

        _loadedTextures.Add(id, texture);

        return id;
    }

    public void UnbindTexture(IntPtr textureId)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        _loadedTextures.Remove(textureId);
    }

    private unsafe void BuildFontAtlas()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        var io = ImGui.GetIO();
        // var defaultFontPtr = ImGui.GetIO().Fonts.AddFontDefault();

        var fa6IconRanges = stackalloc ushort[] { FontAwesome6.IconMin, FontAwesome6.IconMax, 0 };
        var fa6FontPath = Path.Combine(MyGameMain.ContentRoot, "fonts", FontAwesome6.FontIconFileName);

        ImFontPtr CreateFont(string fontPath, int fontSize, int iconFontSize)
        {
            var fontPtr = io.Fonts.AddFontFromFileTTF(fontPath, fontSize);
            var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            var fontConfigPtr = new ImFontConfigPtr(fontConfig);
            fontConfigPtr.MergeMode = true;
            fontConfigPtr.PixelSnapH = true;
            fontConfigPtr.GlyphMinAdvanceX = iconFontSize;
            fontConfigPtr.RasterizerMultiply = 1.5f;

            io.Fonts.AddFontFromFileTTF(fa6FontPath, iconFontSize, fontConfigPtr, (IntPtr)fa6IconRanges);

            fontConfigPtr.Destroy();
            return fontPtr;
        }

        var fontPath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Fonts.RobotoRegularTtf);

        foreach (var font in _fonts)
        {
            font.Value.Destroy();
        }

        _fonts.Clear();

        _fonts[ImGuiFont.Medium] = CreateFont(fontPath, 16, 14);
        _fonts[ImGuiFont.Small] = CreateFont(fontPath, 14, 12);
        _fonts[ImGuiFont.Tiny] = CreateFont(fontPath, 12, 12);

        io.Fonts.TexDesiredWidth = 4096;
        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out var width, out var height, out var bytesPerPixel);

        var pixels = new byte[width * height * bytesPerPixel];
        Marshal.Copy(new IntPtr(pixelData), pixels, 0, pixels.Length);

        var fontAtlasTexture = Texture.CreateTexture2D(_game.GraphicsDevice, (uint)width, (uint)height, TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler);
        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        commandBuffer.SetTextureData(fontAtlasTexture, pixels);
        _game.GraphicsDevice.Submit(commandBuffer);

        if (_fontAtlasTextureId.HasValue)
        {
            UnbindTexture(_fontAtlasTextureId.Value);
        }

        _fontAtlasTextureId = BindTexture(fontAtlasTexture);
        io.Fonts.SetTexID(_fontAtlasTextureId.Value);
        io.Fonts.ClearTexData();

        io.NativePtr->FontDefault = _fonts[ImGuiFont.Default];
    }

    private void CreateWindow(ImGuiViewportPtr vp)
    {
        var flags = SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN | SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN;
        if ((vp.Flags & ImGuiViewportFlags.NoTaskBarIcon) != 0)
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR;
        if ((vp.Flags & ImGuiViewportFlags.NoDecoration) != 0)
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS;
        else
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        if ((vp.Flags & ImGuiViewportFlags.TopMost) != 0)
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;

        var windowCreateInfo = new WindowCreateInfo
        {
            WindowWidth = 200,
            WindowHeight = 200,
            WindowTitle = "ProjectName",
            ScreenMode = ScreenMode.Windowed,
            SystemResizable = true
        };
        var ImGuiWindow = new Window(windowCreateInfo, flags);
        if (!_game.GraphicsDevice.ClaimWindow(ImGuiWindow, windowCreateInfo.PresentMode))
        {
            throw new SystemException("Could not claim window!");
        }

        var gcHandle = GCHandle.Alloc(ImGuiWindow);
        vp.PlatformUserData = (IntPtr)gcHandle;
        Logger.LogInfo("Created window");
    }

    private void DestroyWindow(ImGuiViewportPtr vp)
    {
        var gcHandle = GCHandle.FromIntPtr(vp.PlatformUserData);
        if (gcHandle.Target != null)
        {
            var window = (Window)gcHandle.Target;
            window.Dispose();
        }

        vp.PlatformUserData = IntPtr.Zero;
        gcHandle.Free();
        Logger.LogInfo("Destroyed window");
    }

    private unsafe void GetWindowPos(ImGuiViewportPtr vp, Num.Vector2* outPos)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        SDL.SDL_GetWindowPosition(window.Handle, out var x, out var y);
        var pos = new Num.Vector2(x, y);
        *outPos = pos;
    }

    private void SetWindowPos(ImGuiViewportPtr vp, Num.Vector2 pos)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        SDL.SDL_SetWindowPosition(window.Handle, (int)pos.X, (int)pos.Y);
    }

    private void SetWindowSize(ImGuiViewportPtr vp, Num.Vector2 size)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        window.SetWindowSize((uint)size.X, (uint)size.Y);
    }

    private unsafe void GetWindowSize(ImGuiViewportPtr vp, Num.Vector2* outSize)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        var size = new Num.Vector2(window.Width, window.Height);
        *outSize = size;
    }

    private void ShowWindow(ImGuiViewportPtr vp)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        SDL.SDL_ShowWindow(window.Handle);
    }

    private void SetWindowFocus(ImGuiViewportPtr vp)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        SDL.SDL_RaiseWindow(window.Handle);
    }

    private byte GetWindowFocus(ImGuiViewportPtr vp)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        var flags = (SDL.SDL_WindowFlags)SDL.SDL_GetWindowFlags(window.Handle);
        return (flags & SDL.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS) != 0 ? (byte)1 : (byte)0;
    }

    private byte GetWindowMinimized(ImGuiViewportPtr vp)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        var flags = (SDL.SDL_WindowFlags)SDL.SDL_GetWindowFlags(window.Handle);
        return (flags & SDL.SDL_WindowFlags.SDL_WINDOW_MINIMIZED) != 0 ? (byte)1 : (byte)0;
    }

    private unsafe void SetWindowTitle(ImGuiViewportPtr vp, IntPtr title)
    {
        var window = WindowFromUserData(vp.PlatformUserData);
        var titlePtr = (byte*)title;
        var count = 0;
        while (titlePtr[count] != 0)
        {
            titlePtr += 1;
        }

        var titleStr = Encoding.ASCII.GetString(titlePtr, count);
        SDL.SDL_SetWindowTitle(window.Handle, titleStr);
    }
}

public sealed class FixedAsciiString : IDisposable
{
    public IntPtr DataPtr { get; }

    public unsafe FixedAsciiString(string s)
    {
        var byteCount = Encoding.ASCII.GetByteCount(s);
        DataPtr = Marshal.AllocHGlobal(byteCount + 1);
        fixed (char* sPtr = s)
        {
            var end = Encoding.ASCII.GetBytes(sPtr, s.Length, (byte*)DataPtr, byteCount);
            ((byte*)DataPtr)[end] = 0;
        }
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(DataPtr);
    }
}
