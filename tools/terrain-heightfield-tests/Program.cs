using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Yudian.Terrain;

namespace Tests;

/// <summary>最小检查器：独立 console，无测试框架；非 0 退出码表示存在失败。</summary>
internal static class Check
{
    public static int Total;
    public static int Failed;

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

    /// <summary>断言调用抛出精确 ArgumentException（合同统一异常类型），且消息包含指定片段。</summary>
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
}

internal static class AssertHelp
{
    public static void Eq(double expected, double actual, string what)
    {
        if (expected != actual)
            throw new Exception($"{what}: expected {expected.ToString("R", CultureInfo.InvariantCulture)}, got {actual.ToString("R", CultureInfo.InvariantCulture)}");
    }

    public static void Close(double expected, double actual, double tolerance, string what)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"{what}: |{expected.ToString("R", CultureInfo.InvariantCulture)} - {actual.ToString("R", CultureInfo.InvariantCulture)}| > {tolerance}");
    }

    public static void True(bool condition, string what)
    {
        if (!condition)
            throw new Exception(what);
    }

    public static void VertexEq(TerrainVertex v, double x, double y, double z, string what)
    {
        Eq(x, v.X, $"{what}.X");
        Eq(y, v.Y, $"{what}.Y");
        Eq(z, v.Z, $"{what}.Z");
    }

    public static void MeshShape(TerrainMesh mesh, int fineRows, int fineColumns)
    {
        Eq(fineRows, mesh.GridRows, "GridRows");
        Eq(fineColumns, mesh.GridColumns, "GridColumns");
        Eq(fineRows * fineColumns, mesh.Vertices.Count, "vertex count");
        Eq(6L * (fineRows - 1) * (fineColumns - 1), mesh.Indices.Count, "index count");
    }

    public static void IndicesEq(TerrainMesh mesh, int[] expected)
    {
        Eq(expected.Length, mesh.Indices.Count, "index count");
        for (int i = 0; i < expected.Length; i++)
            Eq(expected[i], mesh.Indices[i], $"indices[{i}]");
    }

    /// <summary>独立按 XZ 重心坐标求三角形面高（不复用实现代码）；query 必须落在三角形内或边上。</summary>
    public static double FaceHeightAt(TerrainMesh mesh, int i0, int i1, int i2, double qx, double qz, out bool contains)
    {
        TerrainVertex v0 = mesh.Vertices[i0], v1 = mesh.Vertices[i1], v2 = mesh.Vertices[i2];
        double det = (v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z);
        double w0 = ((v1.Z - v2.Z) * (qx - v2.X) + (v2.X - v1.X) * (qz - v2.Z)) / det;
        double w1 = ((v2.Z - v0.Z) * (qx - v2.X) + (v0.X - v2.X) * (qz - v2.Z)) / det;
        double w2 = 1.0 - w0 - w1;
        const double eps = 1e-9;
        contains = w0 >= -eps && w1 >= -eps && w2 >= -eps;
        return w0 * v0.Y + w1 * v1.Y + w2 * v2.Y;
    }

    /// <summary>断言调用抛出精确 ArgumentException（主控 Buffer 统一索引异常）。</summary>
    public static void ThrowsArgument(Action call)
    {
        try
        {
            call();
        }
        catch (ArgumentException ex)
        {
            if (ex.GetType() != typeof(ArgumentException))
                throw new Exception($"expected exact ArgumentException, got {ex.GetType().Name}");
            return;
        }
        throw new Exception("expected ArgumentException, but nothing was thrown");
    }

    /// <summary>收集索引缓冲中两端点 X 都等于 borderX 的实际三角边（重复对不去重，取值一致）。</summary>
    public static List<(int A, int B)> BorderEdges(TerrainMesh mesh, double borderX)
    {
        var edges = new List<(int, int)>();
        IReadOnlyList<int> indices = mesh.Indices;
        for (int tri = 0; tri < indices.Count; tri += 3)
        {
            Collect(mesh, indices[tri], indices[tri + 1], borderX, edges);
            Collect(mesh, indices[tri + 1], indices[tri + 2], borderX, edges);
            Collect(mesh, indices[tri + 2], indices[tri], borderX, edges);
        }
        return edges;
    }

    private static void Collect(TerrainMesh mesh, int i, int j, double borderX, List<(int, int)> edges)
    {
        if (mesh.Vertices[i].X == borderX && mesh.Vertices[j].X == borderX)
            edges.Add((i, j));
    }

    /// <summary>在边上按参数线性插值世界位置。</summary>
    public static (double X, double Y, double Z) EdgePointAt(TerrainMesh mesh, List<(int A, int B)> edges, double z)
    {
        foreach ((int a, int b) in edges)
        {
            TerrainVertex va = mesh.Vertices[a], vb = mesh.Vertices[b];
            double za = va.Z, zb = vb.Z;
            double lo = Math.Min(za, zb), hi = Math.Max(za, zb);
            if (z < lo || z > hi)
                continue;
            double t = Math.Abs(zb - za) < 1e-12 ? 0.0 : (z - za) / (zb - za);
            return (va.X + t * (vb.X - va.X), va.Y + t * (vb.Y - va.Y), za + t * (zb - za));
        }
        throw new Exception($"no border edge spans z={z.ToString(CultureInfo.InvariantCulture)}");
    }
}

internal static class HeightfieldMeshTests
{
    // 数据 r1 金样 sample-a：2×3 非对称，origin(-1.5,2)、spacing 0.5，(0,2)=(-0.5,4,2)。
    private const string GoldAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private const string GoldAPatchJson =
        "{\"schema_version\":1,\"patch_id\":\"patch-a\",\"base\":" + GoldAJson + ",\"heights_m\":[1,2,4,8,16,32]}";

    // 数据 r1 金样 sample-b：3×4，origin(-2,3)、spacing 1；patch-b 仅内部两点 21→0、22→-1。
    private const string GoldBJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBPatchJson =
        "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":" + GoldBJson + ",\"heights_m\":[10,11,12,13,20,0,-1,23,30,31,32,33]}";

    // 同 version=9 但内部内容与 base 不同：只版本一致仍须拒绝。
    private const string GoldBSameVersionDifferentContentJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,99,22,23,30,31,32,33]}";

    private const string GoldBExpiredVersionJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":8,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBWrongRegionJson =
        "{\"schema_version\":1,\"region_id\":\"sample-c\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBWrongLayoutJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":5,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33,1,2,3]}";

    private const string WarpedJson =
        "{\"schema_version\":1,\"region_id\":\"warp\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,\"spacing_m\":1,\"rows\":2,\"columns\":2,\"heights_m\":[0,0,0,4]}";

    // 相邻两个 3×3：left origin(0,0)，right origin(2,0)，spacing 1；共享边界列沿 Z 为 [3,5,9]（非零边界），内部互不相同。
    private const string NeighborLeftJson =
        "{\"schema_version\":1,\"region_id\":\"left-zone\",\"version\":3,\"origin_x_m\":0,\"origin_z_m\":0,\"spacing_m\":1,\"rows\":3,\"columns\":3,\"heights_m\":[0,1,3,4,5,5,7,8,9]}";

    private const string NeighborRightJson =
        "{\"schema_version\":1,\"region_id\":\"right-zone\",\"version\":4,\"origin_x_m\":2,\"origin_z_m\":0,\"spacing_m\":1,\"rows\":3,\"columns\":3,\"heights_m\":[3,10,11,5,12,13,9,14,15]}";

    public static int RunAll()
    {
        GoldenSampleASnapshot();
        GoldenSampleAPatchIdentity();
        GoldenSampleBPatch();
        PatchValidation();
        WarpedCell();
        NeighborSeam();
        SizesAndWinding();
        Determinism();
        Immutability();
        NullArguments();
        return Check.Total;
    }

    private static void GoldenSampleASnapshot()
    {
        TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(GoldAJson);
        Check.Ok("gold-a: shape 3x5 = 15 vertices / 48 indices", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            AssertHelp.MeshShape(mesh, 3, 5);
        });
        Check.Ok("gold-a: all 15 vertices exact (asymmetric, no row/col transpose)", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            double[] xs = { -1.5, -1.25, -1.0, -0.75, -0.5 };
            double[] zs = { 2, 2.25, 2.5 };
            double[][] ys =
            {
                new[] { 1.0, 1.5, 2.0, 3.0, 4.0 },
                new[] { 4.5, 6.75, 9.0, 13.5, 18.0 },
                new[] { 8.0, 12.0, 16.0, 24.0, 32.0 },
            };
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 5; column++)
                    AssertHelp.VertexEq(mesh.Vertices[row * 5 + column], xs[column], ys[row][column], zs[row], $"fine({row},{column})");
            AssertHelp.VertexEq(mesh.Vertices[4], -0.5, 4, 2, "original (0,2)");
            AssertHelp.VertexEq(mesh.Vertices[6], -1.25, 6.75, 2.25, "fine center (1,1)");
        });
        Check.Ok("gold-a: all 48 indices exact, fixed a-d diagonal", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            AssertHelp.IndicesEq(mesh, new[]
            {
                0, 6, 1, 0, 5, 6,
                1, 7, 2, 1, 6, 7,
                2, 8, 3, 2, 7, 8,
                3, 9, 4, 3, 8, 9,
                5, 11, 6, 5, 10, 11,
                6, 12, 7, 6, 11, 12,
                7, 13, 8, 7, 12, 13,
                8, 14, 9, 8, 13, 14,
            });
        });
        Check.Ok("gold-a: every triangle has positive Y winding in XZ projection", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            AllWindingPositive(mesh);
        });
    }

    private static void GoldenSampleAPatchIdentity()
    {
        Check.Ok("gold-a: completely unchanged patch is legal and matches base geometry", () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldAPatchJson);
            TerrainMesh fromBase = HeightfieldMeshBuilder.Build(patch.Base);
            TerrainMesh fromPatch = HeightfieldMeshBuilder.Build(patch, patch.Base);
            AssertHelp.MeshShape(fromPatch, 3, 5);
            for (int i = 0; i < fromBase.Vertices.Count; i++)
                AssertHelp.VertexEq(fromPatch.Vertices[i], fromBase.Vertices[i].X, fromBase.Vertices[i].Y, fromBase.Vertices[i].Z, $"vertex[{i}]");
            for (int i = 0; i < fromBase.Indices.Count; i++)
                AssertHelp.Eq(fromBase.Indices[i], fromPatch.Indices[i], $"index[{i}]");
        });
    }

    private static void GoldenSampleBPatch()
    {
        TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        string patchJsonBefore = TerrainDataCodec.Serialize(patch);
        string baseJsonBefore = TerrainDataCodec.Serialize(patch.Base);
        Check.Ok("gold-b: patch build shape 5x7 = 35 vertices / 144 indices", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(patch, patch.Base);
            AssertHelp.MeshShape(mesh, 5, 7);
        });
        Check.Ok("gold-b: all original nodes copy candidate heights (interior 0/-1 kept)", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(patch, patch.Base);
            double[] candidate = { 10, 11, 12, 13, 20, 0, -1, 23, 30, 31, 32, 33 };
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 4; column++)
                    AssertHelp.Eq(candidate[row * 4 + column], mesh.Vertices[(2 * row) * 7 + 2 * column].Y, $"original ({row},{column}) height");
            AssertHelp.Eq(-0.5, mesh.Vertices[2 * 7 + 3].Y, "midpoint between 0 and -1");
            AssertHelp.Eq(15.5, mesh.Vertices[3 * 7 + 3].Y, "center mean of (0,-1,31,32)");
            AssertHelp.Eq(21.75, mesh.Vertices[3 * 7 + 5].Y, "center mean of (-1,23,32,33)");
            AssertHelp.VertexEq(mesh.Vertices[4 * 7 + 6], 1, 33, 5, "original (2,3) world position");
        });
        Check.Ok("gold-b: inputs unchanged after successful build (serialize round-trip)", () =>
        {
            AssertHelp.True(patchJsonBefore == TerrainDataCodec.Serialize(patch), "patch JSON changed");
            AssertHelp.True(baseJsonBefore == TerrainDataCodec.Serialize(patch.Base), "base JSON changed");
        });
    }

    private static void PatchValidation()
    {
        TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        TerrainSnapshot baseSnapshot = patch.Base;
        string patchJsonBefore = TerrainDataCodec.Serialize(patch);
        string baseJsonBefore = TerrainDataCodec.Serialize(baseSnapshot);

        Check.Throws("expired version rejected", "version",
            () => HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBExpiredVersionJson)));
        Check.Throws("wrong region rejected", "region_id",
            () => HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBWrongRegionJson)));
        Check.Throws("same version but different content rejected", "heights_m[5]",
            () => HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBSameVersionDifferentContentJson)));
        Check.Throws("layout mismatch rejected", "columns",
            () => HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBWrongLayoutJson)));
        Check.Ok("failed validations leave patch and base unchanged", () =>
        {
            AssertHelp.True(patchJsonBefore == TerrainDataCodec.Serialize(patch), "patch JSON changed after failures");
            AssertHelp.True(baseJsonBefore == TerrainDataCodec.Serialize(baseSnapshot), "base JSON changed after failures");
        });
    }

    private static void WarpedCell()
    {
        TerrainSnapshot warped = TerrainDataCodec.ParseSnapshot(WarpedJson);
        Check.Ok("warp: 2x2 -> 3x3, 9 vertices / 24 indices, a-d diagonal pattern", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(warped);
            AssertHelp.MeshShape(mesh, 3, 3);
            // 2×2 输出金样 [0,3,1,0,2,3] 是单 cell 的 a,d,b,a,c,d 模式；3×3 输出（2×2 cells）每 cell 同模式、步长 3：
            AssertHelp.IndicesEq(mesh, new[]
            {
                0, 4, 1, 0, 3, 4,
                1, 5, 2, 1, 4, 5,
                3, 7, 4, 3, 6, 7,
                4, 8, 5, 4, 7, 8,
            });
        });
        Check.Ok("warp: all 9 vertices exact (center = mean of four corners = 1)", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(warped);
            AssertHelp.VertexEq(mesh.Vertices[0], 0, 0, 0, "fine(0,0)");
            AssertHelp.VertexEq(mesh.Vertices[1], 0.5, 0, 0, "fine(0,1)");
            AssertHelp.VertexEq(mesh.Vertices[2], 1, 0, 0, "fine(0,2)");
            AssertHelp.VertexEq(mesh.Vertices[3], 0, 0, 0.5, "fine(1,0)");
            AssertHelp.VertexEq(mesh.Vertices[4], 0.5, 1, 0.5, "fine(1,1) center");
            AssertHelp.VertexEq(mesh.Vertices[5], 1, 2, 0.5, "fine(1,2)");
            AssertHelp.VertexEq(mesh.Vertices[6], 0, 0, 1, "fine(2,0)");
            AssertHelp.VertexEq(mesh.Vertices[7], 0.5, 2, 1, "fine(2,1)");
            AssertHelp.VertexEq(mesh.Vertices[8], 1, 4, 1, "fine(2,2)");
        });
        Check.Ok("warp: face at (.75,.25) is planar height 1, not exact bilinear .75", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(warped);
            int found = 0;
            for (int tri = 0; tri < mesh.Indices.Count; tri += 3)
            {
                double height = AssertHelp.FaceHeightAt(mesh, mesh.Indices[tri], mesh.Indices[tri + 1], mesh.Indices[tri + 2], 0.75, 0.25, out bool contains);
                if (!contains)
                    continue;
                found++;
                AssertHelp.Close(1.0, height, 1e-9, "triangle face height at (0.75,0.25)");
            }
            AssertHelp.Eq(2, found, "triangles containing (0.75,0.25) on the shared a-d diagonal");
            // 独立重算精确双线性（四角 0,0,0,4 在 (0.75,0.25)）：0.75*0.25*4。
            double exactBilinear = 0.75 * 0.25 * 4.0;
            AssertHelp.Close(0.75, exactBilinear, 1e-12, "exact bilinear reference");
            AssertHelp.True(Math.Abs(exactBilinear - 1.0) > 0.2, "planar faces must not collapse into the bilinear surface");
        });
    }

    private static void NeighborSeam()
    {
        TerrainMesh left = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(NeighborLeftJson));
        TerrainMesh right = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(NeighborRightJson));
        List<(int, int)> leftEdges = AssertHelp.BorderEdges(left, 2.0);
        List<(int, int)> rightEdges = AssertHelp.BorderEdges(right, 2.0);
        Check.Ok("neighbor: both candidates have real border triangle edges on x=2", () =>
        {
            // 每 cell 仅三角形 (a,d,b) 的 (d,b) 边落在边界列上，3×3 输出 4 个边界 cell → 各恰 4 条。
            AssertHelp.Eq(4, leftEdges.Count, "left border edges");
            AssertHelp.Eq(4, rightEdges.Count, "right border edges");
        });
        // 沿共享边采样：原节点 z=0,1,2、中点 z=0.5,1.5，以及边内参数 0.25/0.75（即 z=0.25,0.75… 按 2 米边参数 0.125/0.375 处也覆盖）。
        double[] samples = { 0, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2 };
        double[] expectedHeights = { 3, 3.5, 4, 4.5, 5, 6, 7, 8, 9 };
        for (int i = 0; i < samples.Length; i++)
        {
            double z = samples[i];
            double expectedY = expectedHeights[i];
            int index = i;
            Check.Ok($"neighbor: shared edge world XYZ matches at z={z.ToString(CultureInfo.InvariantCulture)}", () =>
            {
                (double lx, double ly, double lz) = AssertHelp.EdgePointAt(left, leftEdges, z);
                (double rx, double ry, double rz) = AssertHelp.EdgePointAt(right, rightEdges, z);
                AssertHelp.Close(lx, rx, 1e-9, $"left/right X at z={z}");
                AssertHelp.Close(ly, ry, 1e-9, $"left/right Y at z={z}");
                AssertHelp.Close(lz, rz, 1e-9, $"left/right Z at z={z}");
                AssertHelp.Eq(2, lx, $"left X on shared edge at z={z}");
                AssertHelp.Eq(expectedY, ly, $"left piecewise-linear height at z={z}");
                AssertHelp.Eq(expectedY, ry, $"right piecewise-linear height at z={z}");
            });
        }
    }

    private static void SizesAndWinding()
    {
        Check.Ok("min size: 2x2 snapshot builds 3x3 with exact pattern indices", () =>
        {
            const string json = "{\"schema_version\":1,\"region_id\":\"min\",\"version\":0,\"origin_x_m\":5,\"origin_z_m\":-7,\"spacing_m\":100,\"rows\":2,\"columns\":2,\"heights_m\":[5,-3,0,2]}";
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(json));
            AssertHelp.MeshShape(mesh, 3, 3);
            AssertHelp.IndicesEq(mesh, new[]
            {
                0, 4, 1, 0, 3, 4,
                1, 5, 2, 1, 4, 5,
                3, 7, 4, 3, 6, 7,
                4, 8, 5, 4, 7, 8,
            });
            AssertHelp.VertexEq(mesh.Vertices[8], 105, 2, 93, "fine(2,2)");
        });
        Check.Ok("max size: 257x257 snapshot builds 513x513 = 263169 vertices / 1572864 indices", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(BuildMaxJson());
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            AssertHelp.MeshShape(mesh, 513, 513);
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                TerrainVertex v = mesh.Vertices[i];
                AssertHelp.True(double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z), $"vertex {i} finite");
            }
            for (int i = 0; i < mesh.Indices.Count; i++)
                AssertHelp.True(mesh.Indices[i] >= 0 && mesh.Indices[i] < mesh.Vertices.Count, $"index {i} in range");
        });
        Check.Ok("max size: every cell keeps positive Y winding (all 1572864 indices)", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(BuildMaxJson()));
            AllWindingPositive(mesh);
        });
        Check.Ok("max size: corner world positions exact", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(BuildMaxJson());
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(snapshot);
            AssertHelp.VertexEq(mesh.Vertices[0], -13.5, snapshot.GetHeight(0, 0), 7.25, "fine(0,0)");
            AssertHelp.VertexEq(mesh.Vertices[513 * 513 - 1], 178.5, snapshot.GetHeight(256, 256), 199.25, "fine(512,512)");
        });
    }

    private static void Determinism()
    {
        Check.Ok("determinism: two snapshot builds identical vertex/index values, independent buffers", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(GoldAJson);
            TerrainMesh first = HeightfieldMeshBuilder.Build(snapshot);
            TerrainMesh second = HeightfieldMeshBuilder.Build(snapshot);
            AssertHelp.Eq(first.Vertices.Count, second.Vertices.Count, "vertex count");
            AssertHelp.Eq(first.Indices.Count, second.Indices.Count, "index count");
            for (int i = 0; i < first.Vertices.Count; i++)
            {
                AssertHelp.Eq(first.Vertices[i].X, second.Vertices[i].X, $"vertex {i} X");
                AssertHelp.Eq(first.Vertices[i].Y, second.Vertices[i].Y, $"vertex {i} Y");
                AssertHelp.Eq(first.Vertices[i].Z, second.Vertices[i].Z, $"vertex {i} Z");
            }
            for (int i = 0; i < first.Indices.Count; i++)
                AssertHelp.Eq(first.Indices[i], second.Indices[i], $"index {i}");
            AssertHelp.True(!ReferenceEquals(first.Vertices, second.Vertices), "vertex buffers must be distinct instances");
            AssertHelp.True(!ReferenceEquals(first.Indices, second.Indices), "index buffers must be distinct instances");
        });
        Check.Ok("determinism: two patch builds identical", () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
            TerrainMesh first = HeightfieldMeshBuilder.Build(patch, patch.Base);
            TerrainMesh second = HeightfieldMeshBuilder.Build(patch, patch.Base);
            for (int i = 0; i < first.Vertices.Count; i++)
                AssertHelp.Eq(first.Vertices[i].Y, second.Vertices[i].Y, $"vertex {i} Y");
            for (int i = 0; i < first.Indices.Count; i++)
                AssertHelp.Eq(first.Indices[i], second.Indices[i], $"index {i}");
        });
    }

    private static void Immutability()
    {
        Check.Ok("output: Vertices/Indices expose no array, IList, ICollection or SyncRoot", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(GoldAJson));
            AssertHelp.True(!(mesh.Vertices is TerrainVertex[]), "Vertices must not be an array");
            AssertHelp.True(!(mesh.Indices is int[]), "Indices must not be an array");
            AssertHelp.True(mesh.Vertices as IList<TerrainVertex> == null, "Vertices must not cast to IList<TerrainVertex>");
            AssertHelp.True(mesh.Indices as IList<int> == null, "Indices must not cast to IList<int>");
            AssertHelp.True(mesh.Vertices as ICollection == null, "Vertices must not cast to ICollection (SyncRoot)");
            AssertHelp.True(mesh.Indices as ICollection == null, "Indices must not cast to ICollection (SyncRoot)");
            AssertHelp.True(mesh.Vertices as ICollection<TerrainVertex> == null, "Vertices must not cast to ICollection<TerrainVertex>");
            AssertHelp.True(mesh.Indices as ICollection<int> == null, "Indices must not cast to ICollection<int>");
        });
        Check.Ok("output: out-of-range indexer throws ArgumentException", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(GoldAJson));
            AssertHelp.ThrowsArgument(() => _ = mesh.Vertices[-1]);
            AssertHelp.ThrowsArgument(() => _ = mesh.Vertices[mesh.Vertices.Count]);
            AssertHelp.ThrowsArgument(() => _ = mesh.Indices[mesh.Indices.Count]);
        });
        Check.Ok("output: IReadOnlyList enumeration and indexing work", () =>
        {
            TerrainMesh mesh = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(GoldAJson));
            int count = 0;
            foreach (TerrainVertex _ in mesh.Vertices)
                count++;
            foreach (int _ in mesh.Indices)
                count++;
            AssertHelp.Eq(mesh.Vertices.Count + mesh.Indices.Count, count, "enumerated items");
        });
        Check.Ok("input: snapshot/patch HeightsM never expose arrays or mutable interfaces", () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
            AssertHelp.True(!(patch.HeightsM is double[]), "patch HeightsM must not be an array");
            AssertHelp.True(!(patch.Base.HeightsM is double[]), "base HeightsM must not be an array");
            AssertHelp.True(patch.HeightsM as IList<double> == null, "patch HeightsM must not cast to IList<double>");
            AssertHelp.True(patch.Base.HeightsM as ICollection == null, "base HeightsM must not cast to ICollection (SyncRoot)");
            AssertHelp.True(patch.Base.HeightsM as ICollection<double> == null, "base HeightsM must not cast to ICollection<double>");
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(GoldAJson);
            AssertHelp.True(!(snapshot.HeightsM is double[]), "snapshot HeightsM must not be an array");
            AssertHelp.True(snapshot.HeightsM as IList<double> == null, "snapshot HeightsM must not cast to IList<double>");
        });
        Check.Ok("input: builds and failed validations never change GetHeight values", () =>
        {
            TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
            TerrainSnapshot baseSnapshot = patch.Base;
            double[] before = new double[baseSnapshot.HeightsM.Count];
            for (int i = 0; i < before.Length; i++)
                before[i] = baseSnapshot.HeightsM[i];
            _ = HeightfieldMeshBuilder.Build(baseSnapshot);
            try { _ = HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBExpiredVersionJson)); }
            catch (ArgumentException) { }
            try { _ = HeightfieldMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBSameVersionDifferentContentJson)); }
            catch (ArgumentException) { }
            for (int row = 0; row < baseSnapshot.Rows; row++)
                for (int column = 0; column < baseSnapshot.Columns; column++)
                    AssertHelp.Eq(before[row * baseSnapshot.Columns + column], baseSnapshot.GetHeight(row, column), $"GetHeight({row},{column})");
        });
    }

    private static void NullArguments()
    {
        Check.Throws("Build(null snapshot) rejected", "snapshot", () => HeightfieldMeshBuilder.Build((TerrainSnapshot)null));
        Check.Throws("Build(null patch, current) rejected", "patch", () => HeightfieldMeshBuilder.Build(null, TerrainDataCodec.ParseSnapshot(GoldAJson)));
        Check.Throws("Build(patch, null current) rejected", "current", () => HeightfieldMeshBuilder.Build(TerrainDataCodec.ParsePatch(GoldAPatchJson), null));
        Check.Throws("Build(null, null) rejected", "patch", () => HeightfieldMeshBuilder.Build(null, null));
    }

    private static void AllWindingPositive(TerrainMesh mesh)
    {
        for (int cell = 0; cell < mesh.Indices.Count; cell += 6)
        {
            int ia = mesh.Indices[cell], id = mesh.Indices[cell + 1], ib = mesh.Indices[cell + 2];
            int ia2 = mesh.Indices[cell + 3], ic = mesh.Indices[cell + 4], id2 = mesh.Indices[cell + 5];
            AssertHelp.Eq(ia, ia2, "triangle 1 vertex a equals triangle 2 vertex a");
            AssertHelp.Eq(id, id2, "triangle 1 vertex d equals triangle 2 vertex d");
            TerrainVertex a = mesh.Vertices[ia], b = mesh.Vertices[ib], c = mesh.Vertices[ic], d = mesh.Vertices[id];
            // XZ 投影叉积 Y 分量：(z1-z0)*(x2-x0) - (x1-x0)*(z2-z0) > 0。
            double crossAdB = (d.Z - a.Z) * (b.X - a.X) - (d.X - a.X) * (b.Z - a.Z);
            double crossAcD = (c.Z - a.Z) * (d.X - a.X) - (c.X - a.X) * (d.Z - a.Z);
            AssertHelp.True(crossAdB > 0, $"cell at index {cell}: triangle (a,d,b) winding {crossAdB}");
            AssertHelp.True(crossAcD > 0, $"cell at index {cell}: triangle (a,c,d) winding {crossAcD}");
        }
    }

    private static string BuildMaxJson()
    {
        var json = new StringBuilder(128 + 66049 * 8);
        json.Append("{\"schema_version\":1,\"region_id\":\"max-grid\",\"version\":12,\"origin_x_m\":-13.5,\"origin_z_m\":7.25,\"spacing_m\":0.75,\"rows\":257,\"columns\":257,\"heights_m\":[");
        for (int row = 0; row < 257; row++)
        {
            for (int column = 0; column < 257; column++)
            {
                if (row > 0 || column > 0)
                    json.Append(',');
                json.Append((row * 31 + column * 17) % 201 - 100);
            }
        }
        json.Append("]}");
        return json.ToString();
    }
}

internal static class Program
{
    private static int Main()
    {
        int totalChecks = 0, totalFailed = 0;
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("de-DE") })
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            Console.WriteLine($"== culture {culture.Name} ==");
            Check.Total = 0;
            Check.Failed = 0;
            HeightfieldMeshTests.RunAll();
            Console.WriteLine($"-- {culture.Name}: {Check.Total} checks, {Check.Failed} failed --");
            totalChecks += Check.Total;
            totalFailed += Check.Failed;
        }
        Console.WriteLine($"TOTAL checks={totalChecks} failed={totalFailed}");
        return totalFailed == 0 ? 0 : 1;
    }
}
