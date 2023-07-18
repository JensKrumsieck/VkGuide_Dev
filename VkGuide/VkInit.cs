using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkGuide.Types;
using Buffer = Silk.NET.Vulkan.Buffer;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VkGuide;

public static class VkInit
{
    public static CommandPoolCreateInfo CommandPoolCreateInfo(uint queueFamilyIndex,
                                                              CommandPoolCreateFlags flags = CommandPoolCreateFlags
                                                                  .ResetCommandBufferBit)
    {
        var commandPoolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            PNext = null,
            QueueFamilyIndex = queueFamilyIndex,
            Flags = flags
        };
        return commandPoolInfo;
    }

    public static CommandBufferAllocateInfo CommandBufferAllocateInfo(CommandPool pool, uint count = 1,
                                                                      CommandBufferLevel level =
                                                                          CommandBufferLevel.Primary)
    {
        var cmdAllocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = pool,
            CommandBufferCount = count,
            Level = level
        };
        return cmdAllocInfo;
    }

    public static unsafe PipelineShaderStageCreateInfo ShaderStageCreateInfo(
        ShaderStageFlags stage, ShaderModule shaderModule)
    {
        var info = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            PNext = null,
            Stage = stage,
            Module = shaderModule,
            PName = (byte*) SilkMarshal.StringToPtr("main")
        };
        return info;
    }

    public static unsafe PipelineVertexInputStateCreateInfo VertexInputStateCreateInfo(
        VertexInputDescription? description = null)
    {

        var info = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            PNext = null,
            VertexAttributeDescriptionCount = 0,
            VertexBindingDescriptionCount = 0
        };
        if (description.HasValue)
        {
            fixed (VertexInputAttributeDescription* attributesPtr = description.Value.Attributes)
                info.PVertexAttributeDescriptions = attributesPtr;
            info.VertexAttributeDescriptionCount = (uint) description.Value.Attributes.Length;
            fixed (VertexInputBindingDescription* bindingsPtr = description.Value.Bindings)
                info.PVertexBindingDescriptions = bindingsPtr;
            info.VertexBindingDescriptionCount = (uint) description.Value.Bindings.Length;
        }

        return info;
    }

    public static PipelineInputAssemblyStateCreateInfo InputAssemblyCreateInfo(PrimitiveTopology topology)
    {
        var info = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            PNext = null,
            Topology = topology,
            PrimitiveRestartEnable = false
        };
        return info;
    }

    public static PipelineRasterizationStateCreateInfo RasterizationStateCreateInfo(PolygonMode polygonMode)
    {
        var info = new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PNext = null,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = polygonMode,
            LineWidth = 1,
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false,
            DepthBiasConstantFactor = 0,
            DepthBiasClamp = 0,
            DepthBiasSlopeFactor = 0
        };
        return info;
    }

    public static PipelineMultisampleStateCreateInfo MultisampleStateCreateInfo()
    {
        var info = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            PNext = null,

            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            MinSampleShading = 1f,
            PSampleMask = null,
            AlphaToOneEnable = false,
            AlphaToCoverageEnable = false
        };
        return info;
    }

    public static PipelineColorBlendAttachmentState ColorBlendAttachmentState()
    {
        var colorBlendAttachment = new PipelineColorBlendAttachmentState
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                             ColorComponentFlags.ABit,
            BlendEnable = false
        };
        return colorBlendAttachment;
    }

    public static unsafe PipelineLayoutCreateInfo PipelineLayoutCreateInfo(
        uint pushConstantRangeCount = 0, PushConstantRange* pushConstantRange = null)
    {
        var info = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PNext = null,
            Flags = 0,
            SetLayoutCount = 0,
            PSetLayouts = null,
            PushConstantRangeCount = pushConstantRangeCount,
            PPushConstantRanges = pushConstantRange
        };
        return info;
    }

    public static FenceCreateInfo FenceCreateInfo(FenceCreateFlags flags = 0)
    {
        var info = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            PNext = null,
            Flags = flags
        };
        return info;
    }

    public static SemaphoreCreateInfo SemaphoreCreateInfo(SemaphoreCreateFlags flags = 0)
    {
        var info = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
            PNext = null,
            Flags = flags
        };
        return info;
    }

    public static ImageCreateInfo ImageCreateInfo(Format format, ImageUsageFlags usageFlags, Extent3D extent)
    {
        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            PNext = null,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = extent,
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usageFlags
        };
        return info;
    }

    public static ImageViewCreateInfo ImageViewCreateInfo(Format format, Image image, ImageAspectFlags aspectFlags)
    {
        var info = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            PNext = null,
            ViewType = ImageViewType.Type2D,
            Image = image,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(aspectFlags, 0, 1, 0, 1)
        };
        return info;
    }

    public static PipelineDepthStencilStateCreateInfo DepthStencilCreateInfo(bool bDepthTest, bool bDepthWrite,
                                                                             CompareOp compareOp)
    {
        var info = new PipelineDepthStencilStateCreateInfo
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            PNext = null,
            DepthTestEnable = bDepthTest,
            DepthWriteEnable = bDepthWrite,
            DepthCompareOp = bDepthTest ? compareOp : CompareOp.Always,
            DepthBoundsTestEnable = false,
            MinDepthBounds = 0,
            MaxDepthBounds = 1,
            StencilTestEnable = false
        };
        return info;
    }

    public static DescriptorSetLayoutBinding DescriptorSetLayoutBinding(
        DescriptorType type, ShaderStageFlags stageFlags, uint binding)
    {
        var setBind = new DescriptorSetLayoutBinding
        {
            Binding = binding,
            DescriptorCount = 1,
            DescriptorType = type,
            PImmutableSamplers = null,
            StageFlags = stageFlags
        };
        return setBind;
    }

    public static unsafe WriteDescriptorSet WriteDescriptorBuffer(DescriptorType type, DescriptorSet dstSet,
                                                           DescriptorBufferInfo bufferInfo, uint binding)
    {
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            PNext = null,
            DstBinding = binding,
            DstSet = dstSet,
            DescriptorCount = 1,
            DescriptorType = type,
            PBufferInfo = &bufferInfo
        };
        return write;
    }

    public static unsafe DescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(DescriptorSetLayoutBinding[] bindings)
    {
        fixed (DescriptorSetLayoutBinding* bindingsPtr = bindings)
        {
            var info = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                PNext = null,
                BindingCount = (uint) bindings.Length,
                Flags = 0,
                PBindings = bindingsPtr
            };
            return info;
        }
    }

    public static unsafe DescriptorSetAllocateInfo DescriptorSetAllocateInfo(DescriptorPool pool,
                                                                      DescriptorSetLayout[] setLayouts)
    {
        fixed (DescriptorSetLayout* setLayoutsPtr = setLayouts)
        {
            var info = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                PNext = null,
                DescriptorPool = pool,
                DescriptorSetCount = (uint)setLayouts.Length,
                PSetLayouts = setLayoutsPtr
            };
            return info;
        }
    }

    public static DescriptorBufferInfo DescriptorBufferInfo(Buffer buffer, uint range, uint offset = 0)
    {
        var info = new DescriptorBufferInfo
        {
            Buffer = buffer,
            Offset = offset,
            Range = range
        };
        return info;
    }

    public static CommandBufferBeginInfo CommandBufferBeginInfo(CommandBufferUsageFlags flags = 0)
    {
        var info = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            PNext = null,
            PInheritanceInfo = null,
            Flags = flags
        };
        return info;
    }

    public static unsafe SubmitInfo SubmitInfo(CommandBuffer* commandBuffer, Semaphore[]? waitSemaphores = null, Semaphore[]? signalSemaphores = null, PipelineStageFlags* waitStage = null)
    {
        fixed (Semaphore* signalSemaphorePtr = signalSemaphores)
        fixed (Semaphore* waitSemaphorePtr = waitSemaphores)
        {
            var info = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                PNext = null,
                PWaitDstStageMask = waitStage,
                WaitSemaphoreCount = (uint)(waitSemaphores?.Length??0),
                PWaitSemaphores = waitSemaphorePtr,
                SignalSemaphoreCount = (uint)(signalSemaphores?.Length??0),
                PSignalSemaphores = signalSemaphorePtr,
                CommandBufferCount = 1,
                PCommandBuffers = commandBuffer
            };
            return info;
        }
    }

    public static SamplerCreateInfo SamplerCreateInfo(Filter filters, SamplerAddressMode samplerAddressMode = SamplerAddressMode.Repeat)
    {
        var info = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            PNext = null,
            MagFilter = filters,
            MinFilter = filters,
            AddressModeU = samplerAddressMode,
            AddressModeV = samplerAddressMode,
            AddressModeW = samplerAddressMode
        };
        return info;
    }

    public static unsafe WriteDescriptorSet WriteDescriptorImage(DescriptorType type, DescriptorSet dstSet,
        DescriptorImageInfo imageInfo, uint binding)
    {
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            PNext = null,
            DstBinding = binding,
            DstSet = dstSet,
            DescriptorCount = 1,
            DescriptorType = type,
            PImageInfo = &imageInfo
        };
        return write;
    }
}