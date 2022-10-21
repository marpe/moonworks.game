#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inTexCoord;
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec2 fragCoord;
layout(location = 1) out vec4 color;

layout(set = 2, binding = 0) uniform UBO
{
    mat4 viewProjection;
} ubo;

void main()
{
	gl_Position = ubo.viewProjection * vec4(inPosition, 0, 1.0);
	fragCoord = inTexCoord;
	color = inColor;
}
