using Godot;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Yudian.Terrain;

public partial class TerrainProbe
{
    private bool _collisionEnabled, _collisionSelfTest, _surfaceMaterial;
    private string _pending, _baseline;
    private StaticBody3D _stageBody, _fineBody;
    private CollisionShape3D _stageCollider, _fineCollider;
    private ConcavePolygonShape3D _stageShape, _fineShape;
    private StandardMaterial3D _probeMaterial;
    private int _physicsFrame, _assignedFrame = -1, _testStep, _failureReleased;
    private GodotObject[] _saved;

    private void ConfigureCollision(string[] args)
    {
        _collisionSelfTest = Array.IndexOf(args, "--terrain-collision-self-test") >= 0;
        _collisionEnabled = _collisionSelfTest || Array.IndexOf(args, "--terrain-collision") >= 0;
        int materialArg = Array.IndexOf(args, "--terrain-material");
        if (materialArg < 0) return;
        Require(materialArg + 1 < args.Length, "material path argument");
        Require(!_cullCheck, "material and cull-check modes exclusive");
        _probeMaterial = LoadSurfaceMaterial(args[materialArg + 1]);
        _surfaceMaterial = true;
    }

    private static StandardMaterial3D LoadSurfaceMaterial(string path)
    {
        if (!path.StartsWith("res://", StringComparison.Ordinal) || !ResourceLoader.Exists(path))
            throw new ArgumentException("terrain material resource missing: " + path);
        Resource resource = ResourceLoader.Load(path);
        try
        {
            ValidateSurfaceMaterial(resource);
            string hash = Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(path))));
            GD.Print($"PROBE_MATERIAL path={path} file_SHA256={hash} RID={resource.GetRid()} binding=surface0");
            return (StandardMaterial3D)resource;
        }
        catch { resource?.Dispose(); throw; }
    }

    private static void ValidateSurfaceMaterial(Resource resource)
    {
        if (resource is not StandardMaterial3D material)
            throw new ArgumentException("terrain material must be StandardMaterial3D");
        if (material.CullMode != BaseMaterial3D.CullModeEnum.Back ||
            material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled || material.AlbedoColor.A != 1f)
            throw new ArgumentException("terrain material must be opaque with back-face culling");
    }

    private void BuildCollisionBodies()
    {
        _stageBody = new StaticBody3D { Position = _stageNode.Position, CollisionLayer = 1, CollisionMask = 0 };
        _fineBody = new StaticBody3D { Position = _fineNode.Position, CollisionLayer = 2, CollisionMask = 0 };
        _stageCollider = new CollisionShape3D(); _fineCollider = new CollisionShape3D();
        _stageBody.AddChild(_stageCollider); _fineBody.AddChild(_fineCollider);
        AddChild(_stageBody); AddChild(_fineBody);
        GD.Print($"PROBE_PHYSICS configured_backend={ProjectSettings.GetSetting("physics/3d/physics_engine")} server_class={PhysicsServer3D.Singleton.GetClass()} layers=1,2 masks=0");
    }

    private void QueueRequest(string request)
    {
        if (_pending == request) { GD.Print("PROBE_PENDING_NOOP request=" + request); return; }
        // Even a request for the active mode replaces a prior pending candidate.
        _pending = request;
        GD.Print($"PROBE_QUEUE request={request} active={_mode}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_collisionEnabled) return;
        try
        {
            _physicsFrame++;
            if (_assignedFrame >= 0 && _physicsFrame > _assignedFrame)
            {
                VerifyPhysicsSync();
                _assignedFrame = -1;
            }
            if (_collisionSelfTest) CollisionTestStep();
            string request = _pending; _pending = null;
            if (request == null) return;
            if (request is "stale" or "perimeter") ApplyFailure(request == "perimeter");
            else if (request == "broken")
            {
                try { Replace("broken", () => StageMeshBuilder.Build(_base), () => HeightfieldMeshBuilder.Build(_base), true); }
                catch (ArgumentException ex) { GD.Print("PROBE_REJECT second_shape=" + ex.Message); }
            }
            else ApplySelection(request);
        }
        catch (Exception ex) { GD.PushError("PROBE_COLLISION_FAIL " + ex); GetTree().Quit(1); }
    }

    private static TerrainMesh BrokenCollisionMesh() => new TerrainMesh(2, 2,
        new[] { new TerrainVertex(0, 0, 0), new TerrainVertex(1, 0, 0), new TerrainVertex(0, 0, 1), new TerrainVertex(1, 0, 1) },
        new[] { 0, 1, 2, 1, 3, 2 }); // Negative Y winding, rejected by the existing adapter.

    private Godot.Collections.Dictionary Ray(Vector3 from, Vector3 to, uint mask)
    {
        using var query = PhysicsRayQueryParameters3D.Create(from, to, mask);
        query.CollideWithBodies = true; query.CollideWithAreas = false;
        query.HitBackFaces = false; query.HitFromInside = false;
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    private void VerifyPhysicsSync()
    {
        float center = _mode switch { "base" => 3.5f, "level" => .4f, "dig" => -1.25f, _ => throw new InvalidOperationException("active mode") };
        // The base cell is a planar slope: neighbor at 2m is 2.6m, gradient -.45/m on X and Z.
        float slope = _mode == "base" ? 3.5f - .45f * (.5f + .25f) : center;
        foreach (var item in new[] { (_stageBody, _stageCollider, _stageShape, 1u), (_fineBody, _fineCollider, _fineShape, 2u) })
        {
            var (body, collider, shape, layer) = item;
            Require(collider.Shape == shape && body.Position == (layer == 1 ? _stageNode.Position : _fineNode.Position), "body binding and display transform");
            foreach (var sample in new[] { (Vector3.Zero, center), (new Vector3(.5f, 0, .25f), slope) })
            {
                Vector3 position = body.Position + sample.Item1;
                using var hit = Ray(position + Vector3.Up * 20, position + Vector3.Down * 20, layer);
                Require(hit.Count > 0 && hit["collider"].AsGodotObject() == body, "ray hits expected body");
                Vector3 point = hit["position"].AsVector3(), normal = hit["normal"].AsVector3();
                Require(MathF.Abs(point.Y - sample.Item2) < 1e-4f && MathF.Abs(point.X - position.X) < 1e-4f && MathF.Abs(point.Z - position.Z) < 1e-4f,
                    "ray world position matches independent planar height");
                Require(normal.IsFinite() && normal.Y > 0 && MathF.Abs(normal.Length() - 1) < 1e-4f, "ray normal finite unit upward");
            }
            Vector3 offset = body.Position + new Vector3(.5f, 0, .25f);
            using var reverse = Ray(offset + Vector3.Down * 20, offset + Vector3.Up * 20, layer);
            using var outside = Ray(body.Position + new Vector3(7, 20, 7), body.Position + new Vector3(7, -20, 7), layer);
            using var wrongMask = Ray(offset + Vector3.Up * 20, offset + Vector3.Down * 20, layer == 1 ? 2u : 1u);
            Require(reverse.Count == 0 && outside.Count == 0 && wrongMask.Count == 0, "backside outside wrong-mask rays miss");
        }
        if (_surfaceMaterial)
            Require(_stageNode.MaterialOverride == null && _fineNode.MaterialOverride == null &&
                _stageNode.GetSurfaceOverrideMaterial(0) == _probeMaterial && _fineNode.GetSurfaceOverrideMaterial(0) == _probeMaterial,
                "accepted art material binds only surface0");
        GD.Print($"PROBE_PHYSICS_SYNCED space_state_class={GetWorld3D().DirectSpaceState.GetClass()} mode={_mode} replacements={_count} assigned_frame={_assignedFrame} query_frame={_physicsFrame} stage_body={_stageBody.GetInstanceId()} fine_body={_fineBody.GetInstanceId()} center={center} nonvertex={slope}");
        if (_status != null) _status.Text += "\n静态碰撞已同步 · 物理帧 " + _physicsFrame;
    }

    private void SaveFour() => _saved = new GodotObject[] { _stage, _fine, _stageShape, _fineShape };
    private void SameFour(string name) => Require(ReferenceEquals(_saved[0], _stage) && ReferenceEquals(_saved[1], _fine) &&
        ReferenceEquals(_saved[2], _stageShape) && ReferenceEquals(_saved[3], _fineShape), name);

    private void CollisionTestStep()
    {
        switch (_testStep++)
        {
            case 0: _baseline = TerrainDataCodec.Serialize(_base); break; // Initial pending base is consumed below.
            case 1:
                Require(_mode == "base" && _count == 1, "collision cold start base"); SaveFour();
                Select("level"); Select("base"); break;
            case 2:
                Require(_mode == "base" && _count == 1, "level then active base cancels pending"); SameFour("cancel keeps four resources");
                Select("level"); Select("dig"); Select("dig"); break;
            case 3:
                Require(_mode == "dig" && _count == 2, "latest dig and duplicate pending apply once");
                foreach (GodotObject resource in _saved) Require(!GodotObject.IsInstanceValid(resource), "old native resource released");
                CheckReadback(-1.25f); SaveFour(); Select("level"); Select("dig"); break;
            case 4:
                Require(_mode == "dig" && _count == 2, "active dig cancels pending level"); SameFour("duplicate keeps four resources");
                Select("level"); InjectFailure(false); break;
            case 5:
                Require(_mode == "dig" && _count == 2 && _pending == null, "latest stale rejects without latent candidate"); SameFour("stale keeps four resources");
                InjectFailure(true); break;
            case 6:
                Require(_mode == "dig" && _count == 2 && _pending == null, "perimeter rejection keeps candidate"); SameFour("perimeter keeps four resources");
                _failureReleased = _temporaryReleased; QueueRequest("broken"); break;
            case 7:
                Require(_mode == "dig" && _count == 2 && _temporaryReleased == _failureReleased + 3, "second shape rejection releases three prepared resources");
                SameFour("second shape rejection keeps all four"); Select("level"); break;
            case 8:
                Require(_mode == "level" && _count == 3, "valid request works after rejection"); CheckReadback(.4f);
                Select("base"); break;
            case 9:
                Require(_mode == "base" && _count == 4 && _base.Version == 9 && TerrainDataCodec.Serialize(_base) == _baseline,
                    "collision reset keeps immutable base");
                MaterialRejectionChecks(); ReleaseMeshes();
                Require(_stage == null && _fine == null && _stageShape == null && _fineShape == null &&
                    _stageNode.Mesh == null && _fineNode.Mesh == null && _stageCollider.Shape == null && _fineCollider.Shape == null,
                    "four resources detached and released on exit");
                GD.Print("PROBE_COLLISION_SELF_TEST PASS"); GetTree().Quit(0); break;
        }
    }

    private static void MaterialRejectionChecks()
    {
        bool missing = false;
        try { using var unexpected = LoadSurfaceMaterial("res://fixtures/missing-art-material.tres"); } catch (ArgumentException) { missing = true; }
        Require(missing, "missing material rejected without fallback");
        using var wrong = new ArrayMesh();
        bool badType = false; try { ValidateSurfaceMaterial(wrong); } catch (ArgumentException) { badType = true; }
        Require(badType, "wrong material type rejected");
        using var material = new StandardMaterial3D { CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        bool doubleSided = false; try { ValidateSurfaceMaterial(material); } catch (ArgumentException) { doubleSided = true; }
        Require(doubleSided, "double-sided material rejected");
        material.CullMode = BaseMaterial3D.CullModeEnum.Back; material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        bool transparent = false; try { ValidateSurfaceMaterial(material); } catch (ArgumentException) { transparent = true; }
        Require(transparent, "transparent material rejected");
    }

    private void ReleaseCollision()
    {
        _pending = null; _assignedFrame = -1;
        if (_stageCollider != null) _stageCollider.Shape = null;
        if (_fineCollider != null) _fineCollider.Shape = null;
        _stageShape?.Dispose(); _fineShape?.Dispose(); _stageShape = null; _fineShape = null;
    }
}
