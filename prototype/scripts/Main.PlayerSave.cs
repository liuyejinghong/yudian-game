#nullable enable
using System;
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
        public required int Sequence { get; init; }
        public required RobotSave[] Robots { get; init; }
        public required JobSave? Job { get; init; }
    }
    private static readonly JsonSerializerOptions SaveOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private string PlayerSavePath => ProjectSettings.GlobalizePath("user://saves/d1-player-v1.json");
    private PlayerSave? _loadPending;
    private int _loadStarted;
    private static float[] SavedVector(Vector3 p) => [p.X, p.Y, p.Z];
    private static Vector3 LoadVector(float[] p)
    {
        if (p == null || p.Length != 3 || p.Any(x => !float.IsFinite(x) || Math.Abs(x) > 10000))
            throw new InvalidDataException("存档坐标无效");
        return new(p[0], p[1], p[2]);
    }
    private PlayerSave CapturePlayer() => new()
    {
        Schema = 1, FixtureHash = _fixtureHash, Terrain = TerrainDataCodec.Serialize(_liveTerrain!.Current),
        Time = _playerTime, Paused = _userPaused, Sequence = _levelSequence,
        Robots = _groundRobots.Select(x => new RobotSave { Id = x.Name.ToString(), Position = SavedVector(x.GlobalPosition), Velocity = SavedVector(x.Velocity) }).ToArray(),
        Job = _levelJob is not {} j ? null : new JobSave
        {
            Patch = TerrainDataCodec.Serialize(j.Patch), Stage = j.Stage.ToString(), Worker = j.Worker?.Name.ToString(),
            Center = SavedVector(j.Center), Station = SavedVector(j.Station), Work = j.Work.ElapsedSeconds,
            Travel = j.TravelSeconds, Space = j.SpaceSeconds, AppliedVersion = j.AppliedVersion, Message = j.Message
        }
    };
    private void SavePlayer()
    {
        if (!_groundReady || _loadPending != null) { _playerNotice = "等待世界恢复后保存"; return; }
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
        var next = new TerrainRegionView(); AddChild(next);
        try { next.Initialize(terrain); }
        catch { RemoveChild(next); next.QueueFree(); throw; }
        var old = _liveTerrain!; old.Body.CollisionLayer = 0; RemoveChild(old); old.QueueFree(); _liveTerrain = next;
        _loadPending = snapshot; _loadStarted = _groundFrame; _userPaused = snapshot.Paused;
        _playerTime = snapshot.Time; _levelSequence = snapshot.Sequence; _levelJob = null; _levelNotice = "";
        _groundVerified = null; _groundFault = false; _groundAssignedFrame = _groundFrame;
        _groundRayIndices = Enumerable.Range(0, terrain.HeightsM.Count).ToArray();
        foreach (var saved in snapshot.Robots)
        {
            var actor = _groundRobots.Single(x => x.Name.ToString() == saved.Id);
            actor.ClearOrder(); actor.GlobalPosition = LoadVector(saved.Position); actor.Velocity = LoadVector(saved.Velocity);
        }
        PauseGround(true); _playerNotice = "存档已读取，等待地形和实体物理验证";
    }
    private TerrainSnapshot ValidatePlayerSave(PlayerSave saved)
    {
        if (saved.Schema != 1 || saved.FixtureHash != _fixtureHash) throw new InvalidDataException("存档版本或场景配置不匹配，原档保留");
        if (!double.IsFinite(saved.Time) || saved.Time < 0 || saved.Time > 1e12 || saved.Sequence < 0 || saved.Sequence == int.MaxValue)
            throw new InvalidDataException("存档时间或任务序号无效");
        var terrain = TerrainDataCodec.ParseSnapshot(saved.Terrain);
        if (terrain.RegionId != _liveInitial.RegionId || terrain.Rows != _liveInitial.Rows || terrain.Columns != _liveInitial.Columns ||
            terrain.OriginXM != _liveInitial.OriginXM || terrain.OriginZM != _liveInitial.OriginZM || terrain.SpacingM != _liveInitial.SpacingM)
            throw new InvalidDataException("存档区域不匹配");
        if (saved.Robots == null || saved.Robots.Length != _groundRobots.Count || saved.Robots.Select(x => x?.Id).Distinct().Count() != _groundRobots.Count)
            throw new InvalidDataException("存档机器人集合无效");
        foreach (var robot in saved.Robots)
        {
            if (robot == null || !_groundRobots.Any(x => x.Name.ToString() == robot.Id)) throw new InvalidDataException("未知机器人身份");
            var p = LoadVector(robot.Position); var v = LoadVector(robot.Velocity);
            if (Math.Abs(p.X) >= _cfg.Terrain.Size / 2 || Math.Abs(p.Z) >= _cfg.Terrain.Size / 2 || Math.Abs(p.Y) > 1000 || v.Length() > 100)
                throw new InvalidDataException("机器人位置或速度越界");
        }
        if (saved.Job is {} j)
        {
            var patch = TerrainDataCodec.ParsePatch(j.Patch);
            if (!Enum.TryParse<LevelStage>(j.Stage, out var stage) || !Enum.IsDefined(stage)) throw new InvalidDataException("未知任务阶段");
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
    private void FinishPlayerLoad()
    {
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
            if (job.Active && job.AppliedVersion == null) job.Worker!.SetOrder(job.Station);
            _levelJob = job;
        }
        _loadPending = null; PauseGround(false); _playerNotice = _userPaused ? "读取完成；保持用户暂停" : "读取完成；继续原任务";
        GD.Print($"PLAYER_LOAD_READY version={_liveTerrain.Current.Version} stage={_levelJob?.Stage} paused={_userPaused} time={_playerTime:R}");
    }
}
