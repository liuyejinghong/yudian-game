#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Yudian.Resources;

namespace Yudian;

// Run only with --headless, GUI_TEST=1 and a new absolute private slot root.
// This is an authority test: no user input, health/material grants, TimeScale or route changes.
public partial class Main
{
    private bool R2SelfTest => System.Environment.GetEnvironmentVariable("YUDIAN_R2_SELF_TEST") == "1";
    private int _r2Step, _r2HoldFrame, _r2ReturnStep, _r2PauseRound;
    private int _r2MineReceipts;
    private string _r2Frozen = "", _r2InitialFrozen = "", _r2Goal = "", _r2Task = "", _r2Solar = "";
    private long _r2Epoch, _r2TransitionRevision;
    private bool _r2ReservationSeen, _r2TransitSeen, _r2BatchSeen, _r2UserValue, _r2HoldTicksChecked;
    private string _r2OverwriteBytes = "";
    private int _r2LastFactsFrame = -60, _r2LastProgressFrame = -600, _r2LastProgressStep = -1;
    private R2FactsStamp? _r2LastFacts;
    private readonly HashSet<string> _r2ReservedBuilds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _r2PaidBatches = new(StringComparer.Ordinal);
    private readonly HashSet<string> _r2CargoOperations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _r2LoadedHauls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Goal, bool Connection)> _r2BuildOwners = new(StringComparer.Ordinal);
    private sealed record R2FactsStamp(long Intent, long Epoch, int Step, string? Goal, string? GoalStage,
        string? Task, string? TaskStage, bool Paid, string? Build, string? BuildStage, int Trip, LevelStage? LevelStage);

    private void ValidateR2TestIsolation()
    {
        if (!R2SelfTest) return;
        string? root = System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_TEST_ROOT");
        GroundRequire(_playerMode && TutorialEnabled && DisplayServer.GetName() == "headless"
            && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1"
            && !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root), "R2 requires normal player mode, --headless, GUI_TEST=1 and an explicit absolute private root");
        string[] conflicting = ["YUDIAN_TUTORIAL_SELF_TEST", "YUDIAN_PLAYER_SELF_TEST", "YUDIAN_BOOTSTRAP_SELF_TEST", "YUDIAN_DEVELOPMENT_SELF_TEST", "YUDIAN_DEMO_SELF_TEST", "YUDIAN_LEVEL_SELF_TEST", "YUDIAN_LIVE_SELF_TEST", "YUDIAN_BENCHMARK"];
        GroundRequire(conflicting.All(n => System.Environment.GetEnvironmentVariable(n) != "1"), "R2 test must not run an older authority driver concurrently");
        GroundRequire(System.Environment.GetEnvironmentVariable("YUDIAN_CAPTURE_PNG") == null && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_CAPTURE_DIR") == null,
            "R2 authority runner does not request viewport capture");
        _ = TutorialSaveRoot;
        GroundRequire(!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any(), "R2 private root must be new and empty");
    }
    private string R2AuthorityFingerprint() => JsonSerializer.Serialize(new
    {
        Saved = CapturePlayer(),
        Routes = _routes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new { p.Key, p.Value.Destination, p.Value.Seconds, p.Value.Stalled, p.Value.WorldVersion, p.Value.FacilityRevision, p.Value.Escaping, p.Value.EscapeDestination }).ToArray(),
        Actors = _groundRobots.Select(a => new { Id = a.Name.ToString(), a.GlobalPosition, a.Velocity, a.TravelledM, a.HasOrder, a.OrderReached, a.Blocked }).ToArray()
    }, new JsonSerializerOptions { IncludeFields = true });
    private bool _r2LegacyBuildWaitingSeen, _r2LegacyHaulWaitingSeen;
    private void R2CheckScheduledFacts(bool force = false)
    {
        var task = Production;
        var stamp = new R2FactsStamp(_intentVersion, _worldEpoch, _r2Step, _development?.Id, _development?.Stage,
            task?.Id, task?.Stage, task?.Paid == true, _buildJob?.Id, _buildJob?.Stage, _buildJob?.Trip ?? 0, _levelJob?.Stage);
        if (!force && stamp == _r2LastFacts && _groundFrame - _r2LastFactsFrame < 60) return;
        R2CheckMaterialFacts(); R2CheckPlanFacts(); R2CheckWorkIdentities(); R2CheckLegacyWaitingProjection(_buildJob);
        _r2LastFacts = stamp; _r2LastFactsFrame = _groundFrame;
    }
    private void R2CheckWorkIdentities()
    {
        var work = ReadWork();
        if (_development is { Stage: "Supplying" } supplying)
            GroundRequire(work?.GoalId == supplying.Id && ReadCompatibilityJob()?.Id == supplying.Id, "R2 supplying compatibility goal identity");
        if (_buildJob is not {} build) return;
        var projected = BuildWork(build);
        string expectedGoal = _r2BuildOwners.TryGetValue(build.Id, out var known) ? known.Goal
            : _development?.Id ?? build.Id;
        GroundRequire(projected.GoalId == expectedGoal && projected.TaskId == build.Id, "R2 build task maps to its actual goal");
        if (expectedGoal != build.Id)
        {
            if (_r2BuildOwners.TryGetValue(build.Id, out var previous))
                GroundRequire(previous.Goal == projected.GoalId, "R2 construction goal association never changes");
            else _r2BuildOwners.Add(build.Id, (projected.GoalId, build.Type == "connection"));
        }
        if (work == null || work.TaskId == build.Id)
            GroundRequire(ReadCompatibilityJob()?.Id == build.Id, "R2 compatibility construction instance stable through execution/terminal");
        if (!build.Active && build.Stage != "Blocked")
            GroundRequire(ReadWorkHistory().Any(w => w.GoalId == expectedGoal && w.TaskId == build.Id), "R2 terminal construction retained under actual identity");
        foreach (var row in ReadWorkHistory().Where(w => w.TaskId != null && _r2BuildOwners.ContainsKey(w.TaskId)))
            GroundRequire(row.GoalId == _r2BuildOwners[row.TaskId!].Goal, "R2 history never associates old construction with a later goal");
    }
    private void R2CheckLegacyWaitingProjection(BuildJob? build = null)
    {
        var haul = Production;
        bool checkBuild = !_r2LegacyBuildWaitingSeen && build?.Stage == "Delivering" && _ledger.Load(CargoContainer(build.Hauler)) > 0;
        bool checkHaul = !_r2LegacyHaulWaitingSeen && haul is { Kind: "haul", Stage: "Delivering", Paid: true } && _ledger.Load(CargoContainer(haul.Robot!)) > 0;
        if ((!checkBuild && !checkHaul) || _userPaused || _managementPaused || _playerCommand != null) return;
        QueuePlayerAction("pause"); ProcessPlayerCommand();
        GroundRequire(_userPaused, "R2 old waiting projection checked under real user pause");
        string frozen = R2AuthorityFingerprint();
        if (checkBuild)
        {
            var oldBuild = CloneSaveValue(build!); oldBuild.Stage = "CargoWaiting"; oldBuild.Reason = "已真实取货，驮运载货前往工地";
            GroundRequire(oldBuild.FeedbackCode == PlayerReasonCode.Unknown, "R2 old build restores unpersisted feedback as unknown");
            RequireWaiting(BuildWork(oldBuild));
            foreach (string terminal in new[] { "Completed", "Cancelled" })
            {
                oldBuild.Stage = terminal;
                var terminalView = BuildWork(oldBuild);
                GroundRequire(terminalView.TaskId == build!.Id && terminalView.GoalId == _r2BuildOwners[build.Id].Goal,
                    "R2 retained construction instance maps through both terminal projections");
            }
            _r2LegacyBuildWaitingSeen = true;
        }
        if (checkHaul)
        {
            var oldHaul = CloneSaveValue(haul!); oldHaul.Stage = "CargoWaiting"; oldHaul.Reason = "真实载货运输：" + MaterialName(oldHaul.Material);
            GroundRequire(oldHaul.FeedbackCode == PlayerReasonCode.Unknown, "R2 old haul restores unpersisted feedback as unknown");
            RequireWaiting(ProductionWork(oldHaul)); _r2LegacyHaulWaitingSeen = true;
        }
        GroundRequire(frozen == R2AuthorityFingerprint() && _userPaused, "R2 old paused waiting projection zero world mutation before any tick");
        QueuePlayerAction("pause"); ProcessPlayerCommand();
        GroundRequire(!_userPaused, "R2 projection check restores real user pause");
        static void RequireWaiting(PlayerWorkView view)
        {
            GroundRequire(view.Stage == PlayerWorkStage.WaitingForRoute && view.Status == PlayerWorkStatus.Waiting
                && view.ReasonCode == PlayerReasonCode.Unknown && view.Reason == "载荷保留，等待交付条件；具体原因未核实"
                && view.Progress == null, "R2 legacy cargo waiting never reuses pickup success or invents an obstacle");
        }
    }
    private void R2CheckFailedTransitionAttempt()
    {
        string path = TutorialSlotPath(3);
        string damaged = JsonSerializer.Serialize(CapturePlayer(), SaveOptions).Replace("\"Schema\":4", "\"Schema\":999", StringComparison.Ordinal);
        GroundRequire(damaged.Contains("\"Schema\":999", StringComparison.Ordinal), "R2 corrupt slot retains structured real snapshot with invalid schema");
        using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write))) writer.Write(damaged);
        long revision = ReadLifecycle().WorldTransitionRevision, epoch = _worldEpoch;
        GroundRequire(SetPlayerManagement(true, epoch), "R2 management before unconfirmed load");
        QueueLifecycle("load", 3); ProcessPlayerCommand();
        GroundRequire(ReadLifecycle().Page == "confirm-load" && ReadLifecycle().WorldTransitionRevision == revision && !_managementPaused,
            "R2 load confirmation is not a world attempt and menu departure clears management");
        QueueLifecycle("back"); ProcessPlayerCommand();
        GroundRequire(ReadLifecycle().Page == "game" && !_managementPaused, "R2 confirmation back never revives management");
        string frozen = R2AuthorityFingerprint();
        GroundRequire(SetPlayerManagement(true, epoch), "R2 management before corrupt load execution");
        QueueLifecycle("load", 3, true); ProcessPlayerCommand();
        GroundRequire(ReadLifecycle().WorldTransitionRevision == revision + 1 && _worldEpoch == epoch && _loadPending == null && !_managementPaused
            && ReadLifecycle().Page == "game" && frozen == R2AuthorityFingerprint() && File.ReadAllText(path) == damaged,
            "R2 corrupt load advances attempt revision, preserves old epoch/world/menu and original bytes");
        revision = ReadLifecycle().WorldTransitionRevision;
        // The older direct load command must expire drafts on a missing slot as well.
        QueuePlayerAction("load"); ProcessPlayerCommand();
        GroundRequire(ReadLifecycle().WorldTransitionRevision == revision + 1 && _worldEpoch == epoch && _loadPending == null
            && frozen == R2AuthorityFingerprint(), "R2 legacy direct load failure also advances attempt revision without changing epoch/world");
    }
    private void R2CheckObservationCoverage()
    {
        var ledger = _ledger.Snapshot();
        GroundRequire(_r2BuildOwners.Keys.All(_r2ReservedBuilds.Contains) && _r2BuildOwners.Values.Any(o => o.Connection),
            "R2 every real build/connection reservation observed with stable goal/task");
        GroundRequire(_productionTasks.Where(t => t.Kind == "recipe" && t.Paid).All(t => _r2PaidBatches.Contains(t.Id)), "R2 every actual paid batch observed");
        GroundRequire(_productionTasks.Where(t => t.Kind == "haul" && t.Paid).All(t => _r2LoadedHauls.Contains(t.Id)), "R2 every actual haul cargo observed");
        GroundRequire(ledger.Operations.Where(o => o.Value.Split('|')[2].StartsWith("cargo:", StringComparison.Ordinal)).All(o => _r2CargoOperations.Contains(o.Key)),
            "R2 every real cargo pickup receipt observed before delivery");
        GroundRequire(_r2LegacyBuildWaitingSeen && _r2LegacyHaulWaitingSeen, "R2 old paused build and haul waiting projections exercised");
    }
    private void R2CheckDefinitionsDetached()
    {
        var view = ReadDefinitions(); var expected = ReadDefinitions();
        GroundRequire(view.Materials.Length == 7 && view.Recipes.Length == 4 && view.Buildings.Length == 5, "R2 ordinary config identities");
        string frozen = JsonSerializer.Serialize(expected);
        view.Materials[0] = view.Materials[0] with { Name = "changed copy" };
        view.Recipes[0].Input[0] = view.Recipes[0].Input[0] with { Quantity = 999 };
        view.Recipes[0].Output[0] = view.Recipes[0].Output[0] with { Quantity = 999 };
        view.Buildings[0].Cost[0] = view.Buildings[0].Cost[0] with { Quantity = 999 };
        GroundRequire(JsonSerializer.Serialize(ReadDefinitions()) == frozen, "R2 definitions and nested amounts detached");
        GroundRequire(Definition("solar").Cost["iron"] == 4 && _bootstrapConfig.Recipes.Single(r => r.Id == "iron").Input["iron_ore"] == 2,
            "R2 original numeric config intact");
    }
    private void R2CheckMaterialFacts()
    {
        var view = ReadMaterialsDetail(); var ledger = _ledger.Snapshot();
        GroundRequire(view.WorldEpoch == _worldEpoch && view.ObservedVersion == _intentVersion && view.Locations.All(c => c.Kind != MaterialContainerKind.Consumed), "R2 material context and spent exclusion");
        foreach (var location in view.Locations)
        {
            var physical = ledger.Containers.Single(c => c.Id == location.Id);
            GroundRequire(location.Materials.Sum(a => a.Quantity) == physical.Items.Values.Sum(), "R2 location physical quantity " + location.Id);
            foreach (var item in location.Materials)
            {
                int reserved = ledger.Reservations.Where(r => r.Container == location.Id && r.Material == item.Id).Sum(r => r.Quantity);
                GroundRequire(item.Quantity == physical.Items.GetValueOrDefault(item.Id) && item.Reserved == reserved && item.Unreserved == item.Quantity - reserved && item.Unreserved >= 0,
                    "R2 reservation subset " + location.Id + "/" + item.Id);
            }
        }
        GroundRequire(view.Reservations.Length == ledger.Reservations.Length && view.Reservations.All(r => ledger.Reservations.Any(a => a.Goal == r.GoalId && a.Container == r.ContainerId && a.Material == r.MaterialId && a.Quantity == r.Quantity)),
            "R2 reservations are actual ledger rows");
        var history = ReadWorkHistory(); var current = ReadWork();
        foreach (var reservation in view.Reservations)
        {
            var owner = current != null && (current.GoalId == reservation.GoalId || current.TaskId == reservation.GoalId) ? current
                : history.FirstOrDefault(w => w.GoalId == reservation.GoalId || w.TaskId == reservation.GoalId);
            GroundRequire(owner != null && owner.TaskId == reservation.GoalId && _buildJob?.Id == reservation.GoalId
                && owner.GoalId == _development?.Id, "R2 actual reservation resolves task and development goal");
            _r2ReservedBuilds.Add(reservation.GoalId);
        }
        foreach (var material in view.Summary)
        {
            GroundRequire(material.Stored == Warehouses.Sum(c => _ledger.Count(c, material.Id)) && material.InTransit == _groundRobots.Sum(a => _ledger.Count(CargoContainer(a.Name.ToString()), material.Id))
                && material.InProcess == view.Batches.Sum(b => b.ExpectedOutput.Where(a => a.Id == material.Id).Sum(a => a.Quantity)), "R2 distinct stored/cargo/expected bases");
        }
        foreach (var batch in view.Batches)
        {
            var task = _productionTasks.Single(t => t.Id == batch.TaskId);
            var recipe = Recipe(task.Material);
            GroundRequire(batch.Paid && task.Paid && task.Stage is not ("Completed" or "Cancelled") && batch.Input.All(a => a.Quantity == recipe.Input[a.Id] * task.Quantity)
                && batch.ExpectedOutput.All(a => a.Quantity == recipe.Output[a.Id] * task.Quantity), "R2 paid pending batch only");
            _r2PaidBatches.Add(task.Id);
        }
        foreach (var operation in ledger.Operations.Where(o => o.Value.Split('|')[2].StartsWith("cargo:", StringComparison.Ordinal) && !_r2CargoOperations.Contains(o.Key)))
        {
            string[] transfer = operation.Value.Split('|');
            var cargo = view.Locations.Single(c => c.Id == transfer[2]);
            GroundRequire(cargo.Kind == MaterialContainerKind.RobotCargo && transfer[3].Split(',', StringSplitOptions.RemoveEmptyEntries).All(a =>
            {
                string[] amount = a.Split(':');
                return cargo.Materials.Any(m => m.Id == amount[0] && m.Quantity >= int.Parse(amount[1], System.Globalization.CultureInfo.InvariantCulture));
            }), "R2 each actual pickup observed with retained physical cargo " + operation.Key);
            _r2CargoOperations.Add(operation.Key);
        }
        foreach (var task in _productionTasks.Where(t => t.Kind == "haul" && t.Paid && t.Stage is not ("Completed" or "Cancelled") && !_r2LoadedHauls.Contains(t.Id)))
        {
            GroundRequire(view.Locations.Single(c => c.Id == CargoContainer(task.Robot!)).Materials.Any(m => m.Id == task.Material && m.Quantity >= task.Quantity),
                "R2 every haul paid state retains its actual cargo " + task.Id);
            _r2LoadedHauls.Add(task.Id);
        }
        string frozen = JsonSerializer.Serialize(view);
        if (view.Summary.Length > 0) view.Summary[0] = view.Summary[0] with { Stored = 999 };
        foreach (var location in view.Locations.Where(c => c.Materials.Length > 0)) location.Materials[0] = location.Materials[0] with { Quantity = 999, Reserved = 999 };
        if (view.Reservations.Length > 0) view.Reservations[0] = view.Reservations[0] with { Quantity = 999 };
        foreach (var batch in view.Batches)
        {
            batch.Input[0] = batch.Input[0] with { Quantity = 999 };
            batch.ExpectedOutput[0] = batch.ExpectedOutput[0] with { Quantity = 999 };
        }
        GroundRequire(JsonSerializer.Serialize(ReadMaterialsDetail()) == frozen, "R2 inventory nested DTO copies detached");
        _r2ReservationSeen |= ledger.Reservations.Length > 0;
        _r2TransitSeen |= view.Locations.Any(c => c.Kind == MaterialContainerKind.RobotCargo && c.Materials.Any(a => a.Quantity > 0));
        _r2BatchSeen |= view.Batches.Length > 0;
    }
    private void R2CheckPlanFacts()
    {
        var plan = ReadBuildPlan("storage", new(9, 0, 9));
        GroundRequire(plan.WorldEpoch == _worldEpoch && plan.EligibleStock != null, "R2 plan epoch and qualified physical sources");
        var stock = UsableStock();
        foreach (var cost in plan.Cost)
        {
            int eligible = plan.EligibleStock!.Where(a => a.Id == cost.Id).Sum(a => a.Quantity);
            int gap = plan.Gap.Where(a => a.Id == cost.Id).Sum(a => a.Quantity);
            GroundRequire(eligible == stock[cost.Id] && gap == Math.Max(0, cost.Quantity - stock[cost.Id]), "R2 plan uses execution source/reservation eligibility");
        }
    }
    private void R2CheckObjectsDetached()
    {
        var objects = ReadWorldObjects();
        GroundRequire(objects.Count(o => o.Kind == WorldObjectKind.Robot) == _groundRobots.Count && objects.Count(o => o.Kind == WorldObjectKind.Mine) == 2, "R2 typed stable objects");
        foreach (var obj in objects)
        {
            GroundRequire(obj.WorldEpoch == _worldEpoch && Math.Abs(obj.Position.Y - SavedGroundHeight(_liveTerrain!.Current, obj.Position.X, obj.Position.Z)) < .0001,
                "R2 current terrain Y " + obj.Id);
            var detail = ReadObjectDetail(obj.Kind, obj.Id, _worldEpoch)!;
            GroundRequire(detail != null && ReadObjectDetail(obj.Kind, obj.Id, _worldEpoch - 1) == null, "R2 object epoch gate");
            string frozen = JsonSerializer.Serialize(detail);
            if (detail.Mine != null)
            {
                GroundRequire(detail.Mine.Remaining == _mines.Single(m => m.Id == obj.Id).Remaining && detail.Mine.Products.All(p => _bootstrapConfig.Recipes.Any(r => r.Output.ContainsKey(p.Id)))
                    && detail.Mine.Products.All(p => p.Id != "kit"), "R2 real mine remaining and downstream recipe products");
                if (detail.Mine.Products.Length > 0) detail.Mine.Products[0] = detail.Mine.Products[0] with { Quantity = 999 };
            }
            if (detail.Robot != null)
            {
                var health = _health[obj.Id];
                GroundRequire(detail.Robot.Energy == health.Energy && detail.Robot.Durability == health.Durability
                    && detail.Robot.Cargo.Sum(a => a.Quantity) == _ledger.Load(CargoContainer(obj.Id)), "R2 actual robot health/cargo");
                if (detail.Robot.Cargo.Length > 0) detail.Robot.Cargo[0] = detail.Robot.Cargo[0] with { Quantity = 999 };
            }
            if (detail.Facility?.Stored.Length > 0) detail.Facility.Stored[0] = detail.Facility.Stored[0] with { Quantity = 999 };
            GroundRequire(JsonSerializer.Serialize(ReadObjectDetail(obj.Kind, obj.Id, _worldEpoch)) == frozen, "R2 object nested DTO detached " + obj.Id);
        }
        GroundRequire(ReadObjectDetail(WorldObjectKind.Facility, "missing-object", _worldEpoch) == null, "R2 missing object re-resolves to null");
    }
    private void R2BeginHold(bool userValue, int returnStep)
    {
        GroundRequire(_userPaused == userValue && SetPlayerManagement(true, _worldEpoch) && SetPlayerManagement(true, _worldEpoch), "R2 management open idempotent");
        GroundRequire(ReadPlayerState().ManagementPaused && _groundRobots.All(a => a.Paused && !a.IsPhysicsProcessing()), "R2 immediate drive/physics freeze");
        R2CheckScheduledFacts(true);
        _r2UserValue = userValue; _r2ReturnStep = returnStep; _r2HoldFrame = _groundFrame; _r2HoldTicksChecked = false;
        _r2Frozen = R2AuthorityFingerprint(); _r2Step = 20;
    }
    private void R2TestStep()
    {
        GroundRequire(_groundFrame < 300_000, "R2 frame timeout step=" + _r2Step);
        if (!_groundReady || !ReadPlayerState().Ready || !_groundRobots.All(a => a.IsOnFloor())) return;
        if (_r2LastProgressStep != _r2Step || _groundFrame - _r2LastProgressFrame >= 600)
        {
            GD.Print($"R2_AUTHORITY_PROGRESS frame={_groundFrame} step={_r2Step} time={_playerTime:0.0} goal={_development?.Stage} task={Production?.Stage} build={_buildJob?.Stage} reason={ReadWork()?.Reason}");
            _r2LastProgressStep = _r2Step; _r2LastProgressFrame = _groundFrame;
        }
        if (_development?.Stage == "Blocked" && _r2Step is not (20 or 21 or 22 or 23)) throw new InvalidOperationException("R2 natural route blocked: " + ReadWork()?.Reason);
        if (!_entryOpen && _r2Step != 20) R2CheckScheduledFacts();
        switch (_r2Step)
        {
            case 0:
                GroundRequire(_entryOpen && _initialPlayer != null, "R2 normal home with real initial snapshot");
                _r2InitialFrozen = JsonSerializer.Serialize(_initialPlayer, SaveOptions);
                QueueLifecycle("newgame", 1); _r2Step = 1; break;
            case 1:
                if (_entryOpen || _loadPending != null) break;
                GroundRequire(_worldEpoch == 1 && !_userPaused && _baseFacilities.Count == 1, "R2 normal lifecycle new world");
                R2CheckDefinitionsDetached(); R2CheckObjectsDetached(); R2CheckFailedTransitionAttempt(); R2CheckScheduledFacts(true);
                string before = R2AuthorityFingerprint();
                _ = ReadWork(); _ = ReadWorkHistory(); _ = ReadBuildPlan("solar", new(-3, 0, -14));
                GroundRequire(before == R2AuthorityFingerprint(), "R2 read/preview zero world side effects");
                R2BeginHold(false, 2); break;
            case 2: QueuePlayerAction("pause"); _r2Step = 3; break;
            case 3:
                GroundRequire(_userPaused, "R2 genuine user pause"); R2BeginHold(true, 4); break;
            case 4: QueuePlayerAction("pause"); _r2Step = 5; break;
            case 5:
                GroundRequire(!_userPaused, "R2 resume user pause normally");
                var plan = ReadBuildPlan("solar", new(-3, 0, -14));
                GroundRequire(plan.CanSubmit, "R2 first construction plan");
                QueueBuild("solar", plan.Center, 0, plan.ObservedVersion);
                QueueBuild("solar", plan.Center, 0, plan.ObservedVersion); _r2Step = 6; break;
            case 6:
                GroundRequire(_developmentSequence == 1 && ReadWork()?.Active == true && _r2InitialFrozen == JsonSerializer.Serialize(_initialPlayer, SaveOptions), "R2 double submit one goal; initial deep snapshot isolated");
                _r2Goal = ReadWork()!.GoalId;
                var work = ReadWork()!; string workers = string.Join("|", work.WorkerIds);
                if (work.WorkerIds.Length > 0) work.WorkerIds[0] = "changed-copy";
                GroundRequire(string.Join("|", ReadWork()!.WorkerIds) == workers, "R2 worker IDs detached");
                R2BeginHold(false, 7); break;
            case 7: QueuePlayerAction("pause"); _r2Step = 8; break;
            case 8:
                GroundRequire(_userPaused, "R2 active work genuine user pause"); R2BeginHold(true, 9); break;
            case 9:
                var current = ReadWork()!; string frozenWork = R2AuthorityFingerprint();
                QueueCancelWork(current.GoalId, current.ObservedVersion - 1, _worldEpoch); ProcessPlayerCommand();
                GroundRequire(frozenWork == R2AuthorityFingerprint() && ReadWork()?.GoalId == _r2Goal, "R2 stale cancel version rejected");
                QueueCancelWork("missing-goal", current.ObservedVersion, _worldEpoch); ProcessPlayerCommand();
                QueueCancelWork(current.GoalId, current.ObservedVersion, _worldEpoch - 1); ProcessPlayerCommand();
                GroundRequire(frozenWork == R2AuthorityFingerprint(), "R2 wrong goal and epoch cancel rejected");
                QueuePlayerAction("pause"); _r2Step = 10; break;
            case 10:
                if (_development?.Stage != "Completed") break;
                R2CheckScheduledFacts(true);
                _r2Solar = _baseFacilities.Single(f => f.Built && f.Type == "solar").Id;
                TutorialTestBuild("charger", new(-3, 0, -3)); _r2Step = 11; break;
            case 11:
                if (_development?.Stage != "Completed") break;
                R2CheckScheduledFacts(true);
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "charger").Id, _r2Solar); _r2Step = 12; break;
            case 12:
                if (_development?.Stage != "Completed") break;
                TutorialTestBuild("repair", new(8, 0, -14)); _r2Step = 13; break;
            case 13:
                if (_development?.Stage != "Completed") break;
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "repair").Id, _r2Solar); _r2Step = 14; break;
            case 14:
                if (_development?.Stage != "Completed") break;
                TutorialTestBuild("processor", new(-14, 0, -14)); _r2Step = 15; break;
            case 15:
                if (_development?.Stage != "Completed") break;
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "processor").Id, _r2Solar); _r2Step = 16; break;
            case 16:
                if (_development?.Stage != "Completed") break;
                GroundRequire(SupportSetConnected, "R2 real paid support set");
                TutorialTestBuild("storage", new(9, 0, 9)); _r2Step = 17; break;
            case 17:
                if (Production is not { Kind: "mine", Stage: "Working" } mine || mine.Work < .2) break;
                R2CheckScheduledFacts(true);
                GroundRequire(ReadWork()?.ProgressBasis == PlayerProgressBasis.MiningWork, "R2 mining uses actual six-second denominator");
                _r2Task = mine.Id; _r2MineReceipts = _ledger.Snapshot().Mining!.Length; _r2Goal = _development!.Id;
                R2BeginHold(false, 18); break;
            case 18:
                GroundRequire(SetPlayerManagement(true, _worldEpoch), "R2 management opens for cancel confirmation");
                var cancellation = ReadWork()!;
                GroundRequire(SetPlayerManagement(false, _worldEpoch), "R2 confirmation closes management synchronously");
                cancellation = ReadWork()!;
                QueueCancelWork(cancellation.GoalId, cancellation.ObservedVersion, cancellation.WorldEpoch);
                QueueCancelWork(cancellation.GoalId, cancellation.ObservedVersion, cancellation.WorldEpoch); _r2Step = 19; break;
            case 19:
                GroundRequire(_development?.Stage == "Cancelled" && _ledger.Snapshot().Mining!.Length == _r2MineReceipts && ReadWork() == null
                    && ReadWorkHistory().Any(w => w.GoalId == _r2Goal && w.TaskId == _r2Task && w.Status == PlayerWorkStatus.Cancelled), "R2 cancel uncommitted mine, retained real history and double cancel");
                ValidatePlayerSave(CapturePlayer());
                GroundRequire(ReadWorkHistory().Select(w => (w.GoalId, w.TaskId)).Distinct().Count() == ReadWorkHistory().Length, "R2 history stable identity deduplication");
                QueuePlayerAction("retry"); _r2Step = 30; break;
            case 20:
                if (!_r2HoldTicksChecked)
                {
                    // Exercise each direct tick once at hold entry, in addition to the outer physics gate.
                    TickBootstrap(.5); TickBaseBuild(.5); TickBaseRoutes(.5); TickBaseServices(.5); TickDevelopment(.5); TickLevelJob(.5);
                    if (Production is {} heldTask) TickProduction(heldTask, .5);
                    GroundRequire(_userPaused == _r2UserValue && _r2Frozen == R2AuthorityFingerprint(), "R2 management freezes time/position/health/ledger/work/wait timers");
                    GroundRequire(!OrderBase(_groundRobots[0], _groundRobots[0].GlobalPosition + Vector3.Right) && _r2Frozen == R2AuthorityFingerprint(), "R2 direct drive rejected during management");
                    _r2HoldTicksChecked = true;
                }
                if (_groundFrame - _r2HoldFrame < 12) break;
                GroundRequire(_userPaused == _r2UserValue && _r2Frozen == R2AuthorityFingerprint(), "R2 full hold leaves authority unchanged");
                R2CheckScheduledFacts(true);
                string[] actions = ["tutorial:camera", "tutorial:select-robot", "connection", "build:solar", "level", "cancel", "retry", "restock", "recover"];
                foreach (string action in actions)
                {
                    QueuePlayer(new(action, new(-3, 0, -14), _intentVersion, "Robot_Zhulei_1", Source: _r2Solar));
                    ProcessPlayerCommand();
                    GroundRequire(_playerNotice.StartsWith("操作未执行：管理面板", StringComparison.Ordinal) && _r2Frozen == R2AuthorityFingerprint(), "R2 direct world intent rejected " + action);
                }
                GroundRequire(!SetPlayerManagement(false, _worldEpoch - 1) && _managementPaused, "R2 stale management close rejected");
                GroundRequire(SetPlayerManagement(false, _worldEpoch) && SetPlayerManagement(false, _worldEpoch) && _userPaused == _r2UserValue && !_managementPaused,
                    "R2 management close idempotent, user pause unchanged");
                _r2PauseRound++; _r2Step = _r2ReturnStep; break;
            case 30:
                if (Production is not { Kind: "haul", Stage: "Delivering" }) break;
                R2CheckScheduledFacts(true);
                GroundRequire(ReadWork() is { Progress: null, ProgressBasis: PlayerProgressBasis.None }, "R2 transport never fabricates percentage");
                R2BeginHold(false, 31); break;
            case 31:
                if (Production is not { Kind: "recipe", Stage: "Working", Paid: true } paid || paid.Work < .2) break;
                R2CheckScheduledFacts(true);
                GroundRequire(ReadWork()?.ProgressBasis == PlayerProgressBasis.RecipeWork, "R2 recipe actual batch denominator");
                R2BeginHold(false, 32); break;
            case 32:
                if (_development?.Stage != "Completed") break;
                R2CheckScheduledFacts(true); R2CheckObservationCoverage();
                GroundRequire(_r2ReservationSeen && _r2TransitSeen && _r2BatchSeen && _r2PauseRound >= 7, "R2 actual reservation/transit/paid batch and pause evidence");
                R2CheckObjectsDetached(); ValidatePlayerSave(CapturePlayer());
                GroundRequire(SavePlayer(), "R2 private real save");
                _r2Epoch = _worldEpoch; _r2TransitionRevision = ReadLifecycle().WorldTransitionRevision;
                GroundRequire(SetPlayerManagement(true, _worldEpoch), "R2 management before load");
                QueueLifecycle("load", 1, true); _r2Step = 33; break;
            case 33:
                if (_loadPending != null) break;
                GroundRequire(!_managementPaused && _worldEpoch == _r2Epoch + 1 && ReadLifecycle().WorldTransitionRevision == _r2TransitionRevision + 1
                    && _r2InitialFrozen == JsonSerializer.Serialize(_initialPlayer, SaveOptions), "R2 load clears temporary pause, advances epoch/revision once, preserves deep initial snapshot");
                string fingerprint = R2AuthorityFingerprint();
                QueuePlayer(new("tutorial:camera", Vector3.Zero, -1, null, WorldEpoch: _r2Epoch)); ProcessPlayerCommand();
                GroundRequire(fingerprint == R2AuthorityFingerprint(), "R2 queued old-epoch command rejected at execution");
                // Capture another queued intent, then cross the real home boundary before dispatch.
                GroundRequire(SetPlayerManagement(true, _worldEpoch), "R2 management before captured home-bound intent");
                QueueTutorialAction("camera");
                ProcessLifecycle(new("lifecycle:home", Vector3.Zero, -1, null));
                ProcessLifecycle(new("lifecycle:leave-discard", Vector3.Zero, -1, null));
                string homeFingerprint = R2AuthorityFingerprint();
                ProcessPlayerCommand();
                GroundRequire(homeFingerprint == R2AuthorityFingerprint() && !_managementPaused && _entryOpen, "R2 queued intent epoch checked after actual home switch");
                QueueLifecycle("load", 1, true); _r2Step = 38; break;
            case 38:
                if (_loadPending != null || _entryOpen) break;
                GroundRequire(ReadObjectDetail(WorldObjectKind.Facility, _r2Solar, _r2Epoch) == null && !SetPlayerManagement(true, _r2Epoch), "R2 old-epoch object and management rejected after load");
                _r2Step = 34; break;
            case 34:
                if (Daylight) break;
                foreach (var facility in _baseFacilities.Where(f => ServiceConnected(f)))
                {
                    var detail = ReadObjectDetail(WorldObjectKind.Facility, facility.Id, _worldEpoch)!.Facility!;
                    GroundRequire(detail.Built && detail.Connected && !detail.Powered && detail.SourceId == facility.Source && detail.OperatingState.Contains("等待日照", StringComparison.Ordinal),
                        "R2 natural night keeps connection " + facility.Id);
                    GroundRequire(ReadConnectionOptions(facility.Id).All(o => !o.CanSubmit), "R2 connected night does not offer paid reconnect");
                }
                GroundRequire(SetPlayerManagement(true, _worldEpoch), "R2 management before home");
                QueueLifecycle("home"); _r2Step = 35; break;
            case 35:
                GroundRequire(!_managementPaused && ReadLifecycle().Page == "leave", "R2 home initiation clears management");
                QueueLifecycle("leave-discard"); _r2Step = 36; break;
            case 36:
                GroundRequire(_entryOpen && !_managementPaused, "R2 home has no temporary management state");
                _r2Epoch = _worldEpoch; _r2TransitionRevision = ReadLifecycle().WorldTransitionRevision;
                _r2OverwriteBytes = File.ReadAllText(TutorialSlotPath(1));
                QueueLifecycle("newgame", 1); _r2Step = 39; break;
            case 39:
                GroundRequire(ReadLifecycle().Page == "confirm-newgame" && _entryOpen && _worldEpoch == _r2Epoch
                    && ReadLifecycle().WorldTransitionRevision == _r2TransitionRevision && File.ReadAllText(TutorialSlotPath(1)) == _r2OverwriteBytes,
                    "R2 overwrite confirmation is not a world attempt and keeps original slot bytes");
                QueueLifecycle("back"); _r2Step = 40; break;
            case 40:
                GroundRequire(ReadLifecycle().WorldTransitionRevision == _r2TransitionRevision && _worldEpoch == _r2Epoch,
                    "R2 dismissed overwrite keeps temporary world context revision");
                QueueLifecycle("newgame", 2); _r2Step = 37; break;
            case 37:
                if (_entryOpen || _loadPending != null) break;
                GroundRequire(!_managementPaused && _development == null && _productionTasks.Count == 0 && _baseFacilities.Count == 1
                    && _ledger.Totals()["kit"] == 8 && _mines.All(m => m.Remaining == 96), "R2 newgame resets world without persisting management");
                GroundRequire(ReadLifecycle().WorldTransitionRevision == _r2TransitionRevision + 1 && _worldEpoch == _r2Epoch + 1,
                    "R2 actual newgame executes one temporary attempt and advances world epoch");
                GD.Print("R2_AUTHORITY PASS detached definitions objects inventory reservations batch_expected progress management_user_false_true timers world_intent epoch stale_cancel double_submit stable_build_id reservation_task_goal legacy_paused_waiting failed_load_revision overwrite_confirmation load_home_newgame natural_night schema_hash");
                GetTree().Quit(); break;
        }
    }
}
