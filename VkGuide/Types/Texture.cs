using Silk.NET.Vulkan;

namespace VkGuide.Types;

public struct Texture
{
    public AllocatedImage Image;
    public ImageView ImageView;
}
