#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Yudian.Navigation;
using Yudian.Resources;
using Yudian.Terrain;
namespace Yudian;

public partial class Main
{
    private static MaterialAmountView[] AmountViews(IReadOnlyDictionary<string, int> amounts) => MaterialLedger.Materials
        .Where(m => amounts.GetValueOrDefault(m) > 0).Select(m => new MaterialAmountView(m, MaterialName(m), amounts[m])).ToArray();
    private Dictionary<string, int> PlanningAvailableStock()
    {
        if (!DevelopmentEnabled) return MaterialLedger.Materials.ToDictionary(m => m, m => _ledger.Available("lander", m));
        bool assigned = _development?.Active == true || _development?.Stage == "Blocked" || _buildJob?.Active == true || _buildJob?.Stage == "Blocked";
        var excluded = _groundRobots.Select(a => CargoContainer(a.Name.ToString())).ToHashSet(StringComparer.Ordinal);
        if (assigned && _buildJob is {} build && build.Stage is not ("Completed" or "Cancelled")) excluded.Add(build.Type == "connection" ? build.Id : build.Facility);
        string? goalId = assigned ? _development?.Id : null;
        if (goalId != null)
            foreach (var task in _productionTasks.Where(t => t.Goal == goalId && t.Kind == "recipe")) excluded.Add(task.Destination);
        var stock = MaterialLedger.Materials.ToDictionary(m => m, m => ProductionSources.Where(c => !excluded.Contains(c)).Sum(c => _ledger.Available(c, m)));
        if (goalId != null)
        {
            // Actual extracted material still at a mine belongs to the active goal's supply chain.
            foreach (var site in _mines)
            {
                int owned = _productionTasks.Where(t => t.Goal == goalId && t.Kind == "mine" && t.Paid && t.Source == site.Id).Sum(t => t.Quantity);
                stock[site.Material] -= Math.Min(owned, _ledger.Available(site.Id, site.Material));
            }
            foreach (var owned in _productionTasks.Where(t => t.Goal == goalId && t.Kind == "haul" && t.Stage == "Completed"
                && _baseFacilities.Any(f => f.Id == t.Destination && f.Type == "processor"))
                .GroupBy(t => (t.Destination, t.Material)))
                stock[owned.Key.Material] -= Math.Min(owned.Sum(t => t.Quantity), _ledger.Available(owned.Key.Destination, owned.Key.Material));
            if (_development!.Stage is "Supplying" or "Blocked" && _buildJob?.Active != true && _buildJob?.Stage != "Blocked")
            {
                // Goal staging has custody before StartBaseBuild takes a formal reservation.
                // Only that explicit warehouse's demand is assigned; other stock remains free.
                foreach (var amount in DevelopmentDemand(_development.Type))
                    stock[amount.Key] -= Math.Min(amount.Value, _ledger.Available(_development.Supply, amount.Key));
            }
        }
        // Cancelled site stock stays recoverable, while all general transit is shown separately.
        return stock;
    }
    private MaterialStockView[] ReadMaterialStock()
    {
        if (_ledger == null || !DevelopmentEnabled) return [];
        var cargo = _groundRobots.Select(a => CargoContainer(a.Name.ToString())).ToArray();
        var free = PlanningAvailableStock(); var pending = PendingRecipeOutput();
        return MaterialLedger.Materials.Select(m => new MaterialStockView(m, MaterialName(m), Warehouses.Sum(c => _ledger.Count(c, m)),
            free[m], cargo.Sum(c => _ledger.Count(c, m)), pending[m])).ToArray();
    }
    public PlayerEconomyView ReadEconomy() => _bootstrapConfig == null || !_groundReady ? new([], false, 0, 12)
        : new(ReadMaterialStock(), Daylight, _baseFacilities.Count(f => f.Built && f.Type == "solar") * (Daylight ? _bootstrapConfig.SolarOutput : 0), _bootstrapConfig.ConnectionRangeM);
    private PowerSourceView[] PowerCandidates(Vector3 center) => _baseFacilities.Where(f => f.Built && f.Type == "solar")
        .Select(f => new PowerSourceView(f.Id, BaseName(f.Type), LoadVector(f.Position), XzDistance(LoadVector(f.Position), center),
            _bootstrapConfig.ConnectionRangeM, XzDistance(LoadVector(f.Position), center) <= _bootstrapConfig.ConnectionRangeM, Powered(f)))
        .OrderBy(p => p.DistanceM).ToArray();
    private double KnownHaulDistance(BaseFacility supply, Vector3 center, float radius, out bool found)
    {
        double best = double.PositiveInfinity;
        foreach (var actor in _groundRobots.Where(a => a.Name.ToString().StartsWith("Robot_Tuoyun_", StringComparison.Ordinal)))
        {
            var dock = FreeStation(supply, actor); if (dock == null) continue;
            float d = radius + actor.BodyRadius + 1.2f;
            foreach (var offset in new[] { new Vector3(d, 0, 0), new Vector3(-d, 0, 0), new Vector3(0, 0, d), new Vector3(0, 0, -d) })
            {
                var destination = center + offset;
                var route = BoundedRoute.FindWithTerrainMargin(_liveTerrain!.Current, new(dock.Value.X, dock.Value.Z), new(destination.X, destination.Z), actor.BodyRadius, RouteObstacles(actor.Name.ToString()), GroundPatrol.WaypointReachXzM);
                if (route.Found) best = Math.Min(best, route.LengthM);
            }
        }
        found = double.IsFinite(best); return found ? best : 0;
    }
    private SupplyRouteView[] ReadSupplyRoutes(Vector3 center, float radius) => _baseFacilities.Where(f => f.Built && f.Type is "lander" or "storage").Select(f =>
    {
        double distance = KnownHaulDistance(f, center, radius, out bool found);
        return new SupplyRouteView(f.Id, BaseName(f.Type), LoadVector(f.Position), found, distance,
            found ? $"已知取货位至卸货位约{distance:0.0}m；施工时重验路线和工位" : "当前没有已知可达运输位，需调整位置或等待占位解除");
    }).ToArray();
    private string CandidateGeometry(string type, Vector3 center, float yaw)
    {
        if (!BootstrapEnabled || _bootstrapConfig == null || !_groundReady) return "等待世界准备";
        if (!_bootstrapConfig.Buildings.Any(b => b.Id == type) || !float.IsFinite(yaw) || Math.Abs(yaw) > MathF.PI
            || !float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(center.Z)) return "设施、坐标或朝向无效";
        float radius = (float)Definition(type).Radius; var t = _liveTerrain!.Current;
        if (!_liveTerrain.PermissionGranted || _liveTerrain.CancellationRequested) return "未获建设权限";
        if (Math.Abs(center.X) + radius + t.SpacingM * 3 + 2 >= _cfg.Terrain.Size / 2 || Math.Abs(center.Z) + radius + t.SpacingM * 3 + 2 >= _cfg.Terrain.Size / 2) return "设施与作业站须位于场地内部";
        if (_baseFacilities.Any(f => XzDistance(LoadVector(f.Position), center) <= BaseRadius(f) + radius + (float)t.SpacingM * 2 + 1)) return "与设施或已有工程重叠";
        if (_groundRobots.Any(a => XzDistance(a.GlobalPosition, center) <= a.BodyRadius + radius + (float)t.SpacingM * 2)) return "作业范围内有机器人";
        if (DevelopmentEnabled && _mines.Any(m => XzDistance(LoadVector(m.Position), center) <= radius + 2 + (float)t.SpacingM * 2 + 1)) return "建设不得覆盖已知矿点与采集包络";
        if (type is "charger" or "repair" or "processor" && !PowerCandidates(center).Any(p => p.InRange)) return "用电设施须位于已建太阳能12m覆盖内；先建太阳能或调整位置";
        if (TutorialEnabled && !_tutorial.LegacyPractice && !_tutorial.ConclusionAcknowledged && type == "charger" && TutorialGoal(_tutorial.FirstGoalId)?.Completed == true && TutorialGoal(_tutorial.SecondGoalId)?.Completed == true)
        {
            var source = TutorialFacility(TutorialExpansionSolarId, "solar");
            if (source == null || XzDistance(LoadVector(source.Position), center) > _bootstrapConfig.ConnectionRangeM) return "第二保障片区须位于新增太阳能12m内";
        }
        return "";
    }
    private string CurrentGoal => _development?.Active == true || _development?.Stage == "Blocked" ? _development.Id : _buildJob?.Active == true ? _buildJob.Id : _levelJob?.Active == true ? _levelJob.Id : "";
    private string CurrentWorkStage => _development?.Active == true || _development?.Stage == "Blocked" ? Production?.Stage ?? _buildJob?.Stage ?? _development.Stage
        : _buildJob?.Active == true ? _buildJob.Stage : _levelJob?.Active == true ? _levelJob.Stage.ToString() : "";
    private BaseFacility? RecoveryTarget(string type, Vector3 center, string? facilityId = null)
    {
        if (!DevelopmentEnabled || SupportSetConnected) return null;
        var missing = _baseFacilities.Where(f => f.Built && f.Source == null && f.Type is "repair" or "charger" or "processor");
        if (type == "connection") return missing.FirstOrDefault(f => f.Id == facilityId);
        if (type != "solar") return null;
        return missing.Where(f => !_baseFacilities.Any(s => s.Built && s.Type == "solar" && XzDistance(LoadVector(s.Position), LoadVector(f.Position)) <= _bootstrapConfig.ConnectionRangeM)
            && XzDistance(LoadVector(f.Position), center) <= _bootstrapConfig.ConnectionRangeM).OrderBy(f => XzDistance(LoadVector(f.Position), center)).FirstOrDefault();
    }
    public BuildPlanView ReadBuildPlan(string type, Vector3 center, float yaw = 0)
    {
        string candidate = CandidateGeometry(type, center, yaw);
        bool known = _bootstrapConfig != null && _bootstrapConfig.Buildings.Any(b => b.Id == type);
        if (!known || _bootstrapConfig == null || !_groundReady)
            return new(type, known ? BaseName(type) : type, center, yaw, 2, false, false, candidate, candidate, [], [], [], [], null, [], null, null, 12,
                "", false, true, CurrentGoal.Length == 0 ? null : CurrentGoal, CurrentWorkStage, _intentVersion, _liveTerrain?.Current.Version ?? 0, [], _worldEpoch);
        center.Y = 0;
        // Same unreserved physical sources as DevelopmentFeasibility/TickDevelopment. No expected output or route claim.
        float radius = (float)Definition(type).Radius; var cost = BuildCost(type); var stock = DevelopmentEnabled ? UsableStock() : PlanningAvailableStock();
        var gap = cost.Where(x => stock[x.Key] < x.Value).ToDictionary(x => x.Key, x => x.Value - stock[x.Key]);
        var power = PowerCandidates(center); var source = power.FirstOrDefault(p => p.InRange); var recovery = RecoveryTarget(type, center);
        string submit = candidate;
        string? supply = null;
        if (submit.Length == 0 && !ReadPlayerState().Ready) submit = "等待世界与实体物理准备";
        if (submit.Length == 0 && (_entryOpen || _menuPaused)) submit = "当前是菜单；返回局内后再提交";
        if (submit.Length == 0 && HasCurrentWork) submit = "草案不预约：先完成或取消当前工程";
        if (submit.Length == 0) submit = StartupGuard(type, center);
        if (submit.Length == 0) submit = DevelopmentEnabled ? DevelopmentFeasibility(type, recovery?.Id) : gap.Count > 0 ? "有限启动库存不足" : "";
        try { supply = DevelopmentEnabled ? ChooseBuildSupply(cost, center) : "lander"; }
        catch (InvalidOperationException ex) { if (submit.Length == 0) submit = ex.Message; }
        if (submit.Length == 0 && !_groundRobots.Any(a => a.Name.ToString().StartsWith("Robot_Zhulei_") && Operational(a) && !Servicing(a))) submit = "没有可行动的筑垒";
        string consequence = type switch
        {
            "solar" => $"日照发电+{_bootstrapConfig.SolarOutput:0.#}/s；覆盖12m，建成后仍需明确接线",
            "storage" => "新增100容量与真实供料位置；后续材料先送入仓储再取货",
            "charger" => "电量恢复与路线续航；充电不维修，需要付费接线",
            "repair" => "耐久维修不赠电；成本含两次有限维修耗材，需要付费接线",
            _ => "沿真实矿物和配方生产建设材料；日照与可用功率约束产出"
        };
        if (recovery != null) consequence = "有限供电恢复：保障" + BaseName(recovery.Type) + "的12m覆盖；" + consequence;
        var routes = float.IsFinite(center.X) && float.IsFinite(center.Z) ? ReadSupplyRoutes(center, radius) : Array.Empty<SupplyRouteView>();
        return new(type, BaseName(type), center, yaw, radius + (float)_liveTerrain!.Current.SpacingM, candidate.Length == 0, submit.Length == 0,
            candidate.Length == 0 ? "选址、朝向与供电覆盖合法" : candidate, submit.Length == 0 ? "可授权一个工程；材料沿真实生产和施工链结算" : submit,
            AmountViews(cost), AmountViews(gap), ReadMaterialStock(), routes, supply, power, source?.SourceId, source?.DistanceM,
            _bootstrapConfig.ConnectionRangeM, consequence, recovery != null, true, CurrentGoal.Length == 0 ? null : CurrentGoal, CurrentWorkStage, _intentVersion, _liveTerrain.Current.Version,
            AmountViews(stock), _worldEpoch);
    }
    public ConnectionOptionView[] ReadConnectionOptions(string? facilityId = null)
    {
        if (!BootstrapEnabled || _bootstrapConfig == null || !_groundReady) return [];
        var targets = _baseFacilities.Where(f => f.Built && f.Type is "charger" or "repair" or "processor" && (facilityId == null || f.Id == facilityId));
        return targets.SelectMany(f => _baseFacilities.Where(s => s.Built && s.Type == "solar").Select(s => ReadConnectionOption(f, s))).ToArray();
    }
    private ConnectionOptionView ReadConnectionOption(BaseFacility facility, BaseFacility source)
    {
        double distance = XzDistance(LoadVector(facility.Position), LoadVector(source.Position));
        string candidate = facility.Source != null ? "设施已有真实接线，不能免费重接" : distance > _bootstrapConfig.ConnectionRangeM ? "此太阳能超出12m接线范围" : "";
        if (candidate.Length == 0 && !_liveTerrain!.PermissionGranted) candidate = "未获接线权限";
        if (candidate.Length == 0 && TutorialEnabled && facility.Id == TutorialSecondChargerId && source.Id != TutorialExpansionSolarId) candidate = "迁移目标须明确接入新增太阳能";
        var cost = BuildCost("connection"); var stock = DevelopmentEnabled ? UsableStock() : PlanningAvailableStock();
        var gap = cost.Where(x => stock[x.Key] < x.Value).ToDictionary(x => x.Key, x => x.Value - stock[x.Key]); var recovery = RecoveryTarget("connection", LoadVector(facility.Position), facility.Id);
        string submit = candidate; string? supply = null;
        if (submit.Length == 0 && !ReadPlayerState().Ready) submit = "等待世界与实体物理准备";
        if (submit.Length == 0 && (_entryOpen || _menuPaused)) submit = "返回局内后再提交接线";
        if (submit.Length == 0 && HasCurrentWork) submit = "先完成或取消当前工程";
        if (submit.Length == 0) submit = DevelopmentEnabled ? DevelopmentFeasibility("connection", recovery?.Id) : gap.Count > 0 ? "有限线缆库存不足" : "";
        try { supply = DevelopmentEnabled ? ChooseBuildSupply(cost, LoadVector(facility.Position)) : "lander"; }
        catch (InvalidOperationException ex) { if (submit.Length == 0) submit = ex.Message; }
        return new(facility.Id, BaseName(facility.Type), source.Id, BaseName(source.Type), LoadVector(facility.Position), LoadVector(source.Position), distance,
            _bootstrapConfig.ConnectionRangeM, candidate.Length == 0, submit.Length == 0, candidate.Length == 0 ? "真实设施与电源在12m内，尚未接线" : candidate,
            submit.Length == 0 ? "付费铺设线缆；机器人真实运输与施工" : submit, AmountViews(cost), AmountViews(gap), supply, recovery != null, _intentVersion);
    }
    public void QueueConnection(string facilityId, string sourceId, long observedVersion)
        => QueuePlayer(new("connection", Vector3.Zero, observedVersion, facilityId, 0, sourceId));
    private void ProcessExplicitConnection(PlayerCommand command)
    {
        if (command.Version != _intentVersion) throw new InvalidOperationException("世界或材料已变化，请重新检查接线方案");
        var facility = _baseFacilities.FirstOrDefault(f => f.Id == command.Worker && f.Built && f.Type is "charger" or "repair" or "processor");
        var source = _baseFacilities.FirstOrDefault(f => f.Id == command.Source && f.Built && f.Type == "solar");
        if (facility == null || source == null) throw new InvalidOperationException("请选择真实已建用电设施和太阳能");
        var option = ReadConnectionOption(facility, source);
        if (!option.CanSubmit) throw new InvalidOperationException(option.SubmitReason);
        if (DevelopmentEnabled) StartDevelopment("connection", LoadVector(facility.Position), facility.Yaw, facility.Id, source.Id);
        else StartBaseBuild("connection", LoadVector(facility.Position), facility.Yaw, facility.Id, source.Id);
    }
}
