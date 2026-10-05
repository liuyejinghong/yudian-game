using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Godot;

namespace Yudian.Terrain;

/// <summary>实际权威提交场景；旧Main玩法/导航/存档不在此场景。</summary>
public partial class TerrainWorldDemo : Node3D
{
    private TerrainRegionView _region;
    private Label _status;
    private Action _pending;
    private bool _selfTest;
    private int _frame, _assignedFrame, _step;
    private long? _verifiedVersion;
    private TerrainPatch _firstPatch, _lastPatch;
    private string _lastRequest;
    private Mesh _savedMesh;
    private Shape3D _savedShape;
    private int _requestNumber;
    private string _message = "就绪";

    public override void _Ready()
    {
        try
        {
            string dll = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Yudian.dll");
            using var stream = System.IO.File.OpenRead(dll);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Require(typeof(TerrainWorldDemo).Assembly.ManifestModule.ModuleVersionId ==
                metadata.GetGuid(metadata.GetModuleDefinition().Mvid), "loaded MVID == Debug file");
            GD.Print($"WORLD_IDENTITY CLR={System.Environment.Version} display={DisplayServer.GetName()} " +
                $"adapter={RenderingServer.GetVideoAdapterName()} " +
                $"build_sha256={Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(dll))).ToLowerInvariant()}");
            _selfTest = OS.GetCmdlineUserArgs().Contains("--terrain-world-self-test");
            _region = new TerrainRegionView(); AddChild(_region);
            _region.Initialize(TerrainDataCodec.ParseSnapshot(
                Godot.FileAccess.GetFileAsString("res://fixtures/terrain-probe-r1/base.json")));
            _firstPatch = Patch(_region.Current, "initial-level", .4);
            AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), LightEnergy = 1.5f });
            var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(.12f, .16f, .21f), AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White, AmbientLightEnergy = .55f };
            AddChild(new WorldEnvironment { Environment = environment });
            var camera = new Camera3D { Position = new Vector3(15, 16, 19), Current = true, Fov = 50 };
            AddChild(camera); camera.LookAt(new Vector3(0, 1, 0), Vector3.Up);
            var ui = new CanvasLayer(); AddChild(ui);
            var panel = new VBoxContainer { Position = new Vector2(20, 20) }; ui.AddChild(panel);
            panel.AddChild(new Label { Text = "余电 · 单区域权威提交\n平整 / 挖低修改当前区域；旧版拒绝，故障从当前版本恢复" });
            foreach (string command in new[] { "平整", "挖低", "重放上次请求", "取消请求", "提交旧版", "恢复投影" })
            {
                var button = new Button { Text = command }; panel.AddChild(button);
                button.Pressed += () => _pending = () => RunCommand(command);
            }
            _status = new Label(); panel.AddChild(_status); ShowStatus();
        }
        catch (Exception ex) { Fail(ex); }
    }

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            _frame++;
            if (_region.ProjectionVersion == null) _verifiedVersion = null;
            else if (_frame > _assignedFrame && _verifiedVersion != _region.ProjectionVersion) VerifyProjection();
            if (_selfTest) TestStep();
            if (_pending != null)
            {
                Action action = _pending; _pending = null; action();
            }
            ShowStatus();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private TerrainCommitResult Submit(string id, TerrainPatch patch)
    {
        long? before = _region.ProjectionVersion;
        TerrainCommitResult result = _region.Submit(id, patch);
        if (before != _region.ProjectionVersion || result.Status == TerrainCommitStatus.Committed)
        {
            _assignedFrame = _frame; _verifiedVersion = null;
        }
        _message = $"{result.Status} · request={id} · applied={result.AppliedVersion}";
        GD.Print($"WORLD_REQUEST {_message} authority={_region.Current.Version} bound={_region.ProjectionVersion} error={_region.ProjectionError}");
        return result;
    }

    private void RunCommand(string command)
    {
        if (command == "恢复投影")
        {
            _region.RetryProjection(); _assignedFrame = _frame; _verifiedVersion = null; _message = "从Current重建投影"; return;
        }
        if (command == "重放上次请求")
        {
            if (_lastPatch != null) Submit(_lastRequest, _lastPatch); else _message = "尚无请求";
            return;
        }
        if (command == "提交旧版")
        {
            if (_region.Current.Version == _firstPatch.Base.Version) _message = "尚无旧版本，请先修改区域";
            else Submit("stale-ui", _firstPatch);
            return;
        }
        TerrainPatch patch = Patch(_region.Current, "ui-" + ++_requestNumber, command == "挖低" ? -1.25 : .4);
        _region.CancellationRequested = command == "取消请求";
        try
        {
            _lastRequest = "request-" + _requestNumber; _lastPatch = patch;
            Submit(_lastRequest, patch);
        }
        finally { _region.CancellationRequested = false; }
    }

    private void ShowStatus()
    {
        if (_status == null) return;
        _status.Text = $"权威 v{_region.Current.Version} · 显示 v{_region.ProjectionVersion?.ToString() ?? "未知"} · " +
            $"物理已验证 v{_verifiedVersion?.ToString() ?? "待验"}\n中心高程 {_region.Current.GetHeight(3, 3):F2} m\n" +
            _message + (_region.ProjectionError == null ? "" : "\n投影故障：" + _region.ProjectionError) +
            "\n工程调试材质；导航、保存、机器人与正式模型尚未接入";
    }

    private void VerifyProjection()
    {
        var mesh = (ArrayMesh)_region.MeshNode.Mesh;
        var shape = (ConcavePolygonShape3D)_region.Collider.Shape;
        using Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector3[] faces = shape.GetFaces();
        Require(vertices.Length == faces.Length, "mesh/shape count");
        for (int i = 0; i < faces.Length; i++) Require(vertices[i].DistanceTo(faces[i]) <= .0001f, $"mesh/shape corner {i}: render={vertices[i]} physics={faces[i]}");
        float center = (float)_region.Current.GetHeight(3, 3);
        CheckRay(0, 0, 1, false, center);
        float offCenter = _region.Current.Version == 9 ? 2.825f : center;
        CheckRay(1, .5f, 1, false, offCenter);
        CheckRay(0, 0, 1, true, null); CheckRay(20, 20, 1, false, null); CheckRay(0, 0, 2, false, null);
        _verifiedVersion = _region.ProjectionVersion;
        GD.Print($"WORLD_PHYSICS_SYNCED authority={_region.Current.Version} bound={_region.ProjectionVersion} " +
            $"verified={_verifiedVersion} center={center} assigned_frame={_assignedFrame} query_frame={_frame}");
    }

    private void CheckRay(float x, float z, uint mask, bool upward, float? expected)
    {
        using var query = PhysicsRayQueryParameters3D.Create(new Vector3(x, upward ? -15 : 15, z),
            new Vector3(x, upward ? 15 : -15, z), mask);
        query.HitBackFaces = false;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (expected == null) { Require(hit.Count == 0, "ray miss"); return; }
        Require(hit.Count > 0 && hit["collider"].AsGodotObject() == _region.Body, "ray body");
        Require(Math.Abs(hit["position"].AsVector3().Y - expected.Value) < .0001f, "ray height");
        Require(hit["normal"].AsVector3().Y > 0, "ray up normal");
    }

    private static TerrainPatch Patch(TerrainSnapshot snapshot, string id, double height)
    {
        var heights = snapshot.HeightsM.ToArray();
        for (int r = 1; r < snapshot.Rows - 1; r++)
            for (int c = 1; c < snapshot.Columns - 1; c++) heights[r * snapshot.Columns + c] = height;
        var json = new JsonObject { ["schema_version"] = 1, ["patch_id"] = id,
            ["base"] = JsonNode.Parse(TerrainDataCodec.Serialize(snapshot)),
            ["heights_m"] = new JsonArray(heights.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) };
        return TerrainDataCodec.ParsePatch(json.ToJsonString());
    }

    private void SaveResources() { _savedMesh = _region.MeshNode.Mesh; _savedShape = _region.Collider.Shape; }
    private void SameResources() => Require(ReferenceEquals(_savedMesh, _region.MeshNode.Mesh) &&
        ReferenceEquals(_savedShape, _region.Collider.Shape), "old resource pair retained");
    private void TestStep()
    {
        switch (_step++)
        {
            case 0:
                Require(_verifiedVersion == 9, "initial physics 9"); SaveResources();
                RunCommand("提交旧版");
                Require(_region.Current.Version == 9, "cold stale button does not submit valid initial patch"); SameResources();
                Require(Submit("level-one", _firstPatch).Status == TerrainCommitStatus.Committed, "9->10");
                Require(_region.Current.Version == 10 && _region.ProjectionVersion == 10, "published and bound 10");
                Require(!GodotObject.IsInstanceValid(_savedMesh) && !GodotObject.IsInstanceValid(_savedShape), "old pair released");
                break;
            case 1:
                Require(_verifiedVersion == 10, "physics verified 10");
                Require(Submit("dig-two", Patch(_region.Current, "dig-two", -1.25)).Status == TerrainCommitStatus.Committed, "10->11");
                break;
            case 2:
                Require(_verifiedVersion == 11, "physics verified 11"); SaveResources();
                _region.PermissionGranted = false; _region.CancellationRequested = true;
                var replay = Submit("level-one", _firstPatch);
                Require(replay.Status == TerrainCommitStatus.AlreadyCommitted && replay.AppliedVersion == 10 && replay.Snapshot.Version == 11, "successful historical receipt");
                _region.PermissionGranted = true; _region.CancellationRequested = false; SameResources();
                Require(Submit("level-one", Patch(_region.Current, "conflict", 1)).Status == TerrainCommitStatus.RequestConflict, "request conflict");
                Require(Submit("stale", _firstPatch).Status == TerrainCommitStatus.StaleBase, "stale base");
                Require(Submit("same", Patch(_region.Current, "same", -1.25)).Status == TerrainCommitStatus.NoChange, "no change");
                _region.PermissionGranted = false;
                Require(Submit("denied", Patch(_region.Current, "denied", .4)).Status == TerrainCommitStatus.PermissionDenied, "permission denied");
                _region.PermissionGranted = true;
                _region.AfterPrepareForTest = () => _region.CancellationRequested = true;
                Require(Submit("cancel", Patch(_region.Current, "cancel", .4)).Status == TerrainCommitStatus.Cancelled, "cancel after preparation");
                _region.CancellationRequested = false;
                _region.AfterPrepareForTest = () => _region.PermissionGranted = false;
                Require(Submit("revoke", Patch(_region.Current, "revoke", .4)).Status == TerrainCommitStatus.PermissionDenied, "revoke after preparation");
                _region.PermissionGranted = true; _region.AfterPrepareForTest = null;
                bool rejected = false;
                _region.FailBeforeShapeForTest = true;
                try { Submit("prepare-failed", Patch(_region.Current, "prepare-failed", .4)); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("simulated")) { rejected = true; }
                finally { _region.FailBeforeShapeForTest = false; }
                Require(rejected && _region.Current.Version == 11, "resource rejection before authority publish"); SameResources();
                _region.Body.RemoveChild(_region.Collider);
                bool unavailable = false;
                try { Submit("unavailable", Patch(_region.Current, "unavailable", .4)); }
                catch (InvalidOperationException) { unavailable = true; }
                finally { _region.Body.AddChild(_region.Collider); }
                Require(unavailable && _region.Current.Version == 11, "unavailable targets rejected before commit"); SameResources();
                break;
            case 3:
                TerrainPatch delayed = Patch(_region.Current, "delayed", .4);
                _region.AfterPrepareForTest = () =>
                {
                    _region.AfterPrepareForTest = null;
                    Submit("concurrent", Patch(_region.Current, "concurrent", -.5));
                };
                Require(Submit("delayed", delayed).Status == TerrainCommitStatus.StaleBase && _region.Current.Version == 12,
                    "revalidate current after preparation");
                break;
            case 4:
                Require(_verifiedVersion == 12, "physics 12"); SaveResources();
                _region.AfterMeshBoundForTest = () => throw new InvalidOperationException("synthetic failure after mesh binding");
                var faultPatch = Patch(_region.Current, "fault", .2);
                Require(Submit("prepare-failed", faultPatch).Status == TerrainCommitStatus.Committed, "failed preparation did not consume id");
                Require(_region.Current.Version == 13 && _region.ProjectionVersion == null && _region.ProjectionError != null,
                    "authority 13 remains committed; projection fault explicit"); SameResources();
                Require(Submit("prepare-failed", faultPatch).Status == TerrainCommitStatus.AlreadyCommitted && _region.ProjectionVersion == null,
                    "replay does not silently recover or add version");
                _region.AfterMeshBoundForTest = null;
                break;
            case 5:
                Require(_verifiedVersion == null, "fault not marked physics synchronized");
                CheckRay(0, 0, 1, false, -.5f);
                _region.RetryProjection(); _assignedFrame = _frame; _verifiedVersion = null;
                Require(_region.Current.Version == 13 && _region.ProjectionVersion == 13 && _region.ProjectionError == null,
                    "recover from Current without new commit");
                Require(!GodotObject.IsInstanceValid(_savedMesh) && !GodotObject.IsInstanceValid(_savedShape), "recovery releases old pair");
                break;
            case 6:
                Require(_verifiedVersion == 13, "recovery native physics 13"); SaveResources();
                _region.AfterMeshBoundForTest = () => _region.Collider.Free();
                bool stopped = false;
                try { Submit("target-freed", Patch(_region.Current, "target-freed", .6)); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("restoration failed")) { stopped = true; }
                Require(stopped && _region.Current.Version == 14 && _region.ProjectionVersion == null,
                    "native target loss after commit stops caller without authority rollback");
                RemoveChild(_region); _region.Free();
                Require(!GodotObject.IsInstanceValid(_savedMesh) && !GodotObject.IsInstanceValid(_savedShape), "exit releases pair");
                GD.Print("TERRAIN_WORLD_SELF_TEST PASS"); GetTree().Quit(0); break;
        }
        GD.Print($"WORLD_TEST_STEP PASS {_step - 1}");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);

    }
    private void Fail(Exception ex) { GD.PrintErr("TERRAIN_WORLD_FAIL " + ex); GetTree().Quit(1); }
}
