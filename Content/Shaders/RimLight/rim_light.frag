#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;
layout (set = 1, binding = 1) uniform sampler2D depthMap;

layout (set = 3, binding = 0) uniform UniformBlock
{
    float lightIntensity;
	float lightRadius;
    vec2 lightPos;
    vec4 texelSize; // 1 / renderTargetWith, 1 / renderTargetHeight, renderTargetWidth, renderTargetHeight
	vec4 bounds;
	vec3 lightColor;
	float debug;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;


void main()
{
    vec4 c = texture(uniformTexture, texCoord);

	if (texture(depthMap, texCoord).a == 0 && Uniforms.debug == 0)
	{
		discard;
	}

	// float depth = texture(depthMap,	texCoord).r;
	vec2 rim = vec2(0, 0);
	float value = 0;
    float inFrontOf = 0;
    vec2 dx = vec2(Uniforms.texelSize.x * 4, 0);
	vec2 dy = vec2(0, Uniforms.texelSize.y * 4);

	// negative values = we're behind, 0 = we're same depth, positive = we're in front
	value = int(texture(depthMap, texCoord + dx).a == 0);
	rim.x += sign(value);
	inFrontOf += value;

	value = int(texture(depthMap, texCoord - dx).a == 0);
	rim.x -= sign(value);
	inFrontOf += value;
 
	value = int(texture(depthMap, texCoord + dy).a == 0);
	rim.y += sign(value);
	inFrontOf += value;
    
    value = int(texture(depthMap, texCoord - dy).a == 0);
	rim.y -= sign(value);
	inFrontOf += value;

	vec2 worldPos = Uniforms.bounds.xy + texCoord * Uniforms.bounds.zw;
	vec2 offset = Uniforms.lightPos - worldPos;
	vec2 dir = normalize(offset);
	float relativeLength = length(offset) / Uniforms.lightRadius;
	float atten = clamp(1.0 - sqrt(relativeLength), 0, 1);
	vec3 light = Uniforms.lightIntensity * Uniforms.lightColor * atten;

	if (Uniforms.debug > 0)
	{
		fragColor.rgb = light;
		return;
	}
	fragColor.rgb = light * clamp(dot(dir, rim.xy), 0, 1);
}
