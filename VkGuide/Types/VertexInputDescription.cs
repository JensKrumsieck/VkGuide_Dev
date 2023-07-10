using Silk.NET.Vulkan;

namespace VkGuide.Types;

public struct VertexInputDescription
{
    public VertexInputBindingDescription[] Bindings;
    public VertexInputAttributeDescription[] Attributes;
    public uint Flags;
}
