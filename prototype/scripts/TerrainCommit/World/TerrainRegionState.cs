using System;
using System.Collections.Generic;

namespace Yudian.Terrain;

/// <summary>
/// 一个区域、一个模拟写者的内存权威入口。无跨线程安全、持久化或引擎同步承诺。
/// 成功请求记录仅在本对象生存期有效；取消不会撤回已提交高度。
/// </summary>
public sealed class TerrainRegionState
{
    private readonly Dictionary<string, Receipt> _committed = new(StringComparer.Ordinal);

    public TerrainRegionState(TerrainSnapshot initial)
    {
        Current = initial ?? throw new ArgumentException("initial must not be null", nameof(initial));
    }

    public TerrainSnapshot Current { get; private set; }

    public TerrainCommitResult Commit(string requestId, TerrainPatch patch,
        bool permissionGranted, bool cancellationRequested)
    {
        ValidateRequestId(requestId);
        if (patch is null)
            throw new ArgumentException("patch must not be null", nameof(patch));

        if (_committed.TryGetValue(requestId, out Receipt receipt))
        {
            bool same = SamePatch(receipt.Patch, patch);
            return new TerrainCommitResult(
                same ? TerrainCommitStatus.AlreadyCommitted : TerrainCommitStatus.RequestConflict,
                requestId, patch.PatchId, Current, same ? receipt.Version : null);
        }

        TerrainCommitStatus status = TerrainCommitPolicy.Evaluate(patch, Current,
            permissionGranted, cancellationRequested);
        if (status != TerrainCommitStatus.Ready)
            return new TerrainCommitResult(status, requestId, patch.PatchId, Current, null);

        var heights = new double[patch.HeightsM.Count];
        for (int i = 0; i < heights.Length; i++)
            heights[i] = patch.HeightsM[i];
        long version = Current.Version + 1; // Policy已拒绝版本上限。
        var next = new TerrainSnapshot(Current.SchemaVersion, Current.RegionId, version,
            Current.OriginXM, Current.OriginZM, Current.SpacingM, Current.Rows, Current.Columns, heights);
        var successfulReceipt = new Receipt(patch, version);
        var result = new TerrainCommitResult(TerrainCommitStatus.Committed,
            requestId, patch.PatchId, next, version);

        // 所有可分配对象先准备；单写者发布时不调用外部代码。
        _committed.Add(requestId, successfulReceipt);
        Current = next;
        return result;
    }

    private static bool SamePatch(TerrainPatch left, TerrainPatch right)
    {
        if (left.SchemaVersion != right.SchemaVersion ||
            !string.Equals(left.PatchId, right.PatchId, StringComparison.Ordinal))
            return false;
        try
        {
            TerrainDataCodec.ValidateAgainst(left, right.Base);
        }
        catch (ArgumentException)
        {
            return false;
        }
        for (int i = 0; i < left.HeightsM.Count; i++)
            if (left.HeightsM[i] != right.HeightsM[i])
                return false;
        return true;
    }

    private static void ValidateRequestId(string requestId)
    {
        if (requestId is null || requestId.Length is < 1 or > 64 ||
            requestId[0] is < 'a' or > 'z')
            throw new ArgumentException("requestId must match [a-z][a-z0-9_-]{0,63}", nameof(requestId));
        for (int i = 1; i < requestId.Length; i++)
        {
            char c = requestId[i];
            if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
                throw new ArgumentException("requestId must match [a-z][a-z0-9_-]{0,63}", nameof(requestId));
        }
    }

    private sealed class Receipt
    {
        internal Receipt(TerrainPatch patch, long version) { Patch = patch; Version = version; }
        internal TerrainPatch Patch { get; }
        internal long Version { get; }
    }
}
