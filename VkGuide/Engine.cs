using System.Numerics;
using System.Runtime.CompilerServices;
using GlmSharp;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using VkGuide.Types;
using Vulkanize;
using VMASharp;
using Buffer = Silk.NET.Vulkan.Buffer;
using Framebuffer = Silk.NET.Vulkan.Framebuffer;
using Image = Silk.NET.Vulkan.Image;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VkGuide;

public class Engine
{

    private readonly Vk _vk = Vulkanize.Vulkanize.Vk;
    private VulkanMemoryAllocator _allocator;
    
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

    private Extent2D _windowExtent = new(1700, 900);

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
    private Pipeline _redTrianglePipeline;
    private Pipeline _meshPipeline;
    private PipelineLayout _trianglePipelineLayout;
    private PipelineLayout _meshPipelineLayout;

    private Mesh _triangleMesh;
    
    private int _frameNumber;
    private int _selectedPipeline;
    private bool _isInitialized;

    private DeletionQueue _mainDeletionQueue = new();
    
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
        _mainDeletionQueue.Queue(() => _window.Dispose());
        InitVulkan();
        InitCommands();
        InitDefaultRenderPass();
        InitFrameBuffers();
        InitSyncStructures();
        InitPipelines();
        LoadMeshes();
        _isInitialized = true;
    }

    private unsafe void InitVulkan()
    {
        var instanceInfo = new InstanceBuilder()
            .SetAppName("Example Vulkan Application")
            .UseRequiredWindowExtensions(_window)
            .EnableValidationLayers()
            .RequireApiVersion(Vk.Version11)
            .UseDefaultDebugMessenger()
            .Build();
        _instance = instanceInfo.Instance;
        _mainDeletionQueue.Queue(() => _vk.DestroyInstance(_instance, null));
        _debugMessenger = instanceInfo.DebugMessenger!.Value;
        _mainDeletionQueue.Queue(() => _vk.CheckExtDebugUtils().DestroyDebugUtilsMessenger(_instance, _debugMessenger, null));
        _surface = instanceInfo.CreateSurface(_window);
        _mainDeletionQueue.Queue(() => _vk.CheckKhrSurfaceExtension().DestroySurface(_instance, _surface, null));

        var physicalDeviceInfo = new PhysicalDeviceSelector()
            .SetSurface(_surface)
            .RequireSwapChain()
            .Select();
        _physicalDevice = physicalDeviceInfo.PhysicalDevice;

        var deviceInfo = new DeviceBuilder(physicalDeviceInfo)
            .EnableValidationLayers()
            .Build();
        _device = deviceInfo.Device;
        _mainDeletionQueue.Queue(() => _vk.DestroyDevice(_device, null));
        _graphicsQueueFamily = deviceInfo.QueueFamilies.GraphicsFamily!.Value;
        _graphicsQueue = deviceInfo.Queue;
        InitSwapchain(deviceInfo);

        var createInfo = new VulkanMemoryAllocatorCreateInfo
        {
            VulkanAPIObject = _vk,
            VulkanAPIVersion = Vk.Version11,
            PhysicalDevice = _physicalDevice,
            LogicalDevice = _device,
            Instance = _instance
        };
        _allocator = new VulkanMemoryAllocator(createInfo);
        _mainDeletionQueue.Queue(() => _allocator.Dispose());
    }

    private unsafe void InitSwapchain(DeviceInfo deviceInfo)
    {
        _khrSwapchain = _vk.CheckKhrSwapchainExtension(_device);
        var swapchainInfo = new SwapchainBuilder(deviceInfo)
            .SetDesiredPresentMode(PresentModeKHR.FifoRelaxedKhr)
            .SetDesiredExtent(_windowExtent)
            .UseDefaultFormat()
            .Build();
        _swapchain = swapchainInfo.Swapchain;
        _mainDeletionQueue.Queue(()=> _khrSwapchain.DestroySwapchain(_device, _swapchain, null));
        _swapchainImages = swapchainInfo.Images;
        _swapchainImageViews = swapchainInfo.ImageViews;
        _swapchainImageFormat = swapchainInfo.Format;
    }

    private unsafe void InitCommands()
    {
        var commandPoolInfo = VkInit.CommandPoolCreateInfo(_graphicsQueueFamily);
        _vk.CreateCommandPool(_device, commandPoolInfo, null, out _commandPool);
        _mainDeletionQueue.Queue(() => _vk.DestroyCommandPool(_device, _commandPool, null));
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
        _mainDeletionQueue.Queue(() => _vk.DestroyRenderPass(_device, _renderPass, null));
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
            var view = _swapchainImageViews[i];
            _mainDeletionQueue.Queue(() => _vk.DestroyFramebuffer(_device, framebuffer, null));
            _mainDeletionQueue.Queue(() => _vk.DestroyImageView(_device, view, null));
        }
    }

    private unsafe void InitSyncStructures()
    {
        var fenceInfo = VkInit.FenceCreateInfo(FenceCreateFlags.SignaledBit);
        _vk.CreateFence(_device, fenceInfo, null, out _renderFence);
        _mainDeletionQueue.Queue(() => _vk.DestroyFence(_device, _renderFence, null));
        var semaphoreInfo = VkInit.SemaphoreCreateInfo();
        _vk.CreateSemaphore(_device, semaphoreInfo, null, out _presentSemaphore);
        _mainDeletionQueue.Queue(() => _vk.DestroySemaphore(_device, _presentSemaphore, null));
        _vk.CreateSemaphore(_device, semaphoreInfo, null, out _renderSemaphore);
        _mainDeletionQueue.Queue(() => _vk.DestroySemaphore(_device, _renderSemaphore, null));
    }

    private unsafe void InitPipelines()
    {
        if (!LoaderShaderModule("triangle.frag.spv", out var fragShader))
            Console.WriteLine("Failed to load frag shader");
        _mainDeletionQueue.Queue(() => _vk.DestroyShaderModule(_device, fragShader, null));
        if(!LoaderShaderModule("triangle.vert.spv", out var vertShader))
            Console.WriteLine("Failed to load vert shader");
        _mainDeletionQueue.Queue(() => _vk.DestroyShaderModule(_device, vertShader, null));
        if (!LoaderShaderModule("colored_triangle.frag.spv", out var coloredFragShader))
            Console.WriteLine("Failed to load frag shader");
        _mainDeletionQueue.Queue(() => _vk.DestroyShaderModule(_device, coloredFragShader, null));
        if(!LoaderShaderModule("colored_triangle.vert.spv", out var coloredVertShader))
            Console.WriteLine("Failed to load vert shader");
        _mainDeletionQueue.Queue(() => _vk.DestroyShaderModule(_device, coloredVertShader, null));
        
        var layoutInfo = VkInit.PipelineLayoutCreateInfo();
        _vk.CreatePipelineLayout(_device, layoutInfo, null, out _trianglePipelineLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyPipelineLayout(_device, _trianglePipelineLayout, null));

        var pushConstant =
            new PushConstantRange(ShaderStageFlags.VertexBit, 0, (uint) Unsafe.SizeOf<MeshPushConstants>());
        layoutInfo.PPushConstantRanges = &pushConstant;
        layoutInfo.PushConstantRangeCount = 1;
        _vk.CreatePipelineLayout(_device, layoutInfo, null, out _meshPipelineLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyPipelineLayout(_device, _meshPipelineLayout, null));
        
        var builder = new PipelineBuilder
        {
            ShaderStages = new[]
            {
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.VertexBit, coloredVertShader),
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.FragmentBit, coloredFragShader)
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
        _mainDeletionQueue.Queue(() => _vk.DestroyPipeline(_device, _trianglePipeline, null));
        builder.ShaderStages = new[]
        {
            VkInit.ShaderStageCreateInfo(ShaderStageFlags.VertexBit, vertShader),
            VkInit.ShaderStageCreateInfo(ShaderStageFlags.FragmentBit, fragShader)
        };
        _redTrianglePipeline = builder.Build(_device, _renderPass);
        _mainDeletionQueue.Queue(() => _vk.DestroyPipeline(_device, _redTrianglePipeline, null));

        var vertexDescription = Vertex.GetVertexDescription();
        fixed (VertexInputAttributeDescription* attributesPtr = vertexDescription.Attributes)
            builder.VertexInputInfo.PVertexAttributeDescriptions = attributesPtr;
        builder.VertexInputInfo.VertexAttributeDescriptionCount = (uint) vertexDescription.Attributes.Length;
        fixed (VertexInputBindingDescription* bindingsPtr = vertexDescription.Bindings)
            builder.VertexInputInfo.PVertexBindingDescriptions = bindingsPtr;
        builder.VertexInputInfo.VertexBindingDescriptionCount = (uint) vertexDescription.Bindings.Length;
        
        if (!LoaderShaderModule("tri_mesh.vert.spv", out var triVertShader))
            Console.WriteLine("Failed to load tri vert shader");
        _mainDeletionQueue.Queue(() => _vk.DestroyShaderModule(_device, triVertShader, null));

        builder.ShaderStages = new[]
        {
            VkInit.ShaderStageCreateInfo(ShaderStageFlags.VertexBit, triVertShader),
            VkInit.ShaderStageCreateInfo(ShaderStageFlags.FragmentBit, coloredFragShader)
        };
        builder.PipelineLayout = _meshPipelineLayout;
        _meshPipeline = builder.Build(_device, _renderPass);
        _mainDeletionQueue.Queue(() => _vk.DestroyPipeline(_device, _meshPipeline, null));
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

    private void LoadMeshes()
    {
        var vertices = new Vertex[]
        {
            new() {Position = new Vector3(1, 1, 0), Color = new Vector3(0, 1, 0)},
            new() {Position = new Vector3(-1, 1, 0), Color = new Vector3(0, 1, 0)},
            new() {Position = new Vector3(0,-1,0), Color = new Vector3(0,1,0)}
        };
        _triangleMesh = new Mesh {Vertices = vertices};
        UploadMesh(_triangleMesh);
    }

    private unsafe void UploadMesh(Mesh mesh)
    {
        CreateBuffer<Vertex>(mesh.Vertices, out var buffer, out var allocation);
        mesh.VertexBuffer = new AllocatedBuffer { Allocation = allocation, Buffer = buffer};
        _mainDeletionQueue.Queue(() => _vk.DestroyBuffer(_device, buffer, null));
        _mainDeletionQueue.Queue(() => allocation.Dispose());
    }

    private void CreateBuffer<T>(ReadOnlySpan<T> span, out Buffer buffer, out Allocation allocation)
        where T : unmanaged
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = (uint)span.Length * (uint)Unsafe.SizeOf<T>(),
            Usage = BufferUsageFlags.VertexBufferBit
        };
        var allocInfo = new AllocationCreateInfo
        {
            Flags = AllocationCreateFlags.Mapped,
            Usage = MemoryUsage.CPU_To_GPU
        };
        buffer = _allocator.CreateBuffer(in bufferInfo, in allocInfo, out allocation); 
        
        if (!allocation.TryGetSpan(out Span<T> bufferSpan))
            throw new InvalidOperationException("Unable to get Span<T> to mapped allocation.");
        span.CopyTo(bufferSpan);
    }
    
    public void Run()
    {
        _window.Render += Draw;
        var input = _window.CreateInput();
        input.Keyboards[0].KeyDown += (keyboard, key, arg3) =>
        {
            if (key != Key.Space) return;
            _selectedPipeline++;
            if (_selectedPipeline > 2) _selectedPipeline = 0;
            Console.WriteLine("Switching Shaders");
        };
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
        _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _meshPipeline);
        ulong offset = 0;
        var vertBuffer = _triangleMesh.VertexBuffer.Buffer;
        _vk.CmdBindVertexBuffers(cmd, 0, 1, &vertBuffer, &offset);
        var camPos = new vec3(0, 0, -2f);
        var view = mat4.Translate(camPos);
        var projection = mat4.Perspective(70 * (MathF.PI / 180),
                                                                (_windowExtent.Width / (float) _windowExtent.Height),
                                                                .1f,
                                                                200f);
        projection = projection with {m11 = projection.m11 * -1};
        var model = mat4.Rotate(_frameNumber * .4f * (MathF.PI / 180), vec3.UnitY);
        var meshMatrix = projection * view * model;
        var constants = new MeshPushConstants { RenderMatrix = meshMatrix };
        _vk.CmdPushConstants(cmd, _meshPipelineLayout, ShaderStageFlags.VertexBit, 0,
            (uint)Unsafe.SizeOf<MeshPushConstants>(), &constants);
        
        _vk.CmdDraw(cmd, (uint)_triangleMesh.Vertices.Length, 1, 0, 0);
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

    public void Terminate()
    {
        if (!_isInitialized) return;
        _mainDeletionQueue.Flush();
    }
}
