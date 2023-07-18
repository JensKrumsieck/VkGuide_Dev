using Silk.NET.Vulkan;

namespace VkGuide.Types;

public class Material
{
    public DescriptorSet TextureSet;
    public Pipeline Pipeline;
    public PipelineLayout PipelineLayout;
}