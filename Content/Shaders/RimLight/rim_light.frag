sampler s0;

texture _depthMap;
sampler _depthMapSampler = sampler_state { Texture = <_depthMap>; };

float4x4 MatrixTransform;

float4x4 _WorldToObject;

float2 _ScreenSpaceLightPos0;
float4 _MainTex_TexelSize;

float _lightIntensity;
float _lightRadius;
float4 _lightColor;

struct VertexShaderInput
{
	float4 vertex : SV_Position;
	float2 texCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 vertex: SV_Position;
    float2 texCoord: TEXCOORD0;
    float2 screenPos: TEXCOORD1;
};

VertexShaderOutput vert(VertexShaderInput vsIn)
{
	VertexShaderOutput vsOut;
	vsOut.vertex = mul(vsIn.vertex, MatrixTransform);
	vsOut.texCoord = vsIn.texCoord; 
	vsOut.screenPos = vsOut.vertex;
	return vsOut;
}

float4 frag(VertexShaderOutput input): COLOR0
{
    float4 c = tex2D(s0, input.texCoord);
 
	if (c.a == 0)
	{
		discard;
	}

    float depth = tex2D(_depthMapSampler, input.texCoord).r;

	c = float4(0, 0, 0, 0);
	
	float2 rim = float2(0, 0);
	float addedAlpha = 0;
 
	float value = 0;
	float2 dx = float2(_MainTex_TexelSize.xy.x, 0);
	float2 dy = float2(0, _MainTex_TexelSize.xy.y);

    float inFrontOf = 0;

	// negative values = we're behind, 0 = we're same depth, positive = we're in front
	value = tex2D(_depthMapSampler, input.texCoord + dx).r - depth;
	rim.x += sign(value);
	inFrontOf += value;

	value = tex2D(_depthMapSampler, input.texCoord - dx).r - depth;
	rim.x -= sign(value);
	inFrontOf += value;
 
	value = tex2D(_depthMapSampler, input.texCoord + dy).r - depth;
	rim.y += sign(value);
	inFrontOf += value;
    
    value = tex2D(_depthMapSampler, input.texCoord - dy).r - depth;
	rim.y -= sign(value);
	inFrontOf += value;

    if (inFrontOf > 0)
    {
        float2 lightOffset = _ScreenSpaceLightPos0 - input.texCoord;
        float2 lightDir = normalize(lightOffset);
        float attenuation = saturate( 1.0f - length(lightOffset) / _lightRadius );
        c.rgb = attenuation * _lightIntensity * _lightColor * saturate(dot(lightDir, rim.xy));
    }
    
    return c;
}


technique Technique1
{
    pass Pass1
    {
		VertexShader = compile vs_2_0 vert();
        PixelShader = compile ps_2_0 frag();
    }
}
