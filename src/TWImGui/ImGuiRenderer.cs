#pragma warning disable CS8618

using System.Runtime.InteropServices;
using Mochi.DearImGui;
using Mochi.DearImGui.Infrastructure;
using MyGame.Graphics;
using SDL2;
using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame.TWImGui;

public enum ImGuiFont
{
    Tiny,
    TinyBold,
    Small,
    SmallBold,
    Medium,
    MediumBold,
    Default,
}

public unsafe class ImGuiRenderer
{
    private readonly Dictionary<ImGuiFont, Pointer<ImFont>> _fonts = new();

    private readonly MyGameMain _game;

    private readonly Dictionary<ImGuiMouseCursor, IntPtr> _mouseCursors = new();
    private readonly Sampler _sampler;

    private readonly Num.Vector2 _scaleFactor = Num.Vector2.One;
    private readonly Dictionary<IntPtr, Texture> _textures = new();
    private IntPtr? _fontAtlasTextureId;
    private GCHandle _handle;

    private Buffer? _indexBuffer;
    private uint _indexBufferSize;
    private ImGuiMouseCursor _lastCursor = ImGuiMouseCursor.None;
    private GraphicsPipeline _pipeline;

    private Texture _renderTarget;

    private int _textureIdCounter;

    private Buffer? _vertexBuffer;
    private uint _vertexBufferSize;

    public ImGuiRenderer(MyGameMain game)
    {
        _game = game;

        ImGui.CHECKVERSION();
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        SetupInput();

        var io = ImGui.GetIO();

        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io->ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io->ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io->BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io->BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io->BackendFlags |= ImGuiBackendFlags.RendererHasViewports;
        io->BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
        io->ConfigDockingTransparentPayload = true;

        // Set the backend name
        {
            var name = "MoonWorks.SDL";
            var byteCount = Encoding.UTF8.GetByteCount(name) + 1;
            var dataPtr = (byte*)ImGui.MemAlloc((nuint)byteCount);
            var byteSpan = new Span<byte>(dataPtr, byteCount);
            var encodedBytesCount = Encoding.UTF8.GetBytes(name, byteSpan);
            byteSpan[encodedBytesCount] = 0;
            io->BackendPlatformName = dataPtr;
        }

        io->ConfigViewportsNoDecoration = true;
        // io->BackendFlags |= ImGuiBackendFlags.HasMouseHoveredViewport;
        // io->ConfigViewportsNoAutoMerge = true;

        SDL.SDL_SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
        // SDL.SDL_SetHint(SDL.SDL_HINT_MOUSE_AUTO_CAPTURE, "0");

        UpdateMonitors();

        SetupMouseCursors();

        _sampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.LinearClamp);
        BlendState = new ColorAttachmentBlendState()
        {
            BlendEnable = true,
            AlphaBlendOp = BlendOp.Add,
            ColorBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RGBA,
            SourceColorBlendFactor = BlendFactor.SourceAlpha,
            SourceAlphaBlendFactor = BlendFactor.One,
            DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
            DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha,
        };
        _pipeline = SetupPipeline(game.GraphicsDevice, BlendState);

        var windowSize = game.MainWindow.Size;
        var textureFlags = TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget;
        _renderTarget = Texture.CreateTexture2D(game.GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y, TextureFormat.B8G8R8A8, textureFlags);

        BuildFontAtlas();

        _game.MainWindow.WindowEvent += HandleWindowEvent;

        InitPlatformInterface(_game.MainWindow);

        _handle = GCHandle.Alloc(this, GCHandleType.Weak);
        io->BackendPlatformUserData = (void*)GCHandle.ToIntPtr(_handle);
    }

    public bool IsDisposed { get; private set; }

    public Texture RenderTarget => _renderTarget;

    public ColorAttachmentBlendState BlendState { get; private set; }

    #region Setup

    private void SetupMouseCursors()
    {
        _mouseCursors.Add(ImGuiMouseCursor.Arrow, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW));
        _mouseCursors.Add(ImGuiMouseCursor.TextInput, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeAll, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNS, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeEW, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNESW, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW));
        _mouseCursors.Add(ImGuiMouseCursor.ResizeNWSE, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE));
        _mouseCursors.Add(ImGuiMouseCursor.Hand, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND));
        _mouseCursors.Add(ImGuiMouseCursor.NotAllowed, SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO));
    }

    private void InitPlatformInterface(Window mainWindow)
    {
        var io = ImGui.GetIO();
        var platformIO = ImGui.GetPlatformIO();
        io->BackendFlags |= ImGuiBackendFlags.PlatformHasViewports; // We can create multi-viewports on the Platform side (optional)

        platformIO->Platform_CreateWindow = &CreateWindow;
        platformIO->Platform_DestroyWindow = &DestroyWindow;
        platformIO->Platform_ShowWindow = &ShowWindow;
        platformIO->Platform_SetWindowPos = &SetWindowPos;
        platformIO->Platform_GetWindowPos = &GetWindowPos;
        platformIO->Platform_SetWindowSize = &SetWindowSize;
        platformIO->Platform_GetWindowSize = &GetWindowSize;
        platformIO->Platform_SetWindowFocus = &SetWindowFocus;
        platformIO->Platform_GetWindowFocus = &GetWindowFocus;
        platformIO->Platform_GetWindowMinimized = &GetWindowMinimized;
        platformIO->Platform_SetWindowTitle = &SetWindowTitle;
        platformIO->Platform_SetWindowAlpha = &SetWindowAlpha;

        // Register main window handle (which is owned by the main application, not by us)
        var mainViewport = ImGui.GetMainViewport();
        var gcHandle = GCHandle.Alloc(mainWindow);
        mainViewport->PlatformUserData = (void*)(IntPtr)gcHandle;
        mainViewport->PlatformHandle = (void*)mainWindow.Handle;

        SDL.SDL_SysWMinfo info = new();
        SDL.SDL_VERSION(out info.version);
        if (SDL.SDL_bool.SDL_TRUE == SDL.SDL_GetWindowWMInfo(mainWindow.Handle, ref info))
        {
            mainViewport->PlatformHandleRaw = (void*)info.info.win.window;
        }
    }

    private static GraphicsPipeline SetupPipeline(GraphicsDevice graphicsDevice, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(graphicsDevice, ContentPaths.Shaders.imgui.sprite_vert_spv);
        var fragmentShader =
            new ShaderModule(graphicsDevice, ContentPaths.Shaders.imgui.sprite_frag_spv);

        var myVertexBindings = new[]
        {
            VertexBinding.Create<PositionTextureColorVertex>(),
        };

        var myVertexAttributes = new[]
        {
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.Position), 0),
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<PositionTextureColorVertex>(nameof(PositionTextureColorVertex.Color), 2),
        };

        var myVertexInputState = new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes,
        };

        var pipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
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

    private void BuildFontAtlas()
    {
        var sw = Stopwatch.StartNew();
        var io = ImGui.GetIO();

        var fa6IconRanges = stackalloc ushort[] { FontAwesome6.IconMin, FontAwesome6.IconMax, 0 };
        var fa6FontPath = Path.Combine(MyGameMain.ContentRoot, "fonts", FontAwesome6.FontIconFileName);

        ImFont* CreateFont(string fontPath, int fontSize, int iconFontSize)
        {
            var fontPtr = io->Fonts->AddFontFromFileTTF(fontPath, fontSize);
            var fontConfigPtr = new ImFontConfig
            {
                MergeMode = true,
                PixelSnapH = true,
                GlyphMinAdvanceX = iconFontSize,
                RasterizerMultiply = 1.5f,
            };

            io->Fonts->AddFontFromFileTTF(fa6FontPath, iconFontSize, &fontConfigPtr, (char*)fa6IconRanges);
            return fontPtr;
        }

        var fontPath = ContentPaths.fonts.Roboto_Regular_ttf;
        var fontPathBold = ContentPaths.fonts.Roboto_Bold_ttf;

        foreach (var font in _fonts)
        {
            ((ImFont*)font.Value)->Destructor();
        }

        _fonts.Clear();

        _fonts[ImGuiFont.Medium] = CreateFont(fontPath, 16, 14);
        /*_fonts[ImGuiFont.Default] = ImGui.GetIO().Fonts.AddFontDefault();
        _fonts[ImGuiFont.MediumBold] = CreateFont(fontPathBold, 16, 14);
        _fonts[ImGuiFont.Small] = CreateFont(fontPath, 14, 12);
        _fonts[ImGuiFont.SmallBold] = CreateFont(fontPathBold, 14, 12);
        _fonts[ImGuiFont.Tiny] = CreateFont(fontPath, 12, 12);
        _fonts[ImGuiFont.TinyBold] = CreateFont(fontPathBold, 12, 12);*/

        byte* pixelData;
        int width;
        int height;
        int bytesPerPixel;
        io->Fonts->GetTexDataAsRGBA32(&pixelData, &width, &height, &bytesPerPixel);

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
        io->Fonts->SetTexID((void*)_fontAtlasTextureId.Value);
        io->Fonts->ClearTexData();

        // io.NativePtr->FontDefault = _fonts[ImGuiFont.Default];

        Logger.LogInfo($"Build ImGui fonts in {sw.ElapsedMilliseconds} ms");
    }

    private void SetupInput()
    {
        var io = ImGui.GetIO();

        io->KeyMap[(int)ImGuiKey.Tab] = (int)KeyCode.Tab;
        io->KeyMap[(int)ImGuiKey.LeftArrow] = (int)KeyCode.Left;
        io->KeyMap[(int)ImGuiKey.RightArrow] = (int)KeyCode.Right;
        io->KeyMap[(int)ImGuiKey.UpArrow] = (int)KeyCode.Up;
        io->KeyMap[(int)ImGuiKey.DownArrow] = (int)KeyCode.Down;
        io->KeyMap[(int)ImGuiKey.PageUp] = (int)KeyCode.PageUp;
        io->KeyMap[(int)ImGuiKey.PageDown] = (int)KeyCode.PageDown;
        io->KeyMap[(int)ImGuiKey.Home] = (int)KeyCode.Home;
        io->KeyMap[(int)ImGuiKey.End] = (int)KeyCode.End;
        io->KeyMap[(int)ImGuiKey.Delete] = (int)KeyCode.Delete;
        io->KeyMap[(int)ImGuiKey.Space] = (int)KeyCode.Space;
        io->KeyMap[(int)ImGuiKey.Backspace] = (int)KeyCode.Backspace;
        io->KeyMap[(int)ImGuiKey.Enter] = (int)KeyCode.Return;
        io->KeyMap[(int)ImGuiKey.Escape] = (int)KeyCode.Escape;
        io->KeyMap[(int)ImGuiKey.A] = (int)KeyCode.A;
        io->KeyMap[(int)ImGuiKey.C] = (int)KeyCode.C;
        io->KeyMap[(int)ImGuiKey.V] = (int)KeyCode.V;
        io->KeyMap[(int)ImGuiKey.X] = (int)KeyCode.X;
        io->KeyMap[(int)ImGuiKey.Y] = (int)KeyCode.Y;
        io->KeyMap[(int)ImGuiKey.Z] = (int)KeyCode.Z;
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            ImGui.DestroyPlatformWindows();

            var io = ImGui.GetIO();
            ImGui.MemFree(io->BackendPlatformName);
            io->BackendPlatformName = null;
            io->BackendPlatformUserData = null;
            _handle.Free();

            foreach (var texture in _textures)
            {
                texture.Value.Dispose();
            }

            _textures.Clear();

            foreach (var font in _fonts)
            {
                ((ImFont*)font.Value)->Destructor();
            }

            _fonts.Clear();

            foreach (var cursor in _mouseCursors)
            {
                SDL.SDL_FreeCursor(cursor.Value);
            }

            _mouseCursors.Clear();

            _renderTarget.Dispose();

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _sampler.Dispose();
            _pipeline.Dispose();
        }

        IsDisposed = true;
    }

    #endregion

    #region Rendering

    public void Begin()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        ImGui.NewFrame();
    }

    public void End()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        ImGui.Render();

        var windowSize = _game.MainWindow.Size;
        // SDL.SDL_Vulkan_GetDrawableSize(_game.MainWindow.Handle, out var width, out var height);
        TextureUtils.EnsureTextureSize(ref _renderTarget, _game.GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y);

        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        Render(commandBuffer, _renderTarget, ImGui.GetDrawData());
        _game.GraphicsDevice.Submit(commandBuffer);

        // Update and Render additional Platform Windows
        var io = ImGui.GetIO();
        if ((io->ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();

            var platformIO = ImGui.GetPlatformIO();
            for (var i = 1; i < platformIO->Viewports.Size; i++)
            {
                ImGuiViewport* vp = platformIO->Viewports[i];

                if ((vp->Flags & ImGuiViewportFlags.Minimized) != 0)
                {
                    continue;
                }

                var window = vp->Window();
                var windowCommandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
                var windowTexture = windowCommandBuffer.AcquireSwapchainTexture(window);
                if (windowTexture == null)
                {
                    Logger.LogError("Couldn't acquire swapchain texture");
                    continue;
                }

                Render(windowCommandBuffer, windowTexture, vp->DrawData);
                _game.GraphicsDevice.Submit(windowCommandBuffer);
            }
        }
    }

    private void Render(CommandBuffer commandBuffer, Texture swapchainTexture, ImDrawData* drawData)
    {
        UpdateBuffers(commandBuffer, drawData);
        commandBuffer.BeginRenderPass(
            new ColorAttachmentInfo(swapchainTexture, Color.Transparent)
        );
        RenderDrawData(commandBuffer, drawData);
        commandBuffer.EndRenderPass();
    }

    private void UpdateBuffers(CommandBuffer commandBuffer, ImDrawData* drawData)
    {
        var totalVtxBufferSize =
            (uint)(drawData->TotalVtxCount * Unsafe.SizeOf<PositionTextureColorVertex>()); // Unsafe.SizeOf<ImDrawVert>());
        if (totalVtxBufferSize > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();

            _vertexBufferSize = (uint)(drawData->TotalVtxCount * Unsafe.SizeOf<PositionTextureColorVertex>());
            _vertexBuffer = new Buffer(_game.GraphicsDevice, BufferUsageFlags.Vertex, _vertexBufferSize);
        }

        var totalIdxBufferSize = (uint)(drawData->TotalIdxCount * sizeof(ushort));
        if (totalIdxBufferSize > _indexBufferSize)
        {
            _indexBuffer?.Dispose();

            _indexBufferSize = (uint)(drawData->TotalIdxCount * sizeof(ushort));
            _indexBuffer = new Buffer(_game.GraphicsDevice, BufferUsageFlags.Index, _indexBufferSize);
        }

        var vtxOffset = 0u;
        var idxOffset = 0u;
        var vtxStride = Unsafe.SizeOf<PositionTextureColorVertex>();
        var idxStride = sizeof(ushort);

        for (var n = 0; n < drawData->CmdListsCount; n++)
        {
            var cmdList = drawData->CmdLists[n];
            var imVtxBufferSize = (uint)(cmdList->VtxBuffer.Size * vtxStride);
            var imIdxBufferSize = (uint)(cmdList->IdxBuffer.Size * idxStride);
            commandBuffer.SetBufferData(_vertexBuffer, (IntPtr)cmdList->VtxBuffer.Data, vtxOffset, imVtxBufferSize);
            commandBuffer.SetBufferData(_indexBuffer, (IntPtr)cmdList->IdxBuffer.Data, idxOffset, imIdxBufferSize);
            vtxOffset += imVtxBufferSize;
            idxOffset += imIdxBufferSize;
        }
    }

    private void RenderDrawData(CommandBuffer commandBuffer, ImDrawData* drawData)
    {
        if (drawData->CmdListsCount == 0)
        {
            return;
        }

        commandBuffer.BindGraphicsPipeline(_pipeline);

        commandBuffer.SetViewport(new Viewport
        {
            X = 0,
            Y = 0,
            W = Math.Max(1, drawData->DisplaySize.X),
            H = Math.Max(1, drawData->DisplaySize.Y),
            MaxDepth = 1,
            MinDepth = 0,
        });

        var viewProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
            drawData->DisplayPos.X,
            drawData->DisplayPos.X + drawData->DisplaySize.X,
            drawData->DisplayPos.Y + drawData->DisplaySize.Y,
            drawData->DisplayPos.Y,
            -1f,
            1f
        );
        var vtxUniformsOffset = commandBuffer.PushVertexShaderUniforms(viewProjectionMatrix);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.Sixteen);

        var vtxOffset = 0u;
        var idxOffset = 0u;

        // Will project scissor/clipping rectangles into framebuffer space
        var clipOffset = drawData->DisplayPos; // (0,0) unless using multi-viewports
        var clipScale = drawData->FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        for (var n = 0; n < drawData->CmdListsCount; n++)
        {
            var cmdList = drawData->CmdLists[n];

            for (var cmdi = 0; cmdi < cmdList->CmdBuffer.Size; cmdi++)
            {
                var drawCmd = cmdList->CmdBuffer[cmdi];

                if (!_textures.ContainsKey((IntPtr)drawCmd.TextureId))
                {
                    throw new InvalidOperationException(
                        $"Could not find a texture with id '{(IntPtr)drawCmd.TextureId}', check your bindings"
                    );
                }

                var textureSamplerBindings = new TextureSamplerBinding(_textures[(IntPtr)drawCmd.TextureId], _sampler);
                commandBuffer.BindFragmentSamplers(textureSamplerBindings);

                // Project scissor/clipping rectangles into framebuffer space
                var clipMin = new MoonWorks.Math.Float.Vector2(
                    (drawCmd.ClipRect.X - clipOffset.X) * clipScale.X,
                    (drawCmd.ClipRect.Y - clipOffset.Y) * clipScale.Y
                );
                var clipMax = new MoonWorks.Math.Float.Vector2(
                    (drawCmd.ClipRect.Z - clipOffset.X) * clipScale.X,
                    (drawCmd.ClipRect.W - clipOffset.Y) * clipScale.Y
                );

                // Clamp to viewport as vkCmdSetScissor() won't accept values that are off bounds
                clipMin.X = Math.Max(0, clipMin.X);
                clipMin.Y = Math.Max(0, clipMin.Y);
                clipMax.X = Math.Min(drawData->DisplaySize.X, clipMax.X);
                clipMax.Y = Math.Min(drawData->DisplaySize.Y, clipMax.Y);

                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                {
                    continue;
                }

                // Apply scissor/clipping rectangle
                var scissor = new Rect()
                {
                    X = (int)clipMin.X,
                    Y = (int)clipMin.Y,
                    W = (int)(clipMax.X - clipMin.X),
                    H = (int)(clipMax.Y - clipMin.Y),
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

            vtxOffset += (uint)cmdList->VtxBuffer.Size;
            idxOffset += (uint)cmdList->IdxBuffer.Size;
        }
    }

    #endregion

    #region Update

    public void Update(float deltaTimeInSeconds, in InputState inputState)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        var io = ImGui.GetIO();
        var mainWindowSize = _game.MainWindow.Size;
        io->DisplaySize = new Num.Vector2(
            mainWindowSize.X / _scaleFactor.X,
            mainWindowSize.Y / _scaleFactor.Y
        );
        io->DisplayFramebufferScale = _scaleFactor;

        io->DeltaTime = deltaTimeInSeconds;
        UpdateInput(inputState);
        UpdateMouseCursor();
        UpdateMonitors();
    }

    private static bool HandleWindowEvent(Window window, SDL.SDL_Event evt)
    {
        switch (evt.window.windowEvent)
        {
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                ImGui.GetIO()->AddFocusEvent(true);
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                ImGui.GetIO()->AddFocusEvent(false);
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
            {
                var viewport = ImGui.FindViewportByPlatformHandle((void*)window.Handle);
                viewport->PlatformRequestResize = true;
            }
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
            {
                var viewport = ImGui.FindViewportByPlatformHandle((void*)window.Handle);
                viewport->PlatformRequestMove = true;
            }
                break;
            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
            {
                var viewport = ImGui.FindViewportByPlatformHandle((void*)window.Handle);
                viewport->PlatformRequestClose = true;

                var backend = GetPlatformBackend();

                if (window == backend._game.MainWindow)
                {
                    backend._game.Quit();
                }
            }
                break;
        }

        return true;
    }

    private static ImGuiRenderer GetPlatformBackend()
    {
        var userData = (IntPtr)ImGui.GetIO()->BackendPlatformUserData;
        if (userData == IntPtr.Zero)
        {
            throw new InvalidOperationException("The current ImGui context has no associated platform backend");
        }

        var backend = (ImGuiRenderer)(GCHandle.FromIntPtr(userData).Target ?? throw new InvalidOperationException("Platform backend target was null"));
        return backend;
    }

    private void UpdateMonitors()
    {
        var platformIO = ImGui.GetPlatformIO();
        var numMonitors = SDL.SDL_GetNumVideoDisplays();
        platformIO->Monitors.resize(numMonitors);

        for (var i = 0; i < numMonitors; i++)
        {
            var result = SDL.SDL_GetDisplayUsableBounds(i, out var r);
            if (result < 0)
            {
                Logger.LogError($"SDL_GetDisplayUsableBounds failed: {SDL.SDL_GetError()}");
            }

            ref var monitor = ref platformIO->Monitors[i];
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
        if ((io->ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) != 0)
        {
            return;
        }

        var cursor = ImGui.GetMouseCursor();

        if (_lastCursor == cursor)
        {
            return;
        }

        if (io->MouseDrawCursor || cursor == ImGuiMouseCursor.None)
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

    private void UpdateInput(in InputState input)
    {
        var io = ImGui.GetIO();

        for (var i = 0; i < io->KeysDown.Length; i++)
        {
            if (!Enum.IsDefined((KeyCode)i))
            {
                continue;
            }

            io->KeysDown[i] = InputState.IsKeyDown(input, (KeyCode)i);
        }

        io->KeyShift = InputState.IsAnyKeyDown(input, InputHandler.ShiftKeys);
        io->KeyCtrl = InputState.IsAnyKeyDown(input, InputHandler.ControlKeys);
        io->KeyAlt = InputState.IsAnyKeyDown(input, InputHandler.AltKeys);
        io->KeySuper = InputState.IsAnyKeyDown(input, InputHandler.MetaKeys);

        io->MousePos = new Num.Vector2(input.GlobalMousePosition.X, input.GlobalMousePosition.Y);
        // io.MousePos = new Num.Vector2(_game.Inputs.Mouse.X, _game.Inputs.Mouse.Y);

        io->MouseDown[0] = InputState.IsMouseButtonDown(input, MouseButtonCode.Left);
        io->MouseDown[1] = InputState.IsMouseButtonDown(input, MouseButtonCode.Right);
        io->MouseDown[2] = InputState.IsMouseButtonDown(input, MouseButtonCode.Middle);

        io->MouseWheel = input.MouseWheelDelta;

        for (var i = 0; i < input.NumTextInputChars; i++)
        {
            var c = input.TextInput[i];
            io->AddInputCharacter(c);
        }
    }

    #endregion

    #region Getters/Setters

    public void SetBlendState(ColorAttachmentBlendState blendState)
    {
        _pipeline.Dispose();
        BlendState = blendState;
        _pipeline = SetupPipeline(_game.GraphicsDevice, blendState);
    }

    public Pointer<ImFont> GetFont(ImGuiFont font)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        return _fonts[font];
    }

    public IntPtr BindTexture(Texture texture)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        var id = new IntPtr(++_textureIdCounter);

        _textures.Add(id, texture);

        return id;
    }

    public void UnbindTexture(IntPtr textureId)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImGuiRenderer));
        }

        _textures.Remove(textureId);
    }

    #endregion

    #region PlatformWindowCallbacks

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CreateWindow(ImGuiViewport* viewport)
    {
        var flags = SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN | SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN;
        if ((viewport->Flags & ImGuiViewportFlags.NoTaskBarIcon) != 0)
        {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR;
        }

        if ((viewport->Flags & ImGuiViewportFlags.NoDecoration) != 0)
        {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS;
        }
        else
        {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        }

        if ((viewport->Flags & ImGuiViewportFlags.TopMost) != 0)
        {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
        }

        var windowCreateInfo = new WindowCreateInfo
        {
            WindowWidth = (uint)viewport->Size.X,
            WindowHeight = (uint)viewport->Size.Y,
            WindowTitle = "No Title Yet",
            ScreenMode = ScreenMode.Windowed,
            SystemResizable = true,
        };
        var window = new Window(windowCreateInfo, flags);
        window.WindowEvent += HandleWindowEvent;
        window.SetWindowPosition((int)viewport->Pos.X, (int)viewport->Pos.Y);

        var game = GetPlatformBackend()._game;

        // claim window calls SDL_Vulkan_CreateSurface
        if (!game.GraphicsDevice.ClaimWindow(window, window.PresentMode))
        {
            throw new SystemException("Could not claim window!");
        }

        var gcHandle = GCHandle.Alloc(window);
        viewport->PlatformHandle = (void*)window.Handle;
        viewport->PlatformUserData = (void*)(IntPtr)gcHandle;

        SDL.SDL_SysWMinfo info = new();
        SDL.SDL_VERSION(out info.version);
        if (SDL.SDL_bool.SDL_TRUE == SDL.SDL_GetWindowWMInfo(window.Handle, ref info))
        {
            viewport->PlatformHandleRaw = (void*)info.info.win.window;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void DestroyWindow(ImGuiViewport* viewport)
    {
        var gcHandle = GCHandle.FromIntPtr((IntPtr)viewport->PlatformUserData);
        if (gcHandle.Target != null)
        {
            var window = (Window)gcHandle.Target;
            var title = SDL.SDL_GetWindowTitle(window.Handle);
            Logger.LogInfo($"Destroying window: {title}");
            window.WindowEvent -= HandleWindowEvent;

            var game = GetPlatformBackend()._game;

            if (window.Claimed)
            {
                game.GraphicsDevice.UnclaimWindow(window);
            }

            if (!window.IsDisposed)
            {
                window.Dispose();
            }
        }

        gcHandle.Free();

        viewport->PlatformUserData = null;
        viewport->PlatformHandle = null;
        viewport->PlatformHandleRaw = null;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Num.Vector2* GetWindowPos(Num.Vector2* returnBuffer, ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        SDL.SDL_GetWindowPosition(window.Handle, out var x, out var y);
        *returnBuffer = new Num.Vector2(x, y);
        return returnBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowPos(ImGuiViewport* viewport, Num.Vector2 position)
    {
        var window = viewport->Window();
        SDL.SDL_SetWindowPosition(window.Handle, (int)position.X, (int)position.Y);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowSize(ImGuiViewport* viewport, Num.Vector2 size)
    {
        var window = viewport->Window();
        window.SetWindowSize((uint)size.X, (uint)size.Y);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Num.Vector2* GetWindowSize(Num.Vector2* returnBuffer, ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        var windowSize = window.Size;
        var size = new Num.Vector2(windowSize.X, windowSize.Y);
        *returnBuffer = size;
        return returnBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void ShowWindow(ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        SDL.SDL_ShowWindow(window.Handle);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowFocus(ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        SDL.SDL_RaiseWindow(window.Handle);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static NativeBoolean GetWindowFocus(ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        var flags = (SDL.SDL_WindowFlags)SDL.SDL_GetWindowFlags(window.Handle);
        return (flags & SDL.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS) != 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static NativeBoolean GetWindowMinimized(ImGuiViewport* viewport)
    {
        var window = viewport->Window();
        return window.IsMinimized;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowTitle(ImGuiViewport* viewport, byte* title)
    {
        var window = viewport->Window();
        var titleStr = ImGuiExt.StringFromPtr(title);
        SDL.SDL_SetWindowTitle(window.Handle, titleStr);
        Logger.LogInfo($"Created window: {titleStr}");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowAlpha(ImGuiViewport* viewport, float alpha)
    {
        var window = viewport->Window();
        SDL.SDL_SetWindowOpacity(window.Handle, alpha);
    }

    #endregion
}

public static unsafe class ImGuiViewportPtrExt
{
    public static Window Window(this ImGuiViewport vp)
    {
        return (Window)(GCHandle.FromIntPtr((IntPtr)vp.PlatformUserData).Target ?? throw new InvalidOperationException("UserData was null"));
    }
}
