using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Vulkanize;

namespace VkGuide;

public class Engine
{

    private Vk _vk = Vulkanize.Vulkanize.Vk;
    private IWindow _window;
    private Instance _instance;
    private DebugUtilsMessengerEXT _debugMessenger;
    
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _device;

    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages;
    private ImageView[] _swapchainImageViews;
    private Format _swapchainImageFormat;

    private Extent2D _windowExtent = new(1440, 900);

    private Queue _graphicsQueue;
    private uint _graphicsQueueFamily;

    private CommandPool _commandPool;
    private CommandBuffer _mainCommandBuffer;

    private RenderPass _renderPass;
    private Framebuffer[] _framebuffers;

    private bool _isInitialized;
    
    public void Init()
    {
        Window.PrioritizeGlfw();
        var options = WindowOptions.DefaultVulkan with
        {
            Title = "Vulkan Engine",
            Size = new Vector2D<int>((int) _windowExtent.Width, (int) _windowExtent.Height)
        };
        _window = Window.Create(options);
        _window.Initialize();
        InitVulkan();
        InitCommands();
        InitDefaultRenderPass();
        _isInitialized = true;
    }

    private void InitVulkan()
    {
        var instanceInfo = new InstanceBuilder()
            .SetAppName("Example Vulkan Application")
            .UseRequiredWindowExtensions(_window)
            .EnableValidationLayers()
            .RequireApiVersion(1, 1, 0)
            .UseDefaultDebugMessenger()
            .Build();
        _instance = instanceInfo.Instance;
        _debugMessenger = instanceInfo.DebugMessenger!.Value;
        _surface = instanceInfo.CreateSurface(_window);

        var physicalDeviceInfo = new PhysicalDeviceSelector()
            .SetSurface(_surface)
            .RequireSwapChain()
            .Select();
        _physicalDevice = physicalDeviceInfo.PhysicalDevice;

        var deviceInfo = new DeviceBuilder(physicalDeviceInfo)
            .EnableValidationLayers()
            .Build();
        _device = deviceInfo.Device;
        _graphicsQueueFamily = deviceInfo.QueueFamilies.GraphicsFamily!.Value;
        _graphicsQueue = deviceInfo.Queue;
        InitSwapchain(deviceInfo);
    }

    private void InitSwapchain(DeviceInfo deviceInfo)
    {
        var swapchainInfo = new SwapchainBuilder(deviceInfo)
            .SetDesiredPresentMode(PresentModeKHR.FifoRelaxedKhr)
            .SetDesiredExtent(_windowExtent)
            .UseDefaultFormat()
            .Build();
        _swapchain = swapchainInfo.Swapchain;
        _swapchainImages = swapchainInfo.Images;
        _swapchainImageViews = swapchainInfo.ImageViews;
        _swapchainImageFormat = swapchainInfo.Format;
    }

    private unsafe void InitCommands()
    {
        var commandPoolInfo = VkInit.CommandPoolCreateInfo(_graphicsQueueFamily);
        _vk.CreateCommandPool(_device, commandPoolInfo, null, out _commandPool);

        var cmdAllocInfo = VkInit.CommandBufferAllocateInfo(_commandPool);
        _vk.AllocateCommandBuffers(_device, cmdAllocInfo, out _mainCommandBuffer);
    }

    private unsafe void InitDefaultRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.AttachmentOptimal
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass
        };
        _vk.CreateRenderPass(_device, renderPassInfo, null, out _renderPass);
    }
    private void InitFrameBuffers(){}
    
    public void Run()
    {
        _window.Run();
    }

    public unsafe void Terminate()
    {
        if (!_isInitialized) return;
        
        _vk.DestroyCommandPool(_device, _commandPool, null);
        
        foreach (var view in _swapchainImageViews) 
            _vk.DestroyImageView(_device, view, null);
        _vk.CheckKhrSwapchainExtension(_device).DestroySwapchain(_device, _swapchain, null);
        _vk.DestroyRenderPass(_device, _renderPass, null);
        _vk.DestroyDevice(_device, null);
        _vk.CheckKhrSurfaceExtension().DestroySurface(_instance, _surface, null);
        _vk.CheckExtDebugUtils().DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
        _vk.DestroyInstance(_instance, null);
        _window.Dispose();
    }
}
