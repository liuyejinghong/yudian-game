using System;
using Yudian.Terrain;

namespace TerrainCommit.Policy.Tests;

/// <summary>最小检查器：独立 console，无测试框架；非 0 退出码表示存在失败检查。</summary>
internal static class Check
{
    public static int Failed;
    public static int Total;

    public static void Ok(string name, Action check)
    {
        Total++;
        try
        {
            check();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            Failed++;
            Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal static class TerrainCommitPolicyTests
{
    // terrain-commit-r1 冻结的 3×4 非对称金样：region=sample-b、version=9、origin=(-2,3)、spacing=1。
    private const string GoldBLiteral =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBHeights = "10,11,12,13,20,21,22,23,30,31,32,33";
    // patch-a：仅内部 21/22→0/-1（3×4 的内部格只有 row1 的列 1、列 2，即索引 5、6）。
    private const string PatchACandidates = "10,11,12,13,20,0,-1,23,30,31,32,33";
    private const string NegativeZeroCandidates = "10,11,12,13,20,-0,-1,23,30,31,32,33";
    private const string SameVersionMutatedHeights = "99,11,12,13,20,21,22,23,30,31,32,33";
    private const string TwoRowHeights = "10,11,12,13,20,21,22,23";
    private const string TwoRowChangedCandidate = "10,11,12,13,20,0,22,23";
    private const long VersionLimit = 9007199254740991;

    public static int RunAll()
    {
        int failedBefore = Check.Failed;

        // 输入构造器本身与冻结金样逐字对齐，防止拼 JSON 出错。
        Check.Ok("golden_literal_guard", () =>
        {
            string built = SampleB(9, GoldBHeights);
            if (!string.Equals(built, GoldBLiteral, StringComparison.Ordinal))
                throw new Exception($"built gold differs from frozen literal:\n  {built}");
        });

        // —— null 优先：先于取消/权限，参数名明确 ——
        NullThrows("null_patch_param_patch", "patch", null, SampleB(9, GoldBHeights), true, false);
        NullThrows("null_patch_even_cancel_no_permission", "patch", null, SampleB(9, GoldBHeights), false, true);
        NullThrows("null_current_param_current", "current", ReadyPatchJson("patch-a"), null, true, false);
        NullThrows("null_both_reports_patch", "patch", null, null, true, false);

        // —— 合法输入在四种（取消,权限）组合下的判定矩阵 ——
        string readyPatch = ReadyPatchJson("patch-a");
        Status("ready_granted_no_cancel", TerrainCommitStatus.Ready, readyPatch, SampleB(9, GoldBHeights), true, false);
        Status("permission_denied_no_cancel", TerrainCommitStatus.PermissionDenied, readyPatch, SampleB(9, GoldBHeights), false, false);
        Status("cancelled_granted", TerrainCommitStatus.Cancelled, readyPatch, SampleB(9, GoldBHeights), true, true);
        Status("cancelled_beats_no_permission", TerrainCommitStatus.Cancelled, readyPatch, SampleB(9, GoldBHeights), false, true);

        // —— 取消/无权限优先于旧 base ——
        string stalePatch = PatchJson("patch-a", SampleB(8, GoldBHeights), GoldBHeights);
        // 该候选与 current 高度逐值相等：若顺序错误会先返回 NoChange，必须仍是 StaleBase。
        Status("stale_base_granted_no_cancel", TerrainCommitStatus.StaleBase, stalePatch, SampleB(9, GoldBHeights), true, false);
        Status("permission_denied_beats_stale_base", TerrainCommitStatus.PermissionDenied, stalePatch, SampleB(9, GoldBHeights), false, false);
        Status("cancelled_beats_stale_base_granted", TerrainCommitStatus.Cancelled, stalePatch, SampleB(9, GoldBHeights), true, true);
        Status("cancelled_beats_stale_base_and_no_permission", TerrainCommitStatus.Cancelled, stalePatch, SampleB(9, GoldBHeights), false, true);

        // —— 取消/无权限优先于无变化 ——
        string noChangePatch = PatchJson("patch-a", SampleB(9, GoldBHeights), GoldBHeights);
        Status("no_change_granted_no_cancel", TerrainCommitStatus.NoChange, noChangePatch, SampleB(9, GoldBHeights), true, false);
        Status("permission_denied_beats_no_change", TerrainCommitStatus.PermissionDenied, noChangePatch, SampleB(9, GoldBHeights), false, false);
        Status("cancelled_beats_no_change_granted", TerrainCommitStatus.Cancelled, noChangePatch, SampleB(9, GoldBHeights), true, true);
        Status("cancelled_beats_no_change_and_no_permission", TerrainCommitStatus.Cancelled, noChangePatch, SampleB(9, GoldBHeights), false, true);

        // —— 取消/无权限优先于版本上限 ——
        string limitCurrent = SampleB(VersionLimit, GoldBHeights);
        string limitChangedPatch = PatchJson("patch-a", limitCurrent, PatchACandidates);
        Status("version_limit_changed", TerrainCommitStatus.VersionLimit, limitChangedPatch, limitCurrent, true, false);
        Status("permission_denied_beats_version_limit", TerrainCommitStatus.PermissionDenied, limitChangedPatch, limitCurrent, false, false);
        Status("cancelled_beats_version_limit_granted", TerrainCommitStatus.Cancelled, limitChangedPatch, limitCurrent, true, true);
        Status("cancelled_beats_version_limit_and_no_permission", TerrainCommitStatus.Cancelled, limitChangedPatch, limitCurrent, false, true);

        // —— 合法无变化在版本上限也是 NoChange（NoChange 先于 VersionLimit；base 必须同样是上限快照）——
        Status("no_change_at_version_limit", TerrainCommitStatus.NoChange, PatchJson("patch-a", limitCurrent, GoldBHeights), limitCurrent, true, false);

        // —— 旧 base 全字段：region/version/rows/columns/origin_x/origin_z/spacing/同版高度 ——
        Status("stale_region", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB("sample-c", 9, -2, 3, 1, 3, 4, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_version", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(8, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_rows", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, -2, 3, 1, 2, 6, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_columns", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, -2, 3, 1, 6, 2, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_origin_x", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, -3, 3, 1, 3, 4, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_origin_z", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, -2, 4, 1, 3, 4, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_spacing", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, -2, 3, 2, 3, 4, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);
        Status("stale_same_version_different_heights", TerrainCommitStatus.StaleBase,
            PatchJson("patch-a", SampleB(9, SameVersionMutatedHeights), SameVersionMutatedHeights), SampleB(9, GoldBHeights), true, false);

        // —— NoChange 含正负零：+0/-0 同值 ——
        string plusZeroCurrent = SampleB(10, PatchACandidates);
        Status("no_change_minus_zero_vs_plus_zero", TerrainCommitStatus.NoChange,
            PatchJson("patch-a", plusZeroCurrent, NegativeZeroCandidates), plusZeroCurrent, true, false);
        string minusZeroCurrent = SampleB(10, NegativeZeroCandidates);
        Status("no_change_plus_zero_vs_minus_zero", TerrainCommitStatus.NoChange,
            PatchJson("patch-a", minusZeroCurrent, PatchACandidates), minusZeroCurrent, true, false);

        // —— 不同 patch_id 不影响纯判定 ——
        Status("ready_other_patch_id", TerrainCommitStatus.Ready, ReadyPatchJson("patch-zzz"), SampleB(9, GoldBHeights), true, false);
        Status("no_change_other_patch_id", TerrainCommitStatus.NoChange, PatchJson("patch-b", SampleB(9, GoldBHeights), GoldBHeights), SampleB(9, GoldBHeights), true, false);

        // —— 2×N 无内部：全部格在外围，合法候选必与 base 同值 ——
        string twoRowCurrent = SampleB(5, -2, 3, 1, 2, 4, TwoRowHeights);
        string twoRowNoChange = PatchJson("patch-a", twoRowCurrent, TwoRowHeights);
        Status("two_by_four_no_internal_no_change", TerrainCommitStatus.NoChange, twoRowNoChange, twoRowCurrent, true, false);
        Status("two_by_four_other_patch_id_still_no_change", TerrainCommitStatus.NoChange, PatchJson("patch-b", twoRowCurrent, TwoRowHeights), twoRowCurrent, true, false);
        Check.Ok("two_by_four_codec_rejects_any_change", () =>
        {
            try
            {
                TerrainDataCodec.ParsePatch(PatchJson("patch-a", twoRowCurrent, TwoRowChangedCandidate));
            }
            catch (ArgumentException ex)
            {
                if (ex.GetType() != typeof(ArgumentException))
                    throw new Exception($"expected exact ArgumentException, got {ex.GetType().Name}");
                if (!ex.Message.Contains("locked perimeter", StringComparison.Ordinal))
                    throw new Exception($"message lacks 'locked perimeter': {ex.Message}");
                return;
            }
            throw new Exception("expected ArgumentException, but nothing was thrown");
        });

        return Check.Failed - failedBefore;
    }

    // 每例用严格 codec 创建输入，判定前后 Serialize 比对证明对象未变。
    private static void Status(string name, TerrainCommitStatus expected, string patchJson, string currentJson, bool permissionGranted, bool cancellationRequested)
    {
        Check.Ok(name, () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(patchJson);
            TerrainSnapshot current = TerrainDataCodec.ParseSnapshot(currentJson);
            string patchBefore = TerrainDataCodec.Serialize(patch);
            string currentBefore = TerrainDataCodec.Serialize(current);

            TerrainCommitStatus actual = TerrainCommitPolicy.Evaluate(patch, current, permissionGranted, cancellationRequested);

            if (actual != expected)
                throw new Exception($"expected {expected}, got {actual}");
            if (!string.Equals(TerrainDataCodec.Serialize(patch), patchBefore, StringComparison.Ordinal))
                throw new Exception("patch changed during Evaluate");
            if (!string.Equals(TerrainDataCodec.Serialize(current), currentBefore, StringComparison.Ordinal))
                throw new Exception("current changed during Evaluate");
        });
    }

    private static void NullThrows(string name, string expectedParamName, string patchJson, string currentJson, bool permissionGranted, bool cancellationRequested)
    {
        Check.Ok(name, () =>
        {
            TerrainPatch patch = patchJson is null ? null : TerrainDataCodec.ParsePatch(patchJson);
            TerrainSnapshot current = currentJson is null ? null : TerrainDataCodec.ParseSnapshot(currentJson);
            string patchBefore = patchJson is null ? "null" : TerrainDataCodec.Serialize(patch);
            string currentBefore = currentJson is null ? "null" : TerrainDataCodec.Serialize(current);
            try
            {
                TerrainCommitPolicy.Evaluate(patch, current, permissionGranted, cancellationRequested);
            }
            catch (ArgumentException ex)
            {
                if (ex.GetType() != typeof(ArgumentException))
                    throw new Exception($"expected exact ArgumentException, got {ex.GetType().Name}");
                if (!string.Equals(ex.ParamName, expectedParamName, StringComparison.Ordinal))
                    throw new Exception($"expected paramName '{expectedParamName}', got '{ex.ParamName}'");
                if (!string.Equals(patchJson is null ? "null" : TerrainDataCodec.Serialize(patch), patchBefore, StringComparison.Ordinal))
                    throw new Exception("patch changed during Evaluate");
                if (!string.Equals(currentJson is null ? "null" : TerrainDataCodec.Serialize(current), currentBefore, StringComparison.Ordinal))
                    throw new Exception("current changed during Evaluate");
                return;
            }
            throw new Exception("expected ArgumentException, but nothing was thrown");
        });
    }

    private static string ReadyPatchJson(string patchId)
        => PatchJson(patchId, SampleB(9, GoldBHeights), PatchACandidates);

    private static string SampleB(long version, string heightsCsv)
        => SnapshotJson("sample-b", version, -2, 3, 1, 3, 4, heightsCsv);

    private static string SampleB(string regionId, long version, double originXM, double originZM, double spacingM, int rows, int columns, string heightsCsv)
        => SnapshotJson(regionId, version, originXM, originZM, spacingM, rows, columns, heightsCsv);

    private static string SampleB(long version, double originXM, double originZM, double spacingM, int rows, int columns, string heightsCsv)
        => SnapshotJson("sample-b", version, originXM, originZM, spacingM, rows, columns, heightsCsv);

    // 数值格式与 codec 自身 Serialize 一致：InvariantCulture 最短往返表示是合法 JSON 数字（含 -0）。
    private static string SnapshotJson(string regionId, long version, double originXM, double originZM, double spacingM, int rows, int columns, string heightsCsv)
        => "{\"schema_version\":1,\"region_id\":\"" + regionId + "\",\"version\":" + version.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"origin_x_m\":" + originXM.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"origin_z_m\":" + originZM.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"spacing_m\":" + spacingM.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"rows\":" + rows.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"columns\":" + columns.ToString(System.Globalization.CultureInfo.InvariantCulture)
           + ",\"heights_m\":[" + heightsCsv + "]}";

    private static string PatchJson(string patchId, string baseJson, string heightsCsv)
        => "{\"schema_version\":1,\"patch_id\":\"" + patchId + "\",\"base\":" + baseJson + ",\"heights_m\":[" + heightsCsv + "]}";
}
