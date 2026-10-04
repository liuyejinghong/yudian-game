using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-render-r1 实际引擎测试（headless）。场景启动即跑全部命名用例，
/// 逐条打印 PASS/FAIL，全过退出 0，任何失败退出 1。启动时核对加载的 Yudian.dll
/// MVID与当前工作树Debug文件一致，并打印该文件SHA256；不比对加载字节SHA。
/// </summary>
public partial class AdapterTests : Node
{
    // 数据 r1 金样：flat 2×2、sample-a 2×3 非对称、patch-b 仅内部 21→0、22→-1。
    private const string FlatJson =
        "{\"schema_version\":1,\"region_id\":\"flat\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
        + "\"spacing_m\":1,\"rows\":2,\"columns\":2,\"heights_m\":[0,0,0,0]}";

    private const string SampleAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,"
        + "\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private const string PatchBJson =
        "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":{\"schema_version\":1,\"region_id\":\"sample-b\","
        + "\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,"
        + "\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]},"
        + "\"heights_m\":[10,11,12,13,20,0,-1,23,30,31,32,33]}";

    private static int _pass;
    private static int _fail;

    public override void _Ready()
    {
        try
        {
            Check("identity: current worktree Debug/Yudian.dll loaded, MVID matches fresh build (SHA256 printed)", Identity);
            Check("flat 2x2: full i0,i2,i1 emission order and Up normals", FlatPlaneGolden);
            Check("sample-a: expanded count 48 and full world-XYZ emission mapping", SampleA);
            Check("patch-b: both generators, candidate original nodes readback", PatchBothGenerators);
            Check("surface shape: single, only Vertex+Normal, no index/UV/material, Triangles", SurfaceShape);
            Check("independent resources: repeated builds give fresh RIDs with equal content", Independence);
            Check("inputs preserved: snapshot/patch serialization and CPU buffers unchanged", InputImmutability);
            Check("rejections: null/float overflow/quantized degenerate/negative winding/"
                + "cross overflow/length-squared overflow/zero-length normal/"
                + "unreferenced vertex overflow", Rejections);
            Check("max legal via real generators: 257x257 snapshot -> Stage 257 mesh / Heightfield 513 mesh",
                MaxSizeViaGenerators);
            Check("max legal raw 513x513: counts, finiteness, endpoints, unit up normals", MaxSize);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
        Console.WriteLine($"SUMMARY pass={_pass} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    private static void Check(string name, Action body)
    {
        try
        {
            body();
            _pass++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static void Identity()
    {
        Console.WriteLine($"CLR {System.Environment.Version}");
        System.Reflection.Assembly assembly = typeof(TerrainMeshAdapter).Assembly;
        Console.WriteLine($"Assembly.FullName {assembly.FullName}");
        Console.WriteLine($"Assembly.Location {assembly.Location}");
        Console.WriteLine($"AppDomain.BaseDirectory {AppDomain.CurrentDomain.BaseDirectory}");
        // Godot 4.7 经加载上下文装载，Location 为空；用 MVID 核对刚构建文件的模块身份，文件SHA另行记录。
        Guid loadedMvid = assembly.ManifestModule.ModuleVersionId;
        string expectedPath = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Yudian.dll");
        if (!File.Exists(expectedPath))
            throw new Exception($"fresh Debug build missing: {expectedPath}");
        byte[] freshBytes = File.ReadAllBytes(expectedPath);
        Console.WriteLine($"Fresh Debug Yudian.dll SHA256 {Convert.ToHexString(SHA256.HashData(freshBytes))}");
        Guid freshMvid = MvidOf(freshBytes);
        Console.WriteLine($"Loaded MVID  {loadedMvid}");
        Console.WriteLine($"Fresh  MVID  {freshMvid}");
        Expect(loadedMvid == freshMvid, "loaded Yudian.dll MVID differs from fresh Debug build (stale cache)");
    }

    private static Guid MvidOf(byte[] peBytes)
    {
        using var stream = new MemoryStream(peBytes);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        return reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }

    private static void FlatPlaneGolden()
    {
        // Stage 生成器保留原 2×2 分辨率 → 4 顶点 6 索引的原始平面。
        TerrainMesh cpu = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        int[] expectCpu = { 0, 3, 1, 0, 2, 3 };
        Expect(cpu.Indices.Count == 6, $"CPU indices count {cpu.Indices.Count}");
        Expect(cpu.Indices.SequenceEqual(expectCpu), "CPU fixed a,d,b,a,c,d order");
        using ArrayMesh mesh = TerrainMeshAdapter.Create(cpu);
        Vector3[] verts = ReadVertices(mesh, 0);
        Vector3[] norms = ReadNormals(mesh, 0);
        Expect(verts.Length == 6 && norms.Length == 6, $"expanded {verts.Length}/{norms.Length}");
        Vector3 v0 = new(0, 0, 0), v1 = new(1, 0, 0), v2 = new(0, 0, 1), v3 = new(1, 0, 1);
        Vector3 up = new(0, 1, 0);
        // CPU 三角 (0,3,1),(0,2,3) 按 i0,i2,i1 发出 → 顶点 0,1,3 / 0,3,2。
        Vector3[] expectVerts = { v0, v1, v3, v0, v3, v2 };
        for (int i = 0; i < 6; i++)
        {
            ExpectNear(verts[i], expectVerts[i], 1e-4, $"position[{i}]");
            ExpectNear(norms[i], up, 5e-3, $"normal[{i}]");
        }
    }

    private static void SampleA()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(SampleAJson));
        using ArrayMesh mesh = TerrainMeshAdapter.Create(cpu);
        Expect(mesh.GetSurfaceCount() == 1, $"surfaces {mesh.GetSurfaceCount()}");
        Vector3[] verts = ReadVertices(mesh, 0);
        Expect(verts.Length == 48, $"expanded count {verts.Length} != CPU Indices.Count 48");
        ExpectNear(verts[0], new Vector3(-1.5f, 1, 2), 1e-4, "first expanded vertex");
        VerifyEmissionMapping(cpu, verts, ReadNormals(mesh, 0));
    }

    private static void PatchBothGenerators()
    {
        TerrainPatch patch = TerrainDataCodec.ParsePatch(PatchBJson);
        string patchBefore = TerrainDataCodec.Serialize(patch);
        string baseBefore = TerrainDataCodec.Serialize(patch.Base);

        TerrainMesh stage = StageMeshBuilder.Build(patch, patch.Base);
        TerrainMesh fine = HeightfieldMeshBuilder.Build(patch, patch.Base);
        using ArrayMesh stageMesh = TerrainMeshAdapter.Create(stage);
        using ArrayMesh fineMesh = TerrainMeshAdapter.Create(fine);
        VerifyEmissionMapping(stage, ReadVertices(stageMesh, 0), ReadNormals(stageMesh, 0));
        VerifyEmissionMapping(fine, ReadVertices(fineMesh, 0), ReadNormals(fineMesh, 0));

        // 候选原节点 readback：内部 (1,1)=0、(1,2)=-1 必须出现在两份适配网格的位置 Y 上。
        double[] candidate = { 10, 11, 12, 13, 20, 0, -1, 23, 30, 31, 32, 33 };
        ExpectOriginalNode(stage, ReadVertices(stageMesh, 0), 1 * 4 + 1, candidate[5], "stage original (1,1)");
        ExpectOriginalNode(stage, ReadVertices(stageMesh, 0), 1 * 4 + 2, candidate[6], "stage original (1,2)");
        ExpectOriginalNode(fine, ReadVertices(fineMesh, 0), (2 * 1) * 7 + 2 * 1, candidate[5], "fine original (1,1)");
        ExpectOriginalNode(fine, ReadVertices(fineMesh, 0), (2 * 1) * 7 + 2 * 2, candidate[6], "fine original (1,2)");

        Expect(TerrainDataCodec.Serialize(patch) == patchBefore, "patch serialization changed");
        Expect(TerrainDataCodec.Serialize(patch.Base) == baseBefore, "base serialization changed");
    }

    private static void SurfaceShape()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(SampleAJson));
        using ArrayMesh mesh = TerrainMeshAdapter.Create(cpu);
        Expect(mesh.GetSurfaceCount() == 1, $"surfaces {mesh.GetSurfaceCount()}");
        Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        Expect(arrays.Count == (int)Mesh.ArrayType.Max, $"array slots {arrays.Count}");
        for (int slot = 0; slot < arrays.Count; slot++)
        {
            // 适配器只提供 Vertex/Normal；Godot 在有 Normal 无 Tangent 时引擎自动生成切线（slot 2）。
            Variant.Type type = arrays[slot].VariantType;
            bool ok = slot switch
            {
                (int)Mesh.ArrayType.Vertex or (int)Mesh.ArrayType.Normal
                    => type == Variant.Type.PackedVector3Array,
                (int)Mesh.ArrayType.Tangent => type == Variant.Type.PackedFloat32Array,
                _ => type == Variant.Type.Nil,
            };
            Expect(ok, $"channel slot {slot} unexpected {type}");
        }
        Expect(mesh.SurfaceGetMaterial(0) is null, "surface material must be null");
        Expect(mesh.SurfaceGetPrimitiveType(0) == Mesh.PrimitiveType.Triangles, "primitive must be Triangles");
        // 引擎自动切线（PackedFloat32Array，每顶点 4 分量）长度须与发出顶点数一致。
        float[] tangents = arrays[(int)Mesh.ArrayType.Tangent].As<float[]>();
        Expect(tangents.Length == ReadVertices(mesh, 0).Length * 4,
            $"engine tangent length {tangents.Length} != emitted vertices x4");
    }

    private static void Independence()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        using ArrayMesh first = TerrainMeshAdapter.Create(cpu);
        using ArrayMesh second = TerrainMeshAdapter.Create(cpu);
        Expect(first.GetRid() != second.GetRid(), "repeated builds must be fresh resources (no cache)");
        Vector3[] a = ReadVertices(first, 0);
        Vector3[] b = ReadVertices(second, 0);
        Expect(a.Length == b.Length, "expanded counts differ");
        for (int i = 0; i < a.Length; i++)
            ExpectNear(b[i], a[i], 1e-4, $"content[{i}]");
    }

    private static void InputImmutability()
    {
        TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(SampleAJson);
        TerrainPatch patch = TerrainDataCodec.ParsePatch(PatchBJson);
        string snapshotBefore = TerrainDataCodec.Serialize(snapshot);
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(snapshot);
        TerrainVertex[] verts = cpu.Vertices.ToArray();
        int[] indices = cpu.Indices.ToArray();

        using ArrayMesh fromSnapshot = TerrainMeshAdapter.Create(cpu);
        using ArrayMesh fromPatch = TerrainMeshAdapter.Create(HeightfieldMeshBuilder.Build(patch, patch.Base));

        Expect(TerrainDataCodec.Serialize(snapshot) == snapshotBefore, "snapshot serialization changed");
        for (int i = 0; i < verts.Length; i++)
            Expect(cpu.Vertices[i].Equals(verts[i]), $"CPU vertex[{i}] changed");
        for (int i = 0; i < indices.Length; i++)
            Expect(cpu.Indices[i] == indices[i], $"CPU index[{i}] changed");
    }

    private static void Rejections()
    {
        ExpectArgument(() => TerrainMeshAdapter.Create(null!), "mesh", "null mesh");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(1e300, 0, 0), V(0, 0, 1), V(1, 0, 1) },
            new[] { 0, 3, 1, 0, 2, 3 })), "vertices[1]", "double beyond float range");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(1, 0, 0), V(1 + 1e-9, 0, 1e-9), V(1, 0, 1e-9), V(2, 0, 2) },
            new[] { 0, 1, 2, 0, 3, 2 })), "triangle[0]", "float quantized degenerate");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(1, 0, 0), V(0, 0, 1), V(1, 0, 1) },
            new[] { 0, 1, 3, 0, 2, 3 })), "triangle[0]", "negative winding");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(0, 0, 1e20), V(1e20, 0, 0), V(2, 0, 2) },
            new[] { 0, 1, 2, 0, 3, 2 })), "triangle[0]", "cross product overflow");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(0, 0, 1e19), V(1e19, 0, 0), V(2, 0, 2) },
            new[] { 0, 1, 2, 0, 3, 2 })), "triangle[0]", "finite cross but length-squared overflow");
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 1e-22), V(1e-22, 0, 0), V(2, 0, 2), V(1, 0, 1) },
            new[] { 0, 1, 2, 0, 3, 2 })), "triangle[0]", "subnormal length-squared underflow to zero normal");
        // 公开 TerrainMesh 允许未被索引引用的顶点；溢出不能因未引用而漏过。
        ExpectArgument(() => TerrainMeshAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(0, 0, 1), V(1, 0, 0), V(1e300, 0, 1) },
            new[] { 0, 1, 2, 0, 1, 2 })), "vertices[3]", "unreferenced vertex beyond float range");
    }

    private static void MaxSize()
    {
        int rows = 513, columns = 513;        var vertices = new TerrainVertex[rows * columns];
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
                vertices[row * columns + column] = new TerrainVertex(column, 0, row);
        var indices = new int[6 * (rows - 1) * (columns - 1)];
        int write = 0;
        for (int row = 0; row < rows - 1; row++)
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column;
                indices[write++] = a;
                indices[write++] = a + columns + 1;
                indices[write++] = a + 1;
                indices[write++] = a;
                indices[write++] = a + columns;
                indices[write++] = a + columns + 1;
            }
        TerrainMesh cpu = new(rows, columns, vertices, indices);
        using ArrayMesh mesh = TerrainMeshAdapter.Create(cpu);
        Expect(mesh.GetSurfaceCount() == 1, $"surfaces {mesh.GetSurfaceCount()}");
        Vector3[] verts = ReadVertices(mesh, 0);
        Vector3[] norms = ReadNormals(mesh, 0);
        Expect(verts.Length == 1572864 && norms.Length == 1572864, $"expanded {verts.Length}");
        Vector3 up = new(0, 1, 0);
        for (int i = 0; i < verts.Length; i++)
        {
            Expect(verts[i].IsFinite() && norms[i].IsFinite(), $"non-finite at {i}");
            Expect(norms[i].Dot(up) >= 0.9999f, $"normal[{i}] not up: {norms[i]}");
        }
        ExpectNear(verts[0], new Vector3(0, 0, 0), 1e-4, "first endpoint");
        // 最后展开槽位 = 末 cell (a,c,d) 发出序的 i1 = c 角 = (columns-2, 0, rows-1)。
        ExpectNear(verts[^1], new Vector3(columns - 2, 0, rows - 1), 1e-4, "last endpoint");
        Vector3 farCorner = new(512, 0, 512);
        Expect(verts.Contains(farCorner), "far corner (512,0,512) missing");
    }

    // 最大合法输入经两个 Build 的实际路径各一次：257×257 snapshot -> Stage 257×257 / Heightfield 513×513。
    private static void MaxSizeViaGenerators()
    {
        string heights = string.Join(",", System.Linq.Enumerable.Repeat("0", 257 * 257));
        string snapshotJson =
            "{\"schema_version\":1,\"region_id\":\"max-257\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
            + "\"spacing_m\":1,\"rows\":257,\"columns\":257,\"heights_m\":[" + heights + "]}";
        TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(snapshotJson);

        TerrainMesh stageCpu = StageMeshBuilder.Build(snapshot);
        Expect(stageCpu.GridRows == 257 && stageCpu.GridColumns == 257
            && stageCpu.Vertices.Count == 66049 && stageCpu.Indices.Count == 393216,
            $"stage CPU shape {stageCpu.GridRows}x{stageCpu.GridColumns}");
        using ArrayMesh stageMesh = TerrainMeshAdapter.Create(stageCpu);
        VerifyMaxSurface(stageMesh, 393216, new Vector3(256, 0, 256));

        TerrainMesh fineCpu = HeightfieldMeshBuilder.Build(snapshot);
        Expect(fineCpu.GridRows == 513 && fineCpu.GridColumns == 513
            && fineCpu.Vertices.Count == 263169 && fineCpu.Indices.Count == 1572864,
            $"heightfield CPU shape {fineCpu.GridRows}x{fineCpu.GridColumns}");
        using ArrayMesh fineMesh = TerrainMeshAdapter.Create(fineCpu);
        VerifyMaxSurface(fineMesh, 1572864, new Vector3(256, 0, 256));
    }

    private static void VerifyMaxSurface(ArrayMesh mesh, int expectedExpanded, Vector3 farCorner)
    {
        Expect(mesh.GetSurfaceCount() == 1, $"surfaces {mesh.GetSurfaceCount()}");
        Vector3[] verts = ReadVertices(mesh, 0);
        Vector3[] norms = ReadNormals(mesh, 0);
        Expect(verts.Length == expectedExpanded && norms.Length == expectedExpanded,
            $"expanded {verts.Length}/{norms.Length} != {expectedExpanded}");
        Vector3 up = new(0, 1, 0);
        for (int i = 0; i < verts.Length; i++)
        {
            Expect(verts[i].IsFinite() && norms[i].IsFinite(), $"non-finite at {i}");
            Expect(norms[i].Dot(up) >= 0.9999f, $"normal[{i}] not up: {norms[i]}");
        }
        ExpectNear(verts[0], new Vector3(0, 0, 0), 1e-4, "origin corner");
        Expect(verts.Contains(farCorner), $"far corner {farCorner} missing");
    }

    // 每个展开槽位 = CPU 三角按 i0,i2,i1 的 float 目标；法线 = CPU 序叉积方向（打包误差 <= 5e-3）。
    private static void VerifyEmissionMapping(TerrainMesh cpu, Vector3[] verts, Vector3[] norms)
    {
        Expect(verts.Length == cpu.Indices.Count, $"expanded {verts.Length} != {cpu.Indices.Count}");
        for (int triangle = 0; triangle < cpu.Indices.Count / 3; triangle++)
        {
            int i0 = cpu.Indices[triangle * 3];
            int i1 = cpu.Indices[triangle * 3 + 1];
            int i2 = cpu.Indices[triangle * 3 + 2];
            Vector3 p0 = FloatV(cpu.Vertices[i0]);
            Vector3 p1 = FloatV(cpu.Vertices[i1]);
            Vector3 p2 = FloatV(cpu.Vertices[i2]);
            Vector3 expectedNormal = (p1 - p0).Cross(p2 - p0).Normalized();
            ExpectNear(verts[triangle * 3], p0, 1e-4, $"tri[{triangle}] slot0");
            ExpectNear(verts[triangle * 3 + 1], p2, 1e-4, $"tri[{triangle}] slot1");
            ExpectNear(verts[triangle * 3 + 2], p1, 1e-4, $"tri[{triangle}] slot2");
            for (int slot = 0; slot < 3; slot++)
                Expect(norms[triangle * 3 + slot].Dot(expectedNormal) >= 0.9999f,
                    $"tri[{triangle}] normal slot{slot}");
        }
    }

    private static void ExpectOriginalNode(TerrainMesh cpu, Vector3[] verts, int cpuIndex, double height, string what)
    {
        for (int triangle = 0; triangle < cpu.Indices.Count / 3; triangle++)
            for (int slot = 0; slot < 3; slot++)
                if (cpu.Indices[triangle * 3 + slot] == cpuIndex)
                {
                    int emitted = triangle * 3 + (slot == 0 ? 0 : slot == 1 ? 2 : 1);
                    Expect(Math.Abs(verts[emitted].Y - height) <= 1e-4,
                        $"{what}: Y {verts[emitted].Y} != {height}");
                    return;
                }
        throw new Exception($"{what}: cpu index {cpuIndex} not referenced");
    }

    private static double[] V(params double[] xyz) => xyz;

    private static TerrainMesh RawMesh(double[][] xyz, int[] indices)
    {
        var vertices = new TerrainVertex[xyz.Length];
        for (int i = 0; i < xyz.Length; i++)
            vertices[i] = new TerrainVertex(xyz[i][0], xyz[i][1], xyz[i][2]);
        return new TerrainMesh(2, 2, vertices, indices);
    }

    private static Vector3 FloatV(TerrainVertex v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vector3[] ReadVertices(ArrayMesh mesh, int surface) =>
        mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();

    private static Vector3[] ReadNormals(ArrayMesh mesh, int surface) =>
        mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Normal].AsVector3Array();

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void ExpectNear(Vector3 actual, Vector3 expected, double tolerance, string what)
    {
        Vector3 delta = actual - expected;
        Expect(Math.Abs(delta.X) <= tolerance && Math.Abs(delta.Y) <= tolerance && Math.Abs(delta.Z) <= tolerance,
            $"{what}: got {actual}, want {expected}");
    }

    private static void ExpectArgument(Action action, string messagePart, string what)
    {
        try
        {
            action();
        }
        catch (ArgumentException ex)
        {
            Expect(ex.Message.Contains(messagePart), $"{what}: message missing '{messagePart}': {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception($"{what}: no ArgumentException thrown");
    }
}

