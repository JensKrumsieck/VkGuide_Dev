using GlmSharp;

namespace VkGuide.Types;

public struct CameraData
{
    public mat4 View;
    public mat4 Projection;
    public mat4 ViewProjection;
}