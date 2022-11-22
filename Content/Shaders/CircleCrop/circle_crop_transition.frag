#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

layout (set = 3, binding = 0) uniform UniformBlock
{
    float progress;
    float padding;
    vec2 center;
    vec2 scaling;
    vec4 backgroundColor;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

void main() {
    float dist = length((Uniforms.center - texCoord) * Uniforms.scaling);
    
    float s = 1.0 - Uniforms.progress;
    
    fragColor = mix(texture(uniformTexture, texCoord), Uniforms.backgroundColor, step(s, dist));
}
