namespace Yudian.Terrain;

/// <summary>内存提交回执；Snapshot是调用结束时的权威状态，不表示引擎已同步。</summary>
public sealed class TerrainCommitResult
{
    internal TerrainCommitResult(TerrainCommitStatus status, string requestId, string patchId,
        TerrainSnapshot snapshot, long? appliedVersion)
    {
        Status = status;
        RequestId = requestId;
        PatchId = patchId;
        Snapshot = snapshot;
        AppliedVersion = appliedVersion;
    }

    public TerrainCommitStatus Status { get; }
    public string RequestId { get; }
    public string PatchId { get; }
    public TerrainSnapshot Snapshot { get; }
    public long? AppliedVersion { get; }
}
