#version 450
#define MAX_NUM_TOTAL_LIGHTS 4

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

struct Light
{
    float lightIntensity;
    float lightRadius;
    vec2 lightPos;
    vec3 lightColor;
    float volumetricIntensity;
    float rimIntensity;
    float angle;
    float coneAngle;
};

layout (set = 3, binding = 0) uniform UniformBlock
{
    Light[MAX_NUM_TOTAL_LIGHTS] lights;
    vec4 texelSize; // 1 / renderTargetWith, 1 / renderTargetHeight, renderTargetWidth, renderTargetHeight
    vec4 cameraBounds;
    int scale;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

vec3 CalculateLight(Light light, vec2 worldPos) {
    vec2 offset = light.lightPos - worldPos;
    float distanceToLight = length(offset);
    if (distanceToLight > light.lightRadius) {
        return vec3(0);
    }

    float radialFalloff = clamp(distanceToLight / light.lightRadius, 0, 1);
    radialFalloff = pow(1.0 - radialFalloff, 2.0);

    float angleRad = radians(light.angle);
    float deltaAngle = acos(dot(normalize(offset), vec2(cos(angleRad), sin(angleRad))));
    float maxAngle = radians(light.coneAngle) * 0.5;

    if (deltaAngle > maxAngle) {
        return vec3(0);
    }

    float angularFalloff = max(smoothstep(maxAngle, 0, deltaAngle), int(light.coneAngle == 360));

    float finalIntensity = clamp(light.lightIntensity * radialFalloff * angularFalloff, 0, 1);
    vec4 baseColor = texture(uniformTexture, texCoord);
    vec3 lightColor = light.lightColor * finalIntensity;
    vec3 shadedColor = baseColor.rgb * lightColor;
    shadedColor += lightColor * light.volumetricIntensity;

    return shadedColor;
}

void main()
{
    vec2 worldPos = Uniforms.cameraBounds.xy + texCoord * Uniforms.cameraBounds.zw;

    vec4 finalColor = vec4(0, 0, 0, 1);

    for (int i = 0; i < MAX_NUM_TOTAL_LIGHTS; i++) {
        finalColor.rgb += CalculateLight(Uniforms.lights[i], worldPos);
    }

    fragColor.rgba = finalColor;
}

