using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace VkGuide.Types;

[DebuggerDisplay("Position: {Position}, Normal: {Normal}, Color: {Color}")]
public unsafe struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Color;
    public Vector2 Uv;

    public static VertexInputDescription GetVertexDescription()
    {
        var mainBinding = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint) Unsafe.SizeOf<Vertex>(),
            InputRate = VertexInputRate.Vertex
        };
        var positionAttribute = new VertexInputAttributeDescription
        {
            Binding = 0,
            Location = 0,
            Format = Format.R32G32B32Sfloat,
            Offset = (uint) Marshal.OffsetOf<Vertex>(nameof(Position))
        };
        
        var normalAttribute = new VertexInputAttributeDescription
        {
            Binding = 0,
            Location = 1,
            Format = Format.R32G32B32Sfloat,
            Offset = (uint) Marshal.OffsetOf<Vertex>(nameof(Normal))
        };
        var colorAttribute = new VertexInputAttributeDescription
        {
            Binding = 0,
            Location = 2,
            Format = Format.R32G32B32Sfloat,
            Offset = (uint) Marshal.OffsetOf<Vertex>(nameof(Color))
        };
        var uvAttribute = new VertexInputAttributeDescription
        {
            Binding = 0,
            Location = 3,
            Format = Format.R32G32Sfloat,
            Offset = (uint) Marshal.OffsetOf<Vertex>(nameof(Uv))
        };
        return new VertexInputDescription
        {
            Bindings = new[] {mainBinding},
            Attributes = new[]
            {
                positionAttribute,
                normalAttribute,
                colorAttribute,
                uvAttribute
            }
        };
    }
}
