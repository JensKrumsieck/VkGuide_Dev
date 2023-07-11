using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;

namespace VkGuide;

public static class VkInit
{
    public static CommandPoolCreateInfo CommandPoolCreateInfo(uint queueFamilyIndex, CommandPoolCreateFlags flags = CommandPoolCreateFlags.ResetCommandBufferBit)
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

    public static CommandBufferAllocateInfo CommandBufferAllocateInfo(CommandPool pool, uint count = 1, CommandBufferLevel level = CommandBufferLevel.Primary)
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

    public static unsafe PipelineShaderStageCreateInfo ShaderStageCreateInfo(ShaderStageFlags stage, ShaderModule shaderModule)
    {
        var info = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            PNext = null,
            Stage = stage,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        return info;
    }

    public static PipelineVertexInputStateCreateInfo VertexInputStateCreateInfo()
    {
        var info = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            PNext = null,
            VertexAttributeDescriptionCount = 0,
            VertexBindingDescriptionCount = 0
        };
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

    public static PipelineLayoutCreateInfo PipelineLayoutCreateInfo()
    {
        var info = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PNext = null,
            Flags = 0,
            SetLayoutCount = 0,
            PSetLayouts = null,
            PushConstantRangeCount = 0,
            PPushConstantRanges = null
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
}
