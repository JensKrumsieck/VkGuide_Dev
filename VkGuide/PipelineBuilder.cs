using Silk.NET.Vulkan;

namespace VkGuide;

public class PipelineBuilder
{
    private PipelineShaderStageCreateInfo[] _shaderStages;
    private PipelineVertexInputStateCreateInfo _vertexInputInfo;
    private PipelineInputAssemblyStateCreateInfo _inputAssembly;
    private Viewport _viewport;
    private Rect2D _scissor;
    private PipelineRasterizationStateCreateInfo _rasterizer;
    private PipelineColorBlendAttachmentState _colorBlendAttachment;
    private PipelineLayout _pipelineLayout;
    
    public Pipeline Build(Device device, RenderPass pass){}

}
