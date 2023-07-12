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
            ResetView();
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

    public CameraData CameraData;
    
    private void ResetProjection()
    {
        CameraData.Projection = mat4.Perspective(_fov * (MathF.PI / 180),
            (_extent.Width / (float) _extent.Height),
            _near,
            _far);
        CameraData.Projection = CameraData.Projection with {m11 = CameraData.Projection.m11 * -1};
        ResetViewProjection();
    }

    private void ResetView()
    {
        CameraData.View = mat4.Translate(_position);
        ResetViewProjection();
    }

    private void ResetViewProjection() => CameraData.ViewProjection =  CameraData.Projection * CameraData.View;
}
