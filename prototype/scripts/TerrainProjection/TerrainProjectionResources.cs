using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-view-r1 GLM资源束：复用已验收 TerrainMeshAdapter.Create 与 TerrainCollisionAdapter.Create，
/// 返回全新 ArrayMesh/ConcavePolygonShape3D 资源对。先建 mesh，再建 shape；任一阶段抛异常时
/// 释放已创建资源（shape 失败释放 mesh），不返回半持有状态。成功由接收者持有整个 bundle，
/// 替换/退出后 Dispose；重复 Dispose 幂等无害。只读 Mesh/Shape；不缓存、不持有 RegionState、
/// 不绑定节点、不修改输入或材质，不引入依赖。
/// internal Create(mesh, failBeforeShape) 是唯一内部测试入口：true 在 mesh 创建成功之后、shape
/// 创建之前抛 InvalidOperationException——这是合成的中途异常（测试如实记录为模拟），不冒称
/// 真实 native 分配失败；抛出前释放该 mesh 并记录其 RID 供测试用 RenderingServer 有效性查询
/// 证明确实释放。公开 Create 默认 failBeforeShape=false。
/// </summary>
public sealed class TerrainProjectionResources : IDisposable
{
    private ArrayMesh _mesh;
    private ConcavePolygonShape3D _shape;
    private bool _disposed;

    /// <summary>模拟中途失败时被释放 mesh 的 RID；仅内部测试入口写入，供测试有效性查询。</summary>
    internal static Rid? LastSimulatedFailureMeshRid;

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
            Rid rid = arrayMesh.GetRid();
            arrayMesh.Dispose();
            LastSimulatedFailureMeshRid = rid;
            throw new InvalidOperationException(
                $"simulated mid-flight failure after mesh creation (before shape creation), "
                + $"mesh RID {rid} released; this is a synthesized test exception, not a real native allocation failure");
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
