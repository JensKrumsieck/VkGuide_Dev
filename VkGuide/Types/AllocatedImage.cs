using Silk.NET.Vulkan;
using VMASharp;

namespace VkGuide.Types;

public struct AllocatedImage
{
    public Image Image;
    public Allocation Allocation;
}
