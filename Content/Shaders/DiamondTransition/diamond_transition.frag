#version 450

layout(set = 1, binding = 0) uniform sampler2D uniformTexture;

// An input into the shader from our game code.
// Ranges from 0 to 1 over the course of the transition.
// We use this to actually animate the shader.
layout(set = 3, binding = 0) uniform UniformBlock
{
    float progress;
    float diamondPixelSize;
} Uniforms;

layout(location = 0) in vec2 texCoord;
layout(location = 1) in vec4 color;

layout(location = 0) out vec4 fragColor;

void main() {
    float xFraction = fract(gl_FragCoord.x / Uniforms.diamondPixelSize);
    float yFraction = fract(gl_FragCoord.y / Uniforms.diamondPixelSize);

    float xDistance = abs(xFraction - 0.5);
    float yDistance = abs(yFraction - 0.5);

    if (xDistance + yDistance + texCoord.x + texCoord.y > Uniforms.progress * 4.0f) {
        discard;
    }

    fragColor = vec4(0.0, 0.0, 0.0, 1.0); // texture(uniformTexture, texCoord) * color;
}
