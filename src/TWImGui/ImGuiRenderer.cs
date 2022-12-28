using Mochi.DearImGui;
using Mochi.DearImGui.Infrastructure;

namespace MyGame.TWImGui;

public enum ImGuiFont
{
    Tiny,

    // TinyBold,
    // Small,
    // SmallBold,
    Medium,
    MediumBold,
    // Default,
}

public unsafe class ImGuiRenderer : IDisposable
{
    public bool IsDisposed { get; private set; }

    public ColorAttachmentBlendState BlendState { get; private set; }

    private static KeyCode[] _keys = Enum.GetValues<KeyCode>();
    private readonly Dictionary<ImGuiFont, Pointer<ImFont>> _fonts = new();

    private readonly MyGameMain _game;

    private readonly Dictionary<ImGuiMouseCursor, IntPtr> _mouseCursors = new();

    private readonly Sampler _linearSampler;
    private readonly Sampler _pointSampler;

    public bool UsePointSampler = true;

    private readonly Num.Vector2 _scaleFactor = Num.Vector2.One;
    private readonly Dictionary<IntPtr, Texture> _textures = new();
    private IntPtr? _fontAtlasTextureId;
    private GCHandle _handle;

    private Buffer? _indexBuffer;
    private uint _indexBufferSize;
    private ImGuiMouseCursor _lastCursor = ImGuiMouseCursor.None;
    private GraphicsPipeline _pipeline;

    private Buffer? _vertexBuffer;
    private uint _vertexBufferSize;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static byte* GetClipboardText(void* userData)
    {
        var text = SDL.SDL_GetClipboardText();
        // NB (marpe): Should probably be freed by calling FreeCoTaskMem, see: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.stringtocotaskmemutf8?view=net-7.0
        return (byte*)Marshal.StringToCoTaskMemUTF8(text);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetClipboardText(void* userData, byte* text)
    {
        var str = Marshal.PtrToStringUTF8((IntPtr)text);
        SDL.SDL_SetClipboardText(str);
    }

    public ImGuiRenderer(MyGameMain game)
    {
        _game = game;

        ImGui.CHECKVERSION();
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();

        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io->ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io->ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io->BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io->BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io->BackendFlags |= ImGuiBackendFlags.RendererHasViewports;
        io->BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
        io->ConfigDockingTransparentPayload = true;

        io->GetClipboardTextFn = &GetClipboardText;
        io->SetClipboardTextFn = &SetClipboardText;

        io->HoverDelayNormal = 2.0f;
        
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

        _linearSampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.LinearClamp);
        _pointSampler = new Sampler(game.GraphicsDevice, SamplerCreateInfo.PointClamp);

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


        BuildFontAtlas();

        _game.MainWindow.WindowEvent += HandleWindowEvent;

        InitPlatformInterface(_game.MainWindow);

        _handle = GCHandle.Alloc(this, GCHandleType.Weak);
        io->BackendPlatformUserData = (void*)GCHandle.ToIntPtr(_handle);
    }

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

            io->Fonts->AddFontFromFileTTF(ContentPaths.fonts.fa_6_solid_900_ttf, iconFontSize, &fontConfigPtr, (char*)fa6IconRanges);
            return fontPtr;
        }

        foreach (var font in _fonts)
        {
            ((ImFont*)font.Value)->Destructor();
        }

        _fonts.Clear();

        _fonts[ImGuiFont.Medium] = CreateFont(ContentPaths.fonts.Roboto_Regular_ttf, 16, 14);
        _fonts[ImGuiFont.MediumBold] = CreateFont(ContentPaths.fonts.Roboto_Bold_ttf, 16, 14);
        _fonts[ImGuiFont.Tiny] = CreateFont(ContentPaths.fonts.Roboto_Bold_ttf, 13, 12);
        /*_fonts[ImGuiFont.Default] = ImGui.GetIO().Fonts.AddFontDefault();
        _fonts[ImGuiFont.Small] = CreateFont(fontPath, 14, 12);
        _fonts[ImGuiFont.SmallBold] = CreateFont(fontPathBold, 14, 12);
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

        sw.StopAndLog("ImGui.BuildFontAtlas");
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
            Logs.LogInfo("Disposing ImGuiRenderer");

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

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _linearSampler.Dispose();
            _pointSampler.Dispose();
            _pipeline.Dispose();
        }

        IsDisposed = true;
    }

    #endregion

    #region Rendering

    public void Begin()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        ImGui.NewFrame();
    }

    public void End(Texture renderDestination)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        ImGui.Render();

        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        Render(ref commandBuffer, renderDestination, ImGui.GetDrawData());
        _game.GraphicsDevice.Submit(commandBuffer);

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
                    Logs.LogError("Couldn't acquire swapchain texture");
                    continue;
                }

                Render(ref windowCommandBuffer, windowTexture, vp->DrawData);
                _game.GraphicsDevice.Submit(windowCommandBuffer);
            }
        }
    }

    private void Render(ref CommandBuffer commandBuffer, Texture swapchainTexture, ImDrawData* drawData)
    {
        UpdateBuffers(ref commandBuffer, drawData);
        commandBuffer.BeginRenderPass(
            new ColorAttachmentInfo(swapchainTexture, Color.Transparent)
        );
        RenderDrawData(ref commandBuffer, drawData);
        commandBuffer.EndRenderPass();
    }

    private void UpdateBuffers(ref CommandBuffer commandBuffer, ImDrawData* drawData)
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

    private void RenderDrawData(ref CommandBuffer commandBuffer, ImDrawData* drawData)
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

                var sampler = UsePointSampler ? _pointSampler : _linearSampler;
                var textureSamplerBindings = new TextureSamplerBinding(_textures[(IntPtr)drawCmd.TextureId], sampler);
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

    public void Update(float deltaTimeInSeconds, Point displaySize, InputHandler input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        var io = ImGui.GetIO();

        io->DisplaySize = new Num.Vector2(
            displaySize.X / _scaleFactor.X,
            displaySize.Y / _scaleFactor.Y
        );
        io->DisplayFramebufferScale = _scaleFactor;

        io->DeltaTime = deltaTimeInSeconds;
        UpdateInput(input);
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
                Logs.LogVerbose($"Window \"{window.Title}\" received close event");

                var viewport = ImGui.FindViewportByPlatformHandle((void*)window.Handle);
                viewport->PlatformRequestClose = true;

                var backend = GetPlatformBackend();
                if (window == backend._game.MainWindow)
                {
                    Logs.LogVerbose("MainWindow closing...");
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
            throw new InvalidOperationException("The current ImGui context has no associated platform backend");

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
            if (result != 0)
            {
                Logs.LogError($"SDL_GetDisplayUsableBounds failed: {SDL.SDL_GetError()}");
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

    private void UpdateInput(InputHandler input)
    {
        var io = ImGui.GetIO();

        SDL.SDL_GetGlobalMouseState(out var globalMouseX, out var globalMouseY);
        io->AddMousePosEvent(globalMouseX, globalMouseY);
        io->AddMouseWheelEvent(0, input.MouseWheelDelta);
        io->AddMouseButtonEvent(0, input.IsMouseButtonDown(MouseButtonCode.Left));
        io->AddMouseButtonEvent(1, input.IsMouseButtonDown(MouseButtonCode.Right));
        io->AddMouseButtonEvent(2, input.IsMouseButtonDown(MouseButtonCode.Middle));

        for (var i = 0; i < _keys.Length; i++)
        {
            var keyCode = _keys[i];
            if (!_keyMap.ContainsKey(keyCode))
                continue;
            io->AddKeyEvent(_keyMap[keyCode], input.IsKeyDown(keyCode));
        }

        io->AddKeyEvent(ImGuiKey.ImGuiMod_Ctrl, input.IsAnyKeyDown(InputHandler.ControlKeys));
        io->AddKeyEvent(ImGuiKey.ImGuiMod_Alt, input.IsAnyKeyDown(InputHandler.AltKeys));
        io->AddKeyEvent(ImGuiKey.ImGuiMod_Shift, input.IsAnyKeyDown(InputHandler.ShiftKeys));
        io->AddKeyEvent(ImGuiKey.ImGuiMod_Super, input.IsAnyKeyDown(InputHandler.MetaKeys));

        var textInput = input.GetTextInput();
        for (var i = 0; i < textInput.Length; i++)
        {
            io->AddInputCharacter(textInput[i]);
        }
    }

    #endregion

    #region Getters/Setters

    public void SetBlendState(ColorAttachmentBlendState blendState)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        _pipeline.Dispose();
        BlendState = blendState;
        _pipeline = SetupPipeline(_game.GraphicsDevice, blendState);
    }

    public Pointer<ImFont> GetFont(ImGuiFont font)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        return _fonts[font];
    }

    public IntPtr BindTexture(Texture texture)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        if (!_textures.ContainsKey(texture.Handle))
            _textures.Add(texture.Handle, texture);
        return texture.Handle;
    }

    public void UnbindTexture(IntPtr textureId)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

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
        var game = GetPlatformBackend()._game;
        var window = new Window(game.GraphicsDevice, windowCreateInfo, flags);
        window.WindowEvent += HandleWindowEvent;
        window.SetWindowPosition((int)viewport->Pos.X, (int)viewport->Pos.Y);

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
            Logs.LogVerbose($"ImGui destroying window: {window.Title}");
            window.WindowEvent -= HandleWindowEvent;
            window.Dispose();
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
        Logs.LogVerbose($"Created window: {titleStr}");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowAlpha(ImGuiViewport* viewport, float alpha)
    {
        var window = viewport->Window();
        SDL.SDL_SetWindowOpacity(window.Handle, alpha);
    }

    #endregion

    #region KeyMapping

    private static Dictionary<KeyCode, ImGuiKey> _keyMap = new()
    {
        { KeyCode.Tab, ImGuiKey.Tab },
        { KeyCode.Left, ImGuiKey.LeftArrow },
        { KeyCode.Right, ImGuiKey.RightArrow },
        { KeyCode.Up, ImGuiKey.UpArrow },
        { KeyCode.Down, ImGuiKey.DownArrow },
        { KeyCode.PageUp, ImGuiKey.PageUp },
        { KeyCode.PageDown, ImGuiKey.PageDown },
        { KeyCode.Home, ImGuiKey.Home },
        { KeyCode.End, ImGuiKey.End },
        { KeyCode.Insert, ImGuiKey.Insert },
        { KeyCode.Delete, ImGuiKey.Delete },
        { KeyCode.Backspace, ImGuiKey.Backspace },
        { KeyCode.Space, ImGuiKey.Space },
        { KeyCode.Return, ImGuiKey.Enter },
        { KeyCode.Escape, ImGuiKey.Escape },
        { KeyCode.LeftControl, ImGuiKey.LeftCtrl },
        { KeyCode.LeftShift, ImGuiKey.LeftShift },
        { KeyCode.LeftAlt, ImGuiKey.LeftAlt },
        { KeyCode.LeftMeta, ImGuiKey.LeftSuper },
        { KeyCode.RightControl, ImGuiKey.RightCtrl },
        { KeyCode.RightShift, ImGuiKey.RightShift },
        { KeyCode.RightAlt, ImGuiKey.RightAlt },
        { KeyCode.RightMeta, ImGuiKey.RightSuper },
        { KeyCode.D0, ImGuiKey._0 },
        { KeyCode.D1, ImGuiKey._1 },
        { KeyCode.D2, ImGuiKey._2 },
        { KeyCode.D3, ImGuiKey._3 },
        { KeyCode.D4, ImGuiKey._4 },
        { KeyCode.D5, ImGuiKey._5 },
        { KeyCode.D6, ImGuiKey._6 },
        { KeyCode.D7, ImGuiKey._7 },
        { KeyCode.D8, ImGuiKey._8 },
        { KeyCode.D9, ImGuiKey._9 },
        { KeyCode.A, ImGuiKey.A },
        { KeyCode.B, ImGuiKey.B },
        { KeyCode.C, ImGuiKey.C },
        { KeyCode.D, ImGuiKey.D },
        { KeyCode.E, ImGuiKey.E },
        { KeyCode.F, ImGuiKey.F },
        { KeyCode.G, ImGuiKey.G },
        { KeyCode.H, ImGuiKey.H },
        { KeyCode.I, ImGuiKey.I },
        { KeyCode.J, ImGuiKey.J },
        { KeyCode.K, ImGuiKey.K },
        { KeyCode.L, ImGuiKey.L },
        { KeyCode.M, ImGuiKey.M },
        { KeyCode.N, ImGuiKey.N },
        { KeyCode.O, ImGuiKey.O },
        { KeyCode.P, ImGuiKey.P },
        { KeyCode.Q, ImGuiKey.Q },
        { KeyCode.R, ImGuiKey.R },
        { KeyCode.S, ImGuiKey.S },
        { KeyCode.T, ImGuiKey.T },
        { KeyCode.U, ImGuiKey.U },
        { KeyCode.V, ImGuiKey.V },
        { KeyCode.W, ImGuiKey.W },
        { KeyCode.X, ImGuiKey.X },
        { KeyCode.Y, ImGuiKey.Y },
        { KeyCode.Z, ImGuiKey.Z },
        { KeyCode.F1, ImGuiKey.F1 },
        { KeyCode.F2, ImGuiKey.F2 },
        { KeyCode.F3, ImGuiKey.F3 },
        { KeyCode.F4, ImGuiKey.F4 },
        { KeyCode.F5, ImGuiKey.F5 },
        { KeyCode.F6, ImGuiKey.F6 },
        { KeyCode.F7, ImGuiKey.F7 },
        { KeyCode.F8, ImGuiKey.F8 },
        { KeyCode.F9, ImGuiKey.F9 },
        { KeyCode.F10, ImGuiKey.F10 },
        { KeyCode.F11, ImGuiKey.F11 },
        { KeyCode.F12, ImGuiKey.F12 },
        { KeyCode.Apostrophe, ImGuiKey.Apostrophe },
        { KeyCode.Comma, ImGuiKey.Comma },
        { KeyCode.Minus, ImGuiKey.Minus },
        { KeyCode.Period, ImGuiKey.Period },
        { KeyCode.Slash, ImGuiKey.Slash },
        { KeyCode.Semicolon, ImGuiKey.Semicolon },
        { KeyCode.Equals, ImGuiKey.Equal },
        { KeyCode.LeftBracket, ImGuiKey.LeftBracket },
        { KeyCode.Backslash, ImGuiKey.Backslash },
        { KeyCode.RightBracket, ImGuiKey.RightBracket },
        { KeyCode.Grave, ImGuiKey.GraveAccent },
        { KeyCode.CapsLock, ImGuiKey.CapsLock },
        { KeyCode.ScrollLock, ImGuiKey.ScrollLock },
        { KeyCode.NumLockClear, ImGuiKey.NumLock },
        { KeyCode.PrintScreen, ImGuiKey.PrintScreen },
        { KeyCode.Pause, ImGuiKey.Pause },
        { KeyCode.Keypad0, ImGuiKey.Keypad0 },
        { KeyCode.Keypad1, ImGuiKey.Keypad1 },
        { KeyCode.Keypad2, ImGuiKey.Keypad2 },
        { KeyCode.Keypad3, ImGuiKey.Keypad3 },
        { KeyCode.Keypad4, ImGuiKey.Keypad4 },
        { KeyCode.Keypad5, ImGuiKey.Keypad5 },
        { KeyCode.Keypad6, ImGuiKey.Keypad6 },
        { KeyCode.Keypad7, ImGuiKey.Keypad7 },
        { KeyCode.Keypad8, ImGuiKey.Keypad8 },
        { KeyCode.Keypad9, ImGuiKey.Keypad9 },
        { KeyCode.KeypadPeriod, ImGuiKey.KeypadDecimal },
        { KeyCode.KeypadDivide, ImGuiKey.KeypadDivide },
        { KeyCode.KeypadMultiply, ImGuiKey.KeypadMultiply },
        { KeyCode.KeypadMinus, ImGuiKey.KeypadSubtract },
        { KeyCode.KeypadPlus, ImGuiKey.KeypadAdd },
        { KeyCode.KeypadEnter, ImGuiKey.Enter },
    };

    #endregion
}

public static unsafe class ImGuiViewportPtrExt
{
    public static Window Window(this ImGuiViewport vp)
    {
        return (Window)(GCHandle.FromIntPtr((IntPtr)vp.PlatformUserData).Target ?? throw new InvalidOperationException("UserData was null"));
    }
}
