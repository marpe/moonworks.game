using Buffer = MoonWorks.Graphics.Buffer;

namespace MyGame;

public class SpriteBatch
{
    public const int MAX_SPRITES = 1024;
    private const int MAX_VERTICES = MAX_SPRITES * 4;
    private const int MAX_INDICES = MAX_SPRITES * 6;

    private Buffer VertexBuffer { get; }
    private Buffer IndexBuffer { get; }

    private readonly VertexPositionTexcoord[] Vertices;
    private static readonly ushort[] Indices = GenerateIndexArray();

    private uint VertexCount { get; set; } = 0;

    private SpriteSubBatch[] spriteSubBatches = new SpriteSubBatch[1];
    private uint spriteSubBatchCount = 0;

    public SpriteBatch(GraphicsDevice graphicsDevice)
    {
        Vertices = new VertexPositionTexcoord[MAX_VERTICES];
        VertexBuffer = Buffer.Create<VertexPositionTexcoord>(graphicsDevice, BufferUsageFlags.Vertex, MAX_VERTICES);
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

        var p0 = new Vector3(Vector2.Transform(Vector2.Zero - offset, transform), depth);
        Vertices[VertexCount].position = new Vector2(p0.X, p0.Y);
        Vertices[VertexCount].texcoord = sprite.UV.TopLeft;
        Vertices[VertexCount].color = color;

        var p1 = new Vector3(Vector2.Transform(new Vector2(0, sprite.SliceRect.H) - offset, transform), depth);
        Vertices[VertexCount + 1].position = new Vector2(p1.X, p1.Y);
        Vertices[VertexCount + 1].texcoord = sprite.UV.BottomLeft;
        Vertices[VertexCount + 1].color = color;

        var p2 = new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, 0) - offset, transform), depth);
        Vertices[VertexCount + 2].position = new Vector2(p2.X, p2.Y);
        Vertices[VertexCount + 2].texcoord = sprite.UV.TopRight;
        Vertices[VertexCount + 2].color = color;

        var p3 = new Vector3(Vector2.Transform(new Vector2(sprite.SliceRect.W, sprite.SliceRect.H) - offset, transform), depth);
        Vertices[VertexCount + 3].position = new Vector2(p3.X, p3.Y);
        Vertices[VertexCount + 3].texcoord = sprite.UV.BottomRight;
        Vertices[VertexCount + 3].color = color;

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
