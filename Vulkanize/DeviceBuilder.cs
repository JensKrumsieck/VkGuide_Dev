using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Vulkanize;

public unsafe class DeviceBuilder
{
    private readonly Vk _vk;
    private readonly PhysicalDeviceInfo _physicalDeviceInfo;
    private bool _enableValidationLayers;
    private void* _pNext;
    
    private readonly List<string> _layers = new();
    public DeviceBuilder(PhysicalDeviceInfo physicalDeviceInfo)
    {
        _physicalDeviceInfo = physicalDeviceInfo;
        _vk = Vulkanize.Vk;
    }
    public DeviceBuilder EnableValidationLayers(bool enableValidation = true)
    {
        _enableValidationLayers = enableValidation;
        if(_enableValidationLayers) _layers.Add("VK_LAYER_KHRONOS_validation");
        return this;
    }

    public unsafe DeviceBuilder AddPNext(void* pNext)
    {
        _pNext = pNext;
        return this;
    }
    
    public DeviceInfo Build()
    {
        var indices = Vulkanize.FindQueueFamilies(_physicalDeviceInfo.PhysicalDevice, _physicalDeviceInfo.Surface);
        var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
        uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

        using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

        var queuePriority = 1.0f;
        for (var i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }
        
        var deviceFeatures = _physicalDeviceInfo.PreferredFeatures;
        var deviceExtensions = _physicalDeviceInfo.DeviceExtensions.ToArray();
        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions),
            PNext = _pNext
        };

        var layers = _layers.ToArray();
        if (_enableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)layers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(layers);
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
        }
        
        if (_vk.CreateDevice(_physicalDeviceInfo.PhysicalDevice, in createInfo, null, out var device) != Result.Success)
            throw new Exception("failed to create logical device!");

        if (_enableValidationLayers) SilkMarshal.Free((nint) createInfo.PpEnabledLayerNames);
        SilkMarshal.Free((nint) createInfo.PpEnabledExtensionNames);

        _vk.GetDeviceQueue(device, indices.GraphicsFamily.Value, 0, out var queue);
        
        return new DeviceInfo
        {
            Device = device,
            PhysicalDevice = _physicalDeviceInfo.PhysicalDevice,
            Surface = _physicalDeviceInfo.Surface,
            QueueFamilies = indices,
            Queue = queue
        };
    }
}
