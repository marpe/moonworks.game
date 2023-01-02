#version 450

layout (set = 1, binding = 0) uniform sampler2D uniformTexture;

layout (location = 0) in vec2 texCoord;
layout (location = 1) in vec4 color;

layout (location = 0) out vec4 fragColor;

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

vec4 test4()
{
    vec2 txSize = textureSize(uniformTexture, 0);
    vec2 tex = txSize * texCoord;
    vec2 pix;
    if (tex.x < 8.5f - fwidth(tex.x))
        pix = floor(tex) + 0.5; // regular point sampling
    else if (tex.x > 8.5f)
        pix = floor(tex) + min(fract(tex) / fwidth(tex), 1) - 0.5; // aa point sampling
    else
        return vec4(0);
    return texture(uniformTexture, pix / txSize) * color;
}

vec4 test5()
{
    vec2 txSize = textureSize(uniformTexture, 0);
    float scale = 4;
    vec2 tex = txSize * texCoord + vec2(0.5 / scale);
    vec2 pix = floor(tex) + min(fract(tex) / fwidth(tex), 1) - 0.5; // aa point sampling

    // TODO (marpe): this hack prevents bleed but should probably be fixed by adding padding to the tilesets
    /**int cellSize = 16;
    vec2 cellBounds = floor(tex / cellSize) * cellSize;
    float i = 0.5f;
    pix = vec2(
        clamp(pix.x, cellBounds.x + i, cellBounds.x + cellSize - i),
        clamp(pix.y, cellBounds.y + i, cellBounds.y + cellSize - i)
    );*/
            
    vec2 uv = pix / txSize;
    return texture(uniformTexture, uv) * color;
}

void main()
{
    fragColor = test2();
}

