using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Vulkanize.Tests;

public class SwapchainTests
{
    private readonly PhysicalDeviceInfo _pInfo;
    private readonly DeviceInfo _dInfo;
    private Extent2D _windowExtent = new(1700, 900);
    public SwapchainTests()
    {
        var window = Window.Create(WindowOptions.DefaultVulkan);
        window.Initialize();
        var info = new InstanceBuilder()
            .SetAppName("Test")
            .RequireApiVersion(Vk.Version12)
            .EnableValidationLayers()
            .UseDefaultDebugMessenger()
            .UseRequiredWindowExtensions(window)
            .Build();
        var surface = info.CreateSurface(window);
        _pInfo = new PhysicalDeviceSelector()
            .SetSurface(surface)
            .RequireSwapChain()
            .Select();
        _dInfo = new DeviceBuilder(_pInfo).EnableValidationLayers().Build();
    }

    [Fact]
    public void Swapchain_Can_Be_Created()
    {
        var sInfo = new SwapchainBuilder(_dInfo)
            .UseDefaultFormat()
            .SetDesiredExtent(_windowExtent)
            .SetDesiredPresentMode(PresentModeKHR.FifoRelaxedKhr)
            .Build();
        sInfo.Swapchain.Should().BeOfType<SwapchainKHR>();
        sInfo.Images.Should().HaveCount(3);
    }
}
