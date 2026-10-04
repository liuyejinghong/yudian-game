using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Yudian.Terrain;

namespace TerrainData.Tests;

/// <summary>最小检查器：独立 console，无测试框架；非 0 退出码表示存在失败。</summary>
internal static class Check
{
    public static int Failed;

    public static void Ok(string name, Action check)
    {
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

    /// <summary>断言调用抛出精确 ArgumentException（合同统一异常类型），且消息包含指定路径/名字。</summary>
    public static void Throws(string name, string messageContains, Action call)
    {
        Ok(name, () =>
        {
            try
            {
                call();
            }
            catch (ArgumentException ex)
            {
                if (ex.GetType() != typeof(ArgumentException))
                    throw new Exception($"expected exact ArgumentException, got {ex.GetType().Name}");
                if (messageContains.Length > 0 && !ex.Message.Contains(messageContains, StringComparison.Ordinal))
                    throw new Exception($"message lacks '{messageContains}': {ex.Message}");
                return;
            }
            throw new Exception("expected ArgumentException, but nothing was thrown");
        });
    }

    public static void NotSupported(string name, Action call)
    {
        Ok(name, () =>
        {
            try
            {
                call();
            }
            catch (NotSupportedException)
            {
                return;
            }
            throw new Exception("expected NotSupportedException, but nothing was thrown");
        });
    }
}

internal static class TerrainDataContractTests
{
    private const string GoldAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private const string GoldBJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBPatchHeights = "[10,11,12,13,20,0,-1,23,30,31,32,33]";

    private const string GoldBPatchJson =
        "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":" + GoldBJson + ",\"heights_m\":" + GoldBPatchHeights + "}";

    private static readonly double[] GoldBBaseHeights = { 10, 11, 12, 13, 20, 21, 22, 23, 30, 31, 32, 33 };

    public static int RunAll()
    {
        Check.Failed = 0;
        GoldASnapshot();
        GoldBPatch();
        Indexing();
        PerimeterLock();
        ValidateAgainstTests();
        ReadOnlyCollections();
        JsonFieldErrors();
        IntegerSyntax();
        RangesAndLimits();
        DamagedJson();
        RoundTrips();
        NoSideEffects();
        return Check.Failed;
    }

    // ---------- 值断言辅助 ----------

    private static void Eq(object? expected, object? actual, string what)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{what}: expected {expected}, got {actual}");
    }

    private static void Bits(double expected, double actual, string what)
        => Eq(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual), what);

    private static TerrainSnapshot ParseA() => TerrainDataCodec.ParseSnapshot(GoldAJson);
    private static TerrainSnapshot ParseB() => TerrainDataCodec.ParseSnapshot(GoldBJson);
    private static TerrainPatch ParsePatchB() => TerrainDataCodec.ParsePatch(GoldBPatchJson);

    // 原始 token 拼 JSON（所有参数按已引号/已括号的原样注入），全部数值经 InvariantCulture 格式化。
    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string SnapshotJson(string schemaVersion, string regionId, string version,
        string originX, string originZ, string spacing, string rows, string columns, string heights)
        => $"{{\"schema_version\":{schemaVersion},\"region_id\":{regionId},\"version\":{version},"
         + $"\"origin_x_m\":{originX},\"origin_z_m\":{originZ},\"spacing_m\":{spacing},"
         + $"\"rows\":{rows},\"columns\":{columns},\"heights_m\":{heights}}}";

    private static string PatchJson(string schemaVersion, string patchId, string baseJson, string heights)
        => $"{{\"schema_version\":{schemaVersion},\"patch_id\":{patchId},\"base\":{baseJson},\"heights_m\":{heights}}}";

    private static string CanonicalPatchB() => PatchJson("1", "\"patch-b\"", GoldBJson, GoldBPatchHeights);

    private static string HeightsCsv(params double[] values)
    {
        var csv = new StringBuilder();
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0)
                csv.Append(',');
            csv.Append(Num(values[index]));
        }
        return csv.ToString();
    }

    private static string PatchBHeightsWith(int index, double value)
    {
        var heights = (double[])GoldBBaseHeights.Clone();
        heights[index] = value;
        return "[" + HeightsCsv(heights) + "]";
    }

    private static string ZerosCsv(int count)
    {
        var csv = new StringBuilder(count * 2);
        for (int index = 0; index < count; index++)
        {
            if (index > 0)
                csv.Append(',');
            csv.Append('0');
        }
        return csv.ToString();
    }

    private static double[] SnapshotHeights(TerrainSnapshot snapshot)
    {
        var heights = new double[snapshot.HeightsM.Count];
        for (int index = 0; index < heights.Length; index++)
            heights[index] = snapshot.HeightsM[index];
        return heights;
    }

    // ---------- 检查组 ----------

    private static void GoldASnapshot()
    {
        Check.Ok("gold A: parse and all nine field values", () =>
        {
            TerrainSnapshot a = ParseA();
            Eq(1, a.SchemaVersion, "SchemaVersion");
            Eq("sample-a", a.RegionId, "RegionId");
            Eq(7L, a.Version, "Version");
            Bits(-1.5, a.OriginXM, "OriginXM");
            Bits(2.0, a.OriginZM, "OriginZM");
            Bits(0.5, a.SpacingM, "SpacingM");
            Eq(2, a.Rows, "Rows");
            Eq(3, a.Columns, "Columns");
            Eq(6, a.HeightsM.Count, "HeightsM.Count");
            double[] expected = { 1, 2, 4, 8, 16, 32 };
            for (int index = 0; index < expected.Length; index++)
                Bits(expected[index], a.HeightsM[index], $"HeightsM[{index}]");
        });

        // 合同坐标金样：(0,2) → (-0.5,4,2)，(1,0) → (-1.5,8,2.5)，防 2×3 转置。
        Check.Ok("gold A: contract coordinate probes (0,2) and (1,0)", () =>
        {
            TerrainSnapshot a = ParseA();
            Bits(-1.5 + 2 * 0.5, a.OriginXM + 2 * a.SpacingM, "x(0,2)");
            Bits(4.0, a.GetHeight(0, 2), "y(0,2)");
            Bits(2.0, a.OriginZM + 0 * a.SpacingM, "z(0,2)");
            Bits(-1.5, a.OriginXM + 0 * a.SpacingM, "x(1,0)");
            Bits(8.0, a.GetHeight(1, 0), "y(1,0)");
            Bits(2.5, a.OriginZM + 1 * a.SpacingM, "z(1,0)");
        });

        Check.Ok("gold A: GetHeight row-major over all cells", () =>
        {
            TerrainSnapshot a = ParseA();
            double[] expected = { 1, 2, 4, 8, 16, 32 };
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 3; column++)
                    Bits(expected[row * 3 + column], a.GetHeight(row, column), $"GetHeight({row},{column})");
        });

        Check.Ok("gold A: serialize is exactly the contract example string", () =>
            Eq(GoldAJson, TerrainDataCodec.Serialize(ParseA()), "serialized"));

        Check.Ok("gold A: serialize→parse round trip keeps all values", () =>
        {
            TerrainSnapshot round = TerrainDataCodec.ParseSnapshot(TerrainDataCodec.Serialize(ParseA()));
            Eq("sample-a", round.RegionId, "RegionId");
            Eq(7L, round.Version, "Version");
            Bits(-1.5, round.OriginXM, "OriginXM");
            Bits(2.0, round.OriginZM, "OriginZM");
            Bits(0.5, round.SpacingM, "SpacingM");
            Eq(2, round.Rows, "Rows");
            Eq(3, round.Columns, "Columns");
            double[] expected = { 1, 2, 4, 8, 16, 32 };
            for (int index = 0; index < expected.Length; index++)
                Bits(expected[index], round.HeightsM[index], $"HeightsM[{index}]");
        });
    }

    private static void GoldBPatch()
    {
        Check.Ok("gold B: snapshot parses with 3×4 layout", () =>
        {
            TerrainSnapshot b = ParseB();
            Eq("sample-b", b.RegionId, "RegionId");
            Eq(9L, b.Version, "Version");
            Eq(3, b.Rows, "Rows");
            Eq(4, b.Columns, "Columns");
            Bits(33.0, b.GetHeight(2, 3), "GetHeight(2,3)");
            Bits(21.0, b.GetHeight(1, 1), "GetHeight(1,1)");
        });

        Check.Ok("gold B: patch parses, base kept, only interior (1,1)/(1,2) changed", () =>
        {
            TerrainPatch patch = ParsePatchB();
            Eq(1, patch.SchemaVersion, "SchemaVersion");
            Eq("patch-b", patch.PatchId, "PatchId");
            TerrainSnapshot b = ParseB();
            Eq(patch.Base.RegionId, b.RegionId, "base region");
            Eq(patch.Base.Version, b.Version, "base version");
            Eq(patch.Base.Rows, b.Rows, "base rows");
            Eq(patch.Base.Columns, b.Columns, "base columns");
            for (int index = 0; index < 12; index++)
                Bits(GoldBBaseHeights[index], patch.Base.HeightsM[index], $"base heights[{index}]");
            Bits(0.0, patch.HeightsM[5], "candidate (1,1)");
            Bits(-1.0, patch.HeightsM[6], "candidate (1,2)");
            Bits(20.0, patch.HeightsM[4], "perimeter (1,0) unchanged");
            Bits(23.0, patch.HeightsM[7], "perimeter (1,3) unchanged");
        });

        Check.Ok("gold B: serialize is exactly the contract example shape", () =>
        {
            string expected = "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":"
                + GoldBJson + ",\"heights_m\":" + GoldBPatchHeights + "}";
            Eq(expected, TerrainDataCodec.Serialize(ParsePatchB()), "serialized patch");
        });

        Check.Ok("gold B: patch serialize→parse round trip", () =>
        {
            string round = TerrainDataCodec.Serialize(TerrainDataCodec.ParsePatch(TerrainDataCodec.Serialize(ParsePatchB())));
            Eq(TerrainDataCodec.Serialize(ParsePatchB()), round, "round-tripped patch text");
        });

        Check.Ok("gold B: ValidateAgainst accepts the matching current", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), ParseB()));
    }

    private static void Indexing()
    {
        Check.Throws("GetHeight row -1 rejected with 'row'", "row", () => ParseA().GetHeight(-1, 0));
        Check.Throws("GetHeight column -1 rejected with 'column'", "column", () => ParseA().GetHeight(0, -1));
        Check.Throws("GetHeight row == Rows rejected", "row", () => ParseA().GetHeight(2, 0));
        Check.Throws("GetHeight column == Columns rejected", "column", () => ParseA().GetHeight(0, 3));
        Check.Throws("GetHeight (-1,-1) rejected", "row", () => ParseA().GetHeight(-1, -1));
        Check.Ok("GetHeight boundary cells (0,0) and (2,3) valid", () =>
        {
            Bits(10.0, ParseB().GetHeight(0, 0), "GetHeight(0,0)");
            Bits(33.0, ParseB().GetHeight(2, 3), "GetHeight(2,3)");
        });
    }

    private static void PerimeterLock()
    {
        // 金样 B 外围全部非零：逐个外围格改动都必须拒绝。
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                bool onPerimeter = row == 0 || row == 2 || column == 0 || column == 3;
                if (!onPerimeter)
                    continue;
                int index = row * 4 + column;
                double original = GoldBBaseHeights[index];
                Check.Throws($"gold B perimeter cell ({row},{column}) change rejected", "perimeter",
                    () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson, PatchBHeightsWith(index, original + 2.5))));
            }
        }

        Check.Ok("gold B interior-only candidate with other values accepted", () =>
        {
            var heights = (double[])GoldBBaseHeights.Clone();
            heights[5] = 5;
            heights[6] = 6;
            TerrainPatch patch = TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson, "[" + HeightsCsv(heights) + "]"));
            Bits(5.0, patch.HeightsM[5], "interior (1,1)");
            Bits(6.0, patch.HeightsM[6], "interior (1,2)");
        });

        // 金样 A 是 2×3：每格都是外围，任何改动都拒绝（非零基准外围）。
        double[] goldA = { 1, 2, 4, 8, 16, 32 };
        for (int index = 0; index < goldA.Length; index++)
        {
            int captured = index;
            Check.Throws($"gold A 2×3 all-perimeter cell {captured} change rejected", "perimeter", () =>
            {
                var heights = (double[])goldA.Clone();
                heights[captured] = heights[captured] + 1;
                TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-a\"", GoldAJson, "[" + HeightsCsv(heights) + "]"));
            });
        }

        Check.Ok("gold A unchanged candidate accepted", () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-a\"", GoldAJson, "[1,2,4,8,16,32]"));
            Bits(16.0, patch.HeightsM[4], "height 16 kept");
        });

        // 零外围：全 0 外围的不变候选接受；改动拒绝。
        string zeroBase = SnapshotJson("1", "\"zero-base\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]");
        Check.Ok("all-zero perimeter unchanged candidate accepted", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"zero-patch\"", zeroBase, "[0,0,0,0]")));
        Check.Throws("all-zero perimeter changed candidate rejected", "perimeter",
            () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"zero-patch\"", zeroBase, "[1,0,0,0]")));

        // ±0 同值：外围 0 写作 -0 数值相等，接受；同一值不同写法（1 与 1.0）也接受。
        Check.Ok("perimeter -0 equals +0 and decimal form equals integer form", () =>
        {
            string mixedBase = SnapshotJson("1", "\"mixed-base\"", "1", "0", "0", "1", "2", "2", "[1,0,0,3]");
            TerrainPatch patch = TerrainDataCodec.ParsePatch(PatchJson("1", "\"mixed-patch\"", mixedBase, "[1.0,-0,0.0,3]"));
            Bits(1.0, patch.HeightsM[0], "perimeter 1 vs 1.0");
            Bits(-0.0, patch.HeightsM[1], "perimeter -0");
        });
    }

    private static void ValidateAgainstTests()
    {
        Check.Throws("ValidateAgainst(null, current) rejected", "patch",
            () => TerrainDataCodec.ValidateAgainst(null!, ParseB()));
        Check.Throws("ValidateAgainst(patch, null) rejected", "current",
            () => TerrainDataCodec.ValidateAgainst(ParsePatchB(), null!));

        Check.Throws("wrong region rejected", "base.region_id", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-c\"", "9", "-2", "3", "1", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Throws("older version rejected", "base.version", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "8", "-2", "3", "1", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Throws("newer version rejected", "base.version", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "10", "-2", "3", "1", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Throws("same version but different content rejected", "base.heights_m[7]", () =>
        {
            var heights = (double[])GoldBBaseHeights.Clone();
            heights[7] = 24;
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-2", "3", "1", "3", "4", "[" + HeightsCsv(heights) + "]")));
        });

        Check.Throws("rows mismatch rejected", "base.rows", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-2", "3", "1", "2", "4", "[10,11,12,13,20,21,22,23]"))));

        Check.Throws("columns mismatch rejected", "base.columns", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-2", "3", "1", "3", "3", "[10,11,12,20,21,22,30,31,32]"))));

        Check.Throws("origin_x mismatch rejected", "base.origin_x_m", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-1", "3", "1", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Throws("origin_z mismatch rejected", "base.origin_z_m", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-2", "0", "1", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Throws("spacing mismatch rejected", "base.spacing_m", () =>
            TerrainDataCodec.ValidateAgainst(ParsePatchB(), TerrainDataCodec.ParseSnapshot(
                SnapshotJson("1", "\"sample-b\"", "9", "-2", "3", "2", "3", "4", "[" + HeightsCsv(GoldBBaseHeights) + "]"))));

        Check.Ok("failed ValidateAgainst keeps both sides untouched", () =>
        {
            TerrainPatch patch = ParsePatchB();
            TerrainSnapshot b = ParseB();
            try
            {
                TerrainDataCodec.ValidateAgainst(patch, TerrainDataCodec.ParseSnapshot(
                    SnapshotJson("1", "\"other\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));
                throw new Exception("expected ValidateAgainst to fail");
            }
            catch (ArgumentException)
            {
            }
            Eq("patch-b", patch.PatchId, "patch id");
            Eq("sample-b", patch.Base.RegionId, "base region");
            Eq(9L, patch.Base.Version, "base version");
            double[] heights = SnapshotHeights(patch.Base);
            for (int index = 0; index < 12; index++)
                Bits(GoldBBaseHeights[index], heights[index], $"base heights[{index}]");
            Eq("sample-b", b.RegionId, "current region");
            Eq(9L, b.Version, "current version");
        });
    }

    private static void ReadOnlyCollections()
    {
        Check.Ok("snapshot HeightsM is not an array", () =>
            Eq(false, (object)ParseA().HeightsM is double[], "is double[]"));
        Check.Ok("patch HeightsM is not an array", () =>
            Eq(false, (object)ParsePatchB().HeightsM is double[], "is double[]"));
        Check.NotSupported("snapshot HeightsM rejects Add via ICollection",
            () => ((ICollection<double>)ParseA().HeightsM).Add(99));
        Check.NotSupported("patch HeightsM rejects Add via ICollection",
            () => ((ICollection<double>)ParsePatchB().HeightsM).Add(99));
        Check.NotSupported("snapshot HeightsM rejects Clear via ICollection",
            () => ((ICollection<double>)ParseA().HeightsM).Clear());
        Check.Ok("two parses do not share the height list instance", () =>
            Eq(false, ReferenceEquals(ParseA().HeightsM, ParseA().HeightsM), "same list instance"));
    }

    private static void JsonFieldErrors()
    {
        // 九字段逐一：缺失。
        Check.Throws("missing schema_version", "schema_version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"schema_version\":1,", "")));
        Check.Throws("missing region_id", "region_id", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"region_id\":\"sample-a\",", "")));
        Check.Throws("missing version", "version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"version\":7,", "")));
        Check.Throws("missing origin_x_m", "origin_x_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"origin_x_m\":-1.5,", "")));
        Check.Throws("missing origin_z_m", "origin_z_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"origin_z_m\":2,", "")));
        Check.Throws("missing spacing_m", "spacing_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"spacing_m\":0.5,", "")));
        Check.Throws("missing rows", "rows", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"rows\":2,", "")));
        Check.Throws("missing columns", "columns", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"columns\":3,", "")));
        Check.Throws("missing heights_m", "heights_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace(",\"heights_m\":[1,2,4,8,16,32]", "")));

        // null。
        Check.Throws("null schema_version", "schema_version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"schema_version\":1", "\"schema_version\":null")));
        Check.Throws("null region_id", "region_id", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"sample-a\"", "null")));
        Check.Throws("null version", "version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"version\":7", "\"version\":null")));
        Check.Throws("null origin_x_m", "origin_x_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("-1.5", "null")));
        Check.Throws("null spacing_m", "spacing_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("0.5", "null")));
        Check.Throws("null rows", "rows", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"rows\":2", "\"rows\":null")));
        Check.Throws("null heights_m", "heights_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("[1,2,4,8,16,32]", "null")));

        // 错类型。
        Check.Throws("string schema_version", "schema_version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"schema_version\":1", "\"schema_version\":\"1\"")));
        Check.Throws("numeric region_id", "region_id", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"sample-a\"", "5")));
        Check.Throws("boolean version", "version", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"version\":7", "\"version\":true")));
        Check.Throws("string origin_x_m", "origin_x_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("-1.5", "\"x\"")));
        Check.Throws("string rows", "rows", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"rows\":2", "\"rows\":\"2\"")));
        Check.Throws("object heights_m", "heights_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("[1,2,4,8,16,32]", "{}")));
        Check.Throws("string heights_m", "heights_m", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("[1,2,4,8,16,32]", "\"1\"")));
        Check.Throws("string height item reports index path", "heights_m[4]", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("[1,2,4,8,16,32]", "[1,2,4,8,\"16\",32]")));

        // 重复。
        Check.Throws("duplicate region_id", "duplicate", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"region_id\":\"sample-a\"", "\"region_id\":\"sample-a\",\"region_id\":\"sample-a\"")));
        Check.Throws("duplicate heights_m", "duplicate", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("[1,2,4,8,16,32]", "[1,2,4,8,16,32],\"heights_m\":[1,2,4,8,16,32]")));
        Check.Throws("duplicate rows", "duplicate", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"rows\":2", "\"rows\":2,\"rows\":2")));

        // 未知与大小写。
        Check.Throws("unknown field rejected", "extra", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("{", "{\"extra\":1,")));
        Check.Throws("field names are case-sensitive", "Origin_X_M", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"origin_x_m\"", "\"Origin_X_M\"")));
        Check.Throws("region_id value is case-sensitive", "region_id", () => TerrainDataCodec.ParseSnapshot(GoldAJson.Replace("\"sample-a\"", "\"Sample-a\"")));

        // patch 四字段。
        Check.Throws("patch missing patch_id", "patch_id", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace("\"patch_id\":\"patch-b\",", "")));
        Check.Throws("patch boolean base", "base", () => TerrainDataCodec.ParsePatch(
            "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":true,\"heights_m\":" + GoldBPatchHeights + "}"));
        Check.Throws("patch missing heights_m", "heights_m", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace(",\"heights_m\":" + GoldBPatchHeights, "")));
        Check.Throws("patch null patch_id", "patch_id", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace("\"patch_id\":\"patch-b\"", "\"patch_id\":null")));
        Check.Throws("patch duplicate patch_id", "duplicate", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace("\"patch_id\":\"patch-b\"", "\"patch_id\":\"patch-b\",\"patch_id\":\"patch-b\"")));
        Check.Throws("patch numeric patch_id", "patch_id", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace("\"patch_id\":\"patch-b\"", "\"patch_id\":5")));
        Check.Throws("patch unknown field", "extra", () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson, GoldBPatchHeights).Replace("{", "{\"extra\":1,")));
        Check.Throws("patch numeric heights_m", "heights_m", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace(GoldBPatchHeights, "5")));
        Check.Throws("patch heights_m wrong length", "exactly 12", () => TerrainDataCodec.ParsePatch(CanonicalPatchB().Replace(GoldBPatchHeights, "[10,11,12,13,20,0,-1,23,30,31,32]")));
        Check.Throws("patch uppercase patch_id rejected", "patch_id", () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"Patch-b\"", GoldBJson, GoldBPatchHeights)));
        Check.Throws("patch leading dash patch_id rejected", "patch_id", () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"-patch-b\"", GoldBJson, GoldBPatchHeights)));
        Check.Ok("patch_id with underscore/digits accepted", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch_b2\"", GoldBJson, GoldBPatchHeights)));
        string longId = "a" + new string('z', 63);
        Check.Ok("64-char patch_id accepted", () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"" + longId + "\"", GoldBJson, GoldBPatchHeights)));
        Check.Throws("65-char patch_id rejected", "patch_id", () => TerrainDataCodec.ParsePatch(PatchJson("1", "\"" + "a" + new string('z', 64) + "\"", GoldBJson, GoldBPatchHeights)));

        // 嵌套 base 同样严格。
        Check.Throws("base missing field reports base.version", "base.version", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("\"version\":9,", ""), GoldBPatchHeights)));
        Check.Throws("base unknown field reports base.patch_id", "base.patch_id", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("{", "{\"patch_id\":\"x\","), GoldBPatchHeights)));
        Check.Throws("base duplicate field", "duplicate", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("\"region_id\":\"sample-b\"", "\"region_id\":\"sample-b\",\"region_id\":\"sample-b\""), GoldBPatchHeights)));
        Check.Throws("base schema_version must be 1", "base.schema_version", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("\"schema_version\":1", "\"schema_version\":2"), GoldBPatchHeights)));
        Check.Throws("base version no float allowed", "base.version", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("\"version\":9", "\"version\":9.0"), GoldBPatchHeights)));
        Check.Throws("base heights wrong length", "base.heights_m", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("[10,11,12,13,20,21,22,23,30,31,32,33]", "[10,11,12]"), GoldBPatchHeights)));
        Check.Throws("base region_id case-sensitive", "base.region_id", () =>
            TerrainDataCodec.ParsePatch(PatchJson("1", "\"patch-b\"", GoldBJson.Replace("\"sample-b\"", "\"Sample-b\""), GoldBPatchHeights)));

        // 文档类型不可互换。
        Check.Throws("ParsePatch rejects snapshot JSON", "unknown", () => TerrainDataCodec.ParsePatch(GoldAJson));
        Check.Throws("ParseSnapshot rejects patch JSON", "patch_id", () => TerrainDataCodec.ParseSnapshot(GoldBPatchJson));
    }

    private static void IntegerSyntax()
    {
        Check.Throws("schema_version 1.0 rejected", "schema_version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1.0", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("schema_version 1e0 rejected", "schema_version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1e0", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("version 7.0 rejected", "version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "7.0", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("version 7e0 rejected", "version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "7e0", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("version beyond 2^53-1 rejected", "version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "9007199254740992", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("version overflowing long rejected", "version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "12345678901234567890123", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("rows 2.0 rejected", "rows", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2.0", "2", "[0,0,0,0]")));
        Check.Throws("columns 3e0 rejected", "columns", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "3e0", "[0,0,0,0]")));
        Check.Ok("version 0 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "0", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Ok("version 9007199254740991 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "9007199254740991", "0", "0", "1", "2", "2", "[0,0,0,0]")));
    }

    private static void RangesAndLimits()
    {
        Check.Throws("rows 1 rejected", "rows", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "1", "2", "[0,0]")));
        Check.Throws("rows 258 rejected", "rows", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "258", "2", "[" + ZerosCsv(516) + "]")));
        Check.Throws("columns 258 rejected", "columns", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "258", "[" + ZerosCsv(516) + "]")));
        Check.Ok("rows 257 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "257", "2", "[" + ZerosCsv(514) + "]")));
        Check.Ok("columns 257 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "257", "[" + ZerosCsv(514) + "]")));

        Check.Throws("spacing 0.0099 rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "0.0099", "2", "2", "[0,0,0,0]")));
        Check.Throws("spacing 100.5 rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "100.5", "2", "2", "[0,0,0,0]")));
        Check.Throws("spacing 0 rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "0", "2", "2", "[0,0,0,0]")));
        Check.Throws("spacing -0 rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "-0", "2", "2", "[0,0,0,0]")));
        Check.Throws("spacing -0.5 rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "-0.5", "2", "2", "[0,0,0,0]")));
        Check.Throws("spacing 1e400 (infinite) rejected", "spacing_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1e400", "2", "2", "[0,0,0,0]")));
        Check.Ok("spacing 0.01 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "0.01", "2", "2", "[0,0,0,0]")));
        Check.Ok("spacing 100 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "100", "2", "2", "[0,0,0,0]")));

        Check.Throws("origin_x 10000.5 rejected", "origin_x_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "10000.5", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("origin_z -10000.5 rejected", "origin_z_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "-10000.5", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("origin_x 1e400 rejected", "origin_x_m", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "1e400", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Ok("origins ±10000 accepted", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "10000", "-10000", "1", "2", "2", "[0,0,0,0]")));

        Check.Throws("height 1000.5 rejected", "heights_m[0]", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[1000.5,0,0,0]")));
        Check.Throws("height -1000.5 rejected", "heights_m[1]", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,-1000.5,0,0]")));
        Check.Throws("height 1e400 rejected", "heights_m[2]", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,1e400,0]")));
        Check.Ok("heights ±1000 and negative interior accepted", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "3", "[1000,-1000,0,-1,1,-2]"));
            Bits(-2.0, snapshot.HeightsM[5], "height -2");
        });
        Check.Ok("underflowing 1e-400 height parses as zero", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[1e-400,0,0,0]"));
            Bits(0.0, snapshot.HeightsM[0], "underflow height");
        });

        Check.Throws("schema_version 0 rejected", "schema_version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("0", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("schema_version 2 rejected", "schema_version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("2", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));
        Check.Throws("schema_version -1 rejected", "schema_version", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("-1", "\"r\"", "1", "0", "0", "1", "2", "2", "[0,0,0,0]")));

        // 长度。
        Check.Throws("empty heights for 2×2 rejected", "exactly 4", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[]")));
        Check.Throws("heights short by one rejected", "exactly 4", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[1,2,3]")));
        Check.Throws("heights long by one rejected", "exactly 4", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[1,2,3,4,5]")));
        Check.Ok("257×257 heights accepted", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"big\"", "1", "10000", "10000", "100", "257", "257", "[" + ZerosCsv(66049) + "]"));
            Eq(66049, snapshot.HeightsM.Count, "height count");
        });
        Check.Throws("257×257 minus one height rejected", "exactly 66049", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("1", "\"big\"", "1", "0", "0", "1", "257", "257", "[" + ZerosCsv(66048) + "]")));

        // 4194304 UTF-16 字符上限（含边界）。
        string minimal = SnapshotJson("1", "\"r\"", "1", "0", "0", "1", "2", "2", "[1,2,3,4]");
        string atLimit = minimal + new string(' ', 4194304 - minimal.Length);
        Check.Ok("json of exactly 4194304 chars accepted", () =>
        {
            Eq(4194304, atLimit.Length, "padded length");
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(atLimit);
            Bits(3.0, snapshot.HeightsM[2], "padded json height");
        });
        Check.Throws("json of 4194305 chars rejected", "4194304", () =>
            TerrainDataCodec.ParseSnapshot(atLimit + " "));
        Check.Throws("null json rejected", "json", () => TerrainDataCodec.ParseSnapshot(null!));
        Check.Throws("empty json rejected", "json", () => TerrainDataCodec.ParseSnapshot(""));
    }

    private static void DamagedJson()
    {
        string[] damaged =
        {
            " ", "{", "}", "[]", "null", "42", "\"text\"", "{,}", "{\"a\":1}",
            GoldAJson[..^1], // 截断最后一个字符
            GoldAJson + " {",
            GoldAJson.Replace("[1,2,4,8,16,32]", "[1,2,4,8,16,32,]"), // 尾逗号
            GoldAJson.Replace("{", "/*x*/{"), // 注释
            GoldAJson.Replace("[1,2,4,8,16,32]", "[01,2,4,8,16,32]"), // 前导零
            GoldAJson.Replace("[1,2,4,8,16,32]", "[+1,2,4,8,16,32]"),
            GoldAJson.Replace("[1,2,4,8,16,32]", "[1.,2,4,8,16,32]"),
            GoldAJson.Replace("[1,2,4,8,16,32]", "[.5,2,4,8,16,32]"),
            GoldAJson.Replace("[1,2,4,8,16,32]", "[NaN,2,4,8,16,32]"),
            GoldAJson.Replace("[1,2,4,8,16,32]", "[Infinity,2,4,8,16,32]"),
            GoldAJson.Replace("[1,2,4,8,16,32]", "[-Infinity,2,4,8,16,32]"),
            GoldAJson.Replace("\"sample-a\"", "'sample-a'"), // 单引号
            GoldAJson.Replace("\"sample-a\"", "\"sample-a\"x"),
            GoldAJson.Replace("{", "{,"), // 首个成员前有多余逗号
        };
        for (int index = 0; index < damaged.Length; index++)
        {
            int captured = index;
            Check.Throws($"damaged json #{captured} rejected", "", () => TerrainDataCodec.ParseSnapshot(damaged[captured]));
        }
        Check.Throws("whitespace-only json rejected", "", () => TerrainDataCodec.ParseSnapshot("   "));
        Check.Throws("empty patch json rejected", "", () => TerrainDataCodec.ParsePatch(""));
        Check.Throws("truncated patch rejected", "", () => TerrainDataCodec.ParsePatch(GoldBPatchJson[..^1]));
    }

    private static void RoundTrips()
    {
        Check.Ok("tricky doubles round trip bit-exact", () =>
        {
            string json = SnapshotJson("1", "\"tricky\"", "3", "-9999.5", "9999.5", "0.01", "2", "3", "[0.1,-0.25,1e-7,999.125,-999.5,0]");
            TerrainSnapshot parsed = TerrainDataCodec.ParseSnapshot(json);
            TerrainSnapshot round = TerrainDataCodec.ParseSnapshot(TerrainDataCodec.Serialize(parsed));
            Bits(parsed.OriginXM, round.OriginXM, "origin_x round trip");
            Bits(parsed.OriginZM, round.OriginZM, "origin_z round trip");
            Bits(parsed.SpacingM, round.SpacingM, "spacing round trip");
            for (int index = 0; index < 6; index++)
                Bits(parsed.HeightsM[index], round.HeightsM[index], $"height {index} round trip");
            Bits(1e-7, round.HeightsM[2], "1e-7 exact");
        });

        Check.Ok("negative zero height serializes and round trips with sign", () =>
        {
            string json = SnapshotJson("1", "\"negzero\"", "1", "0", "0", "1", "2", "2", "[-0,1,2,3]");
            TerrainSnapshot parsed = TerrainDataCodec.ParseSnapshot(json);
            string serialized = TerrainDataCodec.Serialize(parsed);
            if (!serialized.Contains("[-0,", StringComparison.Ordinal))
                throw new Exception($"expected '-0' in serialized output: {serialized}");
            Bits(-0.0, TerrainDataCodec.ParseSnapshot(serialized).HeightsM[0], "-0 round trip");
        });

        Check.Ok("exponent form serializes as valid JSON and round trips", () =>
        {
            string json = SnapshotJson("1", "\"expo\"", "1", "0", "0", "1", "2", "2", "[1.5e-8,0,0,0]");
            TerrainSnapshot parsed = TerrainDataCodec.ParseSnapshot(json);
            string serialized = TerrainDataCodec.Serialize(parsed);
            if (!serialized.Contains("E-", StringComparison.Ordinal))
                throw new Exception($"expected exponent form in serialized output: {serialized}");
            Bits(1.5e-8, TerrainDataCodec.ParseSnapshot(serialized).HeightsM[0], "1.5e-8 round trip");
        });

        Check.Throws("Serialize(null snapshot) rejected", "value", () => TerrainDataCodec.Serialize((TerrainSnapshot)null!));
        Check.Throws("Serialize(null patch) rejected", "value", () => TerrainDataCodec.Serialize((TerrainPatch)null!));
    }

    private static void NoSideEffects()
    {
        Check.Ok("failed parse leaves no state; next parse succeeds", () =>
        {
            string broken = PatchJson("1", "\"patch-b\"", GoldBJson, PatchBHeightsWith(3, 13.5)); // 外围改动
            try
            {
                TerrainDataCodec.ParsePatch(broken);
                throw new Exception("expected perimeter rejection");
            }
            catch (ArgumentException)
            {
            }
            Bits(0.0, ParsePatchB().HeightsM[5], "interior after failed parse");
            Bits(33.0, ParseB().GetHeight(2, 3), "snapshot after failed parse");
        });

        Check.Ok("input json strings unchanged by failed parses", () =>
        {
            string broken = "{not json";
            string copy = new string(broken.ToCharArray());
            try
            {
                TerrainDataCodec.ParseSnapshot(broken);
                throw new Exception("expected parse failure");
            }
            catch (ArgumentException)
            {
            }
            Eq(copy, broken, "input string content");
        });
    }
}
