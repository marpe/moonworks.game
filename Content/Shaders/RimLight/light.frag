#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

layout (set = 3, binding = 0) uniform UniformBlock
{
    float lightIntensity;
	float lightRadius;
    vec2 lightPos;
    vec4 texelSize; // 1 / renderTargetWith, 1 / renderTargetHeight, renderTargetWidth, renderTargetHeight
	vec4 bounds;
	vec3 lightColor;
	float debug;
	float rimLightIntensity;
	float angle;
	float coneAngle;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

void main()
{
	vec2 worldPos = Uniforms.bounds.xy + texCoord * Uniforms.bounds.zw;
	vec2 offset = worldPos - Uniforms.lightPos;
	float dist = length(offset);
	float relativeLength = dist / Uniforms.lightRadius;
	/* float atten = clamp(1.0 - relativeLength, 0, 1);
    if ( atten <= 0 )
        discard; */

	float radialFalloff = pow(1.0 - relativeLength, 2.0);
	float angleRad = radians(Uniforms.angle);
float deltaAngle = acos(dot(worldPos, Uniforms.lightPos) / (length(worldPos) + length(Uniforms.lightPos));

	float maxAngle = radians(Uniforms.coneAngle - 180) * 0.5;
	float angularFalloff = smoothstep(maxAngle, -maxAngle, deltaAngle);
	vec3 light = Uniforms.lightIntensity * Uniforms.lightColor * radialFalloff * angularFalloff;
	fragColor.rgba = vec4(light, 1);
}
