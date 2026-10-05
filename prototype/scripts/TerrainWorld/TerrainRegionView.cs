using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>单区域、Godot物理写线程。内存回执与native投影状态分别报告。</summary>
public partial class TerrainRegionView : Node3D
{
    private TerrainRegionState _state;
    private MeshInstance3D _mesh;
    private CollisionShape3D _collider;
    private TerrainProjectionResources _resources;
    private StandardMaterial3D _material;
    public TerrainSnapshot Current => _state.Current;
    public long? ProjectionVersion { get; private set; }
    public string ProjectionError { get; private set; }
    public bool PermissionGranted { get; set; } = true;
    public bool CancellationRequested { get; set; }
    internal bool FailBeforeShapeForTest;
    internal Action AfterPrepareForTest;
    internal Action AfterMeshBoundForTest;
    internal StaticBody3D Body { get; private set; }
    internal MeshInstance3D MeshNode => _mesh;
    internal CollisionShape3D Collider => _collider;

    public void Initialize(TerrainSnapshot initial)
    {
        if (_state != null || !IsInsideTree())
            throw new InvalidOperationException("Initialize once after adding region to the tree");
        _state = new TerrainRegionState(initial);
        _mesh = new MeshInstance3D();
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.63f, 0.32f, 0.19f), Roughness = 0.95f,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };
        _mesh.MaterialOverride = _material;
        AddChild(_mesh);
        Body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        AddChild(Body);
        _collider = new CollisionShape3D();
        Body.AddChild(_collider);
        RetryProjection();
    }

    public TerrainCommitResult Submit(string requestId, TerrainPatch patch)
    {
        EnsureTargets();
        TerrainCommitResult decision = _state.Evaluate(requestId, patch,
            PermissionGranted, CancellationRequested);
        if (decision.Status != TerrainCommitStatus.Ready)
            return decision; // 重放不得从历史patch重建当前世界。
        TerrainProjectionResources prepared = TerrainProjectionResources.Create(
            StageMeshBuilder.Build(patch, Current), FailBeforeShapeForTest);
        try
        {
            AfterPrepareForTest?.Invoke();
            EnsureTargets();
            TerrainCommitResult committed = _state.Commit(requestId, patch,
                PermissionGranted, CancellationRequested);
            if (committed.Status != TerrainCommitStatus.Committed)
                return committed;
            Bind(prepared, committed.Snapshot.Version);
            prepared = null; // Bind接管资源，包括绑定失败时的释放。
            return committed;
        }
        finally { prepared?.Dispose(); }
    }

    /// <summary>从Current恢复投影，不调用Commit，不增加版本或撤销请求回执。</summary>
    public void RetryProjection()
    {
        EnsureTargets();
        TerrainProjectionResources next = TerrainProjectionResources.Create(StageMeshBuilder.Build(Current));
        Bind(next, Current.Version);
    }

    private void EnsureTargets()
    {
        if (_state == null || !IsInsideTree() || !GodotObject.IsInstanceValid(_mesh) ||
            !GodotObject.IsInstanceValid(_collider) || !_mesh.IsInsideTree() || !_collider.IsInsideTree())
            throw new InvalidOperationException("region projection targets unavailable");
    }

    private void Bind(TerrainProjectionResources next, long version)
    {
        TerrainProjectionResources old = _resources;
        try
        {
            _mesh.Mesh = next.Mesh;
            AfterMeshBoundForTest?.Invoke();
            _collider.Shape = next.Shape;
        }
        catch (Exception ex)
        {
            ProjectionVersion = null;
            ProjectionError = ex.Message;
            try
            {
                _mesh.Mesh = old?.Mesh;
                _collider.Shape = old?.Shape;
            }
            catch (Exception restore)
            {
                // 不能确认旧投影仍可用，停止调用者；权威Current已经提交，禁止伪造回滚。
                next.Dispose();
                throw new InvalidOperationException("projection binding and restoration failed", restore);
            }
            next.Dispose();
            return;
        }
        _resources = next;
        ProjectionVersion = version;
        ProjectionError = null;
        old?.Dispose();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_mesh)) _mesh.Mesh = null;
        if (GodotObject.IsInstanceValid(_collider)) _collider.Shape = null;
        _resources?.Dispose();
        _resources = null;
        if (GodotObject.IsInstanceValid(_mesh)) _mesh.MaterialOverride = null;
        _material?.Dispose();
        _material = null;
        ProjectionVersion = null;
        ProjectionError = "region left the scene tree";
    }
}
