using System.Runtime.InteropServices;
using GlmSharp;

namespace VkGuide.Types;

[StructLayout(LayoutKind.Sequential)]
public struct MeshPushConstants
{
    public vec4 Data;
    public mat4 RenderMatrix;
}
