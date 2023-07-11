using GlmSharp;
using Silk.NET.Vulkan;

namespace VkGuide.Types;

public class Camera
{
    private vec3 _position;
    public vec3 Position
    {
        get => _position;
        set
        {
            _position = value;
            View = mat4.Translate(_position);
        }
    }

    private float _fov;
    public float Fov
    {
        get => _fov;
        set
        {
            _fov = value;
            ResetProjection();
        }
    }

    private Extent2D _extent;
    public Extent2D Extent
    {
        get => _extent;
        set
        {
            _extent = value;
            ResetProjection();
        }
    }

    private float _near;
    public float NearPlane
    {
        get => _near;
        set
        {
            _near = value;
            ResetProjection();
        }
    }

    private float _far;

    public float FarPlane
    {
        get => _far;
        set
        {
            _far = value;
            ResetProjection();
        }
    }

    public mat4 View { get; private set; }
    public mat4 Projection { get; private set; }

    private void ResetProjection()
    {
        Projection = mat4.Perspective(_fov * (MathF.PI / 180),
            (_extent.Width / (float) _extent.Height),
            _near,
            _far);
        Projection = Projection with {m11 = Projection.m11 * -1};
    }
}
