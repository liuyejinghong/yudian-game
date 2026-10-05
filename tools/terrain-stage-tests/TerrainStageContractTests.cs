using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Yudian.Terrain;

namespace TerrainStage.Tests;

/// <summary>最小检查器：独立 console，无测试框架；非 0 退出码表示存在失败。</summary>
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
}

internal static class TerrainStageContractTests
{
    // 数据 r1 冻结金样（与 terrain-patch-r1.md 逐字一致）。
    private const string GoldAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private const string GoldBJson =
        "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";

    private const string GoldBPatchJson =
        "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":" + GoldBJson +
        ",\"heights_m\":[10,11,12,13,20,0,-1,23,30,31,32,33]}";

    private static readonly double[] GoldACandidateHeights = { 1, 2, 4, 8, 16, 32 };
    private static readonly double[] GoldBPatchHeights = { 10, 11, 12, 13, 20, 0, -1, 23, 30, 31, 32, 33 };
    private static readonly double[] GoldBBaseHeights = { 10, 11, 12, 13, 20, 21, 22, 23, 30, 31, 32, 33 };

    private static readonly double[] WarpHeights = { 0, 0, 0, 4 };
    private static readonly double[] LeftHeights = { 1, 1, 3, 1, 1, 5, 1, 1, 9 };
    private static readonly double[] RightHeights = { 3, 1, 1, 5, 1, 1, 9, 1, 1 };

    public static int RunAll()
    {
        Check.Failed = 0;
        NullArguments();
        GoldSampleA();
        GoldWarp2x2();
        GoldSampleBPatch();
        VersionAndContent();
        SizesAndLimits();
        NeighborEdge();
        ImmutabilityAndReadOnly();
        Determinism();
        return Check.Failed;
    }

    private static void NullArguments()
    {
        TerrainSnapshot snapshotA = TerrainDataCodec.ParseSnapshot(GoldAJson);
        TerrainPatch patchB = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        Check.Throws("null snapshot rejected", "snapshot", () => StageMeshBuilder.Build((TerrainSnapshot)null));
        Check.Throws("null patch rejected", "patch", () => StageMeshBuilder.Build(null, snapshotA));
        Check.Throws("null current rejected", "current", () => StageMeshBuilder.Build(patchB, null));
    }

    private static void GoldSampleA()
    {
        Check.Ok("sample-a 2x3: counts/positions/heights/literal indices", () =>
        {
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(GoldAJson));
            AssertGridValid(mesh, 2, 3, -1.5, 2, 0.5, GoldACandidateHeights);
            AssertIndices(mesh, 0, 4, 1, 0, 3, 4, 1, 5, 2, 1, 4, 5);
        });
        Check.Ok("sample-a: original node (0,2) is (-0.5,4,2)", () =>
        {
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(GoldAJson));
            TerrainVertex vertex = mesh.Vertices[0 * 3 + 2];
            Equal("X", -0.5, vertex.X);
            Equal("Y", 4.0, vertex.Y);
            Equal("Z", 2.0, vertex.Z);
        });
    }

    private static void GoldWarp2x2()
    {
        const string json =
            "{\"schema_version\":1,\"region_id\":\"warp\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,\"spacing_m\":1,\"rows\":2,\"columns\":2,\"heights_m\":[0,0,0,4]}";
        TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(json));
        Check.Ok("warp 2x2: golden indices [0,3,1,0,2,3]", () =>
            AssertIndices(mesh, 0, 3, 1, 0, 2, 3));
        Check.Ok("warp 2x2: triangle a,d,b surface height at (.5,.5) is 2", () =>
        {
            (bool hit, double y) = SurfaceHeight(mesh, 0, 0.5, 0.5);
            if (!hit) throw new Exception("point (.5,.5) not on triangle a,d,b");
            Equal("surface Y", 2.0, y);
        });
        Check.Ok("warp 2x2: triangle a,c,d surface height at (.5,.5) is 2", () =>
        {
            (bool hit, double y) = SurfaceHeight(mesh, 3, 0.5, 0.5);
            if (!hit) throw new Exception("point (.5,.5) not on triangle a,c,d");
            Equal("surface Y", 2.0, y);
        });
        Check.Ok("warp 2x2: grid/winding/finite valid", () =>
            AssertGridValid(mesh, 2, 2, 0, 0, 1, WarpHeights));
    }

    private static void GoldSampleBPatch()
    {
        TerrainSnapshot current = TerrainDataCodec.ParseSnapshot(GoldBJson);
        TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        Check.Ok("sample-b patch: 12 verts/36 indices, candidate heights, base layout", () =>
            AssertGridValid(StageMeshBuilder.Build(patch, current), 3, 4, -2, 3, 1, GoldBPatchHeights));
        Check.Ok("sample-b patch: full 36-index literal", () =>
            AssertIndices(StageMeshBuilder.Build(patch, current),
                0, 5, 1, 0, 4, 5,
                1, 6, 2, 1, 5, 6,
                2, 7, 3, 2, 6, 7,
                4, 9, 5, 4, 8, 9,
                5, 10, 6, 5, 9, 10,
                6, 11, 7, 6, 10, 11));
        Check.Ok("sample-b patch: perimeter nodes equal base heights", () =>
        {
            TerrainMesh mesh = StageMeshBuilder.Build(patch, current);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    bool perimeter = row == 0 || row == 2 || column == 0 || column == 3;
                    if (!perimeter)
                        continue;
                    int index = row * 4 + column;
                    Equal($"perimeter Y[{index}]", GoldBBaseHeights[index], mesh.Vertices[index].Y);
                }
            }
        });
    }

    private static void VersionAndContent()
    {
        TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        Check.Throws("same version but different content rejected", "base.heights_m[5]", () =>
            StageMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(
                "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,99,22,23,30,31,32,33]}")));
        Check.Throws("expired version rejected", "base.version", () =>
            StageMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(
                "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":10,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}")));
        Check.Throws("wrong region rejected", "base.region_id", () =>
            StageMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(
                "{\"schema_version\":1,\"region_id\":\"sample-c\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}")));
        Check.Throws("rows mismatch rejected", "base.rows", () =>
            StageMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(
                "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":4,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33,7,7,7,7]}")));
        Check.Ok("version and content both matching accepted", () =>
        {
            TerrainMesh mesh = StageMeshBuilder.Build(patch, TerrainDataCodec.ParseSnapshot(GoldBJson));
            if (mesh is null)
                throw new Exception("null mesh");
        });
    }

    private static void SizesAndLimits()
    {
        Check.Ok("min 2x2 builds with golden indices", () =>
        {
            double[] heights = PatternHeights(4, i => (i % 3) - 1);
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
                SnapshotJson("min", 2, 2, 0, 0, 1, heights)));
            AssertGridValid(mesh, 2, 2, 0, 0, 1, heights);
            AssertIndices(mesh, 0, 3, 1, 0, 2, 3);
        });
        Check.Ok("skinny 2x5 builds valid", () =>
        {
            double[] heights = PatternHeights(10, i => (i % 7) - 3);
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
                SnapshotJson("skinny-x", 2, 5, 0, 0, 1, heights)));
            AssertGridValid(mesh, 2, 5, 0, 0, 1, heights);
        });
        Check.Ok("skinny 5x2 builds valid", () =>
        {
            double[] heights = PatternHeights(10, i => (i % 7) - 3);
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
                SnapshotJson("skinny-z", 5, 2, 0, 0, 1, heights)));
            AssertGridValid(mesh, 5, 2, 0, 0, 1, heights);
        });
        Check.Ok("max 257x257: counts and every vertex/index valid", () =>
        {
            double[] heights = PatternHeights(66049, i => ((i % 17) - 8) * 0.5);
            TerrainMesh mesh = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
                SnapshotJson("max-grid", 257, 257, -100, -50, 0.5, heights)));
            Equal("vertex count", 66049, mesh.Vertices.Count);
            Equal("index count", 393216, mesh.Indices.Count);
            AssertGridValid(mesh, 257, 257, -100, -50, 0.5, heights);
        });
        Check.Throws("over-limit 258 rows rejected at data entry", "rows", () =>
            TerrainDataCodec.ParseSnapshot(SnapshotJson("too-big", 258, 2, 0, 0, 1, PatternHeights(516, i => 0))));
    }

    private static void NeighborEdge()
    {
        TerrainMesh left = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
            SnapshotJson("left", 3, 3, 0, 0, 1, LeftHeights)));
        TerrainMesh right = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(
            SnapshotJson("right", 3, 3, 2, 0, 1, RightHeights)));
        // 左块最后一条 cell 列的 a,d,b 三角含共享边 b-d；右块首条 cell 列的 a,c,d 三角含共享边 a-c。
        double[,] queries = { { 0.25, 3.5 }, { 0.75, 4.5 }, { 1.25, 6.0 }, { 1.75, 8.0 } };
        for (int q = 0; q < queries.GetLength(0); q++)
        {
            double z = queries[q, 0];
            double expectedY = queries[q, 1];
            int segment = z < 1.0 ? 0 : 1;
            int leftTriangle = 6 * (segment * 2 + 1);
            int rightTriangle = 6 * (segment * 2) + 3;
            Check.Ok($"neighbor edge at x=2, z={z.ToString(CultureInfo.InvariantCulture)}: both sides within 1e-9 of lerp and of each other", () =>
            {
                (bool leftHit, double leftY) = SurfaceHeight(left, leftTriangle, 2.0, z);
                (bool rightHit, double rightY) = SurfaceHeight(right, rightTriangle, 2.0, z);
                if (!leftHit || !rightHit)
                    throw new Exception("shared-edge point not covered by boundary triangles");
                if (Math.Abs(leftY - expectedY) > 1e-9 || Math.Abs(rightY - expectedY) > 1e-9)
                    throw new Exception(
                        $"edge height off: left {leftY.ToString(CultureInfo.InvariantCulture)}, right {rightY.ToString(CultureInfo.InvariantCulture)}, expected {expectedY.ToString(CultureInfo.InvariantCulture)}");
                if (Math.Abs(leftY - rightY) > 1e-9)
                    throw new Exception("left/right edge heights diverge over 1e-9");
            });
        }
    }

    private static void ImmutabilityAndReadOnly()
    {
        TerrainSnapshot current = TerrainDataCodec.ParseSnapshot(GoldBJson);
        TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
        TerrainMesh mesh = StageMeshBuilder.Build(patch, current);
        Check.Ok("output buffers and input lists expose no ICollection/SyncRoot", () =>
        {
            if (mesh.Vertices as ICollection != null)
                throw new Exception("Vertices exposes ICollection");
            if (mesh.Indices as ICollection != null)
                throw new Exception("Indices exposes ICollection");
            if (current.HeightsM as ICollection != null)
                throw new Exception("snapshot HeightsM exposes ICollection");
            if (patch.HeightsM as ICollection != null)
                throw new Exception("patch HeightsM exposes ICollection");
            if (patch.Base.HeightsM as ICollection != null)
                throw new Exception("base HeightsM exposes ICollection");
        });
        Check.Ok("mesh buffer indexers reject out-of-range with ArgumentException", () =>
        {
            ThrowsArgument("Vertices[-1]", () => _ = mesh.Vertices[-1]);
            ThrowsArgument("Vertices[Count]", () => _ = mesh.Vertices[mesh.Vertices.Count]);
            ThrowsArgument("Indices[-1]", () => _ = mesh.Indices[-1]);
            ThrowsArgument("Indices[Count]", () => _ = mesh.Indices[mesh.Indices.Count]);
        });
        Check.Ok("snapshot GetHeight rejects out-of-range with ArgumentException", () =>
        {
            ThrowsArgument("row -1", () => _ = current.GetHeight(-1, 0));
            ThrowsArgument("column 4", () => _ = current.GetHeight(0, 4));
        });
        Check.Ok("build does not mutate snapshot/patch/base heights", () =>
        {
            double[] beforeSnapshot = Copy(current.HeightsM);
            double[] beforePatch = Copy(patch.HeightsM);
            double[] beforeBase = Copy(patch.Base.HeightsM);
            _ = StageMeshBuilder.Build(current);
            _ = StageMeshBuilder.Build(patch, current);
            AssertSequences("snapshot heights", beforeSnapshot, Copy(current.HeightsM));
            AssertSequences("patch heights", beforePatch, Copy(patch.HeightsM));
            AssertSequences("base heights", beforeBase, Copy(patch.Base.HeightsM));
        });
        Check.Ok("output enumeration is stable across repeats", () =>
        {
            TerrainVertex[] verticesOnce = Copy(mesh.Vertices);
            TerrainVertex[] verticesTwice = Copy(mesh.Vertices);
            AssertSequences("vertices", verticesOnce, verticesTwice);
            int[] indicesOnce = CopyIndices(mesh.Indices);
            int[] indicesTwice = CopyIndices(mesh.Indices);
            AssertSequences("indices", indicesOnce, indicesTwice);
        });
    }

    private static void Determinism()
    {
        Check.Ok("snapshot build is deterministic", () =>
        {
            TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(GoldAJson);
            EqualMesh(StageMeshBuilder.Build(snapshot), StageMeshBuilder.Build(snapshot));
        });
        Check.Ok("patch build is deterministic", () =>
        {
            TerrainSnapshot current = TerrainDataCodec.ParseSnapshot(GoldBJson);
            TerrainPatch patch = TerrainDataCodec.ParsePatch(GoldBPatchJson);
            EqualMesh(StageMeshBuilder.Build(patch, current), StageMeshBuilder.Build(patch, current));
        });
    }

    // ---- 断言与几何辅助 ----

    private static void Equal(string what, double expected, double actual)
    {
        if (expected != actual)
            throw new Exception(
                $"{what}: expected {expected.ToString(CultureInfo.InvariantCulture)}, got {actual.ToString(CultureInfo.InvariantCulture)}");
    }

    private static void Equal(string what, int expected, int actual)
    {
        if (expected != actual)
            throw new Exception($"{what}: expected {expected}, got {actual}");
    }

    private static void ThrowsArgument(string what, Action call)
    {
        try
        {
            call();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ArgumentException, got {ex.GetType().Name}");
        }
        throw new Exception($"{what}: expected ArgumentException, nothing was thrown");
    }

    /// <summary>全量几何校验：计数、每个顶点位置/高度/有限、全部索引范围、全部三角 XZ 投影正 Y 绕序。</summary>
    private static void AssertGridValid(TerrainMesh mesh, int rows, int columns,
        double originX, double originZ, double spacing, double[] expectedHeights)
    {
        Equal("GridRows", rows, mesh.GridRows);
        Equal("GridColumns", columns, mesh.GridColumns);
        Equal("vertex count", rows * columns, mesh.Vertices.Count);
        Equal("index count", 6 * (rows - 1) * (columns - 1), mesh.Indices.Count);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                TerrainVertex vertex = mesh.Vertices[index];
                if (!double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y) || !double.IsFinite(vertex.Z))
                    throw new Exception($"vertex {index} is not finite");
                Equal($"V[{index}].X", originX + column * spacing, vertex.X);
                Equal($"V[{index}].Y", expectedHeights[index], vertex.Y);
                Equal($"V[{index}].Z", originZ + row * spacing, vertex.Z);
            }
        }
        AssertWindingAndIndexRange(mesh);
    }

    private static void AssertWindingAndIndexRange(TerrainMesh mesh)
    {
        int vertexCount = mesh.Vertices.Count;
        for (int triangle = 0; triangle < mesh.Indices.Count; triangle += 3)
        {
            TerrainVertex p0 = mesh.Vertices[mesh.Indices[triangle]];
            TerrainVertex p1 = mesh.Vertices[mesh.Indices[triangle + 1]];
            TerrainVertex p2 = mesh.Vertices[mesh.Indices[triangle + 2]];
            // XZ 投影叉积 Y 分量：(p1-p0) × (p2-p0)，合同要求为正。
            double crossY = (p1.Z - p0.Z) * (p2.X - p0.X) - (p1.X - p0.X) * (p2.Z - p0.Z);
            if (crossY <= 0)
                throw new Exception($"triangle at {triangle} has non-positive Y winding: {crossY.ToString(CultureInfo.InvariantCulture)}");
            for (int k = 0; k < 3; k++)
            {
                int index = mesh.Indices[triangle + k];
                if (index < 0 || index >= vertexCount)
                    throw new Exception($"index {index} out of vertex range");
            }
        }
    }

    private static void AssertIndices(TerrainMesh mesh, params int[] expected)
    {
        Equal("index count", expected.Length, mesh.Indices.Count);
        for (int i = 0; i < expected.Length; i++)
            Equal($"Indices[{i}]", expected[i], mesh.Indices[i]);
    }

    private static void EqualMesh(TerrainMesh first, TerrainMesh second)
    {
        Equal("GridRows", first.GridRows, second.GridRows);
        Equal("GridColumns", first.GridColumns, second.GridColumns);
        Equal("vertex count", first.Vertices.Count, second.Vertices.Count);
        Equal("index count", first.Indices.Count, second.Indices.Count);
        for (int i = 0; i < first.Vertices.Count; i++)
        {
            if (first.Vertices[i] != second.Vertices[i])
                throw new Exception($"vertex {i} differs between builds");
        }
        for (int i = 0; i < first.Indices.Count; i++)
            Equal($"Indices[{i}]", first.Indices[i], second.Indices[i]);
    }

    /// <summary>按三角重心在 XZ 平面求表面高度；点不在三角形上时 hit=false。</summary>
    private static (bool hit, double y) SurfaceHeight(TerrainMesh mesh, int triangleStart, double x, double z)
    {
        TerrainVertex p0 = mesh.Vertices[mesh.Indices[triangleStart]];
        TerrainVertex p1 = mesh.Vertices[mesh.Indices[triangleStart + 1]];
        TerrainVertex p2 = mesh.Vertices[mesh.Indices[triangleStart + 2]];
        double det = (p1.Z - p2.Z) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Z - p2.Z);
        if (det == 0)
            return (false, 0);
        double l0 = ((p1.Z - p2.Z) * (x - p2.X) + (p2.X - p1.X) * (z - p2.Z)) / det;
        double l1 = ((p2.Z - p0.Z) * (x - p2.X) + (p0.X - p2.X) * (z - p2.Z)) / det;
        double l2 = 1 - l0 - l1;
        if (l0 < 0 || l1 < 0 || l2 < 0)
            return (false, 0);
        return (true, l0 * p0.Y + l1 * p1.Y + l2 * p2.Y);
    }

    private static double[] PatternHeights(int count, Func<int, double> heightAt)
    {
        var heights = new double[count];
        for (int i = 0; i < count; i++)
            heights[i] = heightAt(i);
        return heights;
    }

    private static string SnapshotJson(string regionId, int rows, int columns,
        double originX, double originZ, double spacing, double[] heights)
    {
        var json = new StringBuilder(160 + heights.Length * 8);
        json.Append("{\"schema_version\":1,\"region_id\":\"").Append(regionId)
            .Append("\",\"version\":1,\"origin_x_m\":").Append(originX.ToString(CultureInfo.InvariantCulture))
            .Append(",\"origin_z_m\":").Append(originZ.ToString(CultureInfo.InvariantCulture))
            .Append(",\"spacing_m\":").Append(spacing.ToString(CultureInfo.InvariantCulture))
            .Append(",\"rows\":").Append(rows)
            .Append(",\"columns\":").Append(columns)
            .Append(",\"heights_m\":[");
        for (int i = 0; i < heights.Length; i++)
        {
            if (i > 0)
                json.Append(',');
            json.Append(heights[i].ToString(CultureInfo.InvariantCulture));
        }
        json.Append("]}");
        return json.ToString();
    }

    private static double[] Copy(IReadOnlyList<double> source)
    {
        var copy = new double[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return copy;
    }

    private static TerrainVertex[] Copy(IReadOnlyList<TerrainVertex> source)
    {
        var copy = new TerrainVertex[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return copy;
    }

    private static int[] CopyIndices(IReadOnlyList<int> source)
    {
        var copy = new int[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return copy;
    }

    private static void AssertSequences(string what, double[] expected, IReadOnlyList<double> actual)
    {
        Equal($"{what} length", expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
            Equal($"{what}[{i}]", expected[i], actual[i]);
    }

    private static void AssertSequences(string what, TerrainVertex[] expected, IReadOnlyList<TerrainVertex> actual)
    {
        Equal($"{what} length", expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
                throw new Exception($"{what}[{i}] differs");
        }
    }

    private static void AssertSequences(string what, int[] expected, IReadOnlyList<int> actual)
    {
        Equal($"{what} length", expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
            Equal($"{what}[{i}]", expected[i], actual[i]);
    }
}
