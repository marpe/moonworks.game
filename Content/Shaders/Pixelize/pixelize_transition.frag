#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;
layout (set = 1, binding = 1) uniform sampler2D sourceTexture;

layout (set = 3, binding = 0) uniform UniformBlock
{
    float progress;
    int steps;
    ivec2 squaresMin;
} Uniforms;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

float dist = Uniforms.steps > 0 ? ceil(Uniforms.progress * float(Uniforms.steps)) / float(Uniforms.steps) : Uniforms.progress;
vec2 squareSize = dist / vec2(Uniforms.squaresMin);

void main() {
    vec2 p = dist > 0.0 ? (floor(texCoord / squareSize) + 0.5) * squareSize : texCoord;
    fragColor = mix(texture(sourceTexture, p), texture(uniformTexture, p), Uniforms.progress) * color;
}
