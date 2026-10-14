#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
namespace Yudian;

public partial class Main
{
    private sealed class TutorialGoalRecord
    {
        public required string GoalId { get; init; }
        public required string Type { get; init; }
        public string LearningPhase { get; init; } = "first-direction";
        public required float[] Center { get; init; }
        public string? FacilityId { get; set; }
        public bool Completed { get; set; }
    }
    private sealed class TutorialProgress
    {
        public int Version { get; init; } = 1;
        public bool LegacyPractice { get; init; }
        public bool CameraObserved { get; set; }
        public string? SelectedRobot { get; set; }
        public string? InitialSolarId { get; set; }
        public string? ChargerId { get; set; }
        public string? RepairId { get; set; }
        public string? ProcessorId { get; set; }
        public string? FirstDirection { get; set; }
        public string? FirstGoalId { get; set; }
        public string? SecondGoalId { get; set; }
        public string? SecondChargerGoalId { get; set; }
        public bool ConclusionAcknowledged { get; set; }
        public List<TutorialGoalRecord> Goals { get; init; } = new();
    }
    private TutorialProgress _tutorial = new();
    private bool SupportSetConnected => new[] { "charger", "repair", "processor" }.All(type => _baseFacilities.Any(f => f.Type == type && ServiceConnected(f)));
    private BaseFacility? TutorialFacility(string? id, string type) => id == null ? null : _baseFacilities.FirstOrDefault(f => f.Id == id && f.Type == type && f.Built);
    private TutorialGoalRecord? TutorialGoal(string? id) => _tutorial.Goals.FirstOrDefault(g => g.GoalId == id);
    private bool TutorialProductionProven(string? goal)
    {
        if (goal == null) return false;
        var selected = TutorialGoal(goal);
        if (selected == null) return false;
        var lineage = _tutorial.Goals.Where(g => g.LearningPhase == selected.LearningPhase).Select(g => g.GoalId).ToHashSet(StringComparer.Ordinal);
        var snapshot = _ledger.Snapshot();
        return snapshot.Mining?.Any(r => _productionTasks.Any(t => lineage.Contains(t.Goal) && t.Id + "-mine" == r.Operation)) == true
            && snapshot.Recipes?.Any(r => _productionTasks.Any(t => lineage.Contains(t.Goal) && t.Id + "-recipe" == r.Operation)) == true;
    }
    private string? TutorialStorageId => _tutorial.Goals.FirstOrDefault(g => g.Completed && g.Type == "storage")?.FacilityId;
    private string? TutorialExpansionSolarId => _tutorial.Goals.FirstOrDefault(g => g.Completed && g.Type == "solar")?.FacilityId;
    private string? TutorialSecondChargerId => TutorialGoal(_tutorial.SecondChargerGoalId)?.FacilityId;
    private bool TutorialStorageSupplied
    {
        get
        {
            string? storage = TutorialStorageId, charger = TutorialSecondChargerId;
            if (storage == null || charger == null) return false;
            return _ledger.Snapshot().Operations.Any(o => o.Key.StartsWith(charger + "-take-", StringComparison.Ordinal)
                && o.Value.StartsWith(charger + "|" + storage + "|cargo:Robot_Tuoyun_", StringComparison.Ordinal));
        }
    }
    private bool TutorialFinished
    {
        get
        {
            if (_tutorial.LegacyPractice || !_tutorial.CameraObserved || _tutorial.SelectedRobot == null || !SupportSetConnected) return false;
            var first = TutorialGoal(_tutorial.FirstGoalId); var second = TutorialGoal(_tutorial.SecondGoalId);
            var charger = TutorialFacility(TutorialSecondChargerId, "charger");
            return first?.Completed == true && second?.Completed == true && first.Type != second.Type
                && new[] { first.Type, second.Type }.Order().SequenceEqual(new[] { "solar", "storage" })
                && TutorialProductionProven(first.GoalId) && TutorialStorageSupplied
                && charger != null && ServiceConnected(charger) && charger.Source == TutorialExpansionSolarId;
        }
    }
    private void RecordTutorialAuthorization(DevelopmentGoal goal)
    {
        if (!TutorialEnabled || _tutorial.LegacyPractice || !SupportSetConnected) return;
        bool expansionsComplete = TutorialGoal(_tutorial.FirstGoalId)?.Completed == true && TutorialGoal(_tutorial.SecondGoalId)?.Completed == true;
        string phase;
        if (goal.Type is "solar" or "storage" && !expansionsComplete)
        {
            var first = TutorialGoal(_tutorial.FirstGoalId);
            if (first?.Completed != true)
            {
                phase = "first-direction";
                _tutorial.FirstGoalId = goal.Id; _tutorial.FirstDirection = goal.Type;
                _tutorial.SecondGoalId = null; _tutorial.SecondChargerGoalId = null;
            }
            else if (first.Type != goal.Type && TutorialGoal(_tutorial.SecondGoalId)?.Completed != true) { phase = "second-direction"; _tutorial.SecondGoalId = goal.Id; }
            else return;
        }
        else if (goal.Type == "charger" && expansionsComplete && TutorialGoal(_tutorial.SecondChargerGoalId)?.Completed != true)
        { phase = "second-charger"; _tutorial.SecondChargerGoalId = goal.Id; }
        else return;
        _tutorial.Goals.Add(new() { GoalId = goal.Id, Type = goal.Type, LearningPhase = phase, Center = goal.Center.ToArray() });
    }
    private void RecordTutorialBuild(BuildJob build)
    {
        if (!TutorialEnabled || _tutorial.LegacyPractice || build.Type == "connection") return;
        if (build.Type == "solar" && _tutorial.InitialSolarId == null) _tutorial.InitialSolarId = build.Facility;
        if (build.Type == "charger" && _tutorial.ChargerId == null) _tutorial.ChargerId = build.Facility;
        if (build.Type == "repair" && _tutorial.RepairId == null) _tutorial.RepairId = build.Facility;
        if (build.Type == "processor" && _tutorial.ProcessorId == null) _tutorial.ProcessorId = build.Facility;
        var record = TutorialGoal(_development?.Id);
        if (record != null) { record.FacilityId = build.Facility; record.Completed = true; }
    }
    public TutorialView ReadTutorial()
    {
        if (!TutorialEnabled || _bootstrapConfig == null || !_groundReady)
            return new(false, false, "loading", "准备教学世界", "等待世界与物理准备", "", "wait", null, null, null, 0, 8, false, false, null, null, null, null, null, null, []);
        var solar = TutorialFacility(_tutorial.InitialSolarId, "solar") ?? _baseFacilities.FirstOrDefault(f => f.Built && f.Type == "solar");
        BaseFacility? Support(string type, string? id) => TutorialFacility(id, type) ?? _baseFacilities.FirstOrDefault(f => f.Built && f.Type == type);
        var charger = Support("charger", _tutorial.ChargerId); var repair = Support("repair", _tutorial.RepairId); var processor = Support("processor", _tutorial.ProcessorId);
        var first = TutorialGoal(_tutorial.FirstGoalId); var second = TutorialGoal(_tutorial.SecondGoalId); var newCharger = TutorialFacility(TutorialSecondChargerId, "charger");
        var milestones = new[]
        {
            new TutorialMilestoneView("solar", "第一座太阳能建成", solar != null),
            new TutorialMilestoneView("charger", "充电桩实际接线", charger != null && ServiceConnected(charger)),
            new TutorialMilestoneView("repair", "维修站实际接线", repair != null && ServiceConnected(repair)),
            new TutorialMilestoneView("processor", "加工设施实际接线", processor != null && ServiceConnected(processor)),
            new TutorialMilestoneView("first-direction", "首个发展完成并实际生产", !_tutorial.LegacyPractice && first?.Completed == true && TutorialProductionProven(first.GoalId)),
            new TutorialMilestoneView("second-direction", "另一发展方向完成", !_tutorial.LegacyPractice && second?.Completed == true && second.Type != first?.Type),
            new TutorialMilestoneView("storage-supply", "新增仓储实际供应第二充电桩", !_tutorial.LegacyPractice && TutorialStorageSupplied),
            new TutorialMilestoneView("second-charger", "第二充电桩明确接入新增阵列", !_tutorial.LegacyPractice && newCharger != null && ServiceConnected(newCharger) && newCharger.Source == TutorialExpansionSolarId),
        };
        string stage = "solar", title = "建设第一座太阳能", instruction = "选择太阳能蓝图与位置，检查12m覆盖、完整成本和运输距离后授权；机器人送料、整平并施工。", why = "你决定布局；建成阵列在日照时发电，用电设施还要明确接线。", action = "build";
        string? needed = "solar", facility = null, source = null;
        if (solar != null)
        {
            stage = "";
            foreach (var item in new[] { ("charger", charger, "充电桩", "尽早接线，保障工程队续航。"), ("repair", repair, "维修站", "耐久与电量分开；维修需要有限结构件。"), ("processor", processor, "加工设施", "接通加工后，真实矿物可以生产建设材料。") })
            {
                if (item.Item2 != null && ServiceConnected(item.Item2)) continue;
                bool built = item.Item2 != null;
                stage = item.Item1 + (built ? "-wire" : ""); title = built ? "为" + item.Item3 + "明确接线" : "建设" + item.Item3;
                needed = item.Item1; facility = item.Item2?.Id; why = item.Item4;
                var candidate = item.Item2 == null ? null : _baseFacilities.Where(f => f.Built && f.Type == "solar" && XzDistance(LoadVector(f.Position), LoadVector(item.Item2.Position)) <= _bootstrapConfig.ConnectionRangeM)
                    .OrderBy(f => XzDistance(LoadVector(f.Position), LoadVector(item.Item2.Position))).FirstOrDefault();
                source = candidate?.Id;
                action = !built ? "build" : candidate == null ? "recover-power" : "connect";
                instruction = !built ? "在已建太阳能12m内选择设施位置，预览后授权。" : candidate == null ? "这座既有设施超出覆盖；在其12m内规划新增太阳能，再明确接线。" : "选择这座设施与太阳能来源；线缆将沿真实物料和施工链铺设。";
                break;
            }
            if (stage.Length == 0)
            {
                if (_tutorial.LegacyPractice)
                {
                    stage = "legacy-practice"; title = "旧档练习世界"; instruction = "首套保障已接通，可以继续经营或保存到独立新槽。";
                    why = "保留旧世界事实；旧档不自动代签新教学历史。"; action = "choose"; needed = null;
                }
                else if (first?.Completed != true)
                {
                    stage = "first-direction"; title = "选择第一次发展方向"; instruction = "先点矿点可查看剩余量与加工产物，再比较仓储的容量/供料位置和太阳能的功率/覆盖。授权一项后，机器人自动采矿、运入加工设施、送料并施工。";
                    why = "矿物先留在现场，加工产物再由驮运送到仓库或工程；组件套件只能消耗有限启动库存。"; action = "choose"; needed = null;
                }
                else if (second?.Completed != true)
                {
                    stage = "second-direction"; needed = first.Type == "storage" ? "solar" : "storage";
                    title = "自主完成另一个发展方向"; instruction = "运用成本、覆盖和物流规则，自主规划" + BaseName(needed) + "。";
                    why = "保留规则回顾，由你决定新的位置与朝向。"; action = "build";
                }
                else if (newCharger == null)
                {
                    stage = "second-charger"; title = "建立第二保障片区"; needed = "charger"; action = "build"; source = TutorialExpansionSolarId;
                    instruction = "在新增太阳能12m内规划第二充电桩，并使用新增仓储真实供料。"; why = "把两个新增能力用于同一个真实工程。";
                }
                else if (newCharger.Source != TutorialExpansionSolarId)
                {
                    stage = "second-charger-wire"; title = "明确接通第二充电桩"; needed = "charger"; facility = newCharger.Id; source = TutorialExpansionSolarId; action = "connect";
                    instruction = "选择第二充电桩与新增太阳能，检查12m并付费铺设线缆。"; why = "新片区需要自己的真实连接；位置会影响保障路线。";
                    if (newCharger.Source != null) { action = "wait"; instruction = "此桩已接其他阵列，不能免费重接；需在新增阵列内另建保障设施，有限套件仍约束建设。"; }
                }
                else if (!TutorialFinished)
                {
                    stage = "learning-evidence"; title = "检查新增能力的实际使用"; needed = null; action = "wait";
                    instruction = "结尾还需真实采矿、配方结算与新增仓储供料记录。"; why = "世界结果与学习历史共同证明教学完成。";
                }
                else
                {
                    stage = _tutorial.ConclusionAcknowledged ? "freeplay" : "conclusion"; title = _tutorial.ConclusionAcknowledged ? "继续经营你的基地" : "教学完成";
                    instruction = "首套保障与加工已接通；两次发展、真实生产和新保障片区已经建立。可以继续经营，或保存返回主页。";
                    why = "资源、能力与布局都保存在同一世界。"; needed = null; action = "continue";
                }
            }
        }
        if (!_tutorial.LegacyPractice && (!_tutorial.CameraObserved || _tutorial.SelectedRobot == null))
        {
            stage = "observe-team"; title = "认识镜头与工程队"; action = "observe"; needed = null; facility = null; source = null;
            instruction = !_tutorial.CameraObserved ? "用WASD或拖动移动镜头、滚轮缩放；再点选一台机器人查看电量与耐久。" : "点选一台真实机器人，查看电量、耐久与分工。";
            why = "电量与耐久是两个约束；你给目标，机器人执行。也可以先预规划太阳能。";
        }
        if (HasCurrentWork) { action = "wait"; instruction = "当前工程正在执行；可无承诺预规划下一项。\n" + instruction; }
        if (_playerSettings.HelpDetail == "brief") instruction = stage is "first-direction" ? "选择仓储或新增太阳能，比较方案后授权。" : title + "；检查成本、12m覆盖与物流后果。";
        bool completed = TutorialFinished;
        return new(true, _tutorial.LegacyPractice, stage, title, instruction, why, action, needed, facility, source,
            milestones.Count(m => m.Achieved), milestones.Length, completed, completed && !_tutorial.ConclusionAcknowledged,
            _tutorial.FirstDirection, _tutorial.FirstGoalId, _tutorial.SecondGoalId, TutorialStorageId, TutorialExpansionSolarId, TutorialSecondChargerId, milestones);
    }
    public void QueueTutorialAction(string action, string? objectId = null)
        => QueuePlayer(new("tutorial:" + action, Vector3.Zero, -1, objectId));
    private void ProcessTutorialAction(PlayerCommand command)
    {
        if (!TutorialEnabled || _entryOpen || _menuPaused || _loadPending != null) throw new InvalidOperationException("等待局内世界准备");
        switch (command.Action[9..])
        {
            case "camera": _tutorial.CameraObserved = true; break;
            case "select-robot":
                if (!_groundRobots.Any(a => a.Name.ToString() == command.Worker)) throw new ArgumentException("请选真实机器人");
                _tutorial.SelectedRobot = command.Worker; break;
            case "continue":
                if (!TutorialFinished) throw new InvalidOperationException("教学世界里程碑尚未完成");
                _tutorial.ConclusionAcknowledged = true; _playerNotice = "同一世界继续经营；教学结果已保留"; break;
            default: throw new ArgumentException("未知教学操作");
        }
    }
    private TutorialProgress CaptureTutorial() => CloneSaveValue(_tutorial);
    private void ApplyTutorial(TutorialProgress? saved) => _tutorial = saved == null ? new() { LegacyPractice = true } : CloneSaveValue(saved);
    private string SavedTutorialStage(PlayerSave saved)
    {
        if (saved.Tutorial == null || saved.Tutorial.LegacyPractice) return "旧档练习";
        var t = saved.Tutorial;
        if (t.ConclusionAcknowledged) return "继续经营";
        if (t.SecondChargerGoalId != null) return "第二保障片区";
        if (t.SecondGoalId != null) return "第二次发展";
        if (t.FirstGoalId != null) return "第一次发展";
        var facilities = saved.Bootstrap!.Facilities;
        foreach (var type in new[] { "solar", "charger", "repair", "processor" })
            if (!facilities.Any(f => f.Built && f.Type == type && (type == "solar" || f.Source != null))) return BaseName(type);
        return "首套已连接";
    }
    private void ValidateTutorial(PlayerSave saved)
    {
        var t = saved.Tutorial;
        if (saved.Schema != 4) { if (t != null) throw new InvalidDataException("旧版本不能携带教学完成历史"); return; }
        if (t == null || t.Version != 1 || t.Goals == null || t.Goals.Count > 1000 || t.Goals.Any(g => g == null) || t.Goals.Select(g => g.GoalId).Distinct().Count() != t.Goals.Count)
            throw new InvalidDataException("教学历史版本或集合无效");
        var b = saved.Bootstrap!; var d = b.Development!;
        bool Facility(string? id, string type) => id == null || b.Facilities.Any(f => f.Id == id && f.Type == type && f.Built);
        if (!Facility(t.InitialSolarId, "solar") || !Facility(t.ChargerId, "charger") || !Facility(t.RepairId, "repair") || !Facility(t.ProcessorId, "processor")
            || t.SelectedRobot != null && !saved.Robots.Any(r => r.Id == t.SelectedRobot)) throw new InvalidDataException("教学基础引用无效");
        foreach (var record in t.Goals)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.GoalId) || !record.GoalId.StartsWith("goal-", StringComparison.Ordinal) || !int.TryParse(record.GoalId[5..], out int sequence)
                || sequence < 1 || sequence > d.GoalSequence || record.Type is not ("solar" or "storage" or "charger")
                || record.LearningPhase is not ("first-direction" or "second-direction" or "second-charger")
                || (record.LearningPhase == "second-charger") != (record.Type == "charger")) throw new InvalidDataException("教学目标身份或学习阶段无效");
            var position = LoadVector(record.Center);
            if (record.FacilityId != null && !b.Facilities.Any(f => f.Id == record.FacilityId && f.Type == record.Type && LoadVector(f.Position) == position))
                throw new InvalidDataException("教学设施与目标引用无效");
            if (record.Completed && !b.Facilities.Any(f => f.Id == record.FacilityId && f.Type == record.Type && f.Built)) throw new InvalidDataException("教学完成没有真实设施");
            if (d.Goal?.Id == record.GoalId && (d.Goal.Type != record.Type || LoadVector(d.Goal.Center) != position || record.Completed != (d.Goal.Stage == "Completed")))
                throw new InvalidDataException("教学历史与当前目标不一致");
        }
        TutorialGoalRecord? Ref(string? id) => id == null ? null : t.Goals.SingleOrDefault(g => g.GoalId == id) ?? throw new InvalidDataException("教学目标引用缺失");
        var first = Ref(t.FirstGoalId); var second = Ref(t.SecondGoalId); var charger = Ref(t.SecondChargerGoalId);
        if (first == null ? t.FirstDirection != null || second != null || charger != null : first.Type is not ("solar" or "storage") || t.FirstDirection != first.Type
            || second != null && (!first.Completed || second.Type is not ("solar" or "storage") || second.Type == first.Type)
            || charger != null && (!first.Completed || second?.Completed != true || charger.Type != "charger")) throw new InvalidDataException("教学发展顺序或方向无效");
        if (first != null && first.LearningPhase != "first-direction" || second != null && second.LearningPhase != "second-direction" || charger != null && charger.LearningPhase != "second-charger")
            throw new InvalidDataException("教学目标引用的学习阶段无效");
        var ordered = t.Goals.OrderBy(g => int.Parse(g.GoalId[5..])).ToArray();
        foreach (var record in ordered.Where(g => g.LearningPhase == "first-direction"))
            if (first == null || int.Parse(record.GoalId[5..]) > int.Parse(first.GoalId[5..]))
                throw new InvalidDataException("首次学习谱系不能纳入首次最终目标之后的生产授权");
        foreach (var record in ordered.Where(g => g.LearningPhase == "second-direction"))
            if (first?.Completed != true || int.Parse(record.GoalId[5..]) <= int.Parse(first.GoalId[5..]) || record.Type == first.Type)
                throw new InvalidDataException("第二学习阶段缺少前一阶段真实完成");
        foreach (var record in ordered.Where(g => g.LearningPhase == "second-charger"))
            if (second?.Completed != true || int.Parse(record.GoalId[5..]) <= int.Parse(second.GoalId[5..]))
                throw new InvalidDataException("保障迁移阶段缺少两方向真实完成");
        if (t.LegacyPractice && (t.Goals.Count != 0 || t.FirstGoalId != null || t.SecondGoalId != null || t.SecondChargerGoalId != null || t.ConclusionAcknowledged))
            throw new InvalidDataException("旧档不能伪造教学完成");
        if (t.ConclusionAcknowledged)
        {
            string? storage = t.Goals.FirstOrDefault(g => g.Completed && g.Type == "storage")?.FacilityId;
            string? solar = t.Goals.FirstOrDefault(g => g.Completed && g.Type == "solar")?.FacilityId;
            var newCharger = b.Facilities.FirstOrDefault(f => f.Id == charger?.FacilityId && f.Built && f.Type == "charger");
            var lineage = t.Goals.Where(g => g.LearningPhase == "first-direction").Select(g => g.GoalId).ToHashSet(StringComparer.Ordinal);
            bool mining = (b.Ledger.Mining ?? []).Any(r => d.Tasks.Any(p => lineage.Contains(p.Goal) && p.Id + "-mine" == r.Operation));
            bool recipe = (b.Ledger.Recipes ?? []).Any(r => d.Tasks.Any(p => lineage.Contains(p.Goal) && p.Id + "-recipe" == r.Operation));
            bool supplied = storage != null && newCharger != null && b.Ledger.Operations.Any(o => o.Key.StartsWith(newCharger.Id + "-take-", StringComparison.Ordinal) && o.Value.StartsWith(newCharger.Id + "|" + storage + "|cargo:", StringComparison.Ordinal));
            if (!t.CameraObserved || t.SelectedRobot == null || !mining || !recipe || !supplied || newCharger?.Source != solar || solar == null
                || !new[] { "charger", "repair", "processor" }.All(type => b.Facilities.Any(f => f.Built && f.Type == type && f.Source != null)))
                throw new InvalidDataException("教学结尾缺少真实生产、供料或接线证据");
        }
    }
}
