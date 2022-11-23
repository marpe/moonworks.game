namespace MyGame.Screens.Transitions;

public class QuadRenderer
{
    private static Color[] _tempColors = new Color[4];

    private readonly GraphicsDevice _device;
    private Buffer _indexBuffer;

    private uint[] _indices;
    private uint _numSprites = 0;

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

        _tempColors.AsSpan().Fill(color);
        SpriteBatch.PushSpriteVertices(_vertices, _numSprites * 4, sprite, transform, depth, _tempColors, flip);

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

    private static void BindFragmentSamplers(CommandBuffer commandBuffer, in TextureSamplerBinding first, in TextureSamplerBinding? second)
    {
        if (second != null)
        {
            commandBuffer.BindFragmentSamplers(first, second.Value);
            return;
        }
        commandBuffer.BindFragmentSamplers(first);
    }
    
    /// Iterates the submitted sprites, binds uniforms, samplers and calls DrawIndexedPrimitives 
    public void Flush(CommandBuffer commandBuffer, Matrix4x4 viewProjection, TextureSamplerBinding? fragmentSamplerBinding)
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
        var offset = 0u;

        for (var i = 1u; i < _numSprites; i += 1)
        {
            var spriteInfo = _spriteInfo[i];

            if (SpriteBatch.BindingsAreEqual(currSprite, spriteInfo))
                continue;

            BindFragmentSamplers(commandBuffer, currSprite, fragmentSamplerBinding);
            SpriteBatch.DrawIndexedQuads(commandBuffer, offset, i - offset, vertexParamOffset);
            DrawCalls++;
            currSprite = spriteInfo;
            offset = i;
        }

        BindFragmentSamplers(commandBuffer, currSprite, fragmentSamplerBinding);
        SpriteBatch.DrawIndexedQuads(commandBuffer, offset, _numSprites - offset, vertexParamOffset);
        DrawCalls++;

        _numSprites = 0;
    }
}
