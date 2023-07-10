using System.Numerics;
using System.Runtime.InteropServices;

namespace VkGuide.Types;

[StructLayout(LayoutKind.Sequential)]
public struct MeshPushConstants
{
    public Vector4 Data;
    public Matrix4x4 RenderMatrix;
}
