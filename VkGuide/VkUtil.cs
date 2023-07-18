using System.Runtime.CompilerServices;
using VkGuide.Types;
using Silk.NET.Vulkan;
using SkiaSharp;
using VMASharp;
using File = System.IO.File;

namespace VkGuide;

public static class VkUtil
{
    public static unsafe bool LoadImageFromFile(Engine engine, string filename, out AllocatedImage outImage)
    {
        using var fs = File.Open(filename, FileMode.Open);
        using var codec = SKCodec.Create(fs);
        var iInfo = new SKImageInfo(codec.Info.Width, codec.Info.Width, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var img = SKBitmap.Decode(codec, iInfo);
        if(img is null) {
            Console.WriteLine("Failed to load texture");
            outImage = new AllocatedImage();
            return false;
        }

        var pixels = img.GetPixels();
        var texWidth = (uint)img.Width;
        var texHeight = (uint)img.Height;
        var imageSize = texWidth * texWidth * 4; 
        const Format imageFormat = Format.R8G8B8A8Srgb;
        var stagingBuffer = engine.CreateBuffer(imageSize, BufferUsageFlags.TransferSrcBit, MemoryUsage.CPU_Only);

        stagingBuffer.Allocation.Map();
        var data = (byte*)stagingBuffer.Allocation.MappedData;
        Unsafe.CopyBlock(data, (byte*)pixels, imageSize);
        stagingBuffer.Allocation.Unmap();

        var imageExtent = new Extent3D(texWidth, texHeight, 1);
        var dImgInfo = VkInit.ImageCreateInfo(imageFormat, ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            imageExtent);
        var dImgAllocInfo = new AllocationCreateInfo {Usage = MemoryUsage.GPU_Only};
        
        AllocatedImage image = new();
        engine.ImmediateSubmit((cmd) =>
        {
            image.Image = engine._allocator.CreateImage(dImgInfo, dImgAllocInfo, out image.Allocation);
            var range = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            };
            var imageBarrierToTransfer = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                Image = image.Image,
                SubresourceRange = range,
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.TransferWriteBit
            };
            engine._vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0,
                null, 0, null, 1, imageBarrierToTransfer);
            var copyRegion = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                ImageExtent = imageExtent
            };
            engine._vk.CmdCopyBufferToImage(cmd, stagingBuffer.Buffer, image.Image, ImageLayout.TransferDstOptimal, 1,
                copyRegion);
            var imageBarrierToReadable = imageBarrierToTransfer with
            {
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };
            engine._vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
                0, null, 0, null, 1, imageBarrierToReadable);
            engine._mainDeletionQueue.Queue(() =>
            {
                engine._vk.DestroyImage(engine._device, image.Image, null);
                image.Allocation.Dispose();
            });
        });
        engine._vk.DestroyBuffer(engine._device, stagingBuffer.Buffer, null);
        stagingBuffer.Allocation.Dispose();
        outImage = image;
        return true;
    }
}
