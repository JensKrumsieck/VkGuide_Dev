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

    public AllocatedBuffer CameraBuffer;
    public DescriptorSet GlobalDescriptor;

    public AllocatedBuffer ObjectBuffer;
    public DescriptorSet ObjectDescriptor;
}