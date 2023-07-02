using Silk.NET.Vulkan;

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
}
