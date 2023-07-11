using Silk.NET.Vulkan;

namespace VkGuide;

public class PipelineBuilder
{
    public PipelineShaderStageCreateInfo[] ShaderStages;
    public PipelineVertexInputStateCreateInfo VertexInputInfo;
    public PipelineInputAssemblyStateCreateInfo InputAssembly;
    public Viewport Viewport;
    public Rect2D Scissor;
    public PipelineRasterizationStateCreateInfo Rasterizer;
    public PipelineColorBlendAttachmentState ColorBlendAttachment;
    public PipelineMultisampleStateCreateInfo Multisampling;
    public PipelineLayout PipelineLayout;
    public PipelineDepthStencilStateCreateInfo DepthStencil;
    
    public unsafe Pipeline Build(Device device, RenderPass pass)
    {
        var viewport = Viewport;
        var scissor = Scissor;
        var colorBlendAttachment = ColorBlendAttachment;
        var vertexInputInfo = VertexInputInfo;
        var inputAssembly = InputAssembly;
        var rasterizer = Rasterizer;
        var multisampling = Multisampling;
        var depthStencil = DepthStencil;
        
        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor
        };
        var colorBlending = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            PNext = null,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };
        fixed (PipelineShaderStageCreateInfo* shaderStagesPtr = ShaderStages)
        {
            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = null,
                StageCount = (uint) ShaderStages.Length,
                PStages = shaderStagesPtr,
                PInputAssemblyState = &inputAssembly,
                PVertexInputState = &vertexInputInfo,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                Layout = PipelineLayout,
                RenderPass = pass,
                BasePipelineHandle = default,
                PDepthStencilState = &depthStencil
            };
            if (Vulkanize.Vulkanize.Vk.CreateGraphicsPipelines(device, default, 1, pipelineInfo, null,
                    out var pipeline) !=
                Result.Success)
                Console.WriteLine("Failed to create Pipeline");

            return pipeline;
        }
    }

}
