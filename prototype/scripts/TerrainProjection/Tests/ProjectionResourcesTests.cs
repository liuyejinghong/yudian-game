using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-view-r1 GLM资源束实际引擎测试（headless）。场景启动即跑全部命名用例，逐条打印
/// PASS/FAIL，全过退出 0，任何失败退出 1。启动时核对加载的 Yudian.dll MVID 与当前工作树
/// Debug 文件一致并打印该文件 SHA256（不比对加载字节 SHA）。释放证明用 RenderingServer
/// MeshGetSurfaceCount（活 mesh=1，已释放=0，先用已知活/释放对照演示两侧取值）与
/// GodotObject.IsInstanceValid（mesh/shape 实例）；对已释放 RID 的查询会打印引擎错误行，
/// 属预期现象。failBeforeShape 的 InvalidOperationException 是合成中途异常，测试如实记录
/// 为模拟，不冒称真实 native 分配失败。
/// </summary>
public partial class ProjectionResourcesTests : Node
{
    // 数据 r1 金样：flat 2×2、sample-a 2×3 非对称（与既有 render/collision 测试一致）。
    private const string FlatJson =
        "{\"schema_version\":1,\"region_id\":\"flat\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
        + "\"spacing_m\":1,\"rows\":2,\"columns\":2,\"heights_m\":[0,0,0,0]}";

    private const string SampleAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,"
        + "\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private static int _pass;
    private static int _fail;

    public override void _Ready()
    {
        try
        {
            Check("identity: current worktree Debug/Yudian.dll loaded, MVID matches fresh build (SHA256 printed)", Identity);
            Check("corners: flat(Stage) mesh+shape all corners equal CPU emission one-to-one", FlatCorners);
            Check("corners: sample-a(Heightfield) mesh+shape all corners equal CPU emission one-to-one", SampleACorners);
            Check("independent bundles: distinct mesh/shape RIDs, equal content, alive until handover", Independence);
            Check("inputs preserved: CPU buffers unchanged after Create and Dispose", InputImmutability);
            Check("rejections: null mesh / negative winding ArgumentException, no bundle, hook untouched", Rejections);
            Check("simulated mid-flight failure: InvalidOperationException after mesh, mesh RID proven released", SimulatedMidFlightFailure);
            Check("dispose: idempotent, releases mesh+shape instances, access after dispose rejected", RepeatDispose);
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
        System.Reflection.Assembly assembly = typeof(TerrainProjectionResources).Assembly;
        Console.WriteLine($"Assembly.FullName {assembly.FullName}");
        Console.WriteLine($"Assembly.Location {assembly.Location}");
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

    private static void FlatCorners()
    {
        TerrainMesh cpu = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        using TerrainProjectionResources bundle = TerrainProjectionResources.Create(cpu);
        Expect(bundle.Mesh.GetSurfaceCount() == 1, $"mesh surfaces {bundle.Mesh.GetSurfaceCount()}");
        Vector3[] meshVerts = ReadMeshVertices(bundle.Mesh);
        Expect(meshVerts.Length == cpu.Indices.Count, $"mesh expanded {meshVerts.Length} != {cpu.Indices.Count}");
        VerifyEmissionMapping(cpu, meshVerts, ReadMeshNormals(bundle.Mesh));
        VerifyShapeAgainstMesh(cpu, bundle.Shape, meshVerts);
    }

    private static void SampleACorners()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(SampleAJson));
        using TerrainProjectionResources bundle = TerrainProjectionResources.Create(cpu);
        Vector3[] meshVerts = ReadMeshVertices(bundle.Mesh);
        Expect(meshVerts.Length == cpu.Indices.Count, $"mesh expanded {meshVerts.Length} != {cpu.Indices.Count}");
        VerifyEmissionMapping(cpu, meshVerts, ReadMeshNormals(bundle.Mesh));
        VerifyShapeAgainstMesh(cpu, bundle.Shape, meshVerts);
        ExpectNear(meshVerts[0], new Vector3(-1.5f, 1, 2), 1e-4, "first emitted vertex");
    }

    private static void Independence()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        using TerrainProjectionResources first = TerrainProjectionResources.Create(cpu);
        using TerrainProjectionResources second = TerrainProjectionResources.Create(cpu);
        Expect(first.Mesh.GetRid() != second.Mesh.GetRid(), "bundle mesh RIDs must differ (no cache)");
        Expect(first.Shape.GetRid() != second.Shape.GetRid(), "bundle shape RIDs must differ (no cache)");
        Expect(GodotObject.IsInstanceValid(first.Mesh) && GodotObject.IsInstanceValid(first.Shape),
            "first bundle resources must stay alive after successful handover");
        Expect(GodotObject.IsInstanceValid(second.Mesh) && GodotObject.IsInstanceValid(second.Shape),
            "second bundle resources must stay alive after successful handover");
        Expect(RenderingServer.MeshGetSurfaceCount(first.Mesh.GetRid()) == 1,
            "live handed-over mesh must be queryable as a real surface resource");
        Vector3[] a = ReadMeshVertices(first.Mesh);
        Vector3[] b = ReadMeshVertices(second.Mesh);
        Expect(a.Length == b.Length, "expanded counts differ");
        for (int i = 0; i < a.Length; i++)
            ExpectNear(b[i], a[i], 1e-4, $"content[{i}]");
        Vector3[] facesA = first.Shape.GetFaces();
        Vector3[] facesB = second.Shape.GetFaces();
        Expect(facesA.SequenceEqual(facesB), "shape face contents differ");
    }

    private static void InputImmutability()
    {
        TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(SampleAJson);
        string snapshotBefore = TerrainDataCodec.Serialize(snapshot);
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(snapshot);
        TerrainVertex[] verts = cpu.Vertices.ToArray();
        int[] indices = cpu.Indices.ToArray();

        TerrainProjectionResources bundle = TerrainProjectionResources.Create(cpu);
        for (int i = 0; i < verts.Length; i++)
            Expect(cpu.Vertices[i].Equals(verts[i]), $"CPU vertex[{i}] changed after Create");
        for (int i = 0; i < indices.Length; i++)
            Expect(cpu.Indices[i] == indices[i], $"CPU index[{i}] changed after Create");
        bundle.Dispose();
        for (int i = 0; i < verts.Length; i++)
            Expect(cpu.Vertices[i].Equals(verts[i]), $"CPU vertex[{i}] changed after Dispose");
        for (int i = 0; i < indices.Length; i++)
            Expect(cpu.Indices[i] == indices[i], $"CPU index[{i}] changed after Dispose");
        Expect(TerrainDataCodec.Serialize(snapshot) == snapshotBefore, "snapshot serialization changed");
    }

    private static void Rejections()
    {
        Rid? hookBefore = TerrainProjectionResources.LastSimulatedFailureMeshRid;

        ExpectArgument(() => TerrainProjectionResources.Create(null!), "mesh", "null mesh");
        ExpectArgument(() => TerrainProjectionResources.Create(RawMesh(
            new[] { V(0, 0, 0), V(1, 0, 0), V(0, 0, 1), V(1, 0, 1) },
            new[] { 0, 1, 3, 0, 2, 3 })), "triangle[0]", "negative winding");

        Expect(TerrainProjectionResources.LastSimulatedFailureMeshRid == hookBefore,
            "public Create failures must not touch the internal simulated-failure hook record");
    }

    private static void SimulatedMidFlightFailure()
    {
        TerrainMesh cpu = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        TerrainVertex[] verts = cpu.Vertices.ToArray();
        int[] indices = cpu.Indices.ToArray();

        // 对照：同一查询在已知活 mesh 上取 1，已知释放 mesh 上取 0，证明该查询可区分两侧。
        ArrayMesh control = TerrainMeshAdapter.Create(cpu);
        Rid controlRid = control.GetRid();
        Expect(RenderingServer.MeshGetSurfaceCount(controlRid) == 1, "control live mesh query must return 1");
        control.Dispose();
        int controlAfterFree = RenderingServer.MeshGetSurfaceCount(controlRid);
        Expect(controlAfterFree == 0, $"control freed mesh query must return 0, got {controlAfterFree}");

        // 模拟中途失败：mesh 成功后、shape 前抛合成 InvalidOperationException。
        InvalidOperationException failure;
        try
        {
            TerrainProjectionResources.Create(cpu, failBeforeShape: true);
            throw new Exception("internal Create(failBeforeShape:true) must throw InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            failure = ex;
        }
        Console.WriteLine($"SIMULATED (synthetic, not a real native allocation failure): {failure.Message}");
        Expect(failure.Message.Contains("simulated", StringComparison.OrdinalIgnoreCase),
            "failure message must record that this is a simulated exception");

        // 用释放前记录的 RID 证明确实释放（非计数猜测）：查询值须与已释放对照同为 0。
        Rid? recorded = TerrainProjectionResources.LastSimulatedFailureMeshRid;
        Expect(recorded.HasValue, "internal hook must record the doomed mesh RID before release");
        int queryAfter = RenderingServer.MeshGetSurfaceCount(recorded.Value);
        Expect(queryAfter == 0, $"simulated-failure mesh RID must query as released (0), got {queryAfter}");

        for (int i = 0; i < verts.Length; i++)
            Expect(cpu.Vertices[i].Equals(verts[i]), $"CPU vertex[{i}] changed by failed Create");
        for (int i = 0; i < indices.Length; i++)
            Expect(cpu.Indices[i] == indices[i], $"CPU index[{i}] changed by failed Create");
    }

    private static void RepeatDispose()
    {
        TerrainMesh cpu = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        TerrainProjectionResources bundle = TerrainProjectionResources.Create(cpu);
        ArrayMesh mesh = bundle.Mesh;
        ConcavePolygonShape3D shape = bundle.Shape;
        Rid meshRid = mesh.GetRid();

        bundle.Dispose();
        Expect(!GodotObject.IsInstanceValid(mesh), "mesh instance must be invalid after Dispose");
        Expect(!GodotObject.IsInstanceValid(shape), "shape instance must be invalid after Dispose");
        Expect(RenderingServer.MeshGetSurfaceCount(meshRid) == 0,
            "mesh RID must query as released (0) after Dispose");

        bundle.Dispose(); // 重复 Dispose 幂等，不得抛异常。
        Expect(!GodotObject.IsInstanceValid(mesh), "mesh must remain invalid after repeated Dispose");
        Expect(!GodotObject.IsInstanceValid(shape), "shape must remain invalid after repeated Dispose");

        ExpectDisposedAccess(() => _ = bundle.Mesh, "Mesh access after Dispose");
        ExpectDisposedAccess(() => _ = bundle.Shape, "Shape access after Dispose");
    }

    // shape 全部角逐一等值：face 角数 = CPU Indices.Count，位置与 mesh 发出顶点逐槽同值，
    // 且 BackfaceCollision=false（同 collision adapter 冻结行为）。
    private static void VerifyShapeAgainstMesh(TerrainMesh cpu, ConcavePolygonShape3D shape, Vector3[] meshVerts)
    {
        Vector3[] faces = shape.GetFaces();
        Expect(faces.Length == cpu.Indices.Count,
            $"shape face corners {faces.Length} != CPU Indices.Count {cpu.Indices.Count}");
        Expect(!shape.BackfaceCollision, "shape BackfaceCollision must be false");
        for (int i = 0; i < faces.Length; i++)
            ExpectNear(faces[i], meshVerts[i], 1e-4, $"shape corner[{i}] vs mesh vertex[{i}]");
    }

    // 每个展开槽位 = CPU 三角按 i0,i2,i1 的 float 目标；法线 = CPU 序叉积单位方向（打包误差 <= 5e-3）。
    private static void VerifyEmissionMapping(TerrainMesh cpu, Vector3[] verts, Vector3[] norms)
    {
        Expect(verts.Length == cpu.Indices.Count, $"expanded {verts.Length} != {cpu.Indices.Count}");
        Expect(norms.Length == cpu.Indices.Count, $"normals {norms.Length} != {cpu.Indices.Count}");
        for (int triangle = 0; triangle < cpu.Indices.Count / 3; triangle++)
        {
            int i0 = cpu.Indices[triangle * 3];
            int i1 = cpu.Indices[triangle * 3 + 1];
            int i2 = cpu.Indices[triangle * 3 + 2];
            Vector3 p0 = FloatV(cpu.Vertices[i0]);
            Vector3 p1 = FloatV(cpu.Vertices[i1]);
            Vector3 p2 = FloatV(cpu.Vertices[i2]);
            Vector3 cross = (p1 - p0).Cross(p2 - p0);
            Expect(cross.Y > 0, $"tri[{triangle}] CPU winding must be positive-Y");
            Vector3 expectedNormal = cross.Normalized();
            ExpectNear(verts[triangle * 3], p0, 1e-4, $"tri[{triangle}] slot0");
            ExpectNear(verts[triangle * 3 + 1], p2, 1e-4, $"tri[{triangle}] slot1");
            ExpectNear(verts[triangle * 3 + 2], p1, 1e-4, $"tri[{triangle}] slot2");
            for (int slot = 0; slot < 3; slot++)
                Expect(norms[triangle * 3 + slot].Dot(expectedNormal) >= 0.9999f,
                    $"tri[{triangle}] normal slot{slot}");
        }
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

    private static Vector3[] ReadMeshVertices(ArrayMesh mesh) =>
        mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();

    private static Vector3[] ReadMeshNormals(ArrayMesh mesh) =>
        mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal].AsVector3Array();

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

    private static void ExpectDisposedAccess(Action action, string what)
    {
        try
        {
            action();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ObjectDisposedException, got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception($"{what}: no ObjectDisposedException thrown");
    }
}
