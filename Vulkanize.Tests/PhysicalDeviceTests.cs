using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Vulkanize.Tests;

public class PhysicalDeviceTests
{
    private readonly SurfaceKHR _surface;
    public PhysicalDeviceTests()
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
        _surface = info.CreateSurface(window);
    }
    
    [Fact]
    public void PhysicalDevice_Can_Be_Selected()
    {
        var pInfo = new PhysicalDeviceSelector().SetSurface(_surface).Select();
        pInfo.PhysicalDevice.Should().BeOfType<PhysicalDevice>();
        pInfo.DeviceExtensions.Should().HaveCount(0);
    }

    [Fact]
    public void Physical_Device_Can_Be_Selected_With_Features()
    {
        var pInfo = new PhysicalDeviceSelector()
            .SetSurface(_surface)
            .SetDeviceFeatures(new PhysicalDeviceFeatures
            {
                GeometryShader = true
            })
            .Select();
        pInfo.PhysicalDevice.Should().BeOfType<PhysicalDevice>();
        pInfo.DeviceExtensions.Should().HaveCount(0);
    }
}
