#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Yudian.Resources;
using Yudian.Terrain;

namespace Yudian;

// Values projected on the simulation thread. No scene, ledger, config or mutable task escapes.
public partial class Main
{
    private BootstrapConfig? _catalogConfig;
    private DefinitionCatalog? _playerCatalog;
    private DefinitionCatalog PlayerCatalog
    {
        get
        {
            if (!ReferenceEquals(_catalogConfig, _bootstrapConfig))
            {
                _playerCatalog = new DefinitionCatalog(_bootstrapConfig);
                _catalogConfig = _bootstrapConfig;
            }
            return _playerCatalog!;
        }
    }
    public PlayerDefinitionsView ReadDefinitions()
    {
        if (_bootstrapConfig == null) return new([], [], []);
        var catalog = PlayerCatalog;
        return new(catalog.Materials.Select(m => new MaterialDefinitionView(m.Id, m.Name, m.Description, m.Category, m.Unit, m.VisualRef)).ToArray(),
            catalog.Recipes.Select(r => new RecipeDefinitionView(r.Id, r.Name, r.Description, r.Category, r.Unit, r.VisualRef,
                AmountViews(r.Input), AmountViews(r.Output), r.WorkSeconds, r.Power)).ToArray(),
            catalog.Buildings.Select(b => new BuildingDefinitionView(b.Id, b.Name, b.Description, b.Category, b.Unit, b.VisualRef,
                AmountViews(b.Cost), b.Radius, b.WorkSeconds)).ToArray());
    }
    private Vector3 AuthorityPosition(Vector3 position)
    {
        if (_liveTerrain != null) position.Y = (float)SavedGroundHeight(_liveTerrain.Current, position.X, position.Z);
        return position;
    }
    private static string RobotName(string id) => id.Replace("Robot_Zhulei_", "筑垒 ").Replace("Robot_Wangshan_", "望山 ").Replace("Robot_Tuoyun_", "驮运 ");
    private static string RobotRole(string id) => id.StartsWith("Robot_Zhulei_", StringComparison.Ordinal) ? "整平、采矿与施工"
        : id.StartsWith("Robot_Tuoyun_", StringComparison.Ordinal) ? "搬运真实物料" : "望山；本批没有独立勘探工程";
    private static string RobotDefinitionId(string id) => id.StartsWith("Robot_Zhulei_", StringComparison.Ordinal) ? "zhulei"
        : id.StartsWith("Robot_Tuoyun_", StringComparison.Ordinal) ? "tuoyun" : "wangshan";
    private WorldObjectView RobotObject(GroundPatrol actor) => new(_worldEpoch, WorldObjectKind.Robot, actor.Name.ToString(),
        RobotDefinitionId(actor.Name.ToString()), RobotName(actor.Name.ToString()), AuthorityPosition(actor.GlobalPosition), actor.BodyRadius);
    private WorldObjectView MineObject(MineSite mine) => new(_worldEpoch, WorldObjectKind.Mine, mine.Id, mine.Material,
        MaterialName(mine.Material) + "矿点", AuthorityPosition(LoadVector(mine.Position)), 2);
    private WorldObjectView FacilityObject(BaseFacility facility) => new(_worldEpoch, WorldObjectKind.Facility, facility.Id, facility.Type,
        BaseName(facility.Type), AuthorityPosition(LoadVector(facility.Position)), BaseRadius(facility));
    public WorldObjectView[] ReadWorldObjects()
    {
        if (!_playerMode || !_groundReady || _loadPending != null) return [];
        return _groundRobots.Select(RobotObject).Concat(DevelopmentEnabled ? _mines.Select(MineObject) : Enumerable.Empty<WorldObjectView>())
            .Concat(BootstrapEnabled ? _baseFacilities.Select(FacilityObject) : Enumerable.Empty<WorldObjectView>()).ToArray();
    }
    public PlayerObjectDetailView? ReadObjectDetail(WorldObjectKind kind, string id, long observedEpoch)
    {
        if (observedEpoch != _worldEpoch || !_playerMode || !_groundReady || _loadPending != null) return null;
        switch (kind)
        {
            case WorldObjectKind.Robot:
                var actor = _groundRobots.FirstOrDefault(a => a.Name.ToString() == id);
                if (actor == null) return null;
                var health = _health.GetValueOrDefault(id);
                var work = ReadWork();
                bool assigned = work?.WorkerIds.Contains(id) == true;
                var task = DevelopmentEnabled ? _productionTasks.LastOrDefault(t => t.Robot == id && t.Goal == _development?.Id && t.Stage is not ("Completed" or "Cancelled")) : null;
                assigned |= work != null && (task != null || _buildJob is {} assignedBuild && (assignedBuild.Active || assignedBuild.Stage == "Blocked")
                    && (assignedBuild.Builder == id || assignedBuild.Hauler == id || assignedBuild.Clearance.ContainsKey(id)));
                string state = health == null ? "工程模式未接健康" : health.Energy <= 0 && health.Durability <= 0 ? "零电且机械停机"
                    : health.Energy <= 0 ? "零电停机" : health.Durability <= 0 ? "机械停机"
                    : _services.TryGetValue(id, out var service) ? service.Blocked ? "保障阻塞" : service.Returning ? "保障返程" : service.Kind == "repair" ? "前往维修或维修中" : "前往充电或充电中"
                    : assigned ? WorkStageName(work!.Stage) : actor.HasOrder ? "前往工作站" : "待命";
                string reason = health == null ? "本模式没有健康与载荷账本" : _services.ContainsKey(id) || !Operational(actor) ? health.Reason
                    : assigned ? work!.Reason : health.Reason;
                return new(RobotObject(actor), "你授权目标；机器人按分工补齐前置，并消耗真实电量与耐久。",
                    Robot: new(health?.Energy ?? 0, health?.Durability ?? 0, _bootstrapConfig?.Capacity ?? 0, RobotRole(id), state, reason,
                        assigned ? work!.GoalId : null, task?.Id ?? (assigned ? work!.TaskId : null), BootstrapEnabled ? ContainerAmounts(CargoContainer(id)) : []));
            case WorldObjectKind.Mine:
                var mine = DevelopmentEnabled ? _mines.FirstOrDefault(m => m.Id == id) : null;
                if (mine == null) return null;
                // Products are the configured downstream outputs for one recipe batch, not stock or a forecast.
                var reachable = new HashSet<string>(StringComparer.Ordinal) { mine.Material };
                bool changed;
                do
                {
                    changed = false;
                    foreach (var recipe in PlayerCatalog.Recipes.Where(r => r.Input.Keys.All(reachable.Contains)))
                        foreach (string output in recipe.Output.Keys) changed |= reachable.Add(output);
                } while (changed);
                var products = PlayerCatalog.Recipes.Where(r => r.Input.Keys.All(reachable.Contains))
                    .SelectMany(r => r.Output).GroupBy(a => a.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value);
                string prerequisite = ProductionPrerequisite.Length > 0 ? ProductionPrerequisite
                    : "授权缺料建设目标后，筑垒采矿、驮运送入已接线加工设施；生产受日照、功率和仓储空间约束。";
                return new(MineObject(mine), "采出矿物留在矿点现场；驮运实运后才能加工。下列产物为配方单批输出，数量不是库存。",
                    Mine: new(mine.Material, mine.Remaining, AmountViews(products), prerequisite));
            case WorldObjectKind.Facility:
                var facility = BootstrapEnabled ? _baseFacilities.FirstOrDefault(f => f.Id == id) : null;
                if (facility == null) return null;
                bool needsLine = facility.Type is "charger" or "repair" or "processor";
                bool connected = needsLine && ServiceConnected(facility);
                bool powered = Powered(facility);
                string operating = !facility.Built ? "未建成；现场物料与施工按工程推进"
                    : facility.Type is "lander" or "storage" ? "仓内现货可供料；不需要供电接线"
                    : facility.Type == "solar" ? Daylight ? "日照发电" : "夜间等待日照"
                    : !connected ? "已建成，尚未接线" : !Daylight ? "已接线，等待日照" : "已接线；按有限功率运行";
                var processing = _productionTasks.LastOrDefault(t => t.Kind == "recipe" && t.Source == id && t.Paid && t.Stage is not ("Completed" or "Cancelled"));
                if (facility.Built && connected && Daylight && processing != null) operating = processing.Reason.Length > 0 ? processing.Reason : "已投入加工批次，等待实际作业";
                string description = facility.Type == "lander" ? "初始有限物资的真实仓库与供料位置。"
                    : PlayerCatalog.Buildings.Single(b => b.Id == facility.Type).Description;
                return new(FacilityObject(facility), description,
                    Facility: new(facility.Type, facility.Built, connected, powered, facility.Source, operating, FacilityAmounts(id)));
            default: return null;
        }
    }
    private MaterialAmountView[] ContainerAmounts(string id) => AmountViews(_ledger.Snapshot().Containers.Single(c => c.Id == id).Items);
    private MaterialAmountView[] FacilityAmounts(string id)
    {
        var snapshot = _ledger.Snapshot();
        var ids = _productionTasks.Where(t => t.Kind == "recipe" && t.Source == id).Select(t => t.Destination).Append(id).ToHashSet(StringComparer.Ordinal);
        return AmountViews(MaterialLedger.Materials.ToDictionary(m => m, m => snapshot.Containers.Where(c => ids.Contains(c.Id)).Sum(c => c.Items.GetValueOrDefault(m))));
    }
    private static double WorkFraction(double work, double denominator) => Math.Clamp(work / denominator, 0, 1);
    private static PlayerWorkKind GoalKind(string type) => type == "connection" ? PlayerWorkKind.Connection : type == "restock" ? PlayerWorkKind.Restock : PlayerWorkKind.Build;
    private static PlayerWorkStage BuildStage(string stage) => stage switch
    {
        "Preparing" or "Fetching" => PlayerWorkStage.PreparingMaterials,
        "LevelTravel" or "Pickup" or "BuilderTravel" => PlayerWorkStage.Travelling,
        "Levelling" => PlayerWorkStage.Levelling, "LevelPhysics" => PlayerWorkStage.AwaitingPhysics,
        "CargoWaiting" => PlayerWorkStage.WaitingForRoute, "Delivering" => PlayerWorkStage.Transporting,
        "Building" => PlayerWorkStage.Constructing, "Completed" => PlayerWorkStage.Completed,
        "Cancelled" => PlayerWorkStage.Cancelled, "Blocked" => PlayerWorkStage.Blocked, _ => PlayerWorkStage.Unknown
    };
    private static PlayerWorkStage ProductionStage(ProductionTask task, string? phase = null) => (phase ?? task.Stage) switch
    {
        "Fetching" => PlayerWorkStage.PreparingMaterials, "Travel" or "Pickup" => PlayerWorkStage.Travelling,
        "Working" => task.Kind == "mine" ? PlayerWorkStage.Mining : task.Kind == "recipe" ? PlayerWorkStage.Processing : PlayerWorkStage.Unknown,
        "Physics" => PlayerWorkStage.AwaitingPhysics, "CargoWaiting" => PlayerWorkStage.WaitingForRoute,
        "Delivering" => PlayerWorkStage.Transporting, "Completed" => PlayerWorkStage.Completed,
        "Cancelled" => PlayerWorkStage.Cancelled, "Blocked" => PlayerWorkStage.Blocked, _ => PlayerWorkStage.Unknown
    };
    private static string WorkStageName(PlayerWorkStage stage) => stage switch
    {
        PlayerWorkStage.Authorized => "已授权", PlayerWorkStage.PreparingMaterials => "准备材料", PlayerWorkStage.Travelling => "前往工作站",
        PlayerWorkStage.Mining => "现场采矿", PlayerWorkStage.Processing => "加工", PlayerWorkStage.Transporting => "载货运输",
        PlayerWorkStage.WaitingForRoute => "等待运输条件", PlayerWorkStage.Levelling => "整平", PlayerWorkStage.AwaitingPhysics => "验证地表结果",
        PlayerWorkStage.Constructing => "施工", PlayerWorkStage.Completed => "已完成", PlayerWorkStage.Cancelled => "已取消",
        PlayerWorkStage.Blocked => "工程阻塞", _ => "阶段待确认"
    };
    private static PlayerWorkStatus WorkStatus(PlayerWorkStage stage, PlayerReasonCode code) => stage switch
    {
        PlayerWorkStage.Completed => PlayerWorkStatus.Completed, PlayerWorkStage.Cancelled => PlayerWorkStatus.Cancelled,
        PlayerWorkStage.Blocked => PlayerWorkStatus.Blocked,
        PlayerWorkStage.WaitingForRoute when code != PlayerReasonCode.Blocked => PlayerWorkStatus.Waiting,
        _ => code == PlayerReasonCode.Blocked ? PlayerWorkStatus.Blocked : code is PlayerReasonCode.WaitingForWorker or PlayerReasonCode.WaitingForStation or PlayerReasonCode.WaitingForRoute
            or PlayerReasonCode.WaitingForPower or PlayerReasonCode.WaitingForProjection ? PlayerWorkStatus.Waiting : PlayerWorkStatus.Running
    };
    private static PlayerReasonCode StageReason(PlayerWorkStage stage, PlayerReasonCode feedback) => stage switch
    {
        PlayerWorkStage.Completed => PlayerReasonCode.Completed, PlayerWorkStage.Cancelled => PlayerReasonCode.Cancelled,
        PlayerWorkStage.Blocked => PlayerReasonCode.Blocked, PlayerWorkStage.AwaitingPhysics => PlayerReasonCode.WaitingForProjection,
        _ => feedback
    };
    private (PlayerReasonCode Code, string Reason) WorkerFeedback(string[] workers, PlayerReasonCode code, string reason)
    {
        foreach (string id in workers)
            if (_health.TryGetValue(id, out var health) && (!Operational(Actor(id)) || _services.ContainsKey(id)))
                return (PlayerReasonCode.WaitingForWorker, health.Reason.Length > 0 ? health.Reason : "执行者保障或停机；工程与载荷保留");
        return (code, reason);
    }
    private static bool BuildBelongsToGoal(BuildJob build, DevelopmentGoal goal) => goal.Stage != "Supplying"
        && build.Type == goal.Type && build.Supply == goal.Supply && build.Center.SequenceEqual(goal.Center) && build.Yaw == goal.Yaw
        && (build.Type != "connection" || build.Facility == goal.Facility && build.Source == goal.Source);
    private string BuildGoalId(BuildJob build)
    {
        if (_development is {} goal && BuildBelongsToGoal(build, goal)) return goal.Id;
        // Only retained facts can associate an older build; stage alone is not an identity link.
        return build.Type != "connection" ? _tutorial.Goals.FirstOrDefault(g => g.FacilityId == build.Facility
            && g.Type == build.Type && g.Center.SequenceEqual(build.Center))?.GoalId ?? build.Id : build.Id;
    }
    private string UnknownDeliveryReason(string? worker) => worker != null && _ledger.Load(CargoContainer(worker)) > 0
        ? "载荷保留，等待交付条件；具体原因未核实"
        : "等待交付条件；当前载荷为空，具体原因未核实";
    private PlayerWorkView BuildWork(BuildJob build, string? goalId = null)
    {
        var stage = BuildStage(build.Stage);
        var workers = new[] { build.Builder, build.Hauler }.Concat(build.Clearance.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var code = StageReason(stage, build.FeedbackCode);
        if (build.Active && code is PlayerReasonCode.Unknown or PlayerReasonCode.None)
        {
            string routeWorker = build.Stage is "Pickup" or "Delivering" ? build.Hauler : build.Builder;
            if (build.Stage is "Pickup" or "Delivering" or "LevelTravel" or "Levelling" or "Building" && !Actor(routeWorker).HasOrder
                && _routes.TryGetValue(routeWorker, out var route) && !Arrived(Actor(routeWorker), route.Destination)) code = PlayerReasonCode.WaitingForRoute;
        }
        string reason = build.Reason.Length > 0 ? build.Reason : "工程已登记，机器人按真实工段推进";
        bool active = build.Active || build.Stage == "Blocked";
        if (build.Active) (code, reason) = WorkerFeedback(workers, code, reason);
        if (build.Stage == "CargoWaiting" && build.FeedbackCode == PlayerReasonCode.Unknown)
        { code = PlayerReasonCode.Unknown; reason = UnknownDeliveryReason(build.Hauler); }
        PlayerProgressBasis basis = PlayerProgressBasis.None; double? progress = null;
        if (stage is PlayerWorkStage.Levelling or PlayerWorkStage.Constructing)
        {
            if (!Arrived(Actor(build.Builder), LoadVector(build.Station))) stage = PlayerWorkStage.Travelling;
            else
            {
                basis = stage == PlayerWorkStage.Levelling ? PlayerProgressBasis.LevellingWork : PlayerProgressBasis.ConstructionWork;
                progress = WorkFraction(build.Work, stage == PlayerWorkStage.Levelling || build.Type == "connection" ? 3 : Definition(build.Type).WorkSeconds);
            }
        }
        return new(_worldEpoch, goalId ?? BuildGoalId(build), build.Id, GoalKind(build.Type), stage, WorkStatus(stage, code),
            BaseName(build.Type), code, reason, basis, progress, workers, AuthorityPosition(LoadVector(build.Center)), active, _intentVersion);
    }
    private PlayerWorkView ProductionWork(ProductionTask task, string? goalName = null)
    {
        var stage = ProductionStage(task, task.Stage is "Cancelled" or "Blocked" ? task.ResumeStage : null);
        var workers = task.Robot == null ? Array.Empty<string>() : new[] { task.Robot };
        var code = task.Stage == "Cancelled" ? PlayerReasonCode.Cancelled : task.Stage == "Blocked" ? PlayerReasonCode.Blocked : StageReason(stage, task.FeedbackCode);
        string reason = task.Reason.Length > 0 ? task.Reason : "生产任务已登记，等待真实工作条件";
        bool active = task.Goal == _development?.Id && task.Stage is not ("Completed" or "Cancelled");
        if (active) (code, reason) = WorkerFeedback(workers, code, reason);
        if (task.Stage is not ("Cancelled" or "Blocked") && stage == PlayerWorkStage.Processing && !_baseFacilities.Any(f => f.Id == task.Source && Powered(f)))
        { code = PlayerReasonCode.WaitingForPower; reason = "加工已投入，等待已接线电源的日照；未产出物料"; }
        if (active && task.Robot != null && task.Stage is "Travel" or "Working" or "Pickup" or "Delivering"
            && !Actor(task.Robot).HasOrder && _routes.TryGetValue(task.Robot, out var route) && !Arrived(Actor(task.Robot), route.Destination))
        { code = PlayerReasonCode.WaitingForRoute; reason = "当前路线未继续执行；工程与实物保留，未证明具体障碍"; }
        if (task.Stage == "CargoWaiting" && task.FeedbackCode == PlayerReasonCode.Unknown)
        { code = PlayerReasonCode.Unknown; reason = UnknownDeliveryReason(task.Robot); }
        PlayerProgressBasis basis = PlayerProgressBasis.None; double? progress = null;
        if (stage == PlayerWorkStage.Mining)
        {
            if (task.Stage == "Working" && (task.Robot == null || task.Station == null || !Arrived(Actor(task.Robot), LoadVector(task.Station)))) stage = PlayerWorkStage.Travelling;
            else { basis = PlayerProgressBasis.MiningWork; progress = WorkFraction(task.Work, 6); }
        }
        else if (stage == PlayerWorkStage.Processing)
        { basis = PlayerProgressBasis.RecipeWork; progress = WorkFraction(task.Work, Recipe(task.Material).WorkSeconds * task.Quantity); }
        var kind = task.Kind == "mine" ? PlayerWorkKind.Mine : task.Kind == "recipe" ? PlayerWorkKind.Recipe : PlayerWorkKind.Haul;
        string name = goalName ?? (kind == PlayerWorkKind.Mine ? "采集" : kind == PlayerWorkKind.Recipe ? "加工" : "运输") + MaterialName(task.Material);
        var status = task.Stage == "Cancelled" ? PlayerWorkStatus.Cancelled : task.Stage == "Blocked" ? PlayerWorkStatus.Blocked : WorkStatus(stage, code);
        return new(_worldEpoch, task.Goal, task.Id, kind, stage, status, name, code, reason, basis, progress,
            workers, AuthorityPosition(PhysicalEndpoint(task.Kind == "haul" && task.Paid ? task.Destination : task.Source)), active, _intentVersion);
    }
    private PlayerWorkView DevelopmentWork(DevelopmentGoal goal)
    {
        string name = goal.Type == "restock" ? "补充维修耗材" : BaseName(goal.Type);
        if (_buildJob is {} build && BuildBelongsToGoal(build, goal)) return BuildWork(build, goal.Id);
        if (goal.Stage is "Supplying" or "Blocked" && Production is {} task) return ProductionWork(task, name) with { Kind = GoalKind(goal.Type) };
        var stage = goal.Stage switch { "Completed" => PlayerWorkStage.Completed, "Cancelled" => PlayerWorkStage.Cancelled,
            "Blocked" => PlayerWorkStage.Blocked, "Supplying" => PlayerWorkStage.PreparingMaterials, _ => PlayerWorkStage.Unknown };
        var code = StageReason(stage, goal.FeedbackCode);
        return new(_worldEpoch, goal.Id, null, GoalKind(goal.Type), stage, WorkStatus(stage, code), name, code,
            goal.Reason.Length > 0 ? goal.Reason : "目标已授权；机器人自动准备缺料，材料仍须真实采集、加工与运输", PlayerProgressBasis.None, null,
            [], AuthorityPosition(LoadVector(goal.Center)), goal.Active || goal.Stage == "Blocked", _intentVersion);
    }
    private PlayerWorkView LevelWork(LevelJob job)
    {
        var stage = job.Stage switch
        {
            LevelStage.Travelling => PlayerWorkStage.Travelling, LevelStage.Working => PlayerWorkStage.Levelling,
            LevelStage.WaitingForSpace => PlayerWorkStage.Levelling, LevelStage.AwaitingPhysics => PlayerWorkStage.AwaitingPhysics,
            LevelStage.Completed => PlayerWorkStage.Completed, LevelStage.Cancelled => PlayerWorkStage.Cancelled, _ => PlayerWorkStage.Blocked
        };
        string[] workers = job.Worker == null ? [] : [job.Worker.Name.ToString()];
        var code = StageReason(stage, job.Stage == LevelStage.WaitingForSpace ? PlayerReasonCode.WaitingForStation : PlayerReasonCode.None);
        string reason = job.Message;
        if (job.Active) (code, reason) = WorkerFeedback(workers, code, reason);
        bool measured = stage == PlayerWorkStage.Levelling;
        return new(_worldEpoch, job.Id, null, PlayerWorkKind.Level, stage, job.Stage == LevelStage.Failed ? PlayerWorkStatus.Failed : WorkStatus(stage, code),
            "整平地形", code, reason, measured ? PlayerProgressBasis.LevellingWork : PlayerProgressBasis.None, measured ? job.Work.Fraction : null,
            workers, AuthorityPosition(job.Center), job.Active, _intentVersion);
    }
    public PlayerWorkView? ReadWork()
    {
        if (!_groundReady || _loadPending != null) return null;
        if (DevelopmentEnabled && _development is {} goal && (goal.Active || goal.Stage == "Blocked")) return DevelopmentWork(goal);
        if (BootstrapEnabled && _buildJob is {} build && (build.Active || build.Stage == "Blocked")) return BuildWork(build);
        if (_levelJob?.Active == true) return LevelWork(_levelJob);
        return null;
    }
    public PlayerWorkView[] ReadWorkHistory()
    {
        if (!_groundReady || _loadPending != null) return [];
        var rows = new List<PlayerWorkView>();
        if (DevelopmentEnabled)
        {
            rows.AddRange(_productionTasks.Select(t => ProductionWork(t)));
            foreach (var record in _tutorial.Goals)
            {
                if (_development?.Id == record.GoalId && _development.Stage is "Supplying" or "Blocked" && Production != null)
                    rows.Add(new(_worldEpoch, record.GoalId, null, GoalKind(record.Type), PlayerWorkStage.Authorized, PlayerWorkStatus.Waiting, BaseName(record.Type), PlayerReasonCode.None,
                        "教学授权回执；该目标的当前生产任务另列", PlayerProgressBasis.None, null, [], AuthorityPosition(LoadVector(record.Center)), true, _intentVersion));
                else if (_development?.Id == record.GoalId) rows.Add(DevelopmentWork(_development));
                else rows.Add(new(_worldEpoch, record.GoalId, null, GoalKind(record.Type), record.Completed ? PlayerWorkStage.Completed : PlayerWorkStage.Authorized,
                    record.Completed ? PlayerWorkStatus.Completed : PlayerWorkStatus.Waiting, BaseName(record.Type), record.Completed ? PlayerReasonCode.Completed : PlayerReasonCode.Unknown,
                    record.Completed ? "教学回执：设施已建成" : "保留的教学授权回执；未保留该工程的完整终态记录", PlayerProgressBasis.None, null, [],
                    AuthorityPosition(LoadVector(record.Center)), false, _intentVersion));
            }
            if (_development != null) rows.Add(DevelopmentWork(_development));
        }
        if (BootstrapEnabled && _buildJob is {} build && !build.Active && build.Stage != "Blocked")
            rows.Add(BuildWork(build));
        if (_levelJob is {} level && !level.Active) rows.Add(LevelWork(level));
        return rows.GroupBy(r => (r.GoalId, r.TaskId)).Select(g => g.Last()).ToArray();
    }
    private PlayerJobView? ReadCompatibilityJob()
    {
        var work = ReadWork();
        if (work == null && BootstrapEnabled && _buildJob != null) work = BuildWork(_buildJob);
        if (work == null && _levelJob != null) work = LevelWork(_levelJob);
        string id = work?.TaskId is {} taskId && taskId.StartsWith("build-", StringComparison.Ordinal) ? taskId : work?.GoalId ?? "";
        return work == null ? null : new(id, WorkStageName(work.Stage), work.WorkerIds.FirstOrDefault() ?? "", work.Progress ?? 0, work.Center, work.Active);
    }
    private (string Name, MaterialContainerKind Kind, string? Owner, Vector3? Position) ContainerIdentity(string id)
    {
        if (id == "spent") return ("已消耗（不计库存）", MaterialContainerKind.Consumed, null, null);
        var actor = _groundRobots.FirstOrDefault(a => CargoContainer(a.Name.ToString()) == id);
        if (actor != null) return (RobotName(actor.Name.ToString()) + "携带", MaterialContainerKind.RobotCargo, actor.Name.ToString(), AuthorityPosition(actor.GlobalPosition));
        var mine = _mines.FirstOrDefault(m => m.Id == id);
        if (mine != null) return (MaterialName(mine.Material) + "矿点现场", MaterialContainerKind.Mine, mine.Id, AuthorityPosition(LoadVector(mine.Position)));
        var batch = _productionTasks.FirstOrDefault(t => t.Kind == "recipe" && t.Destination == id);
        if (batch != null) return ("加工批次 " + batch.Id, MaterialContainerKind.Processing, batch.Source, AuthorityPosition(PhysicalEndpoint(id)));
        var facility = _baseFacilities.FirstOrDefault(f => f.Id == id);
        if (facility != null) return (BaseName(facility.Type) + (facility.Built ? "" : "工地"),
            !facility.Built ? MaterialContainerKind.Worksite : facility.Type is "lander" or "storage" ? MaterialContainerKind.Warehouse : MaterialContainerKind.Processing,
            facility.Id, AuthorityPosition(LoadVector(facility.Position)));
        if (_buildJob is { Type: "connection" } build && build.Id == id) return ("接线现场 " + id, MaterialContainerKind.Worksite, build.Facility, AuthorityPosition(LoadVector(build.Center)));
        return ("保留物料容器 " + id, MaterialContainerKind.Other, null, null);
    }
    public PlayerMaterialsView ReadMaterialsDetail()
    {
        if (_ledger == null || !_groundReady || _loadPending != null) return new([], [], [], [], _intentVersion, _worldEpoch);
        var snapshot = _ledger.Snapshot();
        var locations = snapshot.Containers.Where(c => c.Id != "spent").Select(c =>
        {
            var identity = ContainerIdentity(c.Id);
            var materials = MaterialLedger.Materials.Where(m => c.Items.GetValueOrDefault(m) > 0).Select(m =>
            {
                int quantity = c.Items.GetValueOrDefault(m), reserved = snapshot.Reservations.Where(r => r.Container == c.Id && r.Material == m).Sum(r => r.Quantity);
                return new MaterialLocationAmountView(m, MaterialName(m), quantity, reserved, quantity - reserved);
            }).ToArray();
            return new MaterialContainerView(c.Id, identity.Name, identity.Kind, identity.Owner, identity.Position, c.Capacity, materials);
        }).ToArray();
        var reservations = snapshot.Reservations.Select(r => new MaterialReservationView(r.Goal, r.Container, r.Material, MaterialName(r.Material), r.Quantity)).ToArray();
        var batches = _productionTasks.Where(t => t.Kind == "recipe" && t.Paid && t.Stage is not ("Completed" or "Cancelled")).Select(t =>
        {
            var recipe = Recipe(t.Material);
            return new ProductionBatchView(t.Id, t.Goal, recipe.Id, t.Source, t.Paid, ProductionStage(t),
                AmountViews(recipe.Input.ToDictionary(a => a.Key, a => a.Value * t.Quantity)),
                AmountViews(recipe.Output.ToDictionary(a => a.Key, a => a.Value * t.Quantity)), WorkFraction(t.Work, recipe.WorkSeconds * t.Quantity));
        }).ToArray();
        return new(ReadMaterialStock(), locations, reservations, batches, _intentVersion, _worldEpoch);
    }
}
