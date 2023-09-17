namespace MyGame.Graphics;

[Flags]
public enum SpriteFlip
{
    None = 0,
    FlipVertically = 1,
    FlipHorizontally = 2,
}

public class SpriteBatch
{
    public static Sampler PointClamp = null!;
    public static Sampler LinearClamp = null!;

    [CVar("batch_round", "Toggle rounding of destinations when rendering with SpriteBatch")]
    public static bool ShouldRoundPositions;

    // Used to calculate texture coordinates
    public static readonly float[] CornerOffsetX = { 0.0f, 0.0f, 1.0f, 1.0f };
    public static readonly float[] CornerOffsetY = { 0.0f, 1.0f, 0.0f, 1.0f };
    private readonly GraphicsDevice _device;
    private Buffer _indexBuffer;

    private uint[] _indices;

    private int _numSprites;
    public int NumSprites
    {
        get => _numSprites;
    }

    private Texture[] _spriteInfo;
    private Buffer _vertexBuffer;

    private Position3DTextureColorVertex[] _vertices;

    private TextureSamplerBinding[] _fragmentSamplerBindings;
    private bool _indicesNeedsUpdate = true;
    public static int DrawCalls;

    public SpriteBatch(GraphicsDevice device)
    {
        PointClamp = new Sampler(device, SamplerCreateInfo.PointClamp);
        LinearClamp = new Sampler(device, SamplerCreateInfo.LinearClamp);

        _device = device;
        var maxSprites = 8192u;
        _spriteInfo = new Texture[maxSprites];
        _vertices = new Position3DTextureColorVertex[_spriteInfo.Length * 4];
        _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(device, BufferUsageFlags.Vertex, (uint)_vertices.Length);
        _indices = GenerateIndexArray((uint)(_spriteInfo.Length * 6));
        _indexBuffer = Buffer.Create<uint>(device, BufferUsageFlags.Index, (uint)_indices.Length);
        _fragmentSamplerBindings = new TextureSamplerBinding[1];
    }

    public void Unload()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();

        PointClamp.Dispose();
        LinearClamp.Dispose();
    }

    public void Draw(Texture texture, in Bounds srcRect, in Bounds dstRect, Color[] colors, float depth, SpriteFlip flip = SpriteFlip.None)
    {
        var uvs = new UV();
        Sprite.GenerateUVs(ref uvs, texture, srcRect);
        Draw(texture, uvs, dstRect, colors, depth, flip);
    }

    public void Draw(Texture texture, in UV uvs, in Bounds dstRect, Color[] colors, float depth, SpriteFlip flip = SpriteFlip.None)
    {
        if (_numSprites == _spriteInfo.Length)
        {
            var maxNumSprites = _numSprites + 2048;
            Logs.LogInfo($"Max number of sprites reached, resizing buffers ({_numSprites} -> {maxNumSprites})");
            Array.Resize(ref _spriteInfo, maxNumSprites);
            Array.Resize(ref _vertices, _vertices.Length + _spriteInfo.Length * 4);

            _indices = GenerateIndexArray((uint)(_spriteInfo.Length * 6));

            _vertexBuffer.Dispose();
            _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(_device, BufferUsageFlags.Vertex, (uint)_vertices.Length);

            _indexBuffer.Dispose();
            _indexBuffer = Buffer.Create<uint>(_device, BufferUsageFlags.Index, (uint)_indices.Length);
            _indicesNeedsUpdate = true;
        }

        if (_numSprites > 0 && _spriteInfo[_numSprites - 1] != texture)
        {
            // this if statement is for debugging texture changes easily
        }
        
        _spriteInfo[_numSprites] = texture;
        PushSpriteVertices(_vertices, _numSprites * 4, uvs, dstRect, depth, colors, flip);
        _numSprites += 1;
    }

    private static void SetVector(ref Vector3 dest, in Vector2 src, float z)
    {
        dest.X = src.X;
        dest.Y = src.Y;
        dest.Z = z;
    }

    public void UpdateBuffers(ref CommandBuffer commandBuffer)
    {
        if (_numSprites == 0)
        {
            Logs.LogWarn("Buffers are empty");
            return;
        }

        if (_indicesNeedsUpdate)
        {
            commandBuffer.SetBufferData(_indexBuffer, _indices, 0, 0, (uint)(_spriteInfo.Length * 6));
            _indicesNeedsUpdate = false;
        }

        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, 0, (uint)(_numSprites * 4));
    }

    /// Iterates the submitted sprites, binds uniforms, samplers and calls DrawIndexedPrimitives 
    public void DrawIndexed<TVertUniform, TFragmentUniform>(ref CommandBuffer commandBuffer, TVertUniform vertUniforms, TFragmentUniform fragmentUniforms,
        TextureSamplerBinding[] fragmentSamplerBindings, bool usePointFiltering, int offset, int numSprites)
        where TVertUniform : unmanaged where TFragmentUniform : unmanaged
    {
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(vertUniforms);
        var fragmentParamOffset = commandBuffer.PushFragmentShaderUniforms(fragmentUniforms);
        DrawIndexed(ref commandBuffer, vertexParamOffset, fragmentParamOffset, fragmentSamplerBindings, usePointFiltering, offset, numSprites);
    }

    public void DrawIndexed(ref CommandBuffer commandBuffer, Matrix4x4 viewProjection, bool usePointFiltering)
    {
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);
        DrawIndexed(ref commandBuffer, vertexParamOffset, 0u, _fragmentSamplerBindings, usePointFiltering, 0, _numSprites);
        _numSprites = 0;
    }

    public void Discard()
    {
        _numSprites = 0;
    }

    public void DrawIndexed(ref CommandBuffer commandBuffer, uint vertexUniformOffset, uint fragmentUniformOffset,
        TextureSamplerBinding[] fragmentSamplerBindings, bool usePointFiltering, int offset, int numSprites)
    {
        if (numSprites == 0)
        {
            Logs.LogWarn("Flushing empty SpriteBatch");
            return;
        }

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);

        var textureBinding = new TextureSamplerBinding(_spriteInfo[0], usePointFiltering ? PointClamp : LinearClamp);

        fragmentSamplerBindings[0] = textureBinding;

        var startOffset = offset;

        for (var i = 1; i < numSprites; i += 1)
        {
            var spriteInfo = _spriteInfo[i];

            if (fragmentSamplerBindings[0].Texture.Handle != spriteInfo.Handle)
            {
                commandBuffer.BindFragmentSamplers(fragmentSamplerBindings);
                DrawIndexedQuads(ref commandBuffer, offset, i - offset, vertexUniformOffset, fragmentUniformOffset);
                fragmentSamplerBindings[0].Texture = spriteInfo;
                offset = i;
            }
        }

        commandBuffer.BindFragmentSamplers(fragmentSamplerBindings);
        DrawIndexedQuads(ref commandBuffer, offset, numSprites - (offset - startOffset), vertexUniformOffset, fragmentUniformOffset);
    }

    public static uint[] GenerateIndexArray(uint maxIndices)
    {
        var result = new uint[maxIndices];
        for (uint i = 0, j = 0; i < maxIndices; i += 6, j += 4)
        {
            result[i] = j;
            result[i + 1] = j + 1;
            result[i + 2] = j + 2;
            result[i + 3] = j + 2;
            result[i + 4] = j + 1;
            result[i + 5] = j + 3;
        }

        return result;
    }

    public static void PushSpriteVertices(Position3DTextureColorVertex[] vertices, int vertexOffset, in UV uvs, in Bounds dstRect, float depth,
        Color[] colors, SpriteFlip flip)
    {
        var minX = dstRect.Min.X;
        var minY = dstRect.Min.Y;
        var maxX = minX + dstRect.Size.X;
        var maxY = minY + dstRect.Size.Y;

        if (ShouldRoundPositions)
        {
            minX = MathF.Floor(minX);
            minY = MathF.Floor(minY);
            maxX = MathF.Floor(maxX);
            maxY = MathF.Floor(maxY);
        }

        vertices[vertexOffset].Position.X = minX;
        vertices[vertexOffset].Position.Y = minY;
        vertices[vertexOffset].Position.Z = depth;

        vertices[vertexOffset + 1].Position.X = minX;
        vertices[vertexOffset + 1].Position.Y = maxY;
        vertices[vertexOffset + 1].Position.Z = depth;

        vertices[vertexOffset + 2].Position.X = maxX;
        vertices[vertexOffset + 2].Position.Y = minY;
        vertices[vertexOffset + 2].Position.Z = depth;
        
        vertices[vertexOffset + 3].Position.X = maxX;
        vertices[vertexOffset + 3].Position.Y = maxY;
        vertices[vertexOffset + 3].Position.Z = depth;
            
        vertices[vertexOffset].TexCoord = uvs.TopLeft;
        vertices[vertexOffset + 1].TexCoord = uvs.BottomLeft;
        vertices[vertexOffset + 2].TexCoord = uvs.TopRight;
        vertices[vertexOffset + 3].TexCoord = uvs.BottomRight;

        var effects = (byte)(flip & (SpriteFlip.FlipVertically | SpriteFlip.FlipHorizontally));
        vertices[vertexOffset].TexCoord.X = CornerOffsetX[0 ^ effects] * uvs.Dimensions.X + uvs.Position.X;
        vertices[vertexOffset].TexCoord.Y = CornerOffsetY[0 ^ effects] * uvs.Dimensions.Y + uvs.Position.Y;
        vertices[vertexOffset + 1].TexCoord.X = CornerOffsetX[1 ^ effects] * uvs.Dimensions.X + uvs.Position.X;
        vertices[vertexOffset + 1].TexCoord.Y = CornerOffsetY[1 ^ effects] * uvs.Dimensions.Y + uvs.Position.Y;
        vertices[vertexOffset + 2].TexCoord.X = CornerOffsetX[2 ^ effects] * uvs.Dimensions.X + uvs.Position.X;
        vertices[vertexOffset + 2].TexCoord.Y = CornerOffsetY[2 ^ effects] * uvs.Dimensions.Y + uvs.Position.Y;
        vertices[vertexOffset + 3].TexCoord.X = CornerOffsetX[3 ^ effects] * uvs.Dimensions.X + uvs.Position.X;
        vertices[vertexOffset + 3].TexCoord.Y = CornerOffsetY[3 ^ effects] * uvs.Dimensions.Y + uvs.Position.Y;

        vertices[vertexOffset].Color = colors[0];
        vertices[vertexOffset + 1].Color = colors[1];
        vertices[vertexOffset + 2].Color = colors[2];
        vertices[vertexOffset + 3].Color = colors[3];
    }

    public static void DrawIndexedQuads(ref CommandBuffer commandBuffer, int offset, int numSprites, uint vertexParamOffset, uint fragmentParamOffset)
    {
        DrawCalls++;
        commandBuffer.DrawIndexedPrimitives((uint)(offset * 4), 0u, (uint)(numSprites * 2), vertexParamOffset, fragmentParamOffset);
    }
}
