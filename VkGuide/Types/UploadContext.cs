using Silk.NET.Vulkan;

namespace VkGuide.Types;

public struct UploadContext
{
    public Fence UploadFence;
    public CommandPool CommandPool;
    public CommandBuffer CommandBuffer;
}