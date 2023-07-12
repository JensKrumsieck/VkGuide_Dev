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
using Framebuffer = Silk.NET.Vulkan.Framebuffer;
using Image = Silk.NET.Vulkan.Image;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;

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

    private RenderPass _renderPass;
    private Framebuffer[] _framebuffers;

    private Pipeline _meshPipeline;
    private PipelineLayout _meshPipelineLayout;

    private Camera _mainCamera;
    private DescriptorSetLayout _globalSetLayout;
    private DescriptorPool _descriptorPool;
    
    private List<RenderObject> _renderables = new();
    private Dictionary<string, Material> _materials = new();
    private Dictionary<string, Mesh> _meshes = new();

    private IInputContext _inputCtx;
    private IKeyboard _keyboard;
    
    private int _frameNumber;
    private bool _isInitialized;

    private const int FrameOverlap = 2;
    private int CurrentFrame => _frameNumber % FrameOverlap;
    private FrameData[] _frames = new FrameData[FrameOverlap];

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
        _inputCtx = _window.CreateInput();
        _keyboard = _inputCtx.Keyboards[0];
        _mainDeletionQueue.Queue(() =>
        {
            _inputCtx.Dispose();
            _window.Dispose();
        });

        InitVulkan();
        InitCommands();
        InitDefaultRenderPass();
        InitFrameBuffers();
        InitSyncStructures();
        InitDescriptors();
        InitPipelines();
        LoadContent();
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
        for (var i = 0; i < FrameOverlap; i++)
        {
            _vk.CreateCommandPool(_device, commandPoolInfo, null, out var commandPool);
            var cmdAllocInfo = VkInit.CommandBufferAllocateInfo(commandPool);
            _vk.AllocateCommandBuffers(_device, cmdAllocInfo, out var mainCommandBuffer);
            _frames[i] = new FrameData
            {
                CommandPool = commandPool,
                MainCommandBuffer = mainCommandBuffer
            };
            _mainDeletionQueue.Queue(() => _vk.DestroyCommandPool(_device, commandPool, null));
        }

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
        var semaphoreInfo = VkInit.SemaphoreCreateInfo();

        for (var i = 0; i < FrameOverlap; i++)
        {
            _vk.CreateFence(_device, fenceInfo, null, out var renderFence);
            _vk.CreateSemaphore(_device, semaphoreInfo, null, out var presentSemaphore);
            _vk.CreateSemaphore(_device, semaphoreInfo, null, out var renderSemaphore);
        
            _frames[i].PresentSemaphore = presentSemaphore;
            _frames[i].RenderSemaphore = renderSemaphore;
            _frames[i].RenderFence = renderFence;
            
            _mainDeletionQueue.Queue(() =>
            {
                _vk.DestroyFence(_device, renderFence, null);
                _vk.DestroySemaphore(_device, presentSemaphore, null);
                _vk.DestroySemaphore(_device, renderSemaphore, null);
            });
        }
    }
    
    private unsafe void InitDescriptors()
    {
        var sizes = new DescriptorPoolSize[]
        {
            new(DescriptorType.UniformBuffer, 10)
        };
        fixed(DescriptorPoolSize* sizesPtr = sizes)
        {
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = 0,
                MaxSets = 10,
                PoolSizeCount = (uint)sizes.Length,
                PPoolSizes = sizesPtr
            };
            _vk.CreateDescriptorPool(_device, poolInfo, null, out _descriptorPool);
        }

        var camBufferBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            StageFlags = ShaderStageFlags.VertexBit
        };
        var setInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            PNext = null,
            BindingCount = 1,
            Flags = 0,
            PBindings = &camBufferBinding
        };
        _vk.CreateDescriptorSetLayout(_device, setInfo, null, out _globalSetLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyDescriptorSetLayout(_device, _globalSetLayout, null));
        
        for (var i = 0; i < FrameOverlap; i++)
        {
            var buffer = CreateBuffer((uint) Unsafe.SizeOf<CameraData>(),
                                      BufferUsageFlags.UniformBufferBit,
                                      MemoryUsage.CPU_To_GPU);
            _frames[i].CameraBuffer = buffer;

            var setLayouts = _globalSetLayout;
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                PNext = null,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &setLayouts
            };
            _vk.AllocateDescriptorSets(_device, allocInfo, out _frames[i].GlobalDescriptor);

            var bInfo = new DescriptorBufferInfo
            {
                Buffer = _frames[i].CameraBuffer.Buffer,
                Offset = 0,
                Range = (uint)Unsafe.SizeOf<CameraData>()
            };
            var setWrite = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                PNext = null,
                DstBinding = 0,
                DstSet = _frames[i].GlobalDescriptor,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &bInfo
            };
            _vk.UpdateDescriptorSets(_device, 1, setWrite, 0, null);

            _mainDeletionQueue.Queue(() =>
            {
                _vk.DestroyBuffer(_device, buffer.Buffer, null);
                buffer.Allocation.Dispose();
            });
        }
        _mainDeletionQueue.Queue(() => _vk.DestroyDescriptorPool(_device, _descriptorPool, null));
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
        var setLayout = _globalSetLayout;
        var layoutInfo = VkInit.PipelineLayoutCreateInfo(1, &pushConstant);
        layoutInfo.SetLayoutCount = 1;
        layoutInfo.PSetLayouts = &setLayout;
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

    private void LoadContent()
    {
        _mainCamera = new Camera
        {
            Fov = 70,
            NearPlane = .1f,
            FarPlane = 200f,
            Position = new vec3(0, -6, -10),
            Extent = _windowExtent
        };
        
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
        CreateBuffer<Vertex>(mesh.Vertices, out var allocatedBuffer);
        mesh.VertexBuffer = allocatedBuffer;
        _mainDeletionQueue.Queue(() => _vk.DestroyBuffer(_device, allocatedBuffer.Buffer, null));
        _mainDeletionQueue.Queue(() => allocatedBuffer.Allocation.Dispose());
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

    private AllocatedBuffer CreateBuffer(uint size, BufferUsageFlags usage, MemoryUsage memoryUsage)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            PNext = null,
            Size = size,
            Usage = usage
        };
        var allocInfo = new AllocationCreateInfo
        {
            Flags = AllocationCreateFlags.Mapped,
            Usage = MemoryUsage.CPU_To_GPU
        };
        var buffer = _allocator.CreateBuffer(bufferInfo, allocInfo, out var allocation);
        return new AllocatedBuffer {Buffer = buffer, Allocation = allocation};
    }
    
    private void CreateBuffer<T>(ReadOnlySpan<T> span, out AllocatedBuffer allocatedBuffer)
        where T : unmanaged
    {
        allocatedBuffer = CreateBuffer((uint) span.Length * (uint) Unsafe.SizeOf<T>(), BufferUsageFlags.VertexBufferBit,
                     MemoryUsage.CPU_To_GPU);

        if (!allocatedBuffer.Allocation.TryGetSpan(out Span<T> bufferSpan))
            throw new InvalidOperationException("Unable to get Span<T> to mapped allocation.");
        span.CopyTo(bufferSpan);
    }
    
    public void Run()
    {
        _window.Render += Draw;
        _window.Update += Update;
        _window.Run();
        _vk.DeviceWaitIdle(_device);
    }

    private void Update(double deltaTime)
    {
        var upKeys = _keyboard.IsKeyPressed(Key.W) || _keyboard.IsKeyPressed(Key.Up) ? 1 : 0;
        var leftKeys = _keyboard.IsKeyPressed(Key.A) || _keyboard.IsKeyPressed(Key.Left) ? 1 : 0;
        var downKeys = _keyboard.IsKeyPressed(Key.S) || _keyboard.IsKeyPressed(Key.Down) ? 1 : 0;
        var rightKeys = _keyboard.IsKeyPressed(Key.D) || _keyboard.IsKeyPressed(Key.Right) ? 1 : 0;
        var inputVector = new vec2(leftKeys - rightKeys, upKeys - downKeys).NormalizedSafe;
        const float movementSpeed = 10f;
        inputVector *= movementSpeed * (float)deltaTime;
        _mainCamera.Position += new vec3(inputVector.x, 0, inputVector.y);
    }

    private unsafe void Draw(double deltaTime)
    {
        _vk.WaitForFences(_device, 1, _frames[CurrentFrame].RenderFence, true, 1000000000);
        _vk.ResetFences(_device, 1, _frames[CurrentFrame].RenderFence);
        var swapchainImageIndex = 0U;
        _khrSwapchain.AcquireNextImage(_device, _swapchain, 1000000000, _frames[CurrentFrame].PresentSemaphore, default,
            ref swapchainImageIndex);
        _vk.ResetCommandBuffer(_frames[CurrentFrame].MainCommandBuffer, 0);
        var cmd = _frames[CurrentFrame].MainCommandBuffer;
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
        _vk.EndCommandBuffer(_frames[CurrentFrame].MainCommandBuffer);
        
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var waitSemaphore = _frames[CurrentFrame].PresentSemaphore;
        var signalSemaphore = _frames[CurrentFrame].RenderSemaphore;
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
        _vk.QueueSubmit(_graphicsQueue, 1, submit, _frames[CurrentFrame].RenderFence);

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
        _frames[CurrentFrame].CameraBuffer.Allocation.Map();
        var data = _mainCamera.CameraData;
        var camPtr = (CameraData*)_frames[CurrentFrame].CameraBuffer.Allocation.MappedData;
        camPtr[0] = data;
        _frames[CurrentFrame].CameraBuffer.Allocation.Unmap();
        
        Mesh lastMesh = default;
        Material lastMaterial = default;
        foreach (var obj in _renderables)
        {
            if (!ReferenceEquals(obj.Material, lastMaterial))
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, obj.Material.Pipeline);
                lastMaterial = obj.Material;
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, obj.Material.PipelineLayout, 0, 1, _frames[CurrentFrame].GlobalDescriptor, 0 ,null);
            }
            var constants = new MeshPushConstants {RenderMatrix = obj.TransformMatrix};
            _vk.CmdPushConstants(cmd, lastMaterial.PipelineLayout, ShaderStageFlags.VertexBit, 0,
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
