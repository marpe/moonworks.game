namespace MyGame.Graphics;

public enum PipelineType
{
    LightsToMain,
    RimLightsToMain,
    Sprite,
    Light,
    RimLight,
    CircleCropTransition,
    PixelizeTransition,
    DiamondTransition,
    PixelArt,
    Light2,
}

[StructLayout(LayoutKind.Sequential)]
public struct LightU
{
    public float LightIntensity;
    public float LightRadius;
    public Vector2 LightPos;

    public Vector3 LightColor;
    public float VolumetricIntensity;

    public float RimIntensity;
    public float Angle;
    public float ConeAngle;
    public float Padding;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct LightUniform
{
    public const int MaxNumLights = 128;

    public fixed byte Lights[MaxNumLights * 12 * 4];

    public Vector4 TexelSize = default;

    public Vector4 Bounds = default;

    public int Scale = 0;

    public int NumLights = 0;

    public Vector2 Padding = default;

    public LightUniform()
    {
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct LightU2
{
    public Vector3 LightColor;
    public float LightIntensity;
    public float VolumetricIntensity;
    public float Angle;
    public float ConeAngle;   
    public float Padding;   
}

public class Pipelines
{
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

    public static readonly ColorAttachmentBlendState CombineBlendState = new()
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

    public delegate GfxPipeline CreatePipelineDelegate(GraphicsDevice device, ColorAttachmentBlendState blendState);

    public static Dictionary<PipelineType, CreatePipelineDelegate> Factories = new()
    {
        { PipelineType.LightsToMain, CreateSpritePipeline },
        { PipelineType.RimLightsToMain, CreateSpritePipeline },
        { PipelineType.Light, CreateLightPipeline },
        { PipelineType.RimLight, CreateRimLightPipeline },
        { PipelineType.Sprite, CreateSpritePipeline },
        { PipelineType.CircleCropTransition, CreateCircleCropTransition },
        { PipelineType.PixelizeTransition, CreatePixelize },
        { PipelineType.DiamondTransition, CreateDiamondTransition },
        { PipelineType.PixelArt, CreatePixelArt },
        { PipelineType.Light2, CreateLight2 },
    };

    public static Dictionary<PipelineType, GfxPipeline> CreatePipelines(GraphicsDevice device)
    {
        return new()
        {
            { PipelineType.LightsToMain, CreateSpritePipeline(device, MultiplyBlendState) },
            { PipelineType.RimLightsToMain, CreateSpritePipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.Light, CreateLightPipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.RimLight, CreateRimLightPipeline(device, ColorAttachmentBlendState.Additive) },
            { PipelineType.Sprite, CreateSpritePipeline(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.CircleCropTransition, CreateCircleCropTransition(device, CombineBlendState) },
            { PipelineType.PixelizeTransition, CreatePixelize(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.DiamondTransition, CreateDiamondTransition(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.PixelArt, CreatePixelArt(device, ColorAttachmentBlendState.AlphaBlend) },
            { PipelineType.Light2, CreateLight2(device, ColorAttachmentBlendState.AlphaBlend) },
        };
    }

    public static GfxPipeline CreateLight2(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.Lights2.light2_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.Lights2.light2_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = new GraphicsShaderInfo()
        {
            ShaderModule = fragmentShader,
            EntryPointName = "main",
            SamplerBindingCount = 1,
            UniformBufferSize = (uint)Marshal.SizeOf<LightU2>(),
        };

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.Lights2.light2_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.Lights2.light2_frag_refresh,
        };
    }

    public static GfxPipeline CreateLightPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.light_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        // var fragmentShaderInfo = GraphicsShaderInfo.Create<LightUniform>(fragmentShader, "main", 1);
        var fragmentShaderInfo = new GraphicsShaderInfo()
        {
            ShaderModule = fragmentShader,
            EntryPointName = "main",
            SamplerBindingCount = 1,
            UniformBufferSize = (uint)Marshal.SizeOf<LightUniform>(),
        };
        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.RimLight.rim_light_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.RimLight.light_frag_refresh
        };
    }

    public static GfxPipeline CreateRimLightPipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.RimLight.rim_light_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        // var fragmentShaderInfo = GraphicsShaderInfo.Create<LightUniform>(fragmentShader, "main", 2);
        var fragmentShaderInfo = new GraphicsShaderInfo()
        {
            ShaderModule = fragmentShader,
            EntryPointName = "main",
            SamplerBindingCount = 2,
            UniformBufferSize = (uint)Marshal.SizeOf<LightUniform>(),
        };

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.RimLight.rim_light_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.RimLight.rim_light_frag_refresh
        };
    }

    public static GfxPipeline CreateDiamondTransition(GraphicsDevice device, ColorAttachmentBlendState blendState)

    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.DiamondTransition.diamond_transition_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<DiamondUniforms>(fragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.DiamondTransition.diamond_transition_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.DiamondTransition.diamond_transition_frag_refresh
        };
    }

    public static GfxPipeline CreatePixelArt(GraphicsDevice device, ColorAttachmentBlendState blendState)

    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.PixelArtShader.pixel_art_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.PixelArtShader.pixel_art_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create(fragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.PixelArtShader.pixel_art_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.PixelArtShader.pixel_art_frag_refresh
        };
    }

    public static GfxPipeline CreateSpritePipeline(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var spriteVertexShader = new ShaderModule(device, ContentPaths.Shaders.Sprite.sprite_vert_refresh);
        var spriteFragmentShader = new ShaderModule(device, ContentPaths.Shaders.Sprite.sprite_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.Sprite.sprite_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.Sprite.sprite_frag_refresh
        };
    }

    public static GfxPipeline CreateCircleCropTransition(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.CircleCrop.circle_crop_transition_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<CircleCropUniforms>(fragmentShader, "main", 1);


        var createInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.CircleCrop.circle_crop_transition_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.CircleCrop.circle_crop_transition_frag_refresh
        };
    }

    public static GfxPipeline CreatePixelize(GraphicsDevice device, ColorAttachmentBlendState blendState)
    {
        var vertexShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_vert_refresh);
        var fragmentShader = new ShaderModule(device, ContentPaths.Shaders.Pixelize.pixelize_transition_frag_refresh);

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(vertexShader, "main", 0);
        var fragmentShaderInfo = GraphicsShaderInfo.Create<PixelizeUniforms>(fragmentShader, "main", 2);

        var createInfo = new GraphicsPipelineCreateInfo()
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.R8G8B8A8, blendState)
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
            VertexShaderPath = ContentPaths.Shaders.Pixelize.pixelize_transition_vert_refresh,
            FragmentShaderPath = ContentPaths.Shaders.Pixelize.pixelize_transition_frag_refresh,
        };
    }

    public static VertexInputState GetVertexInputState()
    {
        return VertexInputState.CreateSingleBinding<Vertex>();
    }
}
