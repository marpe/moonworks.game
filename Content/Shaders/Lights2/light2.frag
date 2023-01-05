#version 450

#extension GL_GOOGLE_include_directive: require
#include "common.glsl"

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

layout (set = 3, binding = 0) uniform UniformBlock
{
    Light light;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;
layout (location = 0) out vec4 fragColor;

void main()
{
    fragColor.rgba = vec4(1.0, 0, 0, 1);
    return;
    vec3 baseColor = texture(uniformTexture, texCoord).rgb;
    vec4 finalColor = CalculateLight(Uniforms.light, texCoord);
    fragColor.rgba = finalColor;
}

