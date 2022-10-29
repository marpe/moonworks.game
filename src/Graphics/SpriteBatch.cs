using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame.Graphics;

public enum BlendState
{
    Additive,
    AlphaBlend,
    NonPremultiplied,
    Opaque,
    None,
    Disable,
    Custom
}

public struct SpriteSubBatch
{
    public uint VertexCount;
    public TextureSamplerBinding Binding;
}

public class SpriteBatch
{
    private const int MAX_SPRITES = 1024;
    private const int MAX_VERTICES = MAX_SPRITES * 4;
    private const int MAX_INDICES = MAX_SPRITES * 6;

    private static readonly ushort[] _indices = GenerateIndexArray();
    private readonly Buffer _vertexBuffer;
    private readonly Buffer _indexBuffer;

    private readonly Position3DTextureColorVertex[] _vertices;

    private uint _addCountSinceDraw;
    public uint AddCountSinceDraw => _addCountSinceDraw;

    private SpriteSubBatch[] _spriteSubBatches = new SpriteSubBatch[1];
    private uint _spriteSubBatchCount = 0;
    private readonly GraphicsDevice _device;
    public BlendState BlendState = BlendState.AlphaBlend;

    public SpriteBatch(GraphicsDevice device)
    {
        _device = device;
        _vertices = new Position3DTextureColorVertex[MAX_VERTICES];
        _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(device, BufferUsageFlags.Vertex, MAX_VERTICES);
        _indexBuffer = Buffer.Create<ushort>(device, BufferUsageFlags.Index, MAX_INDICES);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetBufferData(_indexBuffer, _indices);
        device.Submit(commandBuffer);
    }

    private void StartSubBatch(TextureSamplerBinding binding)
    {
        if (_spriteSubBatches.Length == _spriteSubBatchCount)
        {
            Array.Resize(ref _spriteSubBatches, _spriteSubBatches.Length * 2);
        }

        _spriteSubBatches[_spriteSubBatchCount].VertexCount = 0;
        _spriteSubBatches[_spriteSubBatchCount].Binding = binding;

        _spriteSubBatchCount += 1;
    }

    private void Add(Sprite sprite, Color color, float depth, Matrix3x2 transform)
    {
        _addCountSinceDraw++;
        
        var offset = new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y);

        var vertexCount = _spriteSubBatches[_spriteSubBatchCount - 1].VertexCount;
        
        _vertices[vertexCount].Position = new Vector3(Vector2.Transform(Vector2.Zero - offset, transform), depth);
        _vertices[vertexCount].TexCoord = sprite.UV.TopLeft;
        _vertices[vertexCount].Color = color;

        _vertices[vertexCount + 1].Position =
            new Vector3(Vector2.Transform(new Vector2(0, sprite.SliceRect.H) - offset, transform), depth);
        _vertices[vertexCount + 1].TexCoord = sprite.UV.BottomLeft;
        _vertices[vertexCount + 1].Color = color;

        _vertices[vertexCount + 2].Position =
            new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, 0) - offset, transform), depth);
        _vertices[vertexCount + 2].TexCoord = sprite.UV.TopRight;
        _vertices[vertexCount + 2].Color = color;

        _vertices[vertexCount + 3].Position =
            new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, sprite.SliceRect.H) - offset, transform), depth);
        _vertices[vertexCount + 3].TexCoord = sprite.UV.BottomRight;
        _vertices[vertexCount + 3].Color = color;

        _spriteSubBatches[_spriteSubBatchCount - 1].VertexCount = vertexCount + 4;
    }

    public static bool Equals(TextureSamplerBinding binding, Texture texture, Sampler sampler)
    {
        return binding.TextureHandle == texture.Handle &&
               binding.SamplerHandle == sampler.Handle;
    }
    
    public void Add(Sprite sprite, Color color, float depth, Matrix3x2 transform, Sampler sampler)
    {
        if (_spriteSubBatchCount == 0 || !Equals(_spriteSubBatches[_spriteSubBatchCount - 1].Binding, sprite.Texture, sampler))
        {
            StartSubBatch(new TextureSamplerBinding(sprite.Texture, sampler));
        }
        Add(sprite, color, depth, transform);
    }

    public void PushVertexData(CommandBuffer commandBuffer)
    {
        var totalVertices = 0u;
        for (var i = 0; i < _spriteSubBatchCount; i++)
            totalVertices += _spriteSubBatches[i].VertexCount;
        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, 0, totalVertices);
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

    public void Draw(CommandBuffer commandBuffer, Matrix4x4 viewProjection)
    {
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);
        var fragmentParamOffset = 0u;

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.Sixteen);

        var vertexOffset = 0u;
        for (var i = 0; i < _spriteSubBatchCount; i += 1)
        {
            var spriteSubBatch = _spriteSubBatches[i];

            commandBuffer.BindFragmentSamplers(spriteSubBatch.Binding);

            commandBuffer.DrawIndexedPrimitives(
                vertexOffset,
                0,
                spriteSubBatch.VertexCount / 2,
                vertexParamOffset,
                fragmentParamOffset
            );

            vertexOffset += spriteSubBatch.VertexCount;
        }

        _spriteSubBatchCount = 0;
        _addCountSinceDraw = 0;
    }

    private static ushort[] GenerateIndexArray()
    {
        var result = new ushort[MAX_INDICES];
        for (int i = 0, j = 0; i < MAX_INDICES; i += 6, j += 4)
        {
            result[i] = (ushort)j;
            result[i + 1] = (ushort)(j + 1);
            result[i + 2] = (ushort)(j + 2);
            result[i + 3] = (ushort)(j + 2);
            result[i + 4] = (ushort)(j + 1);
            result[i + 5] = (ushort)(j + 3);
        }

        return result;
    }
}
