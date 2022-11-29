#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;
layout (set = 1, binding = 1) uniform sampler2D depthMap;

layout (set = 3, binding = 0) uniform UniformBlock
{
    float lightIntensity;
	float lightRadius;
    vec4 lightColor;
    vec4 texelSize; // 1 / renderTargetWith, 1 / renderTargetHeight, renderTargetWidth, renderTargetHeight
	vec2 screenSpaceLightPos;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;


void main()
{
    vec4 c = texture(uniformTexture, texCoord);
 
	if (c.a == 0)
	{
		discard;
	}

	float depth = texture(depthMap,	texCoord).r;

	c = vec4(0, 0, 0, 0);
	vec2 rim = vec2(0, 0);
	float addedAlpha = 0;
	float value = 0;
    float inFrontOf = 0;
    vec2 dx = vec2(Uniforms.texelSize.x, 0);
	vec2 dy = vec2(0, Uniforms.texelSize.y);

	// negative values = we're behind, 0 = we're same depth, positive = we're in front
	value = texture(depthMap, texCoord + dx).r - depth;
	rim.x += sign(value);
	inFrontOf += value;

	value = texture(depthMap, texCoord - dx).r - depth;
	rim.x -= sign(value);
	inFrontOf += value;
 
	value = texture(depthMap, texCoord + dy).r - depth;
	rim.y += sign(value);
	inFrontOf += value;
    
    value = texture(depthMap, texCoord - dy).r - depth;
	rim.y -= sign(value);
	inFrontOf += value;

    if (inFrontOf > 0)
    {
        vec2 lightOffset = Uniforms.screenSpaceLightPos - texCoord;
        vec2 lightDir = normalize(lightOffset);
        float attenuation = clamp(1.0 - length(lightOffset) / Uniforms.lightRadius, 0, 1);
        c.rgb = attenuation * Uniforms.lightIntensity * Uniforms.lightColor.rgb * clamp(dot(lightDir, rim.xy), 0, 1);
    }
    
    fragColor = c;
}
