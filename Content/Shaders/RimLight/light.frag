#version 450

#extension GL_GOOGLE_include_directive: require

#include "common.glsl"

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

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
    vec2 worldPos = Uniforms.cameraBounds.xy + texCoord * Uniforms.cameraBounds.zw;
    
//    fragColor = vec4(Uniforms.numLights / float(MAX_NUM_TOTAL_LIGHTS), 0, 0, 1.0);
//    return;
    
//    fragColor = vec4(int(Uniforms.numLights + 50 == int(texCoord * Uniforms.cameraBounds.zw)), 0.01, 0, 1.0);
//    return;
    

    vec3 baseColor = texture(uniformTexture, texCoord).rgb;
    vec4 finalColor = vec4(0, 0, 0, 1);

    for (int i = 0; i < Uniforms.numLights; i++) {
        finalColor.rgb += CalculateLight(baseColor, Uniforms.lights[i], worldPos);
    }

    fragColor.rgba = finalColor;
}

