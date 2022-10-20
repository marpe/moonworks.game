using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame;

public class SpriteBatch
{
    public const int MAX_SPRITES = 1024;
    private const int MAX_VERTICES = MAX_SPRITES * 4;
    private const int MAX_INDICES = MAX_SPRITES * 6;

    private Buffer VertexBuffer { get; }
    private Buffer IndexBuffer { get; }

    private readonly Position3DTextureColorVertex[] Vertices;
    private static readonly ushort[] Indices = GenerateIndexArray();

    private uint VertexCount { get; set; } = 0;

    private SpriteSubBatch[] spriteSubBatches = new SpriteSubBatch[1];
    private uint spriteSubBatchCount = 0;

    public SpriteBatch(GraphicsDevice graphicsDevice)
    {
        Vertices = new Position3DTextureColorVertex[MAX_VERTICES];
        VertexBuffer = Buffer.Create<Position3DTextureColorVertex>(graphicsDevice, BufferUsageFlags.Vertex, MAX_VERTICES);
        IndexBuffer = Buffer.Create<ushort>(graphicsDevice, BufferUsageFlags.Index, MAX_INDICES);

        var commandBuffer = graphicsDevice.AcquireCommandBuffer();
        commandBuffer.SetBufferData(IndexBuffer, Indices);
        graphicsDevice.Submit(commandBuffer);
    }

    public void Start(TextureSamplerBinding binding)
    {
        End();

        if (spriteSubBatches.Length == spriteSubBatchCount)
        {
            Array.Resize(ref spriteSubBatches, spriteSubBatches.Length * 2);
        }

        spriteSubBatches[spriteSubBatchCount].VertexCount = 0;
        spriteSubBatches[spriteSubBatchCount].VertexOffset = VertexCount;
        spriteSubBatches[spriteSubBatchCount].Binding = binding;

        spriteSubBatchCount += 1;
    }

    public void Add(Sprite sprite, Color color, float depth, Matrix3x2 transform)
    {
        var offset = new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y);

        Vertices[VertexCount].Position = new Vector3(Vector2.Transform(Vector2.Zero - offset, transform), depth);
        Vertices[VertexCount].TexCoord = sprite.UV.TopLeft;
        Vertices[VertexCount].Color = color.ToVector4();

        Vertices[VertexCount + 1].Position = new Vector3(Vector2.Transform(new Vector2(0, sprite.SliceRect.H) - offset, transform), depth);
        Vertices[VertexCount + 1].TexCoord = sprite.UV.BottomLeft;
        Vertices[VertexCount + 1].Color = color.ToVector4();

        Vertices[VertexCount + 2].Position = new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, 0) - offset, transform), depth);
        Vertices[VertexCount + 2].TexCoord = sprite.UV.TopRight;
        Vertices[VertexCount + 2].Color = color.ToVector4();

        Vertices[VertexCount + 3].Position = new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, sprite.SliceRect.H) - offset, transform), depth);
        Vertices[VertexCount + 3].TexCoord = sprite.UV.BottomRight;
        Vertices[VertexCount + 3].Color = color.ToVector4();

        VertexCount += 4;
    }

    public void PushVertexData(CommandBuffer commandBuffer)
    {
        End();
        commandBuffer.SetBufferData(VertexBuffer, Vertices, 0, 0, VertexCount);
    }

    private void End()
    {
        if (spriteSubBatchCount > 0)
        {
            spriteSubBatches[spriteSubBatchCount - 1].VertexCount = VertexCount - spriteSubBatches[spriteSubBatchCount - 1].VertexOffset;
        }
    }

    public void Draw(CommandBuffer commandBuffer, uint vertexParamOffset = 0, uint fragmentParamOffset = 0)
    {
        commandBuffer.BindVertexBuffers(VertexBuffer);
        commandBuffer.BindIndexBuffer(IndexBuffer, IndexElementSize.Sixteen);

        for (var i = 0; i < spriteSubBatchCount; i += 1)
        {
            var spriteSubBatch = spriteSubBatches[i];

            commandBuffer.BindFragmentSamplers(spriteSubBatch.Binding);

            commandBuffer.DrawIndexedPrimitives(
                spriteSubBatch.VertexOffset,
                0,
                spriteSubBatch.VertexCount / 2,
                vertexParamOffset,
                fragmentParamOffset
            );
        }

        spriteSubBatchCount = 0;
        VertexCount = 0;
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
