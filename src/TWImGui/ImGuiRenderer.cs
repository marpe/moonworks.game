using System.Runtime.InteropServices;
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

    private bool _beginCalled;

    private int _textureId;
    private Texture? _fontAtlasTexture;
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
            _fontAtlasTexture?.Dispose();

            foreach (var cursor in _mouseCursors)
            {
                SDL.SDL_FreeCursor(cursor.Value);
            }
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

        if (_beginCalled)
        {
            ImGui.Render();
            ImGui.UpdatePlatformWindows();

            Logger.LogError(
                "Begin has been called before calling End" +
                " after the last call to Begin." +
                " Begin cannot be called again until" +
                " End has been successfully called."
            );
        }

        _beginCalled = true;
        var io = ImGui.GetIO();
        io.DisplaySize = new Num.Vector2(
            _game.MainWindow.Width / _scaleFactor.X,
            _game.MainWindow.Height / _scaleFactor.Y
        );
        io.DisplayFramebufferScale = _scaleFactor;
        var platformIo = ImGui.GetPlatformIO();
        platformIo.Viewports[0].Pos = new Num.Vector2(0, 0);
        platformIo.Viewports[0].Size = new Num.Vector2(_game.MainWindow.Width, _game.MainWindow.Height);

        io.DeltaTime = deltaTimeInSeconds;
        UpdateInput();
        UpdateMouseCursor();
        // UpdateWindows();
        UpdateMonitors();
        ImGui.NewFrame();
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

        if (!_beginCalled)
        {
            Logger.LogError(
                "End was called, but Begin has not yet" +
                " been called. You must call Begin " +
                " successfully before you can call End."
            );
        }

        _beginCalled = false;
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
                // var window = (ImGuiWindow)GCHandle.FromIntPtr(vp.PlatformUserData).Target!;
                // var windowCommandBuffer = Render(window, vp.DrawData);
                // commandBuffers.Add(windowCommandBuffer);
            }
        }
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

        var windowSize = new Vector2(1920, 1080);

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

                if (clipMax.X > windowSize.X)
                {
                    clipMax.X = windowSize.X;
                }

                if (clipMax.Y > windowSize.Y)
                {
                    clipMax.Y = windowSize.Y;
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


    public void UpdateInput()
    {
        var io = ImGui.GetIO();

        UpdateMouseCursor();

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

        io.MousePos = new Num.Vector2(_game.Inputs.Mouse.X, _game.Inputs.Mouse.Y);

        io.MouseDown[0] = _game.Inputs.Mouse.LeftButton.IsDown;
        io.MouseDown[1] = _game.Inputs.Mouse.RightButton.IsDown;
        io.MouseDown[2] = _game.Inputs.Mouse.MiddleButton.IsDown;

        var scrollDelta = _game.Inputs.Mouse.Wheel;
        io.MouseWheel = scrollDelta switch
        {
            > 0 => 1,
            < 0 => -1,
            _ => 0
        };
    }

    public IntPtr BindTexture(Texture texture)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        var id = new IntPtr(++_textureId);

        _loadedTextures.Add(id, texture);

        return id;
    }

    public void UnbindTexture(IntPtr textureId)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ImGuiRenderer));

        _loadedTextures.Remove(textureId);
    }


    public unsafe void RebuildFontAtlas()
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

        _fonts[ImGuiFont.Medium] = CreateFont(fontPath, 16, 14);
        _fonts[ImGuiFont.Small] = CreateFont(fontPath, 14, 12);
        _fonts[ImGuiFont.Tiny] = CreateFont(fontPath, 12, 12);

        io.Fonts.TexDesiredWidth = 4096;
        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out var width, out var height, out var bytesPerPixel);

        var pixels = new byte[width * height * bytesPerPixel];
        Marshal.Copy(new IntPtr(pixelData), pixels, 0, pixels.Length);

        _fontAtlasTexture?.Dispose();
        _fontAtlasTexture =
            Texture.CreateTexture2D(_game.GraphicsDevice, (uint)width, (uint)height, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        var commandBuffer = _game.GraphicsDevice.AcquireCommandBuffer();
        commandBuffer.SetTextureData(_fontAtlasTexture, pixels);
        _game.GraphicsDevice.Submit(commandBuffer);

        if (_fontAtlasTextureId.HasValue)
        {
            UnbindTexture(_fontAtlasTextureId.Value);
        }

        _fontAtlasTextureId = BindTexture(_fontAtlasTexture);
        io.Fonts.SetTexID(_fontAtlasTextureId.Value);
        io.Fonts.ClearTexData();

        io.NativePtr->FontDefault = _fonts[ImGuiFont.Default];
    }
}
