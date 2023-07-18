using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    internal readonly Vk _vk = Vulkanize.Vulkanize.Vk;
    internal VulkanMemoryAllocator _allocator;
    
    private IWindow _window;
    private Instance _instance;
    private DebugUtilsMessengerEXT _debugMessenger;
    
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private PhysicalDeviceProperties _gpuProperties;
    internal Device _device;

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
    private DescriptorSetLayout _objectSetLayout;
    private DescriptorPool _descriptorPool;
    
    private List<RenderObject> _renderables = new();
    private Dictionary<string, Material> _materials = new();
    private Dictionary<string, Mesh> _meshes = new();
    private Dictionary<string, Texture> _textures = new();

    private IInputContext _inputCtx;
    private IKeyboard _keyboard;

    private const int FrameOverlap = 2;
    private int _frameNumber;
    private int CurrentFrame => _frameNumber % FrameOverlap;
    private FrameData[] _frames = new FrameData[FrameOverlap];

    private GpuSceneData _sceneParameters;
    private AllocatedBuffer _sceneParameterBuffer;

    private UploadContext _uploadContext = new();

    internal DeletionQueue _mainDeletionQueue = new();
    
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
            .SetPreferredDeviceType(PreferredDeviceType.HighPerformance)
            .Select();
        _physicalDevice = physicalDeviceInfo.PhysicalDevice;
        _gpuProperties = physicalDeviceInfo.PhysicalDeviceProperties;
        fixed(byte* deviceNamePtr = _gpuProperties.DeviceName)
            Console.WriteLine($"Selected GPU: {Marshal.PtrToStringAnsi((nint)deviceNamePtr)}");
        Console.WriteLine($"The GPU has a minimum buffer alignment of {_gpuProperties.Limits.MinUniformBufferOffsetAlignment}");

        var shaderDrawParams = new PhysicalDeviceShaderDrawParameterFeatures
        {
            SType = StructureType.PhysicalDeviceShaderDrawParameterFeatures,
            PNext = null,
            ShaderDrawParameters = true
        };
        
        var deviceInfo = new DeviceBuilder(physicalDeviceInfo)
            .EnableValidationLayers()
            .AddPNext(&shaderDrawParams)
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

        var uploadPoolInfo = VkInit.CommandPoolCreateInfo(_graphicsQueueFamily);
        _vk.CreateCommandPool(_device, uploadPoolInfo, null, out _uploadContext.CommandPool);
        _mainDeletionQueue.Queue(() => _vk.DestroyCommandPool(_device, _uploadContext.CommandPool, null));
        var uploadCmdAllocInfo = VkInit.CommandBufferAllocateInfo(_uploadContext.CommandPool);
        _vk.AllocateCommandBuffers(_device, uploadCmdAllocInfo, out _uploadContext.CommandBuffer);
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
        var uploadFenceInfo = VkInit.FenceCreateInfo();
        _vk.CreateFence(_device, uploadFenceInfo, null, out _uploadContext.UploadFence);
        _mainDeletionQueue.Queue(() => _vk.DestroyFence(_device, _uploadContext.UploadFence, null));
        
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
            new(DescriptorType.UniformBuffer, 10),
            new(DescriptorType.UniformBufferDynamic, 10),
            new(DescriptorType.StorageBuffer, 10)
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

        var sceneParamBufferSize = FrameOverlap * PadUniformBufferSize(Unsafe.SizeOf<GpuSceneData>());
        _sceneParameterBuffer = CreateBuffer((uint)sceneParamBufferSize, BufferUsageFlags.UniformBufferBit, MemoryUsage.CPU_To_GPU);
        _mainDeletionQueue.Queue(() =>
        {
            _vk.DestroyBuffer(_device, _sceneParameterBuffer.Buffer, null);
            _sceneParameterBuffer.Allocation.Dispose();
        });
        var camBufferBinding = VkInit.DescriptorSetLayoutBinding(DescriptorType.UniformBuffer, ShaderStageFlags.VertexBit, 0);
        var sceneBufferBinding = VkInit.DescriptorSetLayoutBinding(DescriptorType.UniformBufferDynamic, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 1);
        var bindings = new [] {camBufferBinding, sceneBufferBinding};
        var setInfo = VkInit.DescriptorSetLayoutCreateInfo(bindings);
        _vk.CreateDescriptorSetLayout(_device, setInfo, null, out _globalSetLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyDescriptorSetLayout(_device, _globalSetLayout, null));
        
        
        var objectBufferBinding = VkInit.DescriptorSetLayoutBinding(DescriptorType.StorageBuffer, ShaderStageFlags.VertexBit, 0);
        var setObjInfo = VkInit.DescriptorSetLayoutCreateInfo(new[]{objectBufferBinding});
        _vk.CreateDescriptorSetLayout(_device, setObjInfo, null, out _objectSetLayout);
        _mainDeletionQueue.Queue(() => _vk.DestroyDescriptorSetLayout(_device, _objectSetLayout, null));
        for (var i = 0; i < FrameOverlap; i++)
        {
            const int maxObjects = 10000;
            var objBuffer = CreateBuffer((uint) Unsafe.SizeOf<GpuObjectData>() * maxObjects,
                                         BufferUsageFlags.StorageBufferBit, MemoryUsage.CPU_To_GPU);
            _frames[i].ObjectBuffer = objBuffer;
            
            var buffer = CreateBuffer((uint) Unsafe.SizeOf<CameraData>(),
                                      BufferUsageFlags.UniformBufferBit,
                                      MemoryUsage.CPU_To_GPU);
            _frames[i].CameraBuffer = buffer;

            var globalAllocInfo = VkInit.DescriptorSetAllocateInfo(_descriptorPool, new[]{_globalSetLayout});
            _vk.AllocateDescriptorSets(_device, globalAllocInfo, out _frames[i].GlobalDescriptor);

            var objectAllocInfo = VkInit.DescriptorSetAllocateInfo(_descriptorPool, new[] {_objectSetLayout});
            _vk.AllocateDescriptorSets(_device, objectAllocInfo, out _frames[i].ObjectDescriptor);

            var cInfo = VkInit.DescriptorBufferInfo(_frames[i].CameraBuffer.Buffer, (uint) Unsafe.SizeOf<CameraData>());
            var sInfo = VkInit.DescriptorBufferInfo(_sceneParameterBuffer.Buffer, (uint) Unsafe.SizeOf<GpuSceneData>());
            var oInfo = VkInit.DescriptorBufferInfo(_frames[i].ObjectBuffer.Buffer, (uint) Unsafe.SizeOf<GpuObjectData>() * maxObjects);
            var camWrite = VkInit.WriteDescriptorBuffer(DescriptorType.UniformBuffer, _frames[i].GlobalDescriptor, cInfo, 0);
            var sceneWrite = VkInit.WriteDescriptorBuffer(DescriptorType.UniformBufferDynamic, _frames[i].GlobalDescriptor, sInfo, 1);
            var objWrite = VkInit.WriteDescriptorBuffer(DescriptorType.StorageBuffer, _frames[i].ObjectDescriptor, oInfo, 0);
            var setWrites = new[] {camWrite, sceneWrite, objWrite};
            fixed(WriteDescriptorSet* setPtr = setWrites)
                _vk.UpdateDescriptorSets(_device, (uint)setWrites.Length, setPtr, 0, null);

            _mainDeletionQueue.Queue(() =>
            {
                _vk.DestroyBuffer(_device, buffer.Buffer, null);
                buffer.Allocation.Dispose();
                _vk.DestroyBuffer(_device, objBuffer.Buffer, null);
                objBuffer.Allocation.Dispose();
            });
        }
        _mainDeletionQueue.Queue(() => _vk.DestroyDescriptorPool(_device, _descriptorPool, null));
    }

    private unsafe void InitPipelines()
    {
        if (!LoaderShaderModule("tri_mesh.vert.spv", out var triVertShader))
            Console.WriteLine("Failed to load tri vert shader");
        if (!LoaderShaderModule("default_lit.frag.spv", out var triFragShader))
            Console.WriteLine("Failed to load frag shader");
        _mainDeletionQueue.Queue(() =>
        {
            _vk.DestroyShaderModule(_device, triVertShader, null);
            _vk.DestroyShaderModule(_device, triFragShader, null);
        });
        
        var pushConstant = new PushConstantRange(ShaderStageFlags.VertexBit, 0, (uint) Unsafe.SizeOf<MeshPushConstants>());
        var setLayouts = new[]{_globalSetLayout, _objectSetLayout};
        var layoutInfo = VkInit.PipelineLayoutCreateInfo(1, &pushConstant);
        layoutInfo.SetLayoutCount = (uint)setLayouts.Length;
        fixed (DescriptorSetLayout* setLayoutsPtr = setLayouts)
            layoutInfo.PSetLayouts = setLayoutsPtr;
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
        LoadImages();
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
        var bufferSize = (uint)(mesh.Vertices.Length * Unsafe.SizeOf<Vertex>());
        CreateBuffer<Vertex>(mesh.Vertices, BufferUsageFlags.TransferSrcBit, MemoryUsage.CPU_Only, out var stagingBuffer);
        var vertexBuffer = CreateBuffer(bufferSize, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, MemoryUsage.GPU_Only);
        ImmediateSubmit((cmd) =>
        {
            var copy = new BufferCopy(0, 0, bufferSize);
            _vk.CmdCopyBuffer(cmd, stagingBuffer.Buffer, vertexBuffer.Buffer, 1, copy);
        });
        mesh.VertexBuffer = vertexBuffer;

        _mainDeletionQueue.Queue(() =>
        {
            mesh.VertexBuffer.Allocation.Dispose();
            _vk.DestroyBuffer(_device, mesh.VertexBuffer.Buffer, null);
            stagingBuffer.Allocation.Dispose();
            _vk.DestroyBuffer(_device, stagingBuffer.Buffer, null);
        });
    }

    internal unsafe void ImmediateSubmit(Action<CommandBuffer> function)
    {
        var cmd = _uploadContext.CommandBuffer;
        var beginInfo = VkInit.CommandBufferBeginInfo(CommandBufferUsageFlags.OneTimeSubmitBit);
        _vk.BeginCommandBuffer(cmd, beginInfo);
        function(cmd);
        _vk.EndCommandBuffer(cmd);
        var submitInfo = VkInit.SubmitInfo(&cmd);
        _vk.QueueSubmit(_graphicsQueue, 1, submitInfo, _uploadContext.UploadFence);
        _vk.WaitForFences(_device, 1, _uploadContext.UploadFence, true, 999999999);
        _vk.ResetFences(_device, 1, _uploadContext.UploadFence);
        _vk.ResetCommandPool(_device, _uploadContext.CommandPool, 0);
    }

    private unsafe void LoadImages()
    {
        if(!VkUtil.LoadImageFromFile(this, "./assets/lost_empire-RGBA.png", out var lostEmpireImage))
            return;
        var texture = new Texture {Image = lostEmpireImage};
        var imageInfo = VkInit.ImageViewCreateInfo(Format.R8G8B8A8Srgb, lostEmpireImage.Image, ImageAspectFlags.ColorBit);
        _vk.CreateImageView(_device, imageInfo, null, out texture.ImageView);
        _mainDeletionQueue.Queue(() => _vk.DestroyImageView(_device, texture.ImageView, null));
        _textures["empire_diffuse"] = texture;
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

    internal AllocatedBuffer CreateBuffer(uint size, BufferUsageFlags usage, MemoryUsage memoryUsage)
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
            Usage = memoryUsage
        };
        var buffer = _allocator.CreateBuffer(bufferInfo, allocInfo, out var allocation);
        return new AllocatedBuffer {Buffer = buffer, Allocation = allocation};
    }

    private void CreateBuffer<T>(ReadOnlySpan<T> span, BufferUsageFlags usage, MemoryUsage memoryUsage, out AllocatedBuffer allocatedBuffer)
        where T : unmanaged
    {
        allocatedBuffer = CreateBuffer((uint) span.Length * (uint) Unsafe.SizeOf<T>(), usage,
                     memoryUsage);

        if (!allocatedBuffer.Allocation.TryGetSpan(out Span<T> bufferSpan))
            throw new InvalidOperationException("Unable to get Span<T> to mapped allocation.");
        span.CopyTo(bufferSpan);
    }

    private ulong PadUniformBufferSize(int originalSize)
    {
        var minUboAlignment = _gpuProperties.Limits.MinUniformBufferOffsetAlignment;
        var alignedSize = (ulong)originalSize;
        if (minUboAlignment > 0)
            alignedSize = (alignedSize + minUboAlignment - 1) & ~(minUboAlignment - 1);
        return alignedSize;
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
        var cmdBeginInfo = VkInit.CommandBufferBeginInfo(CommandBufferUsageFlags.OneTimeSubmitBit);
        _vk.BeginCommandBuffer(cmd, cmdBeginInfo);
        var clearValue = new ClearValue();
        var flashB = MathF.Abs(MathF.Sin(_frameNumber / 120f));
        clearValue.Color = new ClearColorValue(0, 0, flashB, 0);

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
        var submit = VkInit.SubmitInfo(&cmd, new[] {waitSemaphore}, new[] {signalSemaphore}, &waitStage);
       
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
        var framed = _frameNumber / 120f;
        _sceneParameters.AmbientColor = new vec4(MathF.Sin(framed), 0, MathF.Cos(framed), 1);
        _sceneParameterBuffer.Allocation.Map();
        var sceneData = (char*)_sceneParameterBuffer.Allocation.MappedData;
        sceneData += PadUniformBufferSize(Unsafe.SizeOf<GpuSceneData>()) * (ulong)CurrentFrame;
        var scenePar = _sceneParameters;
        Unsafe.CopyBlock(sceneData, &scenePar, (uint)Unsafe.SizeOf<GpuSceneData>());
        _sceneParameterBuffer.Allocation.Unmap();

        _frames[CurrentFrame].CameraBuffer.Allocation.Map();
        var data = _mainCamera.CameraData;
        var camPtr = (CameraData*)_frames[CurrentFrame].CameraBuffer.Allocation.MappedData;
        Unsafe.CopyBlock(camPtr, &data, (uint)Unsafe.SizeOf<CameraData>());
        _frames[CurrentFrame].CameraBuffer.Allocation.Unmap();

        _frames[CurrentFrame].ObjectBuffer.Allocation.Map();
        var objectPtr = (GpuObjectData*)_frames[CurrentFrame].ObjectBuffer.Allocation.MappedData;
        for (var i = 0; i < _renderables.Count; i++)
        {
            var renderObject = _renderables[i];
            objectPtr[i].ModelMatrix = renderObject.TransformMatrix;
        }
        _frames[CurrentFrame].ObjectBuffer.Allocation.Unmap();

        Mesh lastMesh = default;
        Material lastMaterial = default;
        for (var i = 0; i < _renderables.Count; i++)
        {
            var renderObject = _renderables[i];
            if (!Equals(renderObject.Material, lastMaterial))
            {
                _vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, renderObject.Material.Pipeline);
                lastMaterial = renderObject.Material;
                var uniformOffsets = (uint) (PadUniformBufferSize(Unsafe.SizeOf<GpuSceneData>()) * (ulong) CurrentFrame);
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, renderObject.Material.PipelineLayout, 0, 1,
                                          _frames[CurrentFrame].GlobalDescriptor, 1, &uniformOffsets);
                _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, renderObject.Material.PipelineLayout, 1, 1,
                                          _frames[CurrentFrame].ObjectDescriptor, 0, null);
            }

            var constants = new MeshPushConstants {RenderMatrix = renderObject.TransformMatrix};
            _vk.CmdPushConstants(cmd, lastMaterial.PipelineLayout, ShaderStageFlags.VertexBit, 0,
                                 (uint) Unsafe.SizeOf<MeshPushConstants>(), &constants);

            if (renderObject.Mesh != lastMesh)
            {
                ulong offset = 0;
                var vertBuffer = renderObject.Mesh.VertexBuffer.Buffer;
                _vk.CmdBindVertexBuffers(cmd, 0, 1, &vertBuffer, &offset);
                lastMesh = renderObject.Mesh;
            }
            _vk.CmdDraw(cmd, (uint) lastMesh.Vertices.Length, 1, 0, (uint)i);
        }
    }

    public void Terminate()
    {
        if (!_isInitialized) return;
        _mainDeletionQueue.Flush();
    }
}
