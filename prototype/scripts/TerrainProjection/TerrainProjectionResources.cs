using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// 一对独立的渲染/静态碰撞资源，创建失败释放已建资源，成功由接收者Dispose。
/// 复用两个adapter，不持有世界状态；内部钩子只模拟mesh创建后的中途异常。
/// </summary>
public sealed class TerrainProjectionResources : IDisposable
{
    private ArrayMesh _mesh;
    private ConcavePolygonShape3D _shape;
    private bool _disposed;

    /// <summary>模拟中途失败时已释放的 ArrayMesh 实例；仅内部测试入口写入，供测试 IsInstanceValid 证明释放。</summary>
    internal static ArrayMesh LastSimulatedFailureMesh;

    private TerrainProjectionResources(ArrayMesh mesh, ConcavePolygonShape3D shape)
    {
        _mesh = mesh;
        _shape = shape;
    }

    /// <summary>已移交的 ArrayMesh；Dispose 后访问抛 ObjectDisposedException。</summary>
    public ArrayMesh Mesh
    {
        get { ThrowIfDisposed(); return _mesh!; }
    }

    /// <summary>已移交的 ConcavePolygonShape3D；Dispose 后访问抛 ObjectDisposedException。</summary>
    public ConcavePolygonShape3D Shape
    {
        get { ThrowIfDisposed(); return _shape!; }
    }

    public static TerrainProjectionResources Create(TerrainMesh mesh) => Create(mesh, failBeforeShape: false);

    internal static TerrainProjectionResources Create(TerrainMesh mesh, bool failBeforeShape)
    {
        ArrayMesh arrayMesh = TerrainMeshAdapter.Create(mesh);
        if (failBeforeShape)
        {
            arrayMesh.Dispose();
            LastSimulatedFailureMesh = arrayMesh;
            throw new InvalidOperationException(
                $"simulated mid-flight failure after mesh creation (before shape creation), "
                + $"mesh instance released; this is a synthesized test exception, not a real native allocation failure");
        }

        ConcavePolygonShape3D shape;
        try
        {
            shape = TerrainCollisionAdapter.Create(mesh);
        }
        catch (Exception)
        {
            arrayMesh.Dispose();
            throw;
        }

        return new TerrainProjectionResources(arrayMesh, shape);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _mesh?.Dispose();
        _shape?.Dispose();
        _mesh = null;
        _shape = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerrainProjectionResources));
    }
}
