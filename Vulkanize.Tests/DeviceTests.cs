using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Vulkanize.Tests;

public class DeviceTests
{
    private readonly PhysicalDeviceInfo _pInfo;
    public DeviceTests()
    {
        var window = Window.Create(WindowOptions.DefaultVulkan);
        window.Initialize();
        var info = new InstanceBuilder()
            .SetAppName("Test")
            .RequireApiVersion(Vk.Version12)
            .EnableValidationLayers()
            .UseDefaultDebugMessenger().
            UseRequiredWindowExtensions(window)
            .Build();
        var surface = info.CreateSurface(window);
        _pInfo = new PhysicalDeviceSelector().SetSurface(surface).Select();
    }

    [Fact]
    public void LogicalDevice_Can_Be_Created()
    {
        var deviceInfo = new DeviceBuilder(_pInfo).EnableValidationLayers().Build();
        deviceInfo.Device.Should().BeOfType<Device>();
    }
}
