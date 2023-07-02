using Silk.NET.Vulkan;

namespace Vulkanize;

public class SwapchainBuilder
{
    private readonly Vk _vk = Vulkanize.Vk;
    private readonly DeviceInfo _deviceInfo;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Device _device;
    private readonly SurfaceKHR _surface;
    
    private Extent2D _extent;
    private PresentModeKHR _presentMode;
    private SurfaceFormatKHR _surfaceFormat;

    private SwapchainKHR _swapchain;
    private ImageView[] _swapChainImageViews;
    private Image[] _swapChainImages;
    
    public SwapchainBuilder(DeviceInfo deviceInfo)
    {
        _deviceInfo = deviceInfo;
        _physicalDevice = deviceInfo.PhysicalDevice;
        _device = deviceInfo.Device;
        _surface = deviceInfo.Surface;
    }
    
    public SwapchainBuilder SetDesiredExtent(Extent2D extent)
    {
        _extent = extent;
        return this;
    }
    
    public SwapchainBuilder SetDesiredExtent(int width, int height) =>
        SetDesiredExtent(new Extent2D((uint) width, (uint) height));


    public SwapchainBuilder SetDesiredPresentMode(PresentModeKHR presentMode)
    {
        _presentMode = presentMode;
        return this;
    }

    public SwapchainBuilder UseFormat(SurfaceFormatKHR format)
    {
        _surfaceFormat = format;
        return this;
    }
    
    public SwapchainBuilder UseDefaultFormat()
    {
        _surfaceFormat.Format = Format.B8G8R8A8Srgb;
        _surfaceFormat.ColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr;
        return this;
    }

    public SwapchainInfo Build()
    {
        CreateSwapchain();
        CreateImageViews();
        return new SwapchainInfo
        {
            Swapchain = _swapchain,
            Images = _swapChainImages,
            ImageViews = _swapChainImageViews,
            Format = _surfaceFormat.Format
        };
    }
    
    private unsafe void CreateSwapchain()
    {
        var swapchainSupport = Vulkanize.QuerySwapChainSupport(_physicalDevice, _surface);
        if (!swapchainSupport.PresentModes.Contains(_presentMode))
            throw new Exception("Selected present mode is not supported by the current device");
        if (!ValidFormat(swapchainSupport.Formats))
            throw new Exception("Selected format is not supported by the current device");
        _extent = ValidateSwapExtent(swapchainSupport.Capabilities);

        var imgCount = swapchainSupport.Capabilities.MinImageCount + 1;
        if (swapchainSupport.Capabilities.MaxImageCount > 0 && imgCount > swapchainSupport.Capabilities.MaxImageCount)
            imgCount = swapchainSupport.Capabilities.MaxImageCount;

        var swapchainCreateInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imgCount,
            ImageFormat = _surfaceFormat.Format,
            ImageColorSpace = _surfaceFormat.ColorSpace,
            ImageExtent = _extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };

        var indices = _deviceInfo.QueueFamilies;
        var queueFamilyIndices = stackalloc[] {indices.GraphicsFamily!.Value, indices.PresentFamily!.Value};
        
        if (indices.GraphicsFamily != indices.PresentFamily)
        {
            swapchainCreateInfo = swapchainCreateInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices,
            };
        }
        else
        {
            swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        swapchainCreateInfo = swapchainCreateInfo with
        {
            PresentMode = _presentMode,
            Clipped = true,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PreTransform = swapchainSupport.Capabilities.CurrentTransform
        };
        
        var khrSwapchain = Vulkanize.CheckKhrSwapchainExtension(_device);
        if (khrSwapchain.CreateSwapchain(_device, swapchainCreateInfo, null, out _swapchain) != Result.Success)
            throw new Exception("Failed to create swapchain");
        
        khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imgCount, null);
        _swapChainImages = new Image[imgCount];
        fixed (Image* swapChainImagesPtr = _swapChainImages)
            khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imgCount, swapChainImagesPtr);
    }

    private void CreateImageViews()
    {
        _swapChainImageViews = new ImageView[_swapChainImages!.Length];

        for (var i = 0; i < _swapChainImages.Length; i++)
            _swapChainImageViews[i] =
                CreateImageView(_swapChainImages[i], _surfaceFormat.Format, ImageAspectFlags.ColorBit, 1);
    }

    private unsafe ImageView CreateImageView(Image image, Format format, ImageAspectFlags aspectFlags, uint mipLevels)
    {
        var createInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange =
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = mipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1,
            }
        };

        if (_vk.CreateImageView(_device, createInfo, null, out var imageView) != Result.Success)
            throw new Exception("failed to create image view!");

        return imageView;
    }
    
    
    private bool ValidFormat(SurfaceFormatKHR[] formats)
    {
        for (var i = 0; i < formats.Length; i++)
        {
            if (formats[i].Format == _surfaceFormat.Format &&
                formats[i].ColorSpace == _surfaceFormat.ColorSpace) return true;
        }
        return false;
    }

    private Extent2D ValidateSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        var framebufferSize = _extent;
        var actualExtent = new Extent2D(framebufferSize.Width, framebufferSize.Height);
        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);
        return actualExtent;
    }
}
