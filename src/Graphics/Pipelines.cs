namespace MyGame.Graphics;

public enum PipelineType
{
    Additive,
    AlphaBlend,
    NonPremultiplied,
    Opaque,
    Multiply,
    CustomBlendState,
    Sprite,
    Light,
    RimLight,
    CircleCropTransition,
    PixelizeTransition,
    DiamondTransition,
    PixelArt,
}

public class Pipelines
{
    public static readonly ColorAttachmentBlendState CustomBlendState = new()
    {
        BlendEnable = true,
        AlphaBlendOp = BlendOp.Add,
        ColorBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RGBA,
        SourceColorBlendFactor = BlendFactor.DestinationColor,
        SourceAlphaBlendFactor = BlendFactor.DestinationAlpha,
        DestinationColorBlendFactor = BlendFactor.Zero,
        DestinationAlphaBlendFactor = BlendFactor.Zero,
    };

    public static readonly ColorAttachmentBlendState MultiplyBlendState = new()
    {
        BlendEnable = true,
        AlphaBlendOp = BlendOp.Add,
        ColorBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RGBA,
        SourceColorBlendFactor = BlendFactor.DestinationColor,
        SourceAlphaBlendFactor = BlendFactor.DestinationAlpha,
        DestinationColorBlendFactor = BlendFactor.Zero,
        DestinationAlphaBlendFactor = BlendFactor.Zero,
    };


    [StructLayout(LayoutKind.Sequential)]
    public struct RimLightUniforms
    {
        public float LightIntensity;
        public float LightRadius;
        public Vector2 LightPos;
        public Vector4 TexelSize;
        public Vector4 Bounds;
        public Vector3 LightColor;
        public float VolumetricIntensity;
        public float RimIntensity;
        public float Angle;
        public float ConeAngle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CircleCropUniforms
    {
        public float Progress;
        public float Padding0;
        public Vector2 Center;
        public Vector2 Scaling;
        public Vector4 BackgroundColor;
        public Vector4 Padding1;
        public Vector2 Padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PixelizeUniforms
    {
        public float Progress;
        public int Steps;
        public Point SquaresMin;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DiamondUniforms
    {
        public float Progress;
        public float DiamondPixelSize;
    }

    public static Dictionary<PipelineType, Func<GfxPipeline>> Factories = new();
    
    public static Dictionary<PipelineType, GfxPipeline> CreatePipelines(GraphicsDevice device)
    {
        Factories = new Dictionary<PipelineType, Func<GfxPipeline>>
        {
            { PipelineType.Additive, () => CreateSpritePipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.AlphaBlend, () => CreateSpritePipeline(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.NonPremultiplied, () => CreateSpritePipeline(device, ColorAttachmentBlendState.NonPremultiplied) },
            { PipelineType.Opaque, () => CreateSpritePipeline(device, ColorAttachmentBlendState.Opaque) },
            { PipelineType.Multiply, () => CreateSpritePipeline(device, MultiplyBlendState) },
            { PipelineType.CustomBlendState, () => CreateSpritePipeline(device, CustomBlendState) },
            { PipelineType.Light, () => CreateLightPipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.RimLight, () => CreateRimLightPipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.Sprite, () => CreateSpritePipeline(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.CircleCropTransition, () => CreateCircleCropTransition(device) },
            { PipelineType.PixelizeTransition, () =>  CreatePixelize(device) },
            { PipelineType.DiamondTransition, () => CreateDiamondTransition(device) },
            { PipelineType.PixelArt, () => CreatePixelArt(device) },
        };

        var pipelines = new Dictionary<PipelineType, GfxPipeline>();
        foreach (var (key, factory) in Factories)
            pipelines.Add(key, factory());
        return pipelines;
    }

    public static GfxPipeline CreateLightPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.light_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<RimLightUniforms>(fragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.RimLight.rim_light_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.RimLight.light_frag_spv
        };
    }

    public static GfxPipeline CreateRimLightPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<RimLightUniforms>(fragmentShader, "main", 2);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.RimLight.rim_light_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.RimLight.rim_light_frag_spv
        };
    }

    public static GfxPipeline CreateDiamondTransition(GraphicsDevice device)

    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<DiamondUniforms>(fragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.DiamondTransition.diamond_transition_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.DiamondTransition.diamond_transition_frag_spv
        };
    }
    
    public static GfxPipeline CreatePixelArt(GraphicsDevice device)

    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.PixelArtShader.pixel_art_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.PixelArtShader.pixel_art_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create(fragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.PixelArtShader.pixel_art_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.PixelArtShader.pixel_art_frag_spv
        };
    }

    public static GfxPipeline CreateSpritePipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var spriteVertexShader = new ShaderModule(device, ContentPaths.Shaders.Sprite.sprite_vert_spv);
        var spriteFragmentShader = new ShaderModule(device, ContentPaths.Shaders.Sprite.sprite_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.Sprite.sprite_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.Sprite.sprite_frag_spv
        };
    }

    public static GfxPipeline CreateCircleCropTransition(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<CircleCropUniforms>(fragmentShader, "main", 1);

        var blendState = new ColorAttachmentBlendState
        {
            BlendEnable = true,
            AlphaBlendOp = BlendOp.Add,
            ColorBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RGBA,
            SourceColorBlendFactor = BlendFactor.SourceAlpha,
            SourceAlphaBlendFactor = BlendFactor.One,
            DestinationColorBlendFactor = BlendFactor.OneMinusSourceAlpha,
            DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha,
        };

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(new ColorAttachmentDescription(TextureFormat.B8G8R8A8, blendState)),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.CircleCrop.circle_crop_transition_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.CircleCrop.circle_crop_transition_frag_spv
        };
    }

    public static GfxPipeline CreatePixelize(GraphicsDevice device)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_vert_spv);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_frag_spv);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<PixelizeUniforms>(fragmentShader, "main", 2);

        var createInfo = new GraphicsPipelineCreateInfo()
        {
            AttachmentInfo =
                new GraphicsPipelineAttachmentInfo(new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = vertexShaderInfo,
            FragmentShaderInfo = fragmentShaderInfo,
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = GetVertexInputState(),
        };

        return new GfxPipeline
        {
            Pipeline = new GraphicsPipeline(device, createInfo),
            CreateInfo = createInfo,
            VertexShaderPath = ContentPaths.Shaders.Pixelize.pixelize_transition_vert_spv,
            FragmentShaderPath = ContentPaths.Shaders.Pixelize.pixelize_transition_frag_spv,
        };
    }

    public static VertexInputState GetVertexInputState()
    {
        var myVertexBindings = new[]
        {
            VertexBinding.Create<Position3DTextureColorVertex>(),
        };

        var myVertexAttributes = new[]
        {
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Position), 0),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Color), 2),
        };

        return new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes,
        };
    }
}
