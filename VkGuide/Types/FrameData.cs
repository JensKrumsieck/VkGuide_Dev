using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VkGuide.Types;

public struct FrameData
{
    public Semaphore PresentSemaphore;
    public Semaphore RenderSemaphore;
    public Fence RenderFence;

    public CommandPool CommandPool;
    public CommandBuffer MainCommandBuffer;
}