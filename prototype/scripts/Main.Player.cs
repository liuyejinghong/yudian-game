#nullable enable
using System;
using System.Linq;
using Godot;
using Yudian.Terrain;

namespace Yudian;
public partial class Main
{
    private bool _playerMode, _userPaused, _projectionPaused, _entryOpen;
    private double _playerTime;
    private sealed record PlayerCommand(string Action, Vector3 Center, long Version, string? Worker, float Yaw = 0);
    private PlayerCommand? _playerCommand;
    private string _playerNotice = "选择筑垒或地面，预览后下达整平";
    public PlayerReadModel ReadPlayerState() => new(
        _groundReady && !_groundFault && _groundVerified == _liveTerrain?.Current.Version && _loadPending == null,
        _userPaused, PlayerNotice(), DevelopmentEnabled&&_development is {} g&&g.Stage is "Supplying" or "Blocked" ? new(g.Id, Production?.Reason??g.Reason,Production?.Robot??"", Production is {Kind:"mine"} p?p.Work/6:0,LoadVector(g.Center),true) : BootstrapEnabled && _buildJob is {} b ? new(b.Id, BaseStageText(b.Stage), b.Builder, Math.Min(1,b.Work/(b.Type=="connection"?3:Definition(b.Type).WorkSeconds)), LoadVector(b.Center), b.Active) : _levelJob == null ? null : new(_levelJob.Id, LevelStageText(_levelJob.Stage),
            _levelJob.Worker?.Name.ToString() ?? "", _levelJob.Work.Fraction, _levelJob.Center, _levelJob.Active),
        _playerTime, _liveTerrain?.Current.Version ?? 0, _cfg.Terrain.Size / 2, System.IO.File.Exists(PlayerSavePath), _entryOpen, _entryOpen && _loadPending != null);
    private string PlayerNotice()
    {
        if (_groundFault) return _groundMessage;
        if (BootstrapEnabled && _buildJob is {} b && (_levelJob?.Active != true)) return b.Reason + "\n" + _playerNotice;
        if (_loadPending != null || _userPaused || _levelJob?.Active != true || _levelJob.Message == _playerNotice) return _playerNotice;
        return _levelJob.Message + "\n" + _playerNotice;
    }
    public PlayerRobotView[] ReadPlayerRobots() => _groundRobots.Select(x => new PlayerRobotView(
        x.Name.ToString(), x.Name.ToString().Replace("Robot_Zhulei_", "筑垒 ").Replace("Robot_Wangshan_", "望山 ").Replace("Robot_Tuoyun_", "驮运 "),
        x.GlobalPosition, x.BodyRadius)).ToArray();

    public PlayerSitePreview PreviewLevel(Vector3 center, string? workerId = null)
    {
        string reason = "";
        var region = _liveTerrain;
        if (!_playerMode || !ReadPlayerState().Ready) reason = "等待世界与物理准备";
        else if (_levelJob?.Active == true || (BootstrapEnabled && HasCurrentWork)) reason = "先完成或取消当前任务";
        else if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(center.Z)) reason = "选区坐标无效";
        else if (!region!.PermissionGranted || region.CancellationRequested) reason = "尚未获改造权限";
        else
        {
            var s = region!.Current;
            double margin = 2 + s.SpacingM * 2;
            if (center.X - margin <= s.OriginXM || center.Z - margin <= s.OriginZM ||
                center.X + margin >= s.OriginXM + (s.Columns - 1) * s.SpacingM || center.Z + margin >= s.OriginZM + (s.Rows - 1) * s.SpacingM)
                reason = "选区须完整位于场地内部";
            else
            {
                var patch = GroundPatch(center.X, center.Z, 0, "preview");
                if (DevelopmentEnabled&&_mines.Any(m=>XzDistance(LoadVector(m.Position),center)<=2+(float)margin)) reason="选区覆盖已知矿点，请保留采集地形";
                else if (_facilityPositions.Select((p, i) => XzDistance(p, center) <= FacilityRadius(i) + (float)margin).Any(x => x)) reason = "选区与设施范围重叠";
                else if (_groundRobots.Any(x => XzDistance(x.GlobalPosition, center) <= x.BodyRadius + (float)margin)) reason = "选区内有机器人，请换位置";
                else if (FindLevelWorker(patch, center, workerId).Worker == null) reason = "没有可达施工站的筑垒";
            }
        }
        return new(reason.Length == 0, reason.Length == 0 ? "可整平 · 半径 2m，目标高度 0m" : reason,
            center, region?.Current.Version ?? 0, 0);
    }

    public void QueueLevel(Vector3 center, long observedVersion, string? workerId = null)
        => QueuePlayer(new("level", center, observedVersion, workerId));
    public void QueuePlayerAction(string action)
    {
        if((action is "restock" or "legacy")&&!DevelopmentEnabled){_playerNotice="本模式没有此经营操作";return;}
        if (_entryOpen && action is not ("newgame" or "load" or "recover")) { _playerNotice = "请先选择开始新游戏或继续存档"; return; }
        if ((action is "connect" or "retry") && !BootstrapEnabled) { _playerNotice="本模式没有此经营操作"; return; }
        if (action is not ("cancel" or "pause" or "save" or "load" or "recover" or "connect" or "retry" or "restock" or "legacy" or "newgame" or "savequit")) { _playerNotice = "未知操作，未执行"; return; }
        QueuePlayer(new(action, Vector3.Zero, -1, null));
    }
    private void QueuePlayer(PlayerCommand command)
    {
        if (!_playerMode) return;
        if (_entryOpen && command.Action is not ("newgame" or "load" or "recover")) { _playerNotice = "请先选择开始新游戏或继续存档"; return; }
        if (_playerCommand != null) { _playerNotice = "操作正在处理，请稍候"; return; }
        _playerCommand = command;
    }
    private void ProcessPlayerCommand()
    {
        if (_playerCommand is not {} command) return;
        _playerCommand = null;
        try
        {
            if (command.Action == "newgame")
            {
                if (!_entryOpen) { _playerNotice = "新游戏只在启动入口选择"; return; }
                if (!ReadPlayerState().Ready) { _playerNotice = "等待世界准备完成"; return; }
                _entryOpen = false; PauseGround(false); _playerNotice = "先建太阳能、充电、维修和加工，再选择发展方向";
            }
            else if (command.Action == "pause") { _userPaused = !_userPaused; PauseGround(_projectionPaused); _playerNotice = _userPaused ? "模拟已暂停，镜头和界面仍可操作" : "模拟继续"; }
            else if (command.Action == "recover") { RecoverGround(); _playerNotice = "正在重新验证物理投影"; }
            else if (command.Action == "load") LoadPlayer();
            else if(command.Action=="legacy")LoadPlayer(true);
            else if (command.Action == "save") SavePlayer();
            else if (command.Action == "savequit") { if (SavePlayer()) GetTree().Quit(); }
            else if (!ReadPlayerState().Ready) _playerNotice = "等待世界恢复后再操作";
            else if (command.Action == "connect" && BootstrapEnabled) ConnectNextFacility();
            else if(command.Action=="restock"&&DevelopmentEnabled)StartRestock();
            else if (command.Action == "retry" && BootstrapEnabled)
            {if(_services.Values.Any(s=>s.Blocked))RetryBaseBuild();else if(DevelopmentEnabled&&_development?.Stage is "Blocked" or "Cancelled")RetryDevelopment();else RetryBaseBuild();}
            else if (command.Action.StartsWith("build:") && BootstrapEnabled)
            {
                string type = command.Action[6..]; var preview = PreviewBuild(type,command.Center,command.Yaw);
                if (!preview.Legal) _playerNotice=preview.Reason;
                else if(command.Version != _liveTerrain!.Current.Version) _playerNotice="世界已变化，请重预览";
                else if(DevelopmentEnabled)StartDevelopment(type,preview.Center,command.Yaw);
                else StartBaseBuild(type,preview.Center,command.Yaw);
            }
            else if (command.Action == "cancel") { if(DevelopmentEnabled&&(_development?.Active==true||_development?.Stage=="Blocked")){CancelDevelopment();return;} if(BootstrapEnabled && _buildJob?.Active == true) { CancelBaseBuild(); return; } CancelLevelJob(); _playerNotice = _levelJob?.Message ?? _levelNotice; }
            else
            {
                var preview = PreviewLevel(command.Center, command.Worker);
                if (!preview.Legal) _playerNotice = preview.Reason;
                else if (command.Version != _liveTerrain!.Current.Version) _playerNotice = "地形已变化，请重新预览";
                else { StartLevelJob(command.Center.X, command.Center.Z, command.Worker); _playerNotice = _levelJob?.Message ?? _levelNotice; }
            }
        }
        catch (Exception ex) { _playerNotice = command.Action == "load" ? "读取失败：存档损坏、不支持或无法访问；当前世界与原档保留"
                : command.Action is "save" or "savequit" ? "保存失败；此前存档保留，请检查可用空间与访问权限" : "操作未执行：" + ex.Message; GD.Print("PLAYER_COMMAND_REJECTED " + ex.Message); }
    }

    private (GroundPatrol? Worker, Vector3 Station) FindLevelWorker(TerrainPatch patch, Vector3 center, string? workerId)
    {
        var s = patch.Base;
        float offset = 2 + (float)s.SpacingM * 2 + 1.6f;
        Vector3[] stations = [center + new Vector3(-offset, 0, 0), center + new Vector3(offset, 0, 0),
            center + new Vector3(0, 0, -offset), center + new Vector3(0, 0, offset)];
        GroundPatrol? bestWorker = null; Vector3 bestStation = default; float best = float.PositiveInfinity;
        foreach (var actor in _groundRobots.Where(x => x.Name.ToString().StartsWith("Robot_Zhulei_", StringComparison.Ordinal) &&
            (workerId == null || x.Name.ToString() == workerId) && (!BootstrapEnabled || Operational(x) && !Servicing(x))))
            foreach (var station in stations)
            {
                float radius = actor.BodyRadius;
                if (station.X - radius <= s.OriginXM || station.Z - radius <= s.OriginZM ||
                    station.X + radius >= s.OriginXM + (s.Columns - 1) * s.SpacingM || station.Z + radius >= s.OriginZM + (s.Rows - 1) * s.SpacingM ||
                    TouchesFootprint(patch, station.X, station.Z, radius) || !(BootstrapEnabled ? FindRoute(actor,station).Found : PlayerLineClear(actor, station))) continue;
                float distance = XzDistance(actor.GlobalPosition, station);
                if (distance >= best) continue;
                bestWorker = actor; bestStation = station; best = distance;
            }
        return (bestWorker, bestStation);
    }
    private bool PlayerLineClear(GroundPatrol actor, Vector3 destination)
        => PlayerLineClear(actor.GlobalPosition, actor.BodyRadius, destination,
            _groundRobots.Where(x => x != actor).Select(x => (x.GlobalPosition, x.BodyRadius)).ToArray());
    private bool PlayerLineClear(Vector3 origin, float radius, Vector3 destination, (Vector3 Position, float Radius)[] obstacles)
    {
        var from = new Vector2(origin.X, origin.Z);
        var to = new Vector2(destination.X, destination.Z); var segment = to - from;
        float Distance(Vector3 p)
        {
            var point = new Vector2(p.X, p.Z);
            float t = segment.LengthSquared() == 0 ? 0 : Mathf.Clamp((point - from).Dot(segment) / segment.LengthSquared(), 0, 1);
            return point.DistanceTo(from + segment * t);
        }
        return !_facilityPositions.Select((p, i) => Distance(p) <= FacilityRadius(i) + radius).Any(x => x)
            && !obstacles.Any(x => Distance(x.Position) <= x.Radius + radius);
    }
}
