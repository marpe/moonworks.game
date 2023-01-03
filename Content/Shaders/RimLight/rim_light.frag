#version 450

#extension GL_GOOGLE_include_directive: require

#include "common.glsl"

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;
layout (set = 1, binding = 1) uniform sampler2D depthMap;

layout (set = 3, binding = 0) uniform UniformBlock
{
    Light[MAX_NUM_TOTAL_LIGHTS] lights;
    vec4 texelSize; // 1 / renderTargetWith, 1 / renderTargetHeight, renderTargetWidth, renderTargetHeight
    vec4 cameraBounds;
    int scale;
    int numLights;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

void main()
{
    vec4 c = texture(uniformTexture, texCoord);

    vec4 depth = texture(depthMap, texCoord);

    if (depth.a == 0) {
        discard;
    }

    vec2 rim = vec2(0, 0);
    float value = 0;
    float inFrontOf = 0;
    vec2 dx = vec2(Uniforms.texelSize.x * Uniforms.scale, 0);
    vec2 dy = vec2(0, Uniforms.texelSize.y * Uniforms.scale);

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
    
    if (inFrontOf == 0) {
        discard;
    }
    
    vec3 color = vec3(0);
    vec2 worldPos = Uniforms.cameraBounds.xy + texCoord * Uniforms.cameraBounds.zw;

    for (int i = 0; i < Uniforms.numLights; i++) {
        vec3 lightColor = CalculateLight(vec3(0), Uniforms.lights[i], worldPos) * depth.a;
        
        vec2 offset = Uniforms.lights[i].lightPos - worldPos;
        vec2 dir = normalize(offset);
        float rimLightIntensity = Uniforms.lights[i].rimIntensity * clamp(dot(dir, rim.xy), 0, 1);
        color += lightColor * rimLightIntensity; 
    }

    fragColor.rgba = vec4(color, 1);
}
