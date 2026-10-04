namespace Yudian.Terrain;

/// <summary>
/// terrain-commit-r1 冻结的提交判定结果。Evaluate 只返回前六项；
/// Committed/AlreadyCommitted/RequestConflict 属权威提交入口（本票外），纯判定不产生。
/// </summary>
public enum TerrainCommitStatus
{
    Ready,
    Cancelled,
    PermissionDenied,
    StaleBase,
    NoChange,
    VersionLimit,
    Committed,
    AlreadyCommitted,
    RequestConflict,
}
