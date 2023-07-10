using VMASharp;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace VkGuide.Types;

public struct AllocatedBuffer
{
    public Buffer Buffer;
    public Allocation Allocation;
}
