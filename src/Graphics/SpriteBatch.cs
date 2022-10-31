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
    private const int MAX_SPRITES = 8192;
    private const int MAX_VERTICES = MAX_SPRITES * 4;
    private const int MAX_INDICES = MAX_SPRITES * 6;

    private static readonly ushort[] _indices = GenerateIndexArray();
    private readonly Buffer _vertexBuffer;
    private readonly Buffer _indexBuffer;

    private Position3DTextureColorVertex[] _vertices;

    private TextureSamplerBinding[] _spriteInfo;
    private uint _numSprites = 0;
    private readonly GraphicsDevice _device;
    public BlendState BlendState = BlendState.AlphaBlend;

    public DepthStencilAttachmentInfo DepthStencilAttachmentInfo;
    public ColorAttachmentInfo ColorAttachmentInfo;
    public Texture DepthTexture;
    private uint DrawCalls;

    public SpriteBatch(GraphicsDevice device)
    {
        _device = device;
        _vertices = new Position3DTextureColorVertex[MAX_VERTICES];
        _vertexBuffer = Buffer.Create<Position3DTextureColorVertex>(device, BufferUsageFlags.Vertex, MAX_VERTICES);
        _indexBuffer = Buffer.Create<ushort>(device, BufferUsageFlags.Index, MAX_INDICES);
        _spriteInfo = new TextureSamplerBinding[MAX_SPRITES];

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

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetBufferData(_indexBuffer, _indices);
        device.Submit(commandBuffer);
    }

    public void Draw(Sprite sprite, Color color, float depth, Matrix3x2 transform, Sampler sampler)
    {
        if (_numSprites == _spriteInfo.Length)
        {
            Array.Resize(ref _spriteInfo, (int)(_numSprites + MAX_SPRITES));
        }

        _spriteInfo[_numSprites].SamplerHandle = sampler.Handle;
        _spriteInfo[_numSprites].TextureHandle = sprite.Texture.Handle;

        var vertexCount = _numSprites * 4;
        
        if (vertexCount >= _vertices.Length)
        {
            Array.Resize(ref _vertices, _vertices.Length + MAX_SPRITES * 4);
        }

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
        var arrayOffset = 0u;
        // var commandBuffer = _device.AcquireCommandBuffer();
        var baseOff = 0;
        var batchSize = Math.Min(_numSprites, MAX_SPRITES);
        var numElements = batchSize * 4;

        commandBuffer.SetBufferData(_vertexBuffer, _vertices, 0, arrayOffset * 4, numElements);

        commandBuffer.BeginRenderPass(DepthStencilAttachmentInfo, ColorAttachmentInfo);

        commandBuffer.BindGraphicsPipeline(pipeline);

        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);

        commandBuffer.BindVertexBuffers(_vertexBuffer);
        commandBuffer.BindIndexBuffer(_indexBuffer, IndexElementSize.Sixteen);

        var currSprite = _spriteInfo[arrayOffset];
        var offset = 0;
        for (var i = 1; i < batchSize; i += 1)
        {
            var spriteInfo = _spriteInfo[arrayOffset + i];

            if (!BindingsAreEqual(currSprite, spriteInfo))
            {
                commandBuffer.BindFragmentSamplers(currSprite);
                commandBuffer.DrawIndexedPrimitives(
                    (uint)((baseOff + offset) * 4),
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
            (uint)((baseOff + offset) * 4),
            0u,
            (uint)((batchSize - offset) * 2),
            vertexParamOffset,
            0u
        );
        DrawCalls++;

        commandBuffer.EndRenderPass();

        if (_numSprites > MAX_SPRITES)
        { 
            /*Shared.Game.GraphicsDevice.Submit(commandBuffer);
            Shared.Game.GraphicsDevice.Wait();*/
            _numSprites -= MAX_SPRITES;
            arrayOffset += MAX_SPRITES;
            // TODO (marpe): Render remaining sprites
        }

        _numSprites = 0;
    }

    private static ushort[] GenerateIndexArray()
    {
        var result = new ushort[MAX_INDICES];
        for (ushort i = 0, j = 0; i < MAX_INDICES; i += 6, j += 4)
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

    private static bool BindingsAreEqual(TextureSamplerBinding a, TextureSamplerBinding b)
    {
        return a.TextureHandle == b.TextureHandle &&
               a.SamplerHandle == b.SamplerHandle;
    }
}
