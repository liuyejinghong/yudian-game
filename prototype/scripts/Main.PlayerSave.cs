#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Yudian.Construction;
using Yudian.Terrain;

namespace Yudian;
public partial class Main
{
    private sealed class RobotSave
    {
        public required string Id { get; init; }
        public required float[] Position { get; init; }
        public required float[] Velocity { get; init; }
        public required float Yaw { get; init; }
    }
    private sealed class JobSave
    {
        public required string Patch { get; init; }
        public required string Stage { get; init; }
        public required string? Worker { get; init; }
        public required float[] Center { get; init; }
        public required float[] Station { get; init; }
        public required double Work { get; init; }
        public required double Travel { get; init; }
        public required double Space { get; init; }
        public required long? AppliedVersion { get; init; }
        public required string Message { get; init; }
    }
    private sealed class PlayerSave
    {
        public required int Schema { get; init; }
        public required string FixtureHash { get; init; }
        public required string Terrain { get; init; }
        public required double Time { get; init; }
        public required bool Paused { get; init; }
        public required bool Permission { get; init; }
        public required bool Cancelled { get; init; }
        public required int Sequence { get; init; }
        public required RobotSave[] Robots { get; init; }
        public required JobSave? Job { get; init; }
        public BootstrapSave? Bootstrap { get; init; }
    }
    private static readonly JsonSerializerOptions SaveOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private string PlayerSavePath => (System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_SELF_TEST") == "1" || System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1" || System.Environment.GetEnvironmentVariable("YUDIAN_BOOTSTRAP_SELF_TEST") == "1")
        ? System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE") ?? "/private/tmp/yudian-player-test.json"
        : ProjectSettings.GlobalizePath(BootstrapEnabled ? "user://saves/d11-player-v2.json" : "user://saves/d1-player-v1.json");
    private PlayerSave? _loadPending, _loadRollback;
    private bool _loadRecovered;
    private int _loadStarted;
    private static float[] SavedVector(Vector3 p) => [p.X, p.Y, p.Z];
    private static Vector3 LoadVector(float[] p)
    {
        if (p == null || p.Length != 3 || p.Any(x => !float.IsFinite(x) || Math.Abs(x) > 10000))
            throw new InvalidDataException("存档坐标无效");
        return new(p[0], p[1], p[2]);
    }
    private PlayerSave CapturePlayer()
    {
        if (BootstrapEnabled) SettleBaseMovement();
        return new()
        {
        Schema = BootstrapEnabled ? 2 : 1, Bootstrap = BootstrapEnabled ? CaptureBootstrap() : null, FixtureHash = _fixtureHash, Terrain = TerrainDataCodec.Serialize(_liveTerrain!.Current),
        Time = _playerTime, Paused = _userPaused, Permission = _liveTerrain.PermissionGranted, Cancelled = _liveTerrain.CancellationRequested, Sequence = _levelSequence,
        Robots = _groundRobots.Select(x => new RobotSave { Id = x.Name.ToString(), Position = SavedVector(x.GlobalPosition), Velocity = SavedVector(x.Velocity), Yaw = x.Rotation.Y }).ToArray(),
        Job = _levelJob is not {} j ? null : new JobSave
        {
            Patch = TerrainDataCodec.Serialize(j.Patch), Stage = j.Stage.ToString(), Worker = j.Worker?.Name.ToString(),
            Center = SavedVector(j.Center), Station = SavedVector(j.Station), Work = j.Work.ElapsedSeconds,
            Travel = j.TravelSeconds, Space = j.SpaceSeconds, AppliedVersion = j.AppliedVersion, Message = j.Message
        }
        };
    }
    private void SavePlayer()
    {
        if (!_groundReady || _loadPending != null) { _playerNotice = "等待世界恢复后保存"; return; }
        if (_groundRobots.Any(x => !x.IsOnFloor())) { _playerNotice = "等待机器人贴地后保存"; return; }
        var snapshot = CapturePlayer(); ValidatePlayerSave(snapshot);
        string text = JsonSerializer.Serialize(snapshot, SaveOptions);
        string path = PlayerSavePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush(true);
            }
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
            _playerNotice = "已保存；退出后点击读取可继续";
            GD.Print($"PLAYER_SAVE_OK version={_liveTerrain!.Current.Version} stage={_levelJob?.Stage} time={_playerTime:R}");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!seen.Add(property.Name)) throw new InvalidDataException("存档含重复字段"); RejectDuplicateFields(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateFields(child);
    }
    private void LoadPlayer()
    {
        if (!_groundReady || _loadPending != null) { _playerNotice = "等待当前世界恢复后读取"; return; }
        var path = PlayerSavePath;
        if (!File.Exists(path)) { _playerNotice = "还没有存档"; return; }
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("存档过大，未读取");
        string text = File.ReadAllText(path);
        using (var document = JsonDocument.Parse(text)) RejectDuplicateFields(document.RootElement);
        var snapshot = JsonSerializer.Deserialize<PlayerSave>(text, SaveOptions) ?? throw new InvalidDataException("空存档");
        var terrain = ValidatePlayerSave(snapshot);
        _loadRollback = CapturePlayer(); _loadRecovered = false;
        ApplyPlayerLoad(snapshot, terrain);
    }
    private void ApplyPlayerLoad(PlayerSave snapshot, TerrainSnapshot terrain)
    {
        var next = new TerrainRegionView(); AddChild(next);
        if (_playerTestInjectLoadFault && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_SELF_TEST") == "1")
        { _playerTestInjectLoadFault = false; next.AfterMeshBoundForTest = () => throw new InvalidOperationException("synthetic player load projection fault"); }
        try { next.Initialize(terrain); next.PermissionGranted = snapshot.Permission; next.CancellationRequested = snapshot.Cancelled; }
        catch { RemoveChild(next); next.QueueFree(); throw; }
        var old = _liveTerrain!; old.Body.CollisionLayer = 0; RemoveChild(old); old.QueueFree(); _liveTerrain = next;
        _loadPending = snapshot; _loadStarted = _groundFrame; _userPaused = snapshot.Paused;
        _playerTime = snapshot.Time; _levelSequence = snapshot.Sequence; _levelJob = null; _levelNotice = "";
        _groundVerified = null; _groundFault = next.ProjectionVersion != terrain.Version || next.ProjectionError != null; _groundAssignedFrame = _groundFrame;
        _groundRayIndices = Enumerable.Range(0, terrain.HeightsM.Count).ToArray();
        foreach (var saved in snapshot.Robots)
        {
            var actor = _groundRobots.Single(x => x.Name.ToString() == saved.Id);
            actor.ClearOrder(); actor.GlobalPosition = LoadVector(saved.Position); actor.Velocity = LoadVector(saved.Velocity); actor.Rotation = new Vector3(0, saved.Yaw, 0);
        }
        if (BootstrapEnabled) ApplyBootstrap(snapshot.Bootstrap!);
        PauseGround(true); _playerNotice = "存档已读取，等待地形和实体物理验证";
    }
    private TerrainSnapshot ValidatePlayerSave(PlayerSave saved)
    {
        if (saved.Schema != (BootstrapEnabled ? 2 : 1) || (saved.Schema == 1 && saved.Bootstrap != null) || saved.FixtureHash != _fixtureHash) throw new InvalidDataException("存档版本或场景配置不匹配，原档保留");
        if (!double.IsFinite(saved.Time) || saved.Time < 0 || saved.Time > 1e12 || saved.Sequence < 0)
            throw new InvalidDataException("存档时间或任务序号无效");
        var terrain = TerrainDataCodec.ParseSnapshot(saved.Terrain);
        if (terrain.RegionId != _liveInitial.RegionId || terrain.Rows != _liveInitial.Rows || terrain.Columns != _liveInitial.Columns ||
            terrain.OriginXM != _liveInitial.OriginXM || terrain.OriginZM != _liveInitial.OriginZM || terrain.SpacingM != _liveInitial.SpacingM)
            throw new InvalidDataException("存档区域不匹配");
        if (saved.Robots == null || saved.Robots.Length != _groundRobots.Count || saved.Robots.Select(x => x?.Id).Distinct().Count() != _groundRobots.Count)
            throw new InvalidDataException("存档机器人集合无效");
        if (BootstrapEnabled) ValidateBootstrap(saved, terrain);
        foreach (var robot in saved.Robots)
        {
            if (robot == null || !_groundRobots.Any(x => x.Name.ToString() == robot.Id)) throw new InvalidDataException("未知机器人身份");
            var p = LoadVector(robot.Position); var v = LoadVector(robot.Velocity);
            if (!float.IsFinite(robot.Yaw) || Math.Abs(robot.Yaw) > MathF.PI + .001f) throw new InvalidDataException("机器人朝向无效");
            if (Math.Abs(p.X) >= _cfg.Terrain.Size / 2 || Math.Abs(p.Z) >= _cfg.Terrain.Size / 2 || Math.Abs(p.Y) > 1000 || v.Length() > 100)
                throw new InvalidDataException("机器人位置或速度越界");
            double height = SavedGroundHeight(terrain, p.X, p.Z);
            if (p.Y < height - .03 || p.Y > height + .5) throw new InvalidDataException("机器人落点无法安全恢复");
            float radius = _groundRobots.Single(x => x.Name.ToString() == robot.Id).BodyRadius;
            if (BootstrapEnabled ? saved.Bootstrap!.Facilities.Where(f=>f.Built).Any(f=>XzDistance(LoadVector(f.Position),p)<=BaseRadius(f)+radius) : _facilityPositions.Select((f, i) => XzDistance(f, p) <= FacilityRadius(i) + radius).Any(x => x))
                throw new InvalidDataException("机器人落点与设施重叠");
            if (saved.Robots.Any(x => x != robot && XzDistance(LoadVector(x.Position), p) < radius + _groundRobots.Single(a => a.Name.ToString() == x.Id).BodyRadius))
                throw new InvalidDataException("机器人落点重叠");
        }
        if (saved.Job is {} j)
        {
            var patch = TerrainDataCodec.ParsePatch(j.Patch);
            var b = patch.Base;
            if (b.RegionId != terrain.RegionId || b.Rows != terrain.Rows || b.Columns != terrain.Columns || b.OriginXM != terrain.OriginXM || b.OriginZM != terrain.OriginZM || b.SpacingM != terrain.SpacingM)
                throw new InvalidDataException("任务所属区域不匹配");
            if (!Enum.TryParse<LevelStage>(j.Stage, out var stage) || (!Enum.IsDefined(stage) || Enum.GetName(stage) != j.Stage)) throw new InvalidDataException("未知任务阶段");
            if (!patch.PatchId.StartsWith("level-", StringComparison.Ordinal) || !int.TryParse(patch.PatchId[6..], out int id) || id < 1 || id > saved.Sequence)
                throw new InvalidDataException("任务身份无效");
            var center = LoadVector(j.Center); var station = LoadVector(j.Station);
            double margin = 2 + terrain.SpacingM * 2;
            if (Math.Abs(center.X) + margin >= _cfg.Terrain.Size / 2 || Math.Abs(center.Z) + margin >= _cfg.Terrain.Size / 2 ||
                Math.Abs(station.X) >= _cfg.Terrain.Size / 2 || Math.Abs(station.Z) >= _cfg.Terrain.Size / 2)
                throw new InvalidDataException("任务区域或站位越界");
            foreach (double value in new[] { j.Work, j.Travel, j.Space })
                if (!double.IsFinite(value) || value < 0 || value > 3600) throw new InvalidDataException("任务时钟无效");
            if (j.Work > 3 || j.Message == null || j.Message.Length > 1000) throw new InvalidDataException("任务进度或信息无效");
            bool active = stage is not (LevelStage.Completed or LevelStage.Cancelled or LevelStage.Failed);
            if ((active && j.Worker == null) || (j.Worker != null && (!j.Worker.StartsWith("Robot_Zhulei_", StringComparison.Ordinal) || !saved.Robots.Any(x => x.Id == j.Worker))))
                throw new InvalidDataException("任务工人无效");
            var expected = GroundPatch(patch.Base, center.X, center.Z, 0, patch.PatchId);
            if (center.Y != 0 || !expected.HeightsM.SequenceEqual(patch.HeightsM)) throw new InvalidDataException("任务候选与合法选区不一致");
            bool noChange = patch.HeightsM.SequenceEqual(patch.Base.HeightsM);
            if ((stage == LevelStage.Travelling && (j.Work != 0 || j.Space != 0)) || (stage == LevelStage.Working && j.Work >= 3) ||
                (stage is LevelStage.WaitingForSpace or LevelStage.AwaitingPhysics && j.Work != 3) ||
                (active && noChange) || (stage == LevelStage.Completed && (j.AppliedVersion == null ? !noChange || j.Worker != null || j.Work != 0 : j.Work != 3)))
                throw new InvalidDataException("任务阶段、进度与提交不一致");
            if (j.Worker != null)
            {
                float radius = _groundRobots.Single(x => x.Name.ToString() == j.Worker).BodyRadius;
                float offset = 2 + (float)patch.Base.SpacingM * 2 + 1.6f;
                bool candidate = (Math.Abs(Math.Abs(station.X - center.X) - offset) < .0001 && station.Z == center.Z) ||
                    (Math.Abs(Math.Abs(station.Z - center.Z) - offset) < .0001 && station.X == center.X);
                if (!candidate || TouchesFootprint(patch, station.X, station.Z, radius) || Math.Abs(station.Y - SavedGroundHeight(terrain, station.X, station.Z)) > .0001)
                    throw new InvalidDataException("任务施工站无法安全恢复");
                if (active && j.AppliedVersion == null)
                {
                    if (BootstrapEnabled)
                    {
                        var service=saved.Bootstrap!.Services.FirstOrDefault(s=>s.Robot==j.Worker);
                        float[]? target=service==null?saved.Bootstrap.Destinations.GetValueOrDefault(j.Worker):service.ReturnTo;
                        if(target==null||LoadVector(target)!=station)throw new InvalidDataException("整平路线与原工作站不一致");
                    }
                    var worker = saved.Robots.Single(x => x.Id == j.Worker);
                    var obstacles = saved.Robots.Where(x => x.Id != j.Worker).Select(x => (LoadVector(x.Position), _groundRobots.Single(a => a.Name.ToString() == x.Id).BodyRadius)).ToArray();
                    if (BootstrapEnabled ? !Yudian.Navigation.BoundedRoute.Find(terrain,new(worker.Position[0],worker.Position[2]),new(station.X,station.Z),radius,saved.Bootstrap!.Facilities.Select(f=>new Yudian.Navigation.NavObstacle(new(f.Position[0],f.Position[2]),BaseRadius(f))).Concat(obstacles.Select(o=>new Yudian.Navigation.NavObstacle(new(o.Item1.X,o.Item1.Z),o.Item2))).ToArray()).Found : !PlayerLineClear(LoadVector(worker.Position), radius, station, obstacles)) throw new InvalidDataException("存档施工路线受阻");
                }
            }
            if (j.AppliedVersion is {} applied)
            {
                if (applied != terrain.Version || applied != patch.Base.Version + 1 || !patch.HeightsM.SequenceEqual(terrain.HeightsM) ||
                    stage is LevelStage.Travelling or LevelStage.Working or LevelStage.WaitingForSpace)
                    throw new InvalidDataException("已提交任务与权威地形不一致");
            }
            else
            {
                TerrainDataCodec.ValidateAgainst(patch, terrain);
                if (stage == LevelStage.AwaitingPhysics) throw new InvalidDataException("任务缺少已提交版本");
            }
        }
        return terrain;
    }
    private static double SavedGroundHeight(TerrainSnapshot s, float x, float z)
    {
        double cx = (x - s.OriginXM) / s.SpacingM, rz = (z - s.OriginZM) / s.SpacingM;
        int c = Math.Clamp((int)Math.Floor(cx), 0, s.Columns - 2), r = Math.Clamp((int)Math.Floor(rz), 0, s.Rows - 2);
        double u = cx - c, v = rz - r;
        double a = s.GetHeight(r, c), b = s.GetHeight(r, c + 1), d = s.GetHeight(r + 1, c), e = s.GetHeight(r + 1, c + 1);
        return u >= v ? a + u * (b - a) + v * (e - b) : a + u * (e - d) + v * (d - a);
    }
    private void FinishPlayerLoad()
    {
        if (_loadPending is {} pending && _loadRollback != null && (_groundFault || _groundFrame - _loadStarted > 180))
        {
            var rollback = _loadRollback; _loadRollback = null; _loadRecovered = true;
            ApplyPlayerLoad(rollback, TerrainDataCodec.ParseSnapshot(rollback.Terrain));
            _playerNotice = "读取的物理验证失败，正在恢复原世界";
            GD.Print("PLAYER_LOAD_ROLLBACK"); return;
        }
        if (_loadPending is not {} saved || _groundVerified != _liveTerrain!.Current.Version || _groundFault ||
            _groundFrame - _loadStarted < 3 || !_groundRobots.All(x => x.IsOnFloor())) return;
        if (saved.Job is {} j)
        {
            var patch = TerrainDataCodec.ParsePatch(j.Patch);
            var job = new LevelJob { Id = patch.PatchId, Patch = patch, Center = LoadVector(j.Center), Station = LoadVector(j.Station),
                Stage = Enum.Parse<LevelStage>(j.Stage), TravelSeconds = j.Travel, SpaceSeconds = j.Space,
                AppliedVersion = j.AppliedVersion, Message = j.Message, Work = new WorkMeter(3) };
            job.Work.Advance(j.Work, true);
            job.Worker = j.Worker == null ? null : _groundRobots.Single(x => x.Name.ToString() == j.Worker);
            if (job.Active && job.AppliedVersion == null && !BootstrapEnabled) job.Worker!.SetOrder(job.Station);
            _levelJob = job;
        }
        if (BootstrapEnabled) RestoreBaseOrders(saved.Bootstrap!);
        _loadPending = null; _loadRollback = null; RestorePlayerVisuals(); PauseGround(false); _playerNotice = _loadRecovered ? "读取失败；已恢复原世界与任务，原档保留" : _userPaused ? "读取完成；保持用户暂停" : "读取完成；继续原任务";
        GD.Print($"PLAYER_LOAD_READY version={_liveTerrain.Current.Version} stage={_levelJob?.Stage} paused={_userPaused} time={_playerTime:R}");
    }
}
