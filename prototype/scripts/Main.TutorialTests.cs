#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
namespace Yudian;

// Engineering driver of the normal authoritative API, not a player experience or UI test.
// The full and recovery routes never grant health, materials, time, or free connections.
public partial class Main
{
    private int _tutorialTestStep, _tutorialTestFrame;
    private double _tutorialTestTime;
    private string? _tutorialTestInitialSolar, _tutorialTestFirstGoal, _tutorialTestSecondGoal, _tutorialTestStorage, _tutorialTestSolar;
    private string? _tutorialTestRecoveryTarget, _tutorialTestRecoverySolar;
    private string _tutorialTestFingerprint = "";
    private byte[] _tutorialTestFile = [];
    private LifecycleRollback? _tutorialTestLifecycle;
    private string TutorialTestPhase => System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_TEST_PHASE") ?? "full-storage";
    private string TutorialNaturalDirection => System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_NATURAL_DIRECTION") ?? "storage";
    private bool TutorialGoalComplete => _development?.Stage == "Completed";
    private bool _tutorialTestCancelledLineage;
    private string? _tutorialTestCancelledGoal;
    private int _tutorialTestMiningAtCancel, _tutorialTestRecipesAtCancel;
    private Vector3 _tutorialTestStoragePosition = new(9, 0, 9);
    private Vector3 _tutorialTestChargerPosition = new(18, 0, 9);
    private Vector3 TutorialTestRecoveryPosition => new(-12, 0, 2);
    private Vector3 FindTutorialChargerSite()
    {
        foreach (var position in new[] { new Vector3(18, 0, 9), new(21, 0, 7), new(20, 0, 7), new(22, 0, 5), new(19, 0, 8), new(20, 0, 6), new(22, 0, 4) })
        {
            var plan = ReadBuildPlan("charger", position);
            GD.Print($"TUTORIAL_CHARGER_SITE x={position.X} z={position.Z} candidate={plan.CandidateLegal} submit={plan.CanSubmit} reason={plan.SubmitReason}");
            if (plan.CanSubmit && plan.SourceId == TutorialExpansionSolarId && plan.SupplyId == TutorialStorageId) return position;
        }
        throw new InvalidOperationException("工程路线候选未找到合法第二桩：" + string.Join(";", _groundRobots.Select(a => a.Name + "@" + a.GlobalPosition)));
    }
    private void TutorialTestBuild(string type, Vector3 position)
    {
        var plan = ReadBuildPlan(type, position);
        GroundRequire(plan.CandidateLegal && plan.CanSubmit, "tutorial plan " + type + ": " + plan.CandidateReason + " / " + plan.SubmitReason);
        QueueBuild(type, position, 0, plan.ObservedVersion);
    }
    private void TutorialTestWire(string facilityId, string sourceId)
    {
        var option = ReadConnectionOptions(facilityId).Single(o => o.SourceId == sourceId);
        GroundRequire(option.CanSubmit, "tutorial wire " + option.SubmitReason);
        QueueConnection(facilityId, sourceId, option.ObservedVersion);
    }
    private string TutorialWorldFingerprint()
    {
        var saved = CapturePlayer();
        return JsonSerializer.Serialize(new { saved.Terrain, saved.Time, saved.Paused, saved.Sequence, saved.Bootstrap, saved.Tutorial }, SaveOptions);
    }
    private void TutorialTestStep()
    {
        if (!_groundReady || (!ReadPlayerState().Ready && _tutorialTestStep != 125) || !_groundRobots.All(a => a.IsOnFloor())) return;
        GroundRequire(_groundFrame < 1_100_000, "tutorial frame timeout step=" + _tutorialTestStep);
        if (_groundFrame % 2400 == 0) GD.Print($"TUTORIAL_PROGRESS phase={TutorialTestPhase} step={_tutorialTestStep} time={_playerTime:0.0} stage={ReadTutorial().StageId} goal={_development?.Stage} production={Production?.Kind}/{Production?.Material}/{Production?.Stage}/{Production?.Reason} build={_buildJob?.Stage}/{_buildJob?.Reason}");
        if (_development?.Stage == "Blocked" && _tutorialTestStep < 100) throw new InvalidOperationException("tutorial route blocked: " + _development.Reason + " / " + _buildJob?.Reason);
        if (TutorialTestPhase == "boundaries" && _tutorialTestStep >= 100) { TutorialBoundaryStep(); return; }
        if (TutorialTestPhase is "recovery" or "recovery-retry" or "escape-probe" && _tutorialTestStep >= 200) { TutorialRecoveryStep(); return; }
        bool solarFirst = TutorialTestPhase == "full-solar" || TutorialTestPhase == "resume" && TutorialNaturalDirection == "solar";
        switch (_tutorialTestStep)
        {
            case 0:
                GroundRequire(TutorialEnabled && _entryOpen && _initialPlayer != null && _bootstrapConfig.Version == "d12-firstplay-1", "normal tutorial home + independent initial snapshot");
                GroundRequire(_health.All(h => h.Value.Energy == 100 && h.Value.Durability == 100) && _ledger.Totals()["kit"] == 8, "finite ordinary new-world health and kits");
                if (TutorialTestPhase is "resume" or "recovery" or "recovery-retry" or "escape-probe")
                { QueueLifecycle("load", 1); _tutorialTestStep = TutorialTestPhase == "recovery" ? 200 : TutorialTestPhase == "escape-probe" ? 208 : TutorialTestPhase == "recovery-retry" ? 205 : 90; break; }
                QueueLifecycle("newgame", 1); _tutorialTestStep = 1; break;
            case 1:
                if (_loadPending != null || _entryOpen) break;
                GroundRequire(_playerTime < .1 && _baseFacilities.Count == 1 && _worldEpoch == 1, "newgame restored clean world");
                QueueTutorialAction("camera"); _tutorialTestStep = 2; break;
            case 2: QueueTutorialAction("select-robot", "Robot_Zhulei_1"); _tutorialTestStep = 3; break;
            case 3:
                GroundRequire(ReadTutorial().StageId == "solar", "first next missing solar");
                GroundRequire(!ReadBuildPlan("processor", new(-14, 0, -14)).CandidateLegal, "new powered facility lacks coverage before solar");
                TutorialTestBuild("solar", new(-3, 0, -14)); _tutorialTestStep = 4; break;
            case 4:
                if (!TutorialGoalComplete) break;
                _tutorialTestInitialSolar = _baseFacilities.Single(f => f.Built && f.Type == "solar").Id;
                GroundRequire(ReadTutorial().StageId == "charger", "completed solar does not claim support set done");
                TutorialTestBuild("charger", new(-3, 0, -3)); _tutorialTestStep = 5; break;
            case 5:
                if (_buildJob is { Type: "charger", Active: true } assignedBuild && _ledger.Count(assignedBuild.Facility, "parts") > 0)
                {
                    string planBefore = TutorialWorldFingerprint();
                    var future = ReadBuildPlan("solar", new(13, 0, -1));
                    var free = future.Stock.Single(m => m.Id == "parts");
                    GroundRequire(free.Available <= UsableStock()["parts"] - _ledger.Count(assignedBuild.Facility, "parts"), "future draft excludes current site's assigned parts");
                    GroundRequire(planBefore == TutorialWorldFingerprint(), "assigned material preview stays readonly");
                }
                if (!TutorialGoalComplete) break;
                GroundRequire(ReadTutorial().StageId == "charger-wire", "built charger asks real wire");
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "charger").Id, _tutorialTestInitialSolar!); _tutorialTestStep = 6; break;
            case 6:
                if (!TutorialGoalComplete) break;
                GroundRequire(ReadTutorial().StageId == "repair", "wired charger advances to repair");
                TutorialTestBuild("repair", new(8, 0, -14)); _tutorialTestStep = 7; break;
            case 7:
                if (!TutorialGoalComplete) break;
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "repair").Id, _tutorialTestInitialSolar!); _tutorialTestStep = 8; break;
            case 8:
                if (!TutorialGoalComplete) break;
                GroundRequire(ReadTutorial().StageId == "processor", "wired repair advances to processor");
                TutorialTestBuild("processor", new(-14, 0, -14)); _tutorialTestStep = 9; break;
            case 9:
                if (!TutorialGoalComplete) break;
                TutorialTestWire(_baseFacilities.Single(f => f.Built && f.Type == "processor").Id, _tutorialTestInitialSolar!); _tutorialTestStep = 10; break;
            case 10:
                if (!TutorialGoalComplete) break;
                GroundRequire(SupportSetConnected && ReadTutorial().StageId == "first-direction" && !ReadTutorial().Completed, "real support connections precede choices");
                GroundRequire(new[] { "iron", "copper", "parts", "cable" }.All(m => _ledger.Count("lander", m) == 0), "first set leaves genuine production gaps");
                if (TutorialTestPhase == "boundaries") { QueuePlayerAction("pause"); _tutorialTestStep = 100; break; }
                GroundRequire(SavePlayerTo(TutorialSlotPath(2)), "ordinary connected first-set checkpoint for boundary reuse");
                GroundRequire(ReadBuildPlan("storage", new(9, 0, 9)).CanSubmit && ReadBuildPlan("solar", new(13, 0, -1)).CanSubmit, "both first directions actually feasible");
                TutorialTestBuild("solar", new(13, 0, -1)); _tutorialTestStep = 11; break;
            case 11:
                if (_development?.Active != true) break;
                string before = TutorialWorldFingerprint();
                var draft = ReadBuildPlan("storage", new(9, 0, 9));
                _ = ReadConnectionOptions(); _ = ReadTutorial(); _ = ReadEconomy(); _ = ReadLifecycle();
                GroundRequire(draft.CandidateLegal && !draft.CanSubmit && draft.IsDraft && before == TutorialWorldFingerprint(), "active draft is legal but cannot reserve or mutate");
                _tutorialTestStep = 12; break;
            case 12:
                if (!solarFirst && !_tutorialTestCancelledLineage && _development?.Stage == "Supplying"
                    && DevelopmentDemand("solar").All(x => UsableStock()[x.Key] >= x.Value))
                {
                    _tutorialTestCancelledGoal = _development.Id; _tutorialTestMiningAtCancel = _ledger.Snapshot().Mining!.Length; _tutorialTestRecipesAtCancel = _ledger.Snapshot().Recipes!.Length;
                    QueuePlayerAction("cancel"); _tutorialTestStep = 70; break;
                }
                if (!TutorialGoalComplete) break;
                _tutorialTestFirstGoal = ReadTutorial().FirstGoalId;
                GroundRequire(TutorialProductionProven(_tutorialTestFirstGoal) && ReadTutorial().StageId == "second-direction", "first choice has mining and recipe receipts");
                if (_tutorialTestCancelledLineage)
                {
                    GroundRequire(_tutorialTestFirstGoal != _tutorialTestCancelledGoal && _ledger.Snapshot().Mining!.Length == _tutorialTestMiningAtCancel && _ledger.Snapshot().Recipes!.Length == _tutorialTestRecipesAtCancel,
                        "reauthorized first direction uses retained stock with original learning receipts");
                    GD.Print($"TUTORIAL_CANCEL_LINEAGE PASS cancelled={_tutorialTestCancelledGoal} completed={_tutorialTestFirstGoal} phase=first-direction mines={_tutorialTestMiningAtCancel} recipes={_tutorialTestRecipesAtCancel}");
                }
                QueuePlayerAction("pause"); _tutorialTestStep = 80; break;
            case 13:
                if (!TutorialGoalComplete) break;
                _tutorialTestSecondGoal = ReadTutorial().SecondGoalId; _tutorialTestStorage = ReadTutorial().StorageId; _tutorialTestSolar = ReadTutorial().ExpansionSolarId;
                GroundRequire(_tutorialTestStorage != null && _tutorialTestSolar != null && _tutorialTestSecondGoal != _tutorialTestFirstGoal && ReadTutorial().StageId == "second-charger", "both stable directions complete");
                _tutorialTestChargerPosition = FindTutorialChargerSite();
                var chargerPlan = ReadBuildPlan("charger", _tutorialTestChargerPosition);
                GroundRequire(chargerPlan.SupplyId == _tutorialTestStorage && chargerPlan.SourceId == _tutorialTestSolar, "second charger uses new warehouse and new solar coverage");
                TutorialTestBuild("charger", _tutorialTestChargerPosition); _tutorialTestStep = 14; break;
            case 14:
                if (!TutorialGoalComplete) break;
                var tutorial = ReadTutorial();
                GroundRequire(tutorial.SecondChargerId != null && TutorialStorageSupplied && tutorial.StageId == "second-charger-wire" && !tutorial.Completed, "actual new warehouse take precedes conclusion");
                TutorialTestWire(tutorial.SecondChargerId!, tutorial.ExpansionSolarId!); _tutorialTestStep = 15; break;
            case 15:
                if (!TutorialGoalComplete) break;
                GroundRequire(ReadTutorial().Completed && ReadTutorial().ShowConclusion && _ledger.Count("spent", "kit") == 8 && _ledger.Totals()["kit"] == 8, "real end with eight finite kits, no grant");
                ValidatePlayerSave(CapturePlayer()); QueueTutorialAction("continue"); _tutorialTestStep = 16; break;
            case 16:
                GroundRequire(ReadTutorial().Completed && !ReadTutorial().ShowConclusion && ReadTutorial().StageId == "freeplay", "continue same world after conclusion");
                GroundRequire(SavePlayerTo(TutorialSlotPath(3)), "real completed tutorial checkpoint before extra candidate probes");
                var originalSolar = ReadBootstrap().Facilities.Single(f => f.Id == _tutorial.InitialSolarId);
                BuildPlanView? freePlan = null;
                for (int ring = 0; ring < 3 && freePlan == null; ring++)
                    for (int bearing = 0; bearing < 32; bearing++)
                    {
                        float angle = bearing * Mathf.Tau / 32;
                        var site = originalSolar.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (8 + ring * 1.5f);
                        var plan = ReadBuildPlan("charger", site);
                        GD.Print("TUTORIAL_FREEPLAY_PLAN " + JsonSerializer.Serialize(new { plan.Center, plan.CandidateLegal, plan.CanSubmit, plan.CandidateReason, plan.SubmitReason, plan.Gap, plan.PowerSources }, SaveOptions));
                        if (plan.CandidateLegal && plan.PowerSources.Any(p => p.InRange && p.SourceId == originalSolar.Id) && plan.PowerSources.All(p => p.SourceId != TutorialExpansionSolarId || !p.InRange)) { freePlan = plan; break; }
                    }
                GroundRequire(freePlan != null && !freePlan.CanSubmit && freePlan.Gap.Any(m => m.Id == "kit"), "freeplay typed-array scan finds legal original coverage, no ninth kit");
                if (!_userPaused) QueuePlayerAction("pause"); _tutorialTestStep = 17; break;
            case 17:
                if (!_userPaused) break;
                GroundRequire(SavePlayer(), "full route saved"); ValidatePlayerSave(CapturePlayer());
                GD.Print($"TUTORIAL_FULL PASS direction={(solarFirst ? "solar" : "storage")} natural_health=true kits=8 mines={_ledger.Snapshot().Mining!.Length} recipes={_ledger.Snapshot().Recipes!.Length} time={_playerTime:0.0} first={_tutorialTestFirstGoal} second={_tutorialTestSecondGoal} storage={ReadTutorial().StorageId} solar={ReadTutorial().ExpansionSolarId} charger={ReadTutorial().SecondChargerId}");
                GetTree().Quit(); break;
            case 95:
                if (!_userPaused) break;
                GroundRequire(SavePlayer() && ReadPlayerSaveFile(PlayerSavePath).Schema == 4, "schema2 save migrates to independent schema4 slot");
                GD.Print("TUTORIAL_SCHEMA2_MIGRATION PASS raw_config=d11-bootstrap-1 legacy_practice=true"); GetTree().Quit(); break;
            case 70:
                if (_development?.Stage != "Cancelled") break;
                _tutorialTestCancelledLineage = true; _tutorialTestStoragePosition = new(8, 0, 9);
                TutorialTestBuild("storage", _tutorialTestStoragePosition); _tutorialTestStep = 12; break;
            case 80:
                if (!_userPaused) break;
                GroundRequire(SavePlayer(), "first direction cross-process checkpoint");
                GD.Print($"TUTORIAL_PREPARED PASS direction={(solarFirst ? "solar" : "storage")} first={ReadTutorial().FirstGoalId} storage={ReadTutorial().StorageId} time={_playerTime:0.0}");
                GetTree().Quit(); break;
            case 90:
                if (_loadPending != null || _entryOpen) break;
                if (ReadTutorial().Completed)
                {
                    GroundRequire(ReadTutorial().StorageId != null && ReadTutorial().ExpansionSolarId != null, "completed tutorial restores stable capabilities");
                    _tutorialTestFirstGoal = ReadTutorial().FirstGoalId; _tutorialTestSecondGoal = ReadTutorial().SecondGoalId;
                    _tutorialTestStep = 16; break;
                }
                if (ReadTutorial().LegacyPractice)
                {
                    // Schema2 private fixture produced by the ordinary bootstrap runner.
                    GroundRequire(_bootstrapConfig.Version == "d11-bootstrap-1" && _mines.All(m => m.Remaining == 96), "schema2 migration preserves raw legacy config and finite mines");
                    if (!_userPaused) QueuePlayerAction("pause"); _tutorialTestStep = 95; break;
                }
                GroundRequire(_userPaused && ReadTutorial().FirstGoalId != null && ReadTutorial().StageId == "second-direction", "choice and pause survive process restart");
                _tutorialTestFirstGoal = ReadTutorial().FirstGoalId;
                if (ReadTutorial().StorageId is {} savedStorage) _tutorialTestStoragePosition = LoadVector(_baseFacilities.Single(f => f.Id == savedStorage).Position);
                QueuePlayerAction("pause"); _tutorialTestStep = 91; break;
            case 91:
                if (_userPaused) break;
                GroundRequire(ReadTutorial().FirstGoalId == _tutorialTestFirstGoal, "unpause keeps first stable id");
                TutorialTestBuild(solarFirst ? "storage" : "solar", solarFirst ? new(9, 0, 9) : new(13, 0, -1)); _tutorialTestStep = 13; break;
        }
    }
    private void TutorialBoundaryStep()
    {
        switch (_tutorialTestStep)
        {
            case 100:
                if (!_userPaused) break;
                _tutorialTestTime = _playerTime;
                double connectedTime = _playerTime; _playerTime = 450; // Explicit boundary probe, not part of the natural experience route.
                try { GroundRequire(ReadTutorial().Milestones.Take(4).All(m => m.Achieved) && ReadBootstrap().Facilities.Where(f => f.Source != null).All(f => !f.Powered && f.ServiceConnected), "night keeps real service connection milestones"); }
                finally { _playerTime = connectedTime; }
                _tutorialTestFingerprint = TutorialWorldFingerprint();
                QueueLifecycle("pause"); _tutorialTestStep = 101; break;
            case 101:
                GroundRequire(_menuPaused && _userPaused && _playerTime == _tutorialTestTime, "menu pause independent of user pause");
                QueueLifecycle("settings"); _tutorialTestStep = 102; break;
            case 102:
                GroundRequire(ReadLifecycle().Page == "settings", "settings menu stack"); QueueSettings(1.5, 1.7, "brief"); _tutorialTestStep = 103; break;
            case 103:
                GroundRequire(ReadSettings().UiScale == 1.5 && ReadSettings().CameraSpeed == 1.7 && File.Exists(PlayerSettingsPath), "real settings persisted in private root");
                QueueSettings(1.6, double.NaN, "fake"); _tutorialTestStep = 104; break;
            case 104:
                GroundRequire(ReadSettings().UiScale == 1.5 && _userPaused && _menuPaused, "invalid settings reject without unpause");
                QueueLifecycle("back"); _tutorialTestStep = 105; break;
            case 105:
                GroundRequire(ReadLifecycle().Page == "pause" && _userPaused && _menuPaused, "settings back retains both pauses");
                QueueLifecycle("resume"); _tutorialTestStep = 106; break;
            case 106:
                GroundRequire(!_menuPaused && _userPaused && _playerTime == _tutorialTestTime, "resume does not release user pause");
                GroundRequire(SavePlayer(), "boundary good save"); _tutorialTestFile = File.ReadAllBytes(PlayerSavePath);
                foreach (string collection in new[] { "Facilities", "Services" })
                {
                    var damaged = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(_tutorialTestFile))!;
                    if (collection == "Facilities") damaged["Bootstrap"]![collection]![0] = null;
                    else damaged["Bootstrap"]![collection] = new JsonArray((JsonNode?)null);
                    File.WriteAllText(TutorialSlotPath(2), damaged.ToJsonString());
                    RefreshPlayerSlots();
                    GroundRequire(!ReadLifecycle().Slots.Single(s => s.Slot == 2).Loadable && ReadLifecycle().Slots.Single(s => s.Slot == 1).Loadable,
                        "null " + collection + " rejected as bad slot without affecting valid slot");
                }
                var nullGoal = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(_tutorialTestFile))!;
                nullGoal["Tutorial"]!["Goals"] = new JsonArray(JsonSerializer.SerializeToNode(new TutorialGoalRecord { GoalId = null!, Type = "solar", Center = [13, 0, -1] }, SaveOptions));
                File.WriteAllText(TutorialSlotPath(2), nullGoal.ToJsonString()); RefreshPlayerSlots();
                GroundRequire(!ReadLifecycle().Slots.Single(s => s.Slot == 2).Loadable, "null tutorial goal ID rejects without home crash");
                var unknownVersion = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(_tutorialTestFile))!; unknownVersion["Schema"] = 99;
                File.WriteAllText(TutorialSlotPath(3), unknownVersion.ToJsonString()); RefreshPlayerSlots();
                GroundRequire(!ReadLifecycle().Slots.Single(s => s.Slot == 3).Loadable, "unsupported schema bad slot remains visible");
                _tutorialTestFingerprint = TutorialWorldFingerprint();
                QueueLifecycle("pause"); _tutorialTestStep = 107; break;
            case 107:
                QueueLifecycle("slots"); _tutorialTestStep = 108; break;
            case 108:
                GroundRequire(ReadLifecycle().Page == "slots", "slots page modal");
                File.WriteAllText(PlayerSavePath, "{\"Schema\":4,\"Schema\":4}");
                QueueLifecycle("load", 1, true); _tutorialTestStep = 109; break;
            case 109:
                GroundRequire(_loadPending == null && ReadLifecycle().Page == "slots" && _activeSlot == 1 && _userPaused && _tutorialTestFingerprint == TutorialWorldFingerprint(), "bad duplicate save preserves world pause menu slot");
                File.WriteAllBytes(PlayerSavePath, _tutorialTestFile);
                _playerTestInjectLoadFault = true; _tutorialTestLifecycle = CaptureLifecycleRollback();
                QueueLifecycle("load", 1, true); _tutorialTestStep = 125; break;
            case 125:
                if (_loadPending != null) break;
                GroundRequire(_loadRecovered && !_groundFault && _userPaused && _menuPaused && ReadLifecycle().Page == "slots" && _worldEpoch == _tutorialTestLifecycle!.WorldEpoch,
                    "projection failure restores world menu pause and epoch");
                var good = JsonSerializer.Deserialize<PlayerSave>(System.Text.Encoding.UTF8.GetString(_tutorialTestFile), SaveOptions)!;
                GroundRequire(JsonSerializer.Serialize(_ledger.Snapshot(), SaveOptions) == JsonSerializer.Serialize(good.Bootstrap!.Ledger, SaveOptions) && _playerTime == good.Time,
                    "projection rollback keeps ledger and time");
                QueueLifecycle("back"); _tutorialTestStep = 110; break;
            case 110:
                QueueLifecycle("home"); _tutorialTestStep = 111; break;
            case 111:
                GroundRequire(ReadLifecycle().Page == "leave", "home crosses leave confirmation");
                if (OperatingSystem.IsWindows()) throw new InvalidOperationException("Unix save failure fixture unavailable");
                _tutorialTestFingerprint = TutorialWorldFingerprint();
                File.SetUnixFileMode(TutorialSaveRoot, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                QueueLifecycle("leave-save"); _tutorialTestStep = 112; break;
            case 112:
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(TutorialSaveRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                GroundRequire(!_entryOpen && ReadLifecycle().Page == "leave" && _activeSlot == 1 && _tutorialTestFingerprint == TutorialWorldFingerprint()
                    && File.ReadAllBytes(PlayerSavePath).SequenceEqual(_tutorialTestFile), "save failure leaves old file world menu and slot intact");
                QueueLifecycle("leave-cancel"); _tutorialTestStep = 113; break;
            case 113:
                GroundRequire(ReadLifecycle().Page == "pause" && _userPaused, "cancel leave retains pause");
                QueueLifecycle("home"); _tutorialTestStep = 114; break;
            case 114: QueueLifecycle("leave-discard"); _tutorialTestStep = 115; break;
            case 115:
                GroundRequire(_entryOpen && ReadLifecycle().Page == "home", "discard goes home");
                QueueLifecycle("newgame", 1); _tutorialTestStep = 116; break;
            case 116:
                GroundRequire(ReadLifecycle().Page == "confirm-newgame" && _baseFacilities.Count > 1 && File.ReadAllBytes(PlayerSavePath).SequenceEqual(_tutorialTestFile), "existing slot asks confirmation before new world");
                QueueLifecycle("newgame", 1, true); _tutorialTestStep = 117; break;
            case 117:
                if (_loadPending != null || _entryOpen) break;
                GroundRequire(_baseFacilities.Count == 1 && _mines.All(m => m.Remaining == 96) && _productionTasks.Count == 0 && _development == null && _buildJob == null
                    && _playerTime < .1 && !_userPaused && _ledger.Count("lander", "kit") == 8 && ReadTutorial().FirstGoalId == null, "true newgame resets complete world");
                GroundRequire(File.ReadAllBytes(PlayerSavePath).SequenceEqual(_tutorialTestFile), "newgame does not replace old slot before explicit save");
                _Notification((int)NotificationWMCloseRequest); _tutorialTestStep = 118; break;
            case 118:
                GroundRequire(ReadLifecycle().Page == "leave" && ReadLifecycle().PendingAction == "quit", "WM close uses leave confirmation");
                QueueLifecycle("leave-cancel"); _tutorialTestStep = 119; break;
            case 119:
                QueueLifecycle("resume"); _tutorialTestStep = 120; break;
            case 120:
                var initial = _initialPlayer!; string frozen = JsonSerializer.Serialize(initial, SaveOptions);
                TutorialTestBuild("solar", new(-3, 0, -14));
                QueueBuild("solar", new(13, 0, -1), 0, ReadBuildPlan("solar", new(13, 0, -1)).ObservedVersion);
                _tutorialTestFingerprint = frozen; _tutorialTestStep = 121; break;
            case 121:
                GroundRequire(_developmentSequence == 1 && _playerCommand == null && _tutorialTestFingerprint == JsonSerializer.Serialize(_initialPlayer, SaveOptions), "double submit accepts one intent and initial snapshot stays independent");
                GroundRequire(!ReadBuildPlan("repair", new(19, 0, 19)).CandidateLegal, "out of range powered location warning and guard");
                var copied = ReadBuildPlan("solar", new(13, 0, -1));
                copied.Cost[0] = copied.Cost[0] with { Quantity = 999 }; copied.Stock[0] = copied.Stock[0] with { Available = 999 };
                GroundRequire(_ledger.Totals()["iron"] == 17 && Definition("solar").Cost["iron"] == 4, "DTO arrays cannot mutate world");
                QueuePlayerAction("pause"); _tutorialTestStep = 122; break;
            case 122:
                if (!_userPaused) break;
                // Explicit state probe: night changes output, never the established connection capability.
                double time = _playerTime; _playerTime = 450;
                try { GroundRequire(!Daylight && ReadTutorial().Milestones.First(m => m.Id == "solar").Achieved == false, "new world has no old milestone after reset"); }
                finally { _playerTime = time; }
                GD.Print("TUTORIAL_BOUNDARIES PASS menu_pause settings duplicate_fields projection_rollback save_failure newgame_reset double_submit close_confirmation detached_dto");
                GetTree().Quit(); break;
        }
    }
    private bool _tutorialTestRetryObserved;
    private int _tutorialTestRetryStartFrame;
    private void TutorialRecoveryDiagnostics(string reason)
    {
        var task = Production; var ledger = _ledger.Snapshot();
        GD.Print("TUTORIAL_RECOVERY_DIAGNOSTIC " + reason + " task=" + JsonSerializer.Serialize(task, SaveOptions));
        foreach (var actor in _groundRobots)
        {
            string id = actor.Name.ToString(); var health = _health[id];
            var route = _routes.GetValueOrDefault(id); var service = _services.GetValueOrDefault(id);
            GD.Print($"TUTORIAL_RECOVERY_ACTOR id={id} pos={actor.GlobalPosition} order={actor.HasOrder}/{actor.OrderReached} blocked={actor.Blocked} health={health.Energy:R}/{health.Durability:R} reason={health.Reason} route={JsonSerializer.Serialize(route, new JsonSerializerOptions { IncludeFields = true })} service={JsonSerializer.Serialize(service, SaveOptions)}");
        }
        GD.Print("TUTORIAL_RECOVERY_STATIONS " + JsonSerializer.Serialize(_stations, SaveOptions));
        GD.Print("TUTORIAL_RECOVERY_STOCK " + JsonSerializer.Serialize(ledger.Containers, SaveOptions));
        if (ReadPlayerState().Ready)
        {
            try { SavePlayerTo(TutorialSlotPath(2)); }
            catch (Exception ex) { GD.Print("TUTORIAL_RECOVERY_DIAGNOSTIC_SAVE_REJECTED " + ex.Message); }
        }
    }
    private void TutorialRecoveryStep()
    {
        switch (_tutorialTestStep)
        {
            case 200:
                if (_loadPending != null || _entryOpen) break;
                GroundRequire(ReadTutorial().LegacyPractice && !ReadTutorial().Completed, "schema3 accepted without fabricated tutorial");
                var target = _baseFacilities.Single(f => f.Built && f.Type == "repair"); _tutorialTestRecoveryTarget = target.Id;
                GroundRequire(target.Source == null && _baseFacilities.Any(f => f.Type == "processor" && ServiceConnected(f)) && _baseFacilities.Any(f => f.Type == "charger" && ServiceConnected(f)), "real old D001 trigger loaded unchanged");
                GroundRequire(!ReadBuildPlan("storage", new(-12, 0, -15)).CanSubmit, "ordinary expansion cannot bypass support protection");
                var recovery = ReadBuildPlan("solar", TutorialTestRecoveryPosition);
                GroundRequire(recovery.Recovery && recovery.CanSubmit && recovery.Gap.Length > 0, "specific finite recovery can use wired processor and charger");
                TutorialTestBuild("solar", TutorialTestRecoveryPosition); _tutorialTestStep = 201; break;
            case 201:
                if (_development?.Stage == "Blocked") throw new InvalidOperationException("old D001 recovery blocked: " + _development.Reason);
                if (!TutorialGoalComplete) break;
                _tutorialTestRecoverySolar = _baseFacilities.Single(f => f.Built && f.Type == "solar" && LoadVector(f.Position) == TutorialTestRecoveryPosition).Id;
                GroundRequire(_ledger.Snapshot().Mining!.Length > 0 && _ledger.Snapshot().Recipes!.Length > 0, "recovery really mines and processes, no grant");
                TutorialTestWire(_tutorialTestRecoveryTarget!, _tutorialTestRecoverySolar); _tutorialTestStep = 202; break;
            case 202:
                if (_development?.Stage == "Blocked") throw new InvalidOperationException("old D001 wire blocked: " + _development.Reason);
                if (!TutorialGoalComplete) break;
                GroundRequire(ServiceConnected(_baseFacilities.Single(f => f.Id == _tutorialTestRecoveryTarget)) && SupportSetConnected && ProductionPrerequisite.Length == 0, "original repair wired and deadlock removed");
                GroundRequire(ReadTutorial().StageId == "legacy-practice" && !ReadTutorial().Completed, "old restored practice next missing honest");
                GroundRequire(SavePlayerTo(TutorialSlotPath(3)), "real restored power checkpoint before subsequent development");
                var site = new Vector3(-12, 0, -14); GroundRequire(ReadBuildPlan("storage", site).CanSubmit, "subsequent real development unblocked");
                TutorialTestBuild("storage", site); _tutorialTestStep = 203; break;
            case 203:
                if (_development?.Stage == "Blocked") { TutorialRecoveryDiagnostics("subsequent development blocked"); throw new InvalidOperationException("old D001 subsequent goal blocked: " + _development.Reason); }
                if (!TutorialGoalComplete) break;
                QueuePlayerAction("pause"); _tutorialTestStep = 204; break;
            case 208:
                if (_loadPending != null || _entryOpen) break;
                var probeActor = Actor("Robot_Tuoyun_1");
                string unchanged = TutorialWorldFingerprint(); var originalPose = probeActor.GlobalTransform;
                foreach (var point in new[] { new Vector3(-16.18484f, 0, 5.868094f), new(-16.607584f, 0, 5.9829664f), new(-16.508446f, 0, 5.401251f) })
                {
                    var motion = point - probeActor.GlobalPosition; motion.Y = 0;
                    using var parameters = new PhysicsTestMotionParameters3D { From = probeActor.GlobalTransform, Motion = motion, Margin = probeActor.SafeMargin, MaxCollisions = 8, RecoveryAsCollision = false };
                    using var result = new PhysicsTestMotionResult3D();
                    bool collided = PhysicsServer3D.BodyTestMotion(probeActor.GetRid(), parameters, result);
                    var destination = LoadVector(Production!.Station!); var strict = StrictRoute(probeActor, point, destination);
                    GD.Print($"ESCAPE_PROBE point={point} actor_y={probeActor.GlobalPosition.Y} post_ground_y={SavedGroundHeight(_liveTerrain!.Current, point.X, point.Z)} motion={motion} collided={collided} safe={result.GetCollisionSafeFraction()} unsafe={result.GetCollisionUnsafeFraction()} travel={result.GetTravel()} native={NativeEscapeSweep(probeActor, motion)} footprint={Yudian.Navigation.BoundedRoute.FootprintClear(_liveTerrain!.Current, new(point.X, point.Z), RouteFootprint(probeActor))} post_route={strict.Found}/{strict.LengthM} version={strict.WorldVersion}");
                    for (int index = 0; index < result.GetCollisionCount(); index++) GD.Print($"ESCAPE_PROBE_CONTACT rid={result.GetColliderRid(index)} terrain={_liveTerrain.Body.GetRid()} normal={result.GetCollisionNormal(index)} depth={result.GetCollisionDepth(index)}");
                }
                GroundRequire(unchanged == TutorialWorldFingerprint() && probeActor.GlobalTransform == originalPose && !probeActor.HasOrder, "native escape probe does not move or mutate world");
                GD.Print("ESCAPE_PROBE EXECUTED readonly=true recovery_pass=false"); GetTree().Quit(); break;
            case 205:
                if (_loadPending != null || _entryOpen) break;
                GroundRequire(_development?.Stage == "Blocked" && Production?.Paid == true && Production.Kind == "haul", "real blocked haul snapshot loaded");
                TutorialRecoveryDiagnostics("before normal retry");
                if (Production.Robot is {} retryRobot)
                {
                    var actor = Actor(retryRobot); var station = LoadVector(Production.Station!); var route = FindRoute(actor, station);
                    GD.Print("TUTORIAL_RETRY_ROUTE " + JsonSerializer.Serialize(new { route.Found, route.Reason, route.LengthM, Points = route.Points.Select(p => new { p.X, p.Z, Height = SavedGroundHeight(_liveTerrain!.Current, (float)p.X, (float)p.Z) }).ToArray() }, SaveOptions));
                }
                QueuePlayerAction("retry"); _tutorialTestRetryStartFrame = _groundFrame; _tutorialTestStep = 206; break;
            case 206:
                if (_groundFrame % 120 == 0 && Production?.Robot is {} robot)
                {
                    var actor = Actor(robot); var route = _routes.GetValueOrDefault(robot);
                    GD.Print($"TUTORIAL_RETRY_OBSERVE pos={actor.GlobalPosition} blocked={actor.Blocked} onwall={actor.IsOnWall()} normal={actor.GetWallNormal()} stage={Production.Stage} order={actor.HasOrder}/{actor.OrderReached} route_elapsed={route?.Seconds:R} route_stalled={route?.Stalled:R}");
                }
                if (_development?.Stage == "Blocked")
                {
                    TutorialRecoveryDiagnostics("normal retry blocked");
                    throw new InvalidOperationException("normal retry failed to resume delivered ore");
                }
                if (_productionTasks.Single(t => t.Id == "prod-24").Stage != "Completed") break;
                GroundRequire(_ledger.Count(CargoContainer("Robot_Tuoyun_1"), "iron_ore") == 0, "normal retry unloads original cargo exactly once");
                _tutorialTestRetryObserved = true; _tutorialTestStep = 207; break;
            case 207:
                if (_development?.Stage == "Blocked") { TutorialRecoveryDiagnostics("retry later development blocked"); throw new InvalidOperationException("normal retry subsequent development blocked"); }
                if (!TutorialGoalComplete) break;
                GroundRequire(_tutorialTestRetryObserved && SavePlayerTo(TutorialSlotPath(1)), "normal retry preserves and completes original goal");
                GD.Print("TUTORIAL_RECOVERY_RETRY PASS original_cargo=true original_goal=true"); GetTree().Quit(); break;
            case 204:
                if (!_userPaused) break;
                GroundRequire(SavePlayer(), "old restored world migrates to schema4 new slot");
                GroundRequire(ReadPlayerSaveFile(PlayerSavePath).Schema == 4 && ReadTutorial().LegacyPractice && !ReadTutorial().Completed, "migration retains history honesty");
                GD.Print($"TUTORIAL_RECOVERY PASS original_repair={_tutorialTestRecoveryTarget} new_solar={_tutorialTestRecoverySolar} natural_health=true recipes={_ledger.Snapshot().Recipes!.Length} next_goal=storage time={_playerTime:0.0}");
                GetTree().Quit(); break;
        }
    }
}
