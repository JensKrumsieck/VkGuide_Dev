using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Vulkanize;

public record struct SwapChainSupportDetails(SurfaceCapabilitiesKHR Capabilities, SurfaceFormatKHR[] Formats,
    PresentModeKHR[] PresentModes);

public record struct QueueFamilyIndices(uint? GraphicsFamily, uint? PresentFamily)
{
    public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
}

public static class Vulkanize
{
    public static readonly Vk Vk = Vk.GetApi();
    public static KhrSurface CheckKhrSurfaceExtension()
    {
        if (!Vk.CurrentInstance.HasValue)
            throw new Exception("Create an Instance first");
        var instance = Vk.CurrentInstance.Value;
        if (!Vk.TryGetInstanceExtension<KhrSurface>(instance, out var khrSurface))
            throw new NotSupportedException("KHR_surface extension not found.");
        return khrSurface;
    }
    
    public static KhrSwapchain CheckKhrSwapchainExtension(Device device)
    { 
        if (!Vk.CurrentInstance.HasValue)
            throw new Exception("Create an Instance first");
        var instance = Vk.CurrentInstance.Value;
        if (!Vk.TryGetDeviceExtension<KhrSwapchain>(instance, device, out var khrSwapChain))
            throw new NotSupportedException("VK_KHR_swapchain extension not found.");
        return khrSwapChain;
    }
    
    public static unsafe bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        Vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* layersPtr = availableLayers)
        {
            Vk.EnumerateInstanceLayerProperties(ref layerCount, layersPtr);
        }
        var availableLayerNames = availableLayers
            .Select(l => (Marshal.PtrToStringAnsi((nint) l.LayerName) ?? string.Empty)).ToHashSet();
        return availableLayerNames.All(availableLayerNames.Contains);
    }
    
    public static unsafe SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device, SurfaceKHR surface)
    {
        var khrSurface = CheckKhrSurfaceExtension();
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out var capabilities);
        uint formatCount = 0;
        khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, null);
        SurfaceFormatKHR[] formats;
        if (formatCount == 0) formats = Array.Empty<SurfaceFormatKHR>();
        else
        {
            formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = formats)
            {
                khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, formatsPtr);
            }
        }

        uint presentModeCount = 0;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref presentModeCount, null);

        PresentModeKHR[] modes;
        if (presentModeCount == 0) modes = Array.Empty<PresentModeKHR>();
        else
        {
            modes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* modesPtr = modes)
            {
                khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref formatCount, modesPtr);
            }
        }

        return new SwapChainSupportDetails(capabilities, formats, modes);
    }
    
    public static unsafe QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, SurfaceKHR surface)
    {
        var khrSurface = CheckKhrSurfaceExtension();
        var indices = new QueueFamilyIndices();
        uint queueFamilyCount = 0;
        Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);

        
        for(uint i = 0; i< queueFamilyCount; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indices.GraphicsFamily = i;

            khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

            if (presentSupport)
                indices.PresentFamily = i;

            if (indices.IsComplete())
                break;
        }
        return indices;
    }
}

