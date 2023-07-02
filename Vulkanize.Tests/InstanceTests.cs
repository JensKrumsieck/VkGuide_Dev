using Silk.NET.Vulkan;

namespace Vulkanize.Tests;

public class InstanceTests
{
    [Fact]
    public void Instance_Can_Be_Created()
    {
        var info = new InstanceBuilder().Build();
        info.Should().BeOfType<InstanceInfo>();
        info.Instance.Should().BeOfType<Instance>();
        info.DebugMessenger.Should().BeNull();
    }

    [Fact]
    public void Instance_Can_Be_Created_With_Name()
    {
        var info = new InstanceBuilder()
            .SetAppName("Test")
            .Build();
        info.Should().BeOfType<InstanceInfo>();
        info.Instance.Should().BeOfType<Instance>();
        info.DebugMessenger.Should().BeNull();
    }
    
    [Fact]
    public void Instance_Can_Be_Created_With_DefaultValidation()
    {
        var info = new InstanceBuilder()
            .SetAppName("Test")
            .EnableValidationLayers()
            .UseDefaultDebugMessenger()
            .Build();
        info.Should().BeOfType<InstanceInfo>();
        info.Instance.Should().BeOfType<Instance>();
        info.DebugMessenger.Should().BeOfType<DebugUtilsMessengerEXT>();
    }
}
