using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame.Graphics;

public class SpriteBatch
{
    private uint[] _indices;
    private Buffer _vertexBuffer;
    private Buffer _indexBuffer;

    private Position3DTextureColorVertex[] _vertices;

    private TextureSamplerBinding[] _spriteInfo;
    private uint _numSprites = 0;
    private readonly GraphicsDevice _device;
    public uint DrawCalls { get; private set; }

    public SpriteBatch(GraphicsDevice device)
    {
        _device = device;
        var maxSprites = 8192u;
        _spriteInfo = new TextureSamplerBinding[maxSprites];
        _vertices = new Position3DTextureColorVertex[_spriteInfo.Length * 4];
        _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(device, BufferUsageFlags.Vertex, (uint)_vertices.Length);
        _indices = GenerateIndexArray((uint)(_spriteInfo.Length * 6));
        _indexBuffer = Buffer.Create<uint>(device, BufferUsageFlags.Index, (uint)_indices.Length);
    }

    public void Unload()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void Draw(Sprite sprite, Color color, float depth, Matrix3x2 transform, Sampler sampler)
    {
        if (sprite.Texture.IsDisposed)
            throw new ObjectDisposedException(nameof(sprite.Texture));

        if (_numSprites == _spriteInfo.Length)
        {
            var maxNumSprites = (int)(_numSprites + 2048);
            Logger.LogInfo($"Max number of sprites reached, resizing buffers ({_numSprites} -> {maxNumSprites})");
            Array.Resize(ref _spriteInfo, maxNumSprites);
            Array.Resize(ref _vertices, _vertices.Length + _spriteInfo.Length * 4);

            _indices = GenerateIndexArray((uint)(_spriteInfo.Length * 6));

            _vertexBuffer.Dispose();
            _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(_device, BufferUsageFlags.Vertex, (uint)_vertices.Length);

            _indexBuffer.Dispose();
            _indexBuffer = Buffer.Create<uint>(_device, BufferUsageFlags.Index, (uint)_indices.Length);
        }

        _spriteInfo[_numSprites].SamplerHandle = sampler.Handle;
        _spriteInfo[_numSprites].TextureHandle = sprite.Texture.Handle;

        var vertexCount = _numSprites * 4;

        var topLeft = Vector2.Zero;
        var bottomLeft = new Vector2(0, sprite.SrcRect.Height);
        var topRight = new Vector2(sprite.SrcRect.Width, 0);
        var bottomRight = new Vector2(sprite.SrcRect.Width, sprite.SrcRect.Height);

        // var offset = Vector2.Zero;
        /*SubtractVector(ref topLeft, ref offset);
        SubtractVector(ref bottomLeft, ref offset);
        SubtractVector(ref topRight, ref offset);
        SubtractVector(ref bottomRight, ref offset);*/

        TransformVector(ref topLeft, transform);
        TransformVector(ref bottomLeft, transform);
        TransformVector(ref topRight, transform);
        TransformVector(ref bottomRight, transform);

        /*topLeft = Vector2.Transform(topLeft, transform);
        bottomLeft = Vector2.Transform(bottomLeft, transform);
        topRight = Vector2.Transform(topRight, transform);
        bottomRight = Vector2.Transform(bottomRight, transform);*/

        SetVector(ref _vertices[vertexCount].Position, topLeft, depth);
        // _vertices[vertexCount].Position = new Vector3(topLeft, depth);
        _vertices[vertexCount].TexCoord = sprite.UV.TopLeft;
        _vertices[vertexCount].Color = color;

        SetVector(ref _vertices[vertexCount + 1].Position, bottomLeft, depth);
        // _vertices[vertexCount + 1].Position = new Vector3(bottomLeft, depth);
        _vertices[vertexCount + 1].TexCoord = sprite.UV.BottomLeft;
        _vertices[vertexCount + 1].Color = color;

        SetVector(ref _vertices[vertexCount + 2].Position, topRight, depth);
        // _vertices[vertexCount + 2].Position = new Vector3(topRight, depth);
        _vertices[vertexCount + 2].TexCoord = sprite.UV.TopRight;
        _vertices[vertexCount + 2].Color = color;

        SetVector(ref _vertices[vertexCount + 3].Position, bottomRight, depth);
        // _vertices[vertexCount + 3].Position = new Vector3(bottomRight, depth);
        _vertices[vertexCount + 3].TexCoord = sprite.UV.BottomRight;
        _vertices[vertexCount + 3].Color = color;

        _numSprites += 1;
    }

    /// Subtract b from a, modifying a
    private static void SubtractVector(ref Vector2 a, in Vector2 b)
    {
        a.X -= b.X;
        a.Y -= b.Y;
    }

    private static void TransformVector(ref Vector2 vec, in Matrix3x2 matrix)
    {
        vec.X = (vec.X * matrix.M11) + (vec.Y * matrix.M21) + matrix.M31;
        vec.Y = (vec.X * matrix.M12) + (vec.Y * matrix.M22) + matrix.M32;
    }

    private static void SetVector(ref Vector3 dest, in Vector2 src, float z)
    {
        dest.X = src.X;
        dest.Y = src.Y;
        dest.Z = z;
    }

    public static Matrix4x4 GetViewProjection(int x, int y, uint width, uint height)
    {
        var view = Matrix4x4.CreateLookAt(
            new Vector3(x, y, 1),
            new Vector3(x, y, 0),
            Vector3.Up
        );
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0,
            width,
            height,
            0,
            0.0001f,
            4000f
        );
        return view * projection;
    }

    public void UpdateBuffers(CommandBuffer commandBuffer)
    {
        if (_numSprites == 0)
            return;
        commandBuffer.SetBufferData(_indexBuffer, _indices, 0, 0, _numSprites * 6);
        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, 0, _numSprites * 4);
    }

    public void Flush(CommandBuffer commandBuffer, Matrix4x4 viewProjection)
    {
        DrawCalls = 0;

        if (_numSprites == 0)
            return;

        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);

        var currSprite = _spriteInfo[0];
        var offset = 0;
        for (var i = 1; i < _numSprites; i += 1)
        {
            var spriteInfo = _spriteInfo[i];

            if (!BindingsAreEqual(currSprite, spriteInfo))
            {
                commandBuffer.BindFragmentSamplers(currSprite);
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

        commandBuffer.BindFragmentSamplers(currSprite);
        commandBuffer.DrawIndexedPrimitives(
            (uint)(offset * 4),
            0u,
            (uint)((_numSprites - offset) * 2),
            vertexParamOffset,
            0u
        );
        DrawCalls++;

        _numSprites = 0;
    }

    private static uint[] GenerateIndexArray(uint maxIndices)
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

    private static bool BindingsAreEqual(TextureSamplerBinding a, TextureSamplerBinding b)
    {
        return a.TextureHandle == b.TextureHandle &&
               a.SamplerHandle == b.SamplerHandle;
    }
}
