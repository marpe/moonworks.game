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

public class SpriteBatch
{
    private uint[] _indices;
    private Buffer _vertexBuffer;
    private Buffer _indexBuffer;

    private Position3DTextureColorVertex[] _vertices;

    private TextureSamplerBinding[] _spriteInfo;
    private uint _numSprites = 0;
    private readonly GraphicsDevice _device;
    public BlendState BlendState = BlendState.AlphaBlend;

    public DepthStencilAttachmentInfo DepthStencilAttachmentInfo;
    public ColorAttachmentInfo ColorAttachmentInfo;
    public Texture DepthTexture;
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

        DepthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        DepthStencilAttachmentInfo = new DepthStencilAttachmentInfo()
        {
            DepthStencilClearValue = new DepthStencilValue(0, 0),
            Texture = DepthTexture,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store
        };
        ColorAttachmentInfo = new ColorAttachmentInfo()
        {
            ClearColor = Color.CornflowerBlue,
            LoadOp = LoadOp.Clear,
        };
    }

    public void Draw(Sprite sprite, Color color, float depth, Matrix3x2 transform, Sampler sampler)
    {
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

        var offset = new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y);

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

        _numSprites += 1;
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

    public void Flush(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Matrix4x4 viewProjection)
    {
        DrawCalls = 0;

        var batchSize = _numSprites;
        
        commandBuffer.SetBufferData(_indexBuffer, _indices, 0, 0, batchSize * 6);
        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, 0, batchSize * 4);

        commandBuffer.BeginRenderPass(DepthStencilAttachmentInfo, ColorAttachmentInfo);

        commandBuffer.BindGraphicsPipeline(pipeline);

        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);

        var currSprite = _spriteInfo[0];
        var offset = 0;
        for (var i = 1; i < batchSize; i += 1)
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
            (uint)((batchSize - offset) * 2),
            vertexParamOffset,
            0u
        );
        DrawCalls++;

        commandBuffer.EndRenderPass();
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
