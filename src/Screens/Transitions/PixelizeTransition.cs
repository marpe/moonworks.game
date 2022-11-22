namespace MyGame.Screens.Transitions;

public class PixelizeTransition : SceneTransition
{
    private readonly QuadRenderer quadRenderer;
    public readonly GraphicsPipeline Pipeline;

    public Uniforms Uniform = new()
    {
        Progress = 0,
        SquaresMin = new Point(20, 20),
        Steps = 20,
    };

    private TextureSamplerBinding _sourceBinding;
    private ColorAttachmentInfo _colorAttachmentInfo;

    public PixelizeTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<Uniforms>(fragmentShader, "main", 2);

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = Renderer.GetVertexInputState(),
        };

        Pipeline = new GraphicsPipeline(
            device,
            myGraphicsPipelineCreateInfo
        );

        _sourceBinding = new TextureSamplerBinding();
        _sourceBinding.Sampler = Renderer.PointClamp;

        _colorAttachmentInfo = new ColorAttachmentInfo();
        _colorAttachmentInfo.LoadOp = LoadOp.Clear;
        _colorAttachmentInfo.ClearColor = Color.Cyan;
        
        quadRenderer = new QuadRenderer(device);
    }

    public override void Unload()
    {
        Pipeline.Dispose();
    }

    public override void Draw(Renderer renderer, CommandBuffer commandBuffer, Texture renderDestination, float progress, TransitionState state,
        Texture? copyOldGameRender, Texture? copyOldMenuRender, Texture? compositeOldCopy, Texture gameRender, Texture menuRender, Texture compositeNewCopy)
    {
        if (state == TransitionState.Hidden || compositeOldCopy == null)
            return;

        quadRenderer.Draw(new Sprite(compositeOldCopy), Color.White, 0, Matrix4x4.Identity, Renderer.PointClamp);
        quadRenderer.UpdateBuffers(commandBuffer);
        _colorAttachmentInfo.Texture = renderDestination;
        commandBuffer.BeginRenderPass(_colorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(Pipeline);
        Uniform.Progress = progress;
        Uniform.Steps = 30;
        commandBuffer.PushFragmentShaderUniforms(Uniform);
        _sourceBinding.Texture = compositeNewCopy;
        quadRenderer.Flush(commandBuffer, Renderer.GetViewProjection(renderDestination.Width, renderDestination.Height), _sourceBinding);
        commandBuffer.EndRenderPass();
    }

    public struct Uniforms
    {
        public float Progress;
        public int Steps;
        public Point SquaresMin;
    }
}

public class QuadRenderer
{
    private readonly GraphicsDevice _device;
    private Buffer _indexBuffer;

    private uint[] _indices;
    private uint _numSprites = 0;

    public uint LastNumAddedSprites { get; private set; }

    private TextureSamplerBinding[] _spriteInfo;
    private Buffer _vertexBuffer;

    private Position3DTextureColorVertex[] _vertices;

    public uint DrawCalls { get; private set; }

    public QuadRenderer(GraphicsDevice device)
    {
        _device = device;
        var maxSprites = 8192u;
        _spriteInfo = new TextureSamplerBinding[maxSprites];
        _vertices = new Position3DTextureColorVertex[_spriteInfo.Length * 4];
        _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(device, BufferUsageFlags.Vertex, (uint)_vertices.Length);
        _indices = SpriteBatch.GenerateIndexArray((uint)(_spriteInfo.Length * 6));
        _indexBuffer = Buffer.Create<uint>(device, BufferUsageFlags.Index, (uint)_indices.Length);
    }

    public void Unload()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void DrawRect(Rectangle rect, Color color, float depth = 0)
    {
        var scale = Matrix3x2.CreateScale(rect.Width, rect.Height) * Matrix3x2.CreateTranslation(rect.X, rect.Y);
        Draw(Shared.Game.Renderer.BlankSprite, color, depth, scale.ToMatrix4x4(), Renderer.PointClamp);
    }

    public void Draw(Sprite sprite, Color color, float depth, Matrix4x4 transform, Sampler sampler, SpriteFlip flip = SpriteFlip.None)
    {
        if (sprite.Texture.IsDisposed)
            throw new ObjectDisposedException(nameof(sprite.Texture));

        if (_numSprites == _spriteInfo.Length)
        {
            var maxNumSprites = (int)(_numSprites + 2048);
            Logger.LogInfo($"Max number of sprites reached, resizing buffers ({_numSprites} -> {maxNumSprites})");
            Array.Resize(ref _spriteInfo, maxNumSprites);
            Array.Resize(ref _vertices, _vertices.Length + _spriteInfo.Length * 4);

            _indices = SpriteBatch.GenerateIndexArray((uint)(_spriteInfo.Length * 6));

            _vertexBuffer.Dispose();
            _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(_device, BufferUsageFlags.Vertex, (uint)_vertices.Length);

            _indexBuffer.Dispose();
            _indexBuffer = Buffer.Create<uint>(_device, BufferUsageFlags.Index, (uint)_indices.Length);
        }

        _spriteInfo[_numSprites].Sampler = sampler;
        _spriteInfo[_numSprites].Texture = sprite.Texture;

        var vertexCount = _numSprites * 4;

        var topLeft = Vector2.Zero;
        var bottomLeft = new Vector2(0, sprite.SrcRect.Height);
        var topRight = new Vector2(sprite.SrcRect.Width, 0);
        var bottomRight = new Vector2(sprite.SrcRect.Width, sprite.SrcRect.Height);

        Vector2.Transform(ref topLeft, ref transform, out topLeft);
        Vector2.Transform(ref bottomLeft, ref transform, out bottomLeft);
        Vector2.Transform(ref topRight, ref transform, out topRight);
        Vector2.Transform(ref bottomRight, ref transform, out bottomRight);

        SetVector(ref _vertices[vertexCount].Position, topLeft, depth);
        SetVector(ref _vertices[vertexCount + 1].Position, bottomLeft, depth);
        SetVector(ref _vertices[vertexCount + 2].Position, topRight, depth);
        SetVector(ref _vertices[vertexCount + 3].Position, bottomRight, depth);

        _vertices[vertexCount].TexCoord = sprite.UV.TopLeft;
        _vertices[vertexCount + 1].TexCoord = sprite.UV.BottomLeft;
        _vertices[vertexCount + 2].TexCoord = sprite.UV.TopRight;
        _vertices[vertexCount + 3].TexCoord = sprite.UV.BottomRight;

        var effects = (byte)(flip & (SpriteFlip.FlipVertically | SpriteFlip.FlipHorizontally));
        _vertices[vertexCount].TexCoord.X = SpriteBatch.CornerOffsetX[0 ^ effects] * sprite.UV.Dimensions.X + sprite.UV.Position.X;
        _vertices[vertexCount].TexCoord.Y = SpriteBatch.CornerOffsetY[0 ^ effects] * sprite.UV.Dimensions.Y + sprite.UV.Position.Y;
        _vertices[vertexCount + 1].TexCoord.X = SpriteBatch.CornerOffsetX[1 ^ effects] * sprite.UV.Dimensions.X + sprite.UV.Position.X;
        _vertices[vertexCount + 1].TexCoord.Y = SpriteBatch.CornerOffsetY[1 ^ effects] * sprite.UV.Dimensions.Y + sprite.UV.Position.Y;
        _vertices[vertexCount + 2].TexCoord.X = SpriteBatch.CornerOffsetX[2 ^ effects] * sprite.UV.Dimensions.X + sprite.UV.Position.X;
        _vertices[vertexCount + 2].TexCoord.Y = SpriteBatch.CornerOffsetY[2 ^ effects] * sprite.UV.Dimensions.Y + sprite.UV.Position.Y;
        _vertices[vertexCount + 3].TexCoord.X = SpriteBatch.CornerOffsetX[3 ^ effects] * sprite.UV.Dimensions.X + sprite.UV.Position.X;
        _vertices[vertexCount + 3].TexCoord.Y = SpriteBatch.CornerOffsetY[3 ^ effects] * sprite.UV.Dimensions.Y + sprite.UV.Position.Y;

        _vertices[vertexCount].Color = color;
        _vertices[vertexCount + 1].Color = color;
        _vertices[vertexCount + 2].Color = color;
        _vertices[vertexCount + 3].Color = color;

        _numSprites += 1;
    }

    private static void SetVector(ref Vector3 dest, in Vector2 src, float z)
    {
        dest.X = src.X;
        dest.Y = src.Y;
        dest.Z = z;
    }

    public void UpdateBuffers(CommandBuffer commandBuffer)
    {
        if (_numSprites == 0)
        {
            Logger.LogWarn("Buffers are empty");
            return;
        }

        commandBuffer.SetBufferData(_indexBuffer, _indices, 0, 0, _numSprites * 6);
        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, 0, _numSprites * 4);
    }

    /// Iterates the submitted sprites, binds uniforms, samplers and calls DrawIndexedPrimitives 
    public void Flush(CommandBuffer commandBuffer, Matrix4x4 viewProjection, TextureSamplerBinding sourceBinding)
    {
        DrawCalls = 0;

        if (_numSprites == 0)
        {
            Logger.LogWarn("Flushing empty SpriteBatch");
            return;
        }

        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);

        var currSprite = _spriteInfo[0];
        var offset = 0;
        for (var i = 1; i < _numSprites; i += 1)
        {
            var spriteInfo = _spriteInfo[i];

            if (!SpriteBatch.BindingsAreEqual(currSprite, spriteInfo))
            {
                commandBuffer.BindFragmentSamplers(currSprite, sourceBinding);
                commandBuffer.DrawIndexedPrimitives(
                    (uint)(offset * 4),
                    0u,
                    (uint)((i - offset) * 2),
                    vertexParamOffset,
                    0u
                );
                DrawCalls++;
                currSprite = spriteInfo;
                offset = i;
            }
        }

        commandBuffer.BindFragmentSamplers(currSprite, sourceBinding);
        commandBuffer.DrawIndexedPrimitives(
            (uint)(offset * 4),
            0u,
            (uint)((_numSprites - offset) * 2),
            vertexParamOffset,
            0u
        );
        DrawCalls++;

        LastNumAddedSprites = _numSprites;
        _numSprites = 0;
    }
}
