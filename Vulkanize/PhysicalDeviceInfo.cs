using Silk.NET.Vulkan;

namespace Vulkanize;

public struct PhysicalDeviceInfo
{
    public required PhysicalDevice PhysicalDevice;
    public required SurfaceKHR Surface;
    public required List<string> DeviceExtensions;
    public required PhysicalDeviceFeatures PreferredFeatures;
}
