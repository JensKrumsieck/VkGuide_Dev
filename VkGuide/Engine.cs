using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Vulkanize;
using Framebuffer = Silk.NET.Vulkan.Framebuffer;
using Image = Silk.NET.Vulkan.Image;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VkGuide;

public class Engine
{

    private readonly Vk _vk = Vulkanize.Vulkanize.Vk;
    private IWindow _window;
    private Instance _instance;
    private DebugUtilsMessengerEXT _debugMessenger;
    
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _device;

    private SwapchainKHR _swapchain;
    private KhrSwapchain _khrSwapchain;
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

    private Semaphore _presentSemaphore;
    private Semaphore _renderSemaphore;
    private Fence _renderFence;

    private Pipeline _trianglePipeline;
    private PipelineLayout _trianglePipelineLayout;

    private int _frameNumber;
    private bool _isInitialized;
    
    public void Init()
    {
        Window.PrioritizeGlfw();
        var options = WindowOptions.DefaultVulkan with
        {
            Title = "Vulkan Engine",
            Size = new Vector2D<int>((int) _windowExtent.Width, (int) _windowExtent.Height),
        };
        _window = Window.Create(options);
        _window.Initialize();
        InitVulkan();
        InitCommands();
        InitDefaultRenderPass();
        InitFrameBuffers();
        InitSyncStructures();
        InitPipelines();
        _isInitialized = true;
    }

    private void InitVulkan()
    {
        var instanceInfo = new InstanceBuilder()
            .SetAppName("Example Vulkan Application")
            .UseRequiredWindowExtensions(_window)
            .EnableValidationLayers()
            .RequireApiVersion(Vk.Version11)
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
        _khrSwapchain = _vk.CheckKhrSwapchainExtension(_device);
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
            Layout = ImageLayout.ColorAttachmentOptimal
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
    private unsafe void InitFrameBuffers()
    {
        var fbInfo = new FramebufferCreateInfo
        {
            SType = StructureType.FramebufferCreateInfo,
            PNext = null,
            RenderPass = _renderPass,
            AttachmentCount = 1,
            Width = _windowExtent.Width,
            Height = _windowExtent.Height,
            Layers = 1
        };
        var swapchainImageCount = _swapchainImages.Length;
        _framebuffers = new Framebuffer[swapchainImageCount];
        for (var i = 0; i < swapchainImageCount; i++)
        {
            var attachment = _swapchainImageViews[i];
            fbInfo.PAttachments = &attachment;
            _vk.CreateFramebuffer(_device, fbInfo, null, out var framebuffer);
            _framebuffers[i] = framebuffer;
        }
    }

    private unsafe void InitSyncStructures()
    {
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            PNext = null,
            Flags = FenceCreateFlags.SignaledBit
        };
        _vk.CreateFence(_device, fenceInfo, null, out _renderFence);

        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
            PNext = null,
            Flags = 0
        };
        _vk.CreateSemaphore(_device, semaphoreInfo, null, out _presentSemaphore);
        _vk.CreateSemaphore(_device, semaphoreInfo, null, out _renderSemaphore);
    }

    private unsafe void InitPipelines()
    {
        if (!LoaderShaderModule("triangle.frag.spv", out var fragShader))
            Console.WriteLine("Failed to load frag shader");
        if(!LoaderShaderModule("triangle.vert.spv", out var vertShader))
            Console.WriteLine("Failed to load vert shader");

        var layoutInfo = VkInit.PipelineLayoutCreateInfo();
        _vk.CreatePipelineLayout(_device, layoutInfo, null, out _trianglePipelineLayout);

        var builder = new PipelineBuilder
        {
            ShaderStages = new[]
            {
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.VertexBit, vertShader),
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.FragmentBit, fragShader)
            },
            VertexInputInfo = VkInit.VertexInputStateCreateInfo(),
            InputAssembly = VkInit.InputAssemblyCreateInfo(PrimitiveTopology.TriangleList),
            Viewport = new Viewport
            {
                X = 0f,
                Y = 0f,
                Width = _windowExtent.Width,
                Height = _windowExtent.Height,
                MinDepth = 0f,
                MaxDepth = 1f
            },
            Scissor = new Rect2D(new Offset2D(0, 0), _windowExtent),
            Rasterizer = VkInit.RasterizationStateCreateInfo(PolygonMode.Fill),
            Multisampling = VkInit.MultisampleStateCreateInfo(),
            ColorBlendAttachment = VkInit.ColorBlendAttachmentState(),
            PipelineLayout = _trianglePipelineLayout
        };

        _trianglePipeline = builder.Build(_device, _renderPass);
    }
    
    private unsafe bool LoaderShaderModule(string path, out ShaderModule shaderModule)
    {
        var shaderCode = Util.ReadBytesFromResource(path);
        fixed(byte* shaderPtr = shaderCode)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (uint) shaderCode.Length,
                PCode = (uint*) shaderPtr
            };
            return _vk.CreateShaderModule(_device, createInfo, null, out shaderModule) == Result.Success;
        }
    }
    
    public void Run()
    {
        _window.Render += Draw;
        _window.Run();
        _vk.DeviceWaitIdle(_device);
    }

    private unsafe void Draw(double deltaTime)
    {
        _vk.WaitForFences(_device, 1, _renderFence, true, 1000000000);
        _vk.ResetFences(_device, 1, _renderFence);
        var swapchainImageIndex = 0U;
        _khrSwapchain.AcquireNextImage(_device, _swapchain, 1000000000, _presentSemaphore, default,
            ref swapchainImageIndex);
        _vk.ResetCommandBuffer(_mainCommandBuffer, 0);
        var cmd = _mainCommandBuffer;
        var cmdBeginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            PNext = null,
            PInheritanceInfo = null,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        _vk.BeginCommandBuffer(cmd, cmdBeginInfo);
        var clearValue = new ClearValue();
        var flashB = MathF.Abs(MathF.Sin(_frameNumber / 120f));
        var flashG = MathF.Abs(MathF.Sin(_frameNumber / 120f+.5f));
        var flashR = MathF.Abs(MathF.Cos(_frameNumber / 120f));
        clearValue.Color = new ClearColorValue(flashR, flashG, flashB, 0);

        var rpInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            PNext = null,
            RenderPass = _renderPass,
            RenderArea = {Offset = {X = 0, Y = 0}, Extent = _windowExtent},
            Framebuffer = _framebuffers[swapchainImageIndex],
            ClearValueCount = 1,
            PClearValues = &clearValue
        };
        _vk.CmdBeginRenderPass(cmd, rpInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _trianglePipeline);
        _vk.CmdDraw(cmd, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(cmd);
        _vk.EndCommandBuffer(_mainCommandBuffer);
        
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var waitSemaphore = _presentSemaphore;
        var signalSemaphore = _renderSemaphore;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            PNext = null,
            PWaitDstStageMask = &waitStage,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSemaphore,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSemaphore,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };
        _vk.QueueSubmit(_graphicsQueue, 1, submit, _renderFence);

        fixed (SwapchainKHR* swapchainPtr = &_swapchain)
        {
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                PNext = null,
                SwapchainCount = 1,
                PSwapchains = swapchainPtr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &signalSemaphore,
                PImageIndices = &swapchainImageIndex
            };
            _khrSwapchain.QueuePresent(_graphicsQueue, presentInfo);
        }
        _frameNumber++;
    }

    public unsafe void Terminate()
    {
        if (!_isInitialized) return;
        
        _vk.DestroyCommandPool(_device, _commandPool, null);

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            _vk.DestroyImageView(_device, _swapchainImageViews[i], null);
            _vk.DestroyFramebuffer(_device, _framebuffers[i], null);
        }
        _vk.DestroyPipeline(_device, _trianglePipeline, null);
        _vk.DestroyPipelineLayout(_device, _trianglePipelineLayout, null);
        _vk.DestroyFence(_device, _renderFence, null);
        _vk.DestroySemaphore(_device, _presentSemaphore, null);
        _vk.DestroySemaphore(_device, _renderSemaphore, null);
        _vk.CheckKhrSwapchainExtension(_device).DestroySwapchain(_device, _swapchain, null);
        _vk.DestroyRenderPass(_device, _renderPass, null);
        _vk.DestroyDevice(_device, null);
        _vk.CheckKhrSurfaceExtension().DestroySurface(_instance, _surface, null);
        _vk.CheckExtDebugUtils().DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
        _vk.DestroyInstance(_instance, null);
        _window.Dispose();
    }
}
