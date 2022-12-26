#version 450

layout(set = 1, binding = 0) uniform sampler2D uniformTexture;

layout(location = 0) in vec2 texCoord;
layout(location = 1) in vec4 color;

layout(location = 0) out vec4 fragColor;

vec4 test1()
{
    ivec2 txSize = textureSize(uniformTexture, 0);
    vec2 pixel = texCoord * txSize;

    // emulate point sampling
    pixel = floor(pixel) + 0.5;

    // subpixel aa algorithm
    float scale = 4.0;
    pixel += 1.0 - clamp((1.0 - fract(pixel)) * scale, 0.0, 1.0);

    vec2 uv = pixel / txSize;

    // output
    return texture(uniformTexture, uv) * color;
}

vec4 test2()
{
    ivec2 texSize = textureSize(uniformTexture, 0);
    
    // compute the new uv
    float smoothing_factor = 1.0;
    vec2 uv = texCoord;
    vec2 uv_width = fwidth(uv);
    vec2 sprite_screen_resolution = smoothing_factor / uv_width;

    vec2 uv_pixel_src = floor(uv * texSize + 0.499);

    vec2 edge = uv_pixel_src;
    edge = edge / texSize * sprite_screen_resolution;

    vec2 uv_pixel = uv * sprite_screen_resolution;
    vec2 uv_factor = clamp(uv_pixel - edge + 0.5, 0.0, 1.0);

    uv = (mix(uv_pixel_src - 1.0, uv_pixel_src, uv_factor) + 0.5) / texSize;

    return texture(uniformTexture, uv) * color;
}

vec4 test3()
{
    return texture(uniformTexture, texCoord) * color;
}

void main()
{
    fragColor = test1();
}

