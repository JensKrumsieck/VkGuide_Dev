using Silk.NET.Vulkan;

namespace Vulkanize;

public struct SwapchainInfo
{
    public required SwapchainKHR Swapchain;
    public required Image[] Images;
    public required ImageView[] ImageViews;
    public required Format Format;
}
