using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Vulkanize;

public enum PreferredDeviceType
{
    Any,
    LowEnergy,
    HighPerformance
}

public class PhysicalDeviceSelector
{
    private readonly Vk _vk = Vulkanize.Vk;
    private readonly Instance _instance;
    
    private SurfaceKHR _surface;

    private bool _requireSwapchain;

    private PreferredDeviceType _preferredDeviceType;
    private readonly List<string> _deviceExtensions = new();
    private PhysicalDeviceFeatures _deviceFeatures;

    public PhysicalDeviceSelector()
    {
        if(_vk.CurrentInstance.HasValue)
            _instance = _vk.CurrentInstance.Value;
        else throw new ArgumentNullException(nameof(_vk.CurrentInstance),"Create Instance first!");
    }
    
    public PhysicalDeviceSelector SetPreferredDeviceType(PreferredDeviceType deviceType)
    {
        _preferredDeviceType = deviceType;
        return this;
    }
    
    public PhysicalDeviceSelector SetSurface(SurfaceKHR surface)
    {
        _surface = surface;
        return this;
    }

    public PhysicalDeviceSelector AddExtension(string extension)
    {
        _deviceExtensions.Add(extension);
        return this;
    }

    public PhysicalDeviceSelector SetDeviceFeatures(PhysicalDeviceFeatures features)
    {
        _deviceFeatures = features;
        return this;
    }

    public PhysicalDeviceSelector RequireSwapChain()
    {
        _deviceExtensions.Add(KhrSwapchain.ExtensionName);
        _requireSwapchain = true;
        return this;
    }
    
    public unsafe PhysicalDeviceInfo Select()
    {
        var deviceCount = 0U;
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
        if (deviceCount == 0)
            throw new Exception("Failed to find GPU with Vulkan support");
        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);
        
        var device = PickDevice(devices);

        return new PhysicalDeviceInfo
        {
            PhysicalDevice = device,
            Surface = _surface,
            DeviceExtensions = _deviceExtensions,
            PreferredFeatures = _deviceFeatures
        };
    }
    
    private unsafe PhysicalDevice PickDevice(PhysicalDevice[] devices)
    {
        var useGpu = 0;
        var deviceType = _preferredDeviceType switch
        {
            PreferredDeviceType.LowEnergy => PhysicalDeviceType.IntegratedGpu,
            PreferredDeviceType.HighPerformance => PhysicalDeviceType.DiscreteGpu,
            _ => PhysicalDeviceType.Other
        };
        
        for (var i = 0; i < devices.Length; i++)
        {
            PhysicalDeviceProperties properties;
            _vk.GetPhysicalDeviceProperties(devices[i], &properties);
            if (!IsSuitable(devices[i])) continue;
            if (_preferredDeviceType == PreferredDeviceType.Any)
                return devices[i];
            if (properties.DeviceType == deviceType) useGpu = i;
        }
        return devices[useGpu];
    }
    
    private bool IsSuitable(PhysicalDevice device)
    {
        var indices = Vulkanize.FindQueueFamilies(device, _surface);
        if (!indices.IsComplete()) return false;
        
        var extensionsSupported = CheckDeviceExtensionsSupport(device);
        if (!extensionsSupported) return false;
        
        var swapChainAdequate = false;
        if (extensionsSupported)
        {
            var swapChainSupport = Vulkanize.QuerySwapChainSupport(device, _surface);
            swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
        }

        if (_requireSwapchain&& !swapChainAdequate) return false;

        _vk.GetPhysicalDeviceFeatures(device, out var supportedFeatures);
        return SupportsRequestedFeatures(supportedFeatures);

    }

    private bool SupportsRequestedFeatures(PhysicalDeviceFeatures supportedFeatures) => 
        typeof(PhysicalDeviceFeatures)
            .GetFields()
            .Where(f => f.IsPublic)
            .All(f => !(Bool32) f.GetValue(supportedFeatures)! || (Bool32) f.GetValue(_deviceFeatures)!);
    
    private unsafe bool CheckDeviceExtensionsSupport(PhysicalDevice device)
    {
        uint extensionsCount = 0;
        _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionsCount, null);

        var availableExtensions = new ExtensionProperties[extensionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            _vk.EnumerateDeviceExtensionProperties(device, (byte*) null, ref extensionsCount, availableExtensionsPtr);

        var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();
        return _deviceExtensions.All(availableExtensionNames.Contains);
    }
}
