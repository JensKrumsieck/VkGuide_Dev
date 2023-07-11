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

    private ImageView _depthImageView;
    private AllocatedImage _depthImage;
    private Format _depthFormat;
    
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

    private Pipeline _meshPipeline;
    private PipelineLayout _meshPipelineLayout;

    private Camera _mainCamera;
    
    private List<RenderObject> _renderables = new();
    private Dictionary<string, Material> _materials = new();
    private Dictionary<string, Mesh> _meshes = new();
    
    private int _frameNumber;
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
        
        _mainCamera = new Camera
        {
            Fov = 70,
            NearPlane = .1f,
            FarPlane = 200f,
            Position = new vec3(0, -6, -10),
            Extent = _windowExtent
        };
        
        InitVulkan();
        InitCommands();
        InitDefaultRenderPass();
        InitFrameBuffers();
        InitSyncStructures();
        InitPipelines();
        LoadMeshes();
        InitScene();
        _isInitialized = true;
    }

    private void CreateMaterial(Pipeline pipeline, PipelineLayout layout, string name) => _materials[name] = new Material {Pipeline = pipeline, PipelineLayout = layout};
    
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
        
        InitSwapchain(deviceInfo);
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

        var depthImageExtent = new Extent3D(_windowExtent.Width, _windowExtent.Height, 1);
        _depthFormat = Format.D32Sfloat;
        var dImgInfo =
            VkInit.ImageCreateInfo(_depthFormat, ImageUsageFlags.DepthStencilAttachmentBit, depthImageExtent);
        var dImgAllocInfo = new AllocationCreateInfo
        {
            Usage = MemoryUsage.GPU_Only,
            RequiredFlags = MemoryPropertyFlags.DeviceLocalBit
        };
        var image = _allocator.CreateImage(dImgInfo, dImgAllocInfo, out var allocation);
        _depthImage = new AllocatedImage {Image = image, Allocation = allocation};
        var dViewInfo = VkInit.ImageViewCreateInfo(_depthFormat, _depthImage.Image, ImageAspectFlags.DepthBit);
        _vk.CreateImageView(_device, dViewInfo, null, out _depthImageView);
        _mainDeletionQueue.Queue(() =>
        {
            _vk.DestroyImageView(_device, _depthImageView, null);
            _vk.DestroyImage(_device, _depthImage.Image, null);
            allocation.Dispose();
        });
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

        var depthAttachment = new AttachmentDescription
        {
            Format = _depthFormat,
            Flags = 0,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.Clear,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        var depthAttachmentRef = new AttachmentReference
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef
        };

        var attachments = new[]
        {
            colorAttachment,
            depthAttachment
        };
        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };
        var depthDependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            DstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit
        };
        var dependencies = new[] {dependency, depthDependency};
        fixed (AttachmentDescription* attachmentPtr = attachments)
        fixed (SubpassDependency* dependencyPtr = dependencies)
        {
            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentPtr,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = (uint)dependencies.Length,
                PDependencies = dependencyPtr
            };
            _vk.CreateRenderPass(_device, renderPassInfo, null, out _renderPass);
            _mainDeletionQueue.Queue(() => _vk.DestroyRenderPass(_device, _renderPass, null));
        }
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
            var attachments = new[] {_swapchainImageViews[i], _depthImageView};
            fixed(ImageView* attachmentPtr = attachments)
                fbInfo.PAttachments = attachmentPtr;
            fbInfo.AttachmentCount = (uint)attachments.Length;
            
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
        if (!LoaderShaderModule("tri_mesh.vert.spv", out var triVertShader))
            Console.WriteLine("Failed to load tri vert shader");
        if (!LoaderShaderModule("tri_mesh.frag.spv", out var triFragShader))
            Console.WriteLine("Failed to load frag shader");
        _mainDeletionQueue.Queue(() =>
        {
            _vk.DestroyShaderModule(_device, triVertShader, null);
            _vk.DestroyShaderModule(_device, triFragShader, null);
        });
        
        var pushConstant = new PushConstantRange(ShaderStageFlags.VertexBit, 0, (uint) Unsafe.SizeOf<MeshPushConstants>());
        var layoutInfo = VkInit.PipelineLayoutCreateInfo(1, &pushConstant);
        _vk.CreatePipelineLayout(_device, layoutInfo, null, out _meshPipelineLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyPipelineLayout(_device, _meshPipelineLayout, null));
        var vertexInputInfo = VkInit.VertexInputStateCreateInfo(Vertex.GetVertexDescription());
        var builder = new PipelineBuilder
        {
            ShaderStages = new[]
            {
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.VertexBit, triVertShader),
                VkInit.ShaderStageCreateInfo(ShaderStageFlags.FragmentBit, triFragShader)
            },
            VertexInputInfo = vertexInputInfo,
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
            PipelineLayout = _meshPipelineLayout,
            DepthStencil = VkInit.DepthStencilCreateInfo(true, true, CompareOp.LessOrEqual)
        };
        _meshPipeline = builder.Build(_device, _renderPass);
        CreateMaterial(_meshPipeline, _meshPipelineLayout, "defaultmesh");
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
        var triangleMesh = new Mesh {Vertices = vertices};
        var monkeyMesh = Mesh.LoadFromObj("./assets/monkey_smooth.obj");

        UploadMesh(monkeyMesh);
        UploadMesh(triangleMesh);

        _meshes["monkey"] = monkeyMesh;
        _meshes["triangle"] = triangleMesh;
    }
    
    private unsafe void UploadMesh(Mesh mesh)
    {
        CreateBuffer<Vertex>(mesh.Vertices, out var buffer, out var allocation);
        mesh.VertexBuffer = new AllocatedBuffer { Allocation = allocation, Buffer = buffer};
        _mainDeletionQueue.Queue(() => _vk.DestroyBuffer(_device, buffer, null));
        _mainDeletionQueue.Queue(() => allocation.Dispose());
    }
    
    private void InitScene()
    {
        var monkey = new RenderObject
            {Mesh = _meshes["monkey"], Material = _materials["defaultmesh"], TransformMatrix = mat4.Identity};
        _renderables.Add(monkey);

        for(var x = -20; x <= 20; x++)
        for (var y = -20; y <= 20; y++)
        {
            var translation = mat4.Translate(x, 0, y);
            var scale = mat4.Scale(.2f, .2f, .2f);
            var tri = new RenderObject
            {
                Mesh = _meshes["triangle"], Material = _materials["defaultmesh"], TransformMatrix = translation * scale
            };
            _renderables.Add(tri);
        }
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

        var depthClear = new ClearValue {DepthStencil = {Depth = 1}};
        var clearValues = new[] {clearValue, depthClear};
        fixed (ClearValue* clearValuePtr = clearValues)
        {
            var rpInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                PNext = null,
                RenderPass = _renderPass,
                RenderArea = {Offset = {X = 0, Y = 0}, Extent = _windowExtent},
                Framebuffer = _framebuffers[swapchainImageIndex],
                ClearValueCount = (uint) clearValues.Length,
                PClearValues = clearValuePtr
            };
            _vk.CmdBeginRenderPass(cmd, rpInfo, SubpassContents.Inline);
            DrawObjects(cmd);
            _vk.CmdEndRenderPass(cmd);
        }
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

    private unsafe void DrawObjects(CommandBuffer cmd)
    {
        Mesh lastMesh = default;
        Material lastMaterial = default;
        foreach (var obj in _renderables)
        {
            if (obj.Material != lastMaterial)
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, obj.Material.Pipeline);
                lastMaterial = obj.Material;
            }
            var modelMatrix = obj.TransformMatrix;
            var meshMatrix = _mainCamera.Projection * _mainCamera.View * modelMatrix;
            var constants = new MeshPushConstants {RenderMatrix = meshMatrix};
            _vk.CmdPushConstants(cmd, _meshPipelineLayout, ShaderStageFlags.VertexBit, 0,
                (uint) Unsafe.SizeOf<MeshPushConstants>(), &constants);

            if (obj.Mesh != lastMesh)
            {
                ulong offset = 0;
                var vertBuffer = obj.Mesh.VertexBuffer.Buffer;
                _vk.CmdBindVertexBuffers(cmd, 0, 1, &vertBuffer, &offset);
                lastMesh = obj.Mesh;
            }
            _vk.CmdDraw(cmd, (uint)lastMesh.Vertices.Length, 1, 0, 0);
        }
    }

    public void Terminate()
    {
        if (!_isInitialized) return;
        _mainDeletionQueue.Flush();
    }
}
