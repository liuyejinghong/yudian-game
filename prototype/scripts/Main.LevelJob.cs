#nullable enable
using System;
using System.Linq;
using Godot;
using Yudian.Construction;
using Yudian.Terrain;

namespace Yudian;

public partial class Main
{
    private enum LevelStage { Travelling, Working, WaitingForSpace, AwaitingPhysics, Completed, Cancelled, Failed }
    private sealed class LevelJob
    {
        public required string Id;
        public required TerrainPatch Patch;
        public GroundPatrol? Worker;
        public Vector3 Station;
        public WorkMeter Work = new(3);
        public LevelStage Stage;
        public double TravelSeconds, SpaceSeconds;
        public long? AppliedVersion;
        public string Message = "";
        public bool Active => Stage is not (LevelStage.Completed or LevelStage.Cancelled or LevelStage.Failed);
    }
    private LevelJob? _levelJob;
    private Label? _levelStatus;
    private int _levelSequence;
    private string _levelNotice = "可下达土坡整平任务";

    private void BuildLevelPanel(VBoxContainer panel)
    {
        foreach (string command in new[] { "下达整平任务", "取消整平任务" })
        {
            var button = new Button { Text = command }; panel.AddChild(button);
            button.Pressed += () => _groundCommand = command;
        }
        _levelStatus = new Label(); panel.AddChild(_levelStatus);
    }

    private void StartLevelJob()
    {
        if (_levelJob?.Active == true)
        { _levelNotice = "已有活动任务：" + _levelJob.Id; return; }
        if (!_groundReady || _groundFault || _groundVerified != _liveTerrain!.Current.Version)
        { _levelNotice = "等待地形物理恢复后下单"; return; }
        var site = _cfg.Terrain.Mound.Position;
        string id = "level-" + ++_levelSequence;
        var job = new LevelJob { Id = id, Patch = GroundPatch(site[0], site[1], 0, id) };
        _levelJob = job; _levelNotice = "";
        if (!_liveTerrain.PermissionGranted || _liveTerrain.CancellationRequested)
        { FinishLevelJob(LevelStage.Failed, "未获改造权限或已取消"); return; }
        if (job.Patch.HeightsM.SequenceEqual(job.Patch.Base.HeightsM))
        { FinishLevelJob(LevelStage.Completed, "无需改造；地形未增加版本"); return; }
        var s = job.Patch.Base;
        float offset = 2 + (float)s.SpacingM * 2 + .35f;
        Vector3[] candidates = [new(site[0] - offset, 0, site[1]), new(site[0] + offset, 0, site[1]),
            new(site[0], 0, site[1] - offset), new(site[0], 0, site[1] + offset)];
        float best = float.PositiveInfinity;
        foreach (var actor in _groundRobots.Where(x => x.Name.ToString().StartsWith("Robot_Zhulei_", StringComparison.Ordinal)))
            foreach (var station in candidates)
            {
                if (station.X - .35f <= s.OriginXM || station.Z - .35f <= s.OriginZM ||
                    station.X + .35f >= s.OriginXM + (s.Columns - 1) * s.SpacingM ||
                    station.Z + .35f >= s.OriginZM + (s.Rows - 1) * s.SpacingM ||
                    TouchesFootprint(job.Patch, station.X, station.Z, .35f)) continue;
                if (_facilityPositions.Select((p, i) => XzDistance(p, station) <= FacilityRadii[i % 6] + .35f).Any(x => x)) continue;
                float distance = XzDistance(actor.GlobalPosition, station);
                if (distance >= best) continue;
                job.Worker = actor; job.Station = station; best = distance;
            }
        if (job.Worker == null) { FinishLevelJob(LevelStage.Failed, "没有筑垒或安全施工站"); return; }
        job.Station.Y = GroundHeight(job.Station.X, job.Station.Z);
        job.Worker.SetOrder(job.Station);
        job.Message = "前往施工区边缘；直线受阻会失败";
        GD.Print($"LEVEL_JOB_START id={job.Id} worker={job.Worker.Name} base={job.Patch.Base.Version} station={job.Station} distance={best:F3}");
    }

    private static float XzDistance(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private bool WorkerAtStation(LevelJob job)
        => job.Worker != null && job.Worker.OrderReached && !job.Worker.Paused && job.Worker.IsOnFloor() &&
            XzDistance(job.Worker.GlobalPosition, job.Station) <= .25f &&
            new Vector2(job.Worker.Velocity.X, job.Worker.Velocity.Z).Length() <= .05f;

    private void TickLevelJob(double delta)
    {
        var job = _levelJob;
        if (job?.Active == true)
        {
            var region = _liveTerrain!;
            if (job.Stage == LevelStage.AwaitingPhysics)
            {
                if (region.Current.Version != job.AppliedVersion)
                    FinishLevelJob(LevelStage.Failed, "提交后世界版本变化；已提交结果保留");
                else if (!_groundFault && _groundVerified == job.AppliedVersion && region.Current.Version == job.AppliedVersion)
                    FinishLevelJob(LevelStage.Completed, "整平已提交，物理验证完成");
                else job.Message = "地形已提交；等待物理验证，故障时从Current恢复";
            }
            else if (region.Current.Version != job.Patch.Base.Version)
                FinishLevelJob(LevelStage.Failed, "地形版本已变化；原任务不自动改写范围");
            else if (!region.PermissionGranted || region.CancellationRequested)
                FinishLevelJob(LevelStage.Failed, "改造权限失效或已取消");
            else if (_groundFault || _groundVerified != region.Current.Version)
            { job.Work.Advance(delta, false); job.Message = "等待地形物理恢复；未完成作业计时清零"; }
            else switch (job.Stage)
            {
                case LevelStage.Travelling:
                    job.TravelSeconds += delta;
                    if (WorkerAtStation(job))
                    {
                        job.Stage = LevelStage.Working; job.Message = "到场，连续整平作业";
                        GD.Print($"LEVEL_JOB_ARRIVED id={job.Id} seconds={job.TravelSeconds:F3} position={job.Worker!.GlobalPosition}");
                    }
                    else if (job.TravelSeconds >= 60) FinishLevelJob(LevelStage.Failed, "60秒内未实际到场");
                    break;
                case LevelStage.Working:
                    bool eligible = WorkerAtStation(job);
                    job.Work.Advance(delta, eligible);
                    job.Message = eligible ? "连续整平作业中" : "工人未停驻贴地；作业计时清零";
                    if (!eligible && (job.TravelSeconds += delta) >= 60)
                        FinishLevelJob(LevelStage.Failed, "工人无法保持施工站");
                    else if (job.Work.IsComplete)
                    { job.Stage = LevelStage.WaitingForSpace; job.Message = "作业完成，重验施工区占用"; }
                    break;
                case LevelStage.WaitingForSpace:
                    if (!WorkerAtStation(job)) { FinishLevelJob(LevelStage.Failed, "提交前工人离开施工站"); break; }
                    job.SpaceSeconds += delta;
                    var result = SubmitGroundResult(job.Patch);
                    if (result == null)
                    {
                        job.Message = _groundMessage;
                        if (job.SpaceSeconds >= 15) FinishLevelJob(LevelStage.Failed, "施工区持续被占用，未提交改造");
                    }
                    else if (result.Status is TerrainCommitStatus.Committed or TerrainCommitStatus.AlreadyCommitted)
                    {
                        job.AppliedVersion = result.AppliedVersion;
                        job.Stage = LevelStage.AwaitingPhysics;
                        job.Message = "已提交，等待下一物理帧";
                        GD.Print($"LEVEL_JOB_COMMIT id={job.Id} status={result.Status} applied={job.AppliedVersion} verified={_groundVerified?.ToString() ?? "none"}");
                    }
                    else if (result.Status == TerrainCommitStatus.NoChange)
                        FinishLevelJob(LevelStage.Completed, "无需改造；地形未增加版本");
                    else FinishLevelJob(LevelStage.Failed, "改造未提交：" + result.Status);
                    break;
            }
        }
        if (_levelStatus != null)
            _levelStatus.Text = _levelNotice + (_levelJob == null ? "" :
                $"\n整平任务 {_levelJob.Id} · {LevelStageText(_levelJob.Stage)}\n工人 {_levelJob.Worker?.Name.ToString() ?? "无"} · 进度 {_levelJob.Work.Fraction:P0}\n" +
                _levelJob.Message + (_levelJob.AppliedVersion is long version ? $" · 已提交 v{version}" : ""));
    }

    private static string LevelStageText(LevelStage stage) => stage switch
    {
        LevelStage.Travelling => "前往现场", LevelStage.Working => "施工中", LevelStage.WaitingForSpace => "等待施工区空闲",
        LevelStage.AwaitingPhysics => "验证改造结果", LevelStage.Completed => "已完成", LevelStage.Cancelled => "已取消", _ => "失败"
    };

    private void CancelLevelJob()
    {
        if (_levelJob?.Active != true) { _levelNotice = "没有可取消的活动任务"; return; }
        FinishLevelJob(LevelStage.Cancelled, _levelJob.AppliedVersion == null ? "已取消；未改造地形" : "已取消后续工作；已提交地形保留");
    }

    private void FinishLevelJob(LevelStage stage, string message)
    {
        var job = _levelJob!; job.Stage = stage; job.Message = message; job.Worker?.ClearOrder();
        GD.Print($"LEVEL_JOB_END id={job.Id} stage={stage} base={job.Patch.Base.Version} applied={job.AppliedVersion?.ToString() ?? "none"} current={_liveTerrain!.Current.Version} message={message}");
    }
}
