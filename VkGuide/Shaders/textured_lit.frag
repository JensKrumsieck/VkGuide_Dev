#version 460

layout (location = 0) in vec3 vertColor;
layout (location = 1) in vec2 texCoord;

layout (location = 0) out vec4 outFragColor;

layout (set = 0, binding = 1) uniform SceneData{
    vec4 fogColor;
    vec4 fogDistances;
    vec4 ambientColor;
    vec4 sunlightDirection;
    vec4 sunlightColor;
} sceneData;

layout (set = 2, binding = 0) uniform sampler2D tex1;

void main()
{
    vec3 color = texture(tex1,texCoord).xyz;
    outFragColor = vec4(color,1.0f);
}