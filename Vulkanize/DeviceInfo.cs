using Silk.NET.Vulkan;

namespace Vulkanize;

public struct DeviceInfo
{
    public required Device Device;
    public required PhysicalDevice PhysicalDevice;
    public required SurfaceKHR Surface;
    public required QueueFamilyIndices QueueFamilies;
}
