using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Vulkanize;

public struct InstanceInfo
{
    public required Instance Instance;
    public DebugUtilsMessengerEXT? DebugMessenger;
    public unsafe SurfaceKHR CreateSurface(IWindow window)
    {
        ArgumentNullException.ThrowIfNull(window.VkSurface);
        return window.VkSurface.Create<AllocationCallbacks>(Instance.ToHandle(), null).ToSurface();
    }
    
}
