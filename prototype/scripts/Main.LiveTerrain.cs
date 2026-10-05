#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Yudian.Terrain;

namespace Yudian;

public partial class Main
{
    private bool _liveMode, _groundReady, _groundFault;
    private TerrainSnapshot _liveInitial = null!;
    private TerrainRegionView? _liveTerrain;
    private readonly List<GroundPatrol> _groundRobots = new();
    private Label? _groundStatus;
    private string? _groundCommand;
    private string _groundMessage = "等待原生物理同步";
    private long? _groundVerified;
    private int _groundFrame, _groundAssignedFrame, _groundRequest;
    private int[] _groundRayIndices = [];
    private float _groundRayTop, _groundRayBottom;
    private static readonly float[] FacilityRadii = [2.5f, 2f, 2.8f, 1.7f, 1.9f, 1.1f];

    private void PrepareLiveTerrain()
    {
        int n = _cfg.Terrain.Segments + 1;
        if (n > 257) throw new ArgumentException("live-terrain requires terrain.segments <= 256");
        double spacing = (double)_cfg.Terrain.Size / _cfg.Terrain.Segments;
        var heights = new double[n * n];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                heights[r * n + c] = TerrainHeight((float)(-_cfg.Terrain.Size / 2 + c * spacing),
                    (float)(-_cfg.Terrain.Size / 2 + r * spacing));
        _liveInitial = TerrainDataCodec.ParseSnapshot(JsonSerializer.Serialize(new
        {
            schema_version = 1, region_id = "main-region", version = 0,
            origin_x_m = -_cfg.Terrain.Size / 2, origin_z_m = -_cfg.Terrain.Size / 2,
            spacing_m = spacing, rows = n, columns = n, heights_m = heights
        }));
        float half = _cfg.Terrain.Size / 2;
        if (_cfg.Scale.Facilities > 0 && _cfg.Scale.RingRadius + FacilityRadii.Max() >= half)
            throw new ArgumentException("live-terrain facility footprints must lie inside terrain");
        foreach (var feature in _features)
            if (Math.Abs(feature.Position[0]) + 2 >= half || Math.Abs(feature.Position[1]) + 2 >= half)
                throw new ArgumentException("live-terrain modification sites must lie inside terrain");
    }

    private void BuildLiveTerrain()
    {
        _liveTerrain = new TerrainRegionView(); AddChild(_liveTerrain); _liveTerrain.Initialize(_liveInitial);
        _groundRayIndices = Enumerable.Range(0, _liveInitial.HeightsM.Count).ToArray();
        var ui = new CanvasLayer(); AddChild(ui);
        var panel = new VBoxContainer { Position = new Vector2(20, 20) }; ui.AddChild(panel);
        panel.AddChild(new Label { Text = "余电 · 整平任务\n工程灰模；一名筑垒执行整平" });
        BuildLevelPanel(panel);
        foreach (string command in new[] { "工程：直接整平", "矿点挖低", "从Current恢复投影" })
        {
            var button = new Button { Text = command }; panel.AddChild(button);
            button.Pressed += () => _groundCommand = command;
        }
        _groundStatus = new Label(); panel.AddChild(_groundStatus);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_liveMode || _liveTerrain == null) return;
        try
        {
            _groundFrame++;
            if (!_groundFault && _groundFrame > _groundAssignedFrame &&
                _groundVerified != _liveTerrain.ProjectionVersion) VerifyGroundProjection();
            if (!_groundReady && _groundVerified != null)
            {
                BuildFacilities(); SpawnRobots(); _groundReady = true;
                GD.Print($"MAIN_GROUND_READY robots={_groundRobots.Count} facilities={_facilityPositions.Count}");
            }
            if (_groundCommand != null)
            {
                string command = _groundCommand; _groundCommand = null;
                if (command == "下达整平任务") StartLevelJob();
                else if (command == "取消整平任务") CancelLevelJob();
                else if (command == "从Current恢复投影") RecoverGround();
                else if (_levelJob?.Active == true) _groundMessage = "活动整平任务期间拒绝直接改造";
                else if (_groundReady && !_groundFault && _groundVerified == _liveTerrain.Current.Version)
                {
                    var site = command == "工程：直接整平" ? _cfg.Terrain.Mound : _cfg.Terrain.MineralPit;
                    SubmitGround(GroundPatch(site.Position[0], site.Position[1], command == "工程：直接整平" ? 0 : -3));
                }
                else _groundMessage = "等待物理同步或先恢复投影";
            }
            TickLevelJob(delta);
            if (System.Environment.GetEnvironmentVariable("YUDIAN_LEVEL_SELF_TEST") == "1") LevelTestStep(delta);
            if (System.Environment.GetEnvironmentVariable("YUDIAN_LIVE_SELF_TEST") == "1") GroundTestStep();
            _groundStatus!.Text = $"权威 v{_liveTerrain.Current.Version} · 绑定 v{_liveTerrain.ProjectionVersion?.ToString() ?? "未知"} · 物理 v{_groundVerified?.ToString() ?? "待验"}\n" +
                $"机器人 {_groundRobots.Count} · 贴地 {_groundRobots.Count(x => x.IsOnFloor())} · 停滞 {_groundRobots.Count(x => x.Blocked)}\n" +
                _groundMessage + "\n完整导航、设施建设、经济、保存尚未接入";
        }
        catch (Exception ex)
        {
            GroundFault(ex);
            if (System.Environment.GetEnvironmentVariable("YUDIAN_LIVE_SELF_TEST") == "1" ||
                System.Environment.GetEnvironmentVariable("YUDIAN_LEVEL_SELF_TEST") == "1") Fail(ex);
        }
    }

    private float GroundHeight(float x, float z)
    {
        using var query = PhysicsRayQueryParameters3D.Create(new Vector3(x, _groundRayTop, z), new Vector3(x, _groundRayBottom, z), 1);
        query.HitBackFaces = false; query.CollideWithAreas = false;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != _liveTerrain!.Body || hit["normal"].AsVector3().Y <= 0)
            throw new InvalidOperationException($"terrain ray missing at {x},{z}");
        return hit["position"].AsVector3().Y;
    }

    private void VerifyGroundProjection()
    {
        var region = _liveTerrain!;
        if (region.ProjectionVersion != region.Current.Version || region.ProjectionError != null)
            throw new InvalidOperationException("authority/projection mismatch: " + region.ProjectionError);
        var mesh = (ArrayMesh)region.MeshNode.Mesh;
        var shape = (ConcavePolygonShape3D)region.Collider.Shape;
        using var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(); var faces = shape.GetFaces();
        GroundRequire(vertices.Length == faces.Length, "mesh/shape corner count");
        for (int i = 0; i < faces.Length; i++) GroundRequire(vertices[i].DistanceTo(faces[i]) <= .0001f, "mesh/shape corner");
        var s = region.Current;
        // 贴近当前高度范围，避免长射线的float消减误差；不放宽高度核验容差。
        _groundRayTop = (float)s.HeightsM.Max() + 2; _groundRayBottom = (float)s.HeightsM.Min() - 2;
        foreach (int i in _groundRayIndices)
        {
            float x = (float)(s.OriginXM + i % s.Columns * s.SpacingM);
            float z = (float)(s.OriginZM + i / s.Columns * s.SpacingM);
            float observed = GroundHeight(x, z);
            GroundRequire(Math.Abs(observed - s.HeightsM[i]) <= .0001, $"changed vertex native ray index={i} expected={s.HeightsM[i]:R} observed={observed:R}");
        }
        _groundVerified = s.Version; PauseGround(false);
        _groundMessage = "当前物理已验证，继续巡逻";
        GD.Print($"MAIN_GROUND_SYNC authority={s.Version} bound={region.ProjectionVersion} verified={_groundVerified} assigned_frame={_groundAssignedFrame} query_frame={_groundFrame} rays={_groundRayIndices.Length}");
    }

    private TerrainPatch GroundPatch(float x, float z, double height, string? id = null)
    {
        var s = _liveTerrain!.Current; var heights = s.HeightsM.ToArray();
        for (int r = 1; r < s.Rows - 1; r++)
            for (int c = 1; c < s.Columns - 1; c++)
            {
                double dx = s.OriginXM + c * s.SpacingM - x, dz = s.OriginZM + r * s.SpacingM - z;
                if (dx * dx + dz * dz <= 4) heights[r * s.Columns + c] = height;
            }
        return GroundPatchFromHeights(s, heights, id ?? "main-edit-" + ++_groundRequest);
    }

    private static TerrainPatch GroundPatchFromHeights(TerrainSnapshot s, double[] heights, string id)
        => TerrainDataCodec.ParsePatch(new JsonObject { ["schema_version"] = 1, ["patch_id"] = id,
            ["base"] = JsonNode.Parse(TerrainDataCodec.Serialize(s)),
            ["heights_m"] = new JsonArray(heights.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) }.ToJsonString());

    private static bool TouchesFootprint(TerrainPatch patch, float x, float z, float radius)
    {
        var s = patch.Base;
        // ponytail: small main scene, scan changed vertices; spatial index only if measured occupancy cost warrants it.
        for (int i = 0; i < patch.HeightsM.Count; i++)
        {
            if (patch.HeightsM[i] == s.HeightsM[i]) continue;
            int r = i / s.Columns, c = i % s.Columns;
            for (int rr = Math.Max(0, r - 1); rr <= Math.Min(r, s.Rows - 2); rr++)
                for (int cc = Math.Max(0, c - 1); cc <= Math.Min(c, s.Columns - 2); cc++)
                {
                    double left = s.OriginXM + cc * s.SpacingM, top = s.OriginZM + rr * s.SpacingM;
                    if (x + radius >= left && x - radius <= left + s.SpacingM &&
                        z + radius >= top && z - radius <= top + s.SpacingM) return true;
                }
        }
        return false;
    }

    private bool SubmitGround(TerrainPatch patch)
        => SubmitGroundResult(patch)?.Status == TerrainCommitStatus.Committed;

    private TerrainCommitResult? SubmitGroundResult(TerrainPatch patch)
    {
        PauseGround(true);
        var region = _liveTerrain!;
        if (_groundFault || _groundVerified != region.Current.Version || region.ProjectionVersion != region.Current.Version)
        { _groundMessage = "物理尚未验证，拒绝改造"; return null; }
        for (int i = 0; i < _facilityPositions.Count; i++)
            if (TouchesFootprint(patch, _facilityPositions[i].X, _facilityPositions[i].Z, FacilityRadii[i % 6]))
                { RejectOccupied("设施 " + i); return null; }
        foreach (var actor in _groundRobots)
            if (TouchesFootprint(patch, actor.GlobalPosition.X, actor.GlobalPosition.Z, .35f))
                { RejectOccupied(actor.Name.ToString()); return null; }
        _groundRayIndices = Enumerable.Range(0, patch.HeightsM.Count).Where(i => patch.HeightsM[i] != patch.Base.HeightsM[i]).ToArray();
        try
        {
            var result = region.Submit(patch.PatchId, patch);
            _groundMessage = result.Status.ToString();
            if (result.Status != TerrainCommitStatus.Committed)
            { PauseGround(false); return result; }
            _groundAssignedFrame = _groundFrame; _groundVerified = null;
            if (region.ProjectionVersion != region.Current.Version) GroundFault(new InvalidOperationException(region.ProjectionError));
            GD.Print($"MAIN_GROUND_COMMIT authority={region.Current.Version} bound={region.ProjectionVersion} paused={_groundRobots.All(x => x.Paused)}");
            return result;
        }
        catch (Exception ex) { GroundFault(ex); throw; }
    }

    private bool RejectOccupied(string name)
    {
        _groundMessage = "占用拒绝：" + name + "；巡逻继续，可待离开后重试"; PauseGround(false);
        GD.Print("MAIN_GROUND_OCCUPIED " + name); return false;
    }
    private void PauseGround(bool value) { foreach (var actor in _groundRobots) actor.Paused = value; }
    private void GroundFault(Exception ex)
    {
        _groundFault = true; _groundVerified = null; PauseGround(true); _groundMessage = "投影故障，巡逻暂停：" + ex.Message;
        GD.Print("MAIN_GROUND_PAUSED " + ex.Message);
    }
    private void RecoverGround()
    {
        PauseGround(true); _groundVerified = null; _liveTerrain!.RetryProjection();
        _groundFault = _liveTerrain.ProjectionVersion != _liveTerrain.Current.Version;
        _groundAssignedFrame = _groundFrame;
        _groundRayIndices = Enumerable.Range(0, _liveTerrain.Current.HeightsM.Count).ToArray();
        if (_groundFault) GroundFault(new InvalidOperationException(_liveTerrain.ProjectionError ?? "恢复绑定失败"));
        else _groundMessage = "从Current恢复，等待下一物理帧";
    }
    private static void GroundRequire([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string message)
    { if (!value) throw new InvalidOperationException("MAIN_GROUND_CHECK " + message); }
}
