using System;

namespace Yudian.Terrain;

/// <summary>
/// terrain-commit-r1 冻结的纯判定：只读输入，不改对象、不创建版本、不写文件、不登记请求。
/// 判定顺序固定：null → 取消 → 无权限 → 旧 base → 无变化 → 版本上限 → Ready；
/// 取消优先于无权限，二者优先于旧 base；合法无变化在版本上限也返回 NoChange。
/// 权限/取消由权威调用者取得，此处不自报授权。
/// </summary>
public static class TerrainCommitPolicy
{
    // 与 TerrainDataCodec 冻结的版本上限一致（该常量不公开，避免为读取它扩大数据合同面）。
    private const long MaxVersion = 9007199254740991;

    public static TerrainCommitStatus Evaluate(TerrainPatch patch, TerrainSnapshot current, bool permissionGranted, bool cancellationRequested)
    {
        if (patch is null)
            throw new ArgumentException("patch must not be null", nameof(patch));
        if (current is null)
            throw new ArgumentException("current must not be null", nameof(current));

        if (cancellationRequested)
            return TerrainCommitStatus.Cancelled;
        if (!permissionGranted)
            return TerrainCommitStatus.PermissionDenied;

        try
        {
            TerrainDataCodec.ValidateAgainst(patch, current);
        }
        catch (ArgumentException)
        {
            // null 已在上方自查；ValidateAgainst 对合法对象仅在 base 与 current 不完全匹配时抛 ArgumentException。
            return TerrainCommitStatus.StaleBase;
        }

        bool anyCandidateDifferent = false;
        for (int index = 0; index < current.HeightsM.Count; index++)
        {
            // double != 将 +0/-0 视为同值；codec 已保证高度有限，无 NaN 干扰。
            if (patch.HeightsM[index] != current.HeightsM[index])
            {
                anyCandidateDifferent = true;
                break;
            }
        }

        if (!anyCandidateDifferent)
            return TerrainCommitStatus.NoChange;
        if (current.Version == MaxVersion)
            return TerrainCommitStatus.VersionLimit;
        return TerrainCommitStatus.Ready;
    }
}
