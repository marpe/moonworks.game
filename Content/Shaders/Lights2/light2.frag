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
    vec3 lightColor = CalculateLight(Uniforms.light, texCoord);
    lightColor += lightColor * Uniforms.light.volumetricIntensity;
    fragColor = vec4(lightColor, 1);
}

