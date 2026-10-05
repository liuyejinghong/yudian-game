using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-collision-r1 实际引擎测试（headless）。_Ready 跑 shape 构建类命名用例；
/// _PhysicsProcess 在节点/shape 设置并至少等待下一物理查询帧后，才读 DirectSpaceState 跑射线用例。
/// 逐条打印 PASS/FAIL，全过退出 0，任何失败退出 1。启动时打印 CLR、实际物理后端与当前 Debug
/// Yudian.dll SHA256，核对 loaded MVID == 该文件 PE MVID；Assembly.Location 为空如实记录，
/// 不比对加载字节 SHA。金样斜坡（2×2 origin(0,0) spacing 2 heights[0,2,4,8]）局部 (x=.5,z=.25)
/// 按各自真实三角平面：Stage Y=1.25、Heightfield 细分 Y=1.125（非原 bilinear 面）。
/// </summary>
public partial class CollisionTests : Node3D
{
    // 数据 r1 金样：flat 2×2、sample-a 2×3 非对称（同 render 测试）；slope 为合同物理金样。
    private const string FlatJson =
        "{\"schema_version\":1,\"region_id\":\"flat\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
        + "\"spacing_m\":1,\"rows\":2,\"columns\":2,\"heights_m\":[0,0,0,0]}";

    private const string SampleAJson =
        "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,"
        + "\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";

    private const string SlopeJson =
        "{\"schema_version\":1,\"region_id\":\"slope\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
        + "\"spacing_m\":2,\"rows\":2,\"columns\":2,\"heights_m\":[0,2,4,8]}";

    private static int _pass;
    private static int _fail;

    private int _physicsFrame;
    private bool _physicsRan;
    private bool _physicsWorldReady;
    private StaticBody3D _stageBody;
    private StaticBody3D _heightfieldBody;

    public override void _Ready()
    {
        try
        {
            Check("identity: CLR version, physics backend, loaded MVID == fresh Debug PE MVID, SHA256", Identity);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
        try
        {
            // 两个 StaticBody 位置相隔，layer 分别 1 与 2，CollisionMask=0；金样斜坡各挂一个生成器。
            BuildPhysicsWorld();
        }
        catch (Exception ex)
        {
            _fail++;
            _physicsWorldReady = false;
            Console.WriteLine($"FAIL physics world setup: {ex.Message}");
        }
        try
        {
            Check("stage flat 2x2: face corners equal ArrayMesh emitted order/count, backface false", StageFlatFaces);
            Check("sample-a: shape face count 48 == Indices.Count, emitted-order equality", SampleAFaces);
            Check("independent RIDs, equal content, shape queryable after temp mesh freed inside Create", Independence);
            Check("collision Create leaves CPU mesh unchanged", InputImmutability);
            Check("rejections: null/unreferenced vertex overflow/quantized degenerate/negative winding", Rejections);
            Check("max legal via real generators: shape counts and endpoints (not a performance claim)", MaxSizeShapes);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // 节点/shape 设置后至少等下一物理查询帧（这里等两个完整物理步）才读 DirectSpaceState。
        _physicsFrame++;
        if (_physicsRan || _physicsFrame < 2)
            return;
        _physicsRan = true;
        try
        {
            Check("physics: mask 1 from above hits stage slope at Y=1.25 with up normal", StageSlopeHit);
            Check("physics: mask 2 from above hits heightfield slope at Y=6.125 with up normal", HeightfieldSlopeHit);
            Check("physics: from below no hit (single-sided, backfaces off)", BelowNoHit);
            Check("physics: outside terrain XZ no hit", OutsideNoHit);
            Check("physics: wrong mask no cross-layer hit", WrongMaskNoHit);
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
        Console.WriteLine(
            $"physics/3d/physics_engine = {ProjectSettings.GetSetting("physics/3d/physics_engine")}");
        System.Reflection.Assembly assembly = typeof(TerrainCollisionAdapter).Assembly;
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

    // 合同物理用例世界：两个 StaticBody 位置相隔，layer 1=Stage 斜坡、2=Heightfield 斜坡，Mask=0。
    private void BuildPhysicsWorld()
    {
        TerrainSnapshot slope = TerrainDataCodec.ParseSnapshot(SlopeJson);
        ConcavePolygonShape3D stageShape = TerrainCollisionAdapter.Create(StageMeshBuilder.Build(slope));
        TerrainMesh fineCpu = HeightfieldMeshBuilder.Build(slope);
        ConcavePolygonShape3D fineShape = TerrainCollisionAdapter.Create(fineCpu);
        // 2×2 细分后 fine 3×3 = 4 cell = 24 索引角；以 CPU Indices.Count 为准，不硬编码。
        Expect(stageShape.GetFaces().Length == 6, $"stage slope shape corners {stageShape.GetFaces().Length}");
        Expect(fineShape.GetFaces().Length == fineCpu.Indices.Count,
            $"fine slope shape corners {fineShape.GetFaces().Length} != {fineCpu.Indices.Count}");

        _stageBody = new StaticBody3D
        {
            CollisionLayer = 1,
            CollisionMask = 0,
            Position = new Vector3(0, 0, 0),
        };
        _stageBody.AddChild(new CollisionShape3D { Shape = stageShape });
        _heightfieldBody = new StaticBody3D
        {
            CollisionLayer = 2,
            CollisionMask = 0,
            Position = new Vector3(0, 5, 10),
        };
        _heightfieldBody.AddChild(new CollisionShape3D { Shape = fineShape });
        AddChild(_stageBody);
        AddChild(_heightfieldBody);
        _physicsWorldReady = true;
    }

    private static void StageFlatFaces()
    {
        TerrainMesh cpu = StageMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        using ConcavePolygonShape3D shape = TerrainCollisionAdapter.Create(cpu);
        Expect(shape.BackfaceCollision == false, "BackfaceCollision must be false");
        Vector3[] faces = shape.GetFaces();
        Expect(faces.Length == cpu.Indices.Count, $"face corners {faces.Length} != Indices.Count {cpu.Indices.Count}");
        using ArrayMesh render = TerrainMeshAdapter.Create(cpu);
        Vector3[] emitted = render.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Expect(faces.Length == emitted.Length, $"faces {faces.Length} != emitted {emitted.Length}");
        for (int i = 0; i < faces.Length; i++)
            ExpectNear(faces[i], emitted[i], 1e-4, $"face[{i}]");
    }

    private static void SampleAFaces()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(SampleAJson));
        using ConcavePolygonShape3D shape = TerrainCollisionAdapter.Create(cpu);
        Expect(shape.BackfaceCollision == false, "BackfaceCollision must be false");
        Vector3[] faces = shape.GetFaces();
        Expect(faces.Length == cpu.Indices.Count && faces.Length == 48,
            $"face corners {faces.Length} != Indices.Count 48");
        using ArrayMesh render = TerrainMeshAdapter.Create(cpu);
        Vector3[] emitted = render.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (int i = 0; i < faces.Length; i++)
            ExpectNear(faces[i], emitted[i], 1e-4, $"face[{i}]");
        ExpectNear(faces[0], new Vector3(-1.5f, 1, 2), 1e-4, "first face corner");
    }

    private static void Independence()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(FlatJson));
        using ConcavePolygonShape3D first = TerrainCollisionAdapter.Create(cpu);
        using ConcavePolygonShape3D second = TerrainCollisionAdapter.Create(cpu);
        Expect(first.GetRid() != second.GetRid(), "same input must give fresh shape RIDs (no cache)");
        Vector3[] a = first.GetFaces();
        Vector3[] b = second.GetFaces();
        Expect(a.Length == b.Length, "face corner counts differ");
        for (int i = 0; i < a.Length; i++)
            ExpectNear(b[i], a[i], 1e-4, $"content[{i}]");
        // 临时 ArrayMesh 已在 Create 内释放；返回 shape 仍可完整查询且不依赖其存活。
        Vector3[] again = first.GetFaces();
        Expect(again.Length == a.Length && again[0].IsEqualApprox(a[0]),
            "shape query failed after temp mesh freed inside Create");
    }

    private static void InputImmutability()
    {
        TerrainMesh cpu = HeightfieldMeshBuilder.Build(TerrainDataCodec.ParseSnapshot(SampleAJson));
        TerrainVertex[] verts = cpu.Vertices.ToArray();
        int[] indices = cpu.Indices.ToArray();
        using ConcavePolygonShape3D shape = TerrainCollisionAdapter.Create(cpu);
        Expect(shape.GetFaces().Length == indices.Length, "shape corners changed");
        for (int i = 0; i < verts.Length; i++)
            Expect(cpu.Vertices[i].Equals(verts[i]), $"CPU vertex[{i}] changed");
        for (int i = 0; i < indices.Length; i++)
            Expect(cpu.Indices[i] == indices[i], $"CPU index[{i}] changed");
    }

    private static void Rejections()
    {
        ExpectArgument(() => TerrainCollisionAdapter.Create(null!), "mesh", "null mesh");
        // 原 render 其他拒绝矩阵不重复；此处覆盖合同点名的四类入口。
        ExpectArgument(() => TerrainCollisionAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(0, 0, 1), V(1, 0, 0), V(1e300, 0, 1) },
            new[] { 0, 1, 2, 0, 1, 2 })), "vertices[3]", "unreferenced vertex beyond float range");
        ExpectArgument(() => TerrainCollisionAdapter.Create(RawMesh(
            new[] { V(1, 0, 0), V(1 + 1e-9, 0, 1e-9), V(1, 0, 1e-9), V(2, 0, 2) },
            new[] { 0, 1, 2, 0, 3, 2 })), "triangle[0]", "float quantized degenerate");
        ExpectArgument(() => TerrainCollisionAdapter.Create(RawMesh(
            new[] { V(0, 0, 0), V(1, 0, 0), V(0, 0, 1), V(1, 0, 1) },
            new[] { 0, 1, 3, 0, 2, 3 })), "triangle[0]", "negative winding");
    }

    // 最大合法输入经两个生成器实际路径各一次：257×257 snapshot -> Stage 257 / Heightfield 513。
    private static void MaxSizeShapes()
    {
        string heights = string.Join(",", Enumerable.Repeat("0", 257 * 257));
        string snapshotJson =
            "{\"schema_version\":1,\"region_id\":\"max-257\",\"version\":1,\"origin_x_m\":0,\"origin_z_m\":0,"
            + "\"spacing_m\":1,\"rows\":257,\"columns\":257,\"heights_m\":[" + heights + "]}";
        TerrainSnapshot snapshot = TerrainDataCodec.ParseSnapshot(snapshotJson);

        using ConcavePolygonShape3D stageShape = TerrainCollisionAdapter.Create(StageMeshBuilder.Build(snapshot));
        Vector3[] stageFaces = stageShape.GetFaces();
        Expect(stageFaces.Length == 393216, $"stage shape corners {stageFaces.Length} != 393216");
        ExpectNear(stageFaces[0], new Vector3(0, 0, 0), 1e-4, "stage first corner");
        Expect(stageFaces.Contains(new Vector3(256, 0, 256)), "stage far corner (256,0,256) missing");

        using ConcavePolygonShape3D fineShape = TerrainCollisionAdapter.Create(HeightfieldMeshBuilder.Build(snapshot));
        Vector3[] fineFaces = fineShape.GetFaces();
        Expect(fineFaces.Length == 1572864, $"fine shape corners {fineFaces.Length} != 1572864");
        ExpectNear(fineFaces[0], new Vector3(0, 0, 0), 1e-4, "fine first corner");
        Expect(fineFaces.Contains(new Vector3(256, 0, 256)), "fine far corner (256,0,256) missing");
    }

    private Godot.Collections.Dictionary QueryRay(Vector3 from, Vector3 to, uint collisionMask)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        query.HitBackFaces = false;
        query.HitFromInside = false;
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    // 金样：Stage 平面 y=x+3z → (0.5,0.25) 处 Y=1.25；Heightfield 细分平面 y=x+2.5z → Y=1.125（body 本地）。
    private void StageSlopeHit()
    {
        Expect(_physicsWorldReady, "physics world not ready");
        var hit = QueryRay(new Vector3(0.5f, 50, 0.25f), new Vector3(0.5f, -50, 0.25f), 1u);
        Expect(hit.Count > 0, "no hit from above with mask 1");
        var collider = hit["collider"].As<StaticBody3D>();
        Expect(collider == _stageBody, $"collider {collider?.Name ?? "null"} is not stage body");
        Vector3 position = hit["position"].AsVector3();
        Vector3 normal = hit["normal"].AsVector3();
        Console.WriteLine($"  stage slope hit position {position} normal {normal}");
        ExpectNear(position, new Vector3(0.5f, 1.25f, 0.25f), 1e-3, "stage slope hit world position");
        Expect(normal.Y > 0, $"normal not up: {normal}");
    }

    private void HeightfieldSlopeHit()
    {
        Expect(_physicsWorldReady, "physics world not ready");
        var hit = QueryRay(new Vector3(0.5f, 55, 10.25f), new Vector3(0.5f, -45, 10.25f), 2u);
        Expect(hit.Count > 0, "no hit from above with mask 2");
        var collider = hit["collider"].As<StaticBody3D>();
        Expect(collider == _heightfieldBody, $"collider {collider?.Name ?? "null"} is not heightfield body");
        Vector3 position = hit["position"].AsVector3();
        Vector3 normal = hit["normal"].AsVector3();
        Console.WriteLine($"  heightfield slope hit position {position} normal {normal}");
        ExpectNear(position, new Vector3(0.5f, 6.125f, 10.25f), 1e-3, "heightfield slope hit world position");
        Expect(normal.Y > 0, $"normal not up: {normal}");
    }

    private void BelowNoHit()
    {
        Expect(_physicsWorldReady, "physics world not ready");
        Expect(QueryRay(new Vector3(0.5f, -50, 0.25f), new Vector3(0.5f, 50, 0.25f), 1u).Count == 0,
            "stage slope hit from below (backface)");
        Expect(QueryRay(new Vector3(0.5f, -45, 10.25f), new Vector3(0.5f, 55, 10.25f), 2u).Count == 0,
            "heightfield slope hit from below (backface)");
    }

    private void OutsideNoHit()
    {
        Expect(_physicsWorldReady, "physics world not ready");
        Expect(QueryRay(new Vector3(50, 50, 0.25f), new Vector3(50, -50, 0.25f), 1u).Count == 0,
            "hit outside stage terrain XZ");
        Expect(QueryRay(new Vector3(100, 55, 100), new Vector3(100, -45, 100), 2u).Count == 0,
            "hit outside heightfield terrain XZ");
    }

    private void WrongMaskNoHit()
    {
        Expect(_physicsWorldReady, "physics world not ready");
        Expect(QueryRay(new Vector3(0.5f, 50, 0.25f), new Vector3(0.5f, -50, 0.25f), 2u).Count == 0,
            "stage slope hit with wrong mask 2");
        Expect(QueryRay(new Vector3(0.5f, 55, 10.25f), new Vector3(0.5f, -45, 10.25f), 1u).Count == 0,
            "heightfield slope hit with wrong mask 1");
    }

    private static double[] V(params double[] xyz) => xyz;

    private static TerrainMesh RawMesh(double[][] xyz, int[] indices)
    {
        var vertices = new TerrainVertex[xyz.Length];
        for (int i = 0; i < xyz.Length; i++)
            vertices[i] = new TerrainVertex(xyz[i][0], xyz[i][1], xyz[i][2]);
        return new TerrainMesh(2, 2, vertices, indices);
    }

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
