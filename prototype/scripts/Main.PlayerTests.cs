#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Yudian.Terrain;

namespace Yudian;
public partial class Main
{
    private int _playerTestStep, _playerTestFrame;
    private double _playerTestTime, _playerTestWork;
    private Vector3[] _playerTestPositions = [];
    private LevelJob? _playerTestJob;
    private byte[] _playerTestSave = [];
    private Vector3 _playerTestSite;
    private bool _playerTestSavedCommit, _playerTestInjectLoadFault;
    private void RejectPlayerTestSave(string name, Action<JsonNode> edit)
    {
        var json = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(_playerTestSave))!; edit(json);
        File.WriteAllText(PlayerSavePath, json.ToJsonString()); var world = _liveTerrain; var job = _levelJob;
        bool rejected = false;
        try { LoadPlayer(); } catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or ArgumentException) { rejected = true; }
        GroundRequire(rejected && ReferenceEquals(world, _liveTerrain) && ReferenceEquals(job, _levelJob) && _loadPending == null, "bad save " + name + " preserves world");
        File.WriteAllBytes(PlayerSavePath, _playerTestSave); GD.Print("PLAYER_BAD_SAVE_REJECT PASS " + name);
    }
    private void TestPlayerWriteFailure()
    {
        if (System.OperatingSystem.IsWindows()) { GD.Print("PLAYER_SAVE_WRITE_FAILURE NOT_RUN Windows permission fixture"); return; }
        string original = PlayerSavePath; string dir = original + ".readonly"; Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "valid.json"); File.WriteAllBytes(file, _playerTestSave);
        File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        bool rejected = false;
        try
        {
            System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE", file);
            try { SavePlayer(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { rejected = true; }
            GroundRequire(rejected && File.ReadAllBytes(file).SequenceEqual(_playerTestSave), "write failure retains valid save");
            GD.Print("PLAYER_SAVE_WRITE_FAILURE PASS");
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE", original);
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
    private void PlayerTestStep()
    {
        GroundRequire(_groundFrame < 10000, "player test timeout");
        if (!_groundReady || _groundFrame < 100) return;
        string mode = System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_TEST_PHASE") ?? "full";
        if (mode is "resume" or "completed")
        {
            if (_playerTestStep == 0) { QueuePlayerAction("load"); _playerTestStep++; return; }
            if (_loadPending != null || _levelJob == null) return;
            if (_playerTestStep == 1)
            {
                if (mode == "completed")
                {
                    GroundRequire(_levelJob.Stage == LevelStage.Completed && _liveTerrain!.Current.Version == 1, "cross-process completed no replay");
                    GD.Print("PLAYER_CROSS_PROCESS_COMPLETED PASS"); GetTree().Quit(); return;
                }
                GroundRequire(_userPaused && _levelJob.Work.ElapsedSeconds >= .4 && _levelJob.Stage == LevelStage.Working, "cross-process working progress and pause");
                QueuePlayerAction("pause"); _playerTestStep++; return;
            }
            if (_levelJob.Stage == LevelStage.Completed)
            {
                GroundRequire(_liveTerrain!.Current.Version == 1, "cross-process single commit");
                SavePlayer(); GD.Print("PLAYER_CROSS_PROCESS_RESUME PASS"); GetTree().Quit();
            }
            return;
        }
        switch (_playerTestStep)
        {
            case 0:
                GroundRequire(_groundRobots.Count == 12 && _groundRobots.All(x => x.IsOnFloor()), "player initial grounded");
                foreach (var actor in _groundRobots)
                    GroundRequire(Math.Abs(SavedGroundHeight(_liveTerrain!.Current, actor.GlobalPosition.X, actor.GlobalPosition.Z) - GroundHeight(actor.GlobalPosition.X, actor.GlobalPosition.Z)) < .0001, "loaded height matches native triangles");
                foreach (var actor in _groundRobots.Where(x => x.Name.ToString().StartsWith("Robot_Zhulei_")))
                {
                    Vector3[] sites = [new(12, 0, -10), new(19, 0, 18), new(0, 0, -20), new(-15, 0, -18)];
                    GD.Print($"PLAYER_REACHABILITY {actor.Name} mound={PreviewLevel(sites[0], actor.Name.ToString()).Reason}");
                    GroundRequire(sites.Any(x => PreviewLevel(x, actor.Name.ToString()).Legal), "each筑垒 has a reachable legal target");
                }
                _playerTestPositions = _groundRobots.Select(x => x.GlobalPosition).ToArray();
                QueuePlayerAction("pause"); break;
            case 1:
                _playerTestTime = _playerTime; _playerTestFrame = _groundFrame; RecoverGround(); break;
            case 2:
                if (_groundFrame - _playerTestFrame < 30) return;
                GroundRequire(_userPaused && _playerTime == _playerTestTime && _groundRobots.All(x => !x.IsPhysicsProcessing()), "projection cannot release user pause");
                GroundRequire(_groundRobots.Select((x, i) => x.GlobalPosition.DistanceTo(_playerTestPositions[i]) < .0001f).All(x => x), "paused entities stationary");
                QueueLevel(new Vector3(float.NaN, 0, 0), 0); break;
            case 3:
                GroundRequire(_levelJob == null && _liveTerrain!.Current.Version == 0, "nonfinite command rejected");
                QueueLevel(new Vector3(29, 0, 29), 0); break;
            case 4:
                GroundRequire(_levelJob == null && _liveTerrain!.Current.Version == 0, "out of bounds rejected without clipping");
                Vector3[] candidates = [new(12, 0, -10), new(-15, 0, -18), new(19, 0, 18), new(-20, 0, 0), new(0, 0, -20)];
                var legal = candidates.Where(x => PreviewLevel(x).Legal).ToArray();
                GroundRequire(legal.Length >= 2, "two legal locations"); _playerTestSite = legal[0];
                QueueLevel(_playerTestSite, -1); break;
            case 5:
                GroundRequire(_levelJob == null && _liveTerrain!.Current.Version == 0, "stale preview rejected");
                QueuePlayerAction("pause"); break;
            case 6:
                QueueLevel(_playerTestSite, 0); break;
            case 7:
                GroundRequire(_levelJob?.Worker != null && _levelJob.Active, "player task assigned");
                _playerTestJob = _levelJob; QueueLevel(_playerTestSite, 0); break;
            case 8:
                GroundRequire(ReferenceEquals(_playerTestJob, _levelJob) && _levelSequence == 1, "duplicate does not create task");
                if (_levelJob!.Stage != LevelStage.Working || _levelJob.Work.ElapsedSeconds < .4) return;
                QueuePlayerAction("pause"); break;
            case 9:
                GroundRequire(_userPaused && _liveTerrain!.Current.Version == 0, "pause before modification");
                _playerTestWork = _levelJob!.Work.ElapsedSeconds; _playerTestTime = _playerTime;
                QueuePlayerAction("save"); break;
            case 10:
                GroundRequire(File.Exists(PlayerSavePath) && _levelJob!.Work.ElapsedSeconds == _playerTestWork && _playerTime == _playerTestTime, "paused working saved without progress loss");
                _playerTestSave = File.ReadAllBytes(PlayerSavePath);
                if (mode == "prepare") { GD.Print("PLAYER_CROSS_PROCESS_PREPARE PASS"); GetTree().Quit(); return; }
                RejectPlayerTestSave("below-terrain", json => json["Robots"]![0]!["Position"]![1] = -100);
                RejectPlayerTestSave("skip-work", json => { json["Job"]!["Stage"] = "WaitingForSpace"; json["Job"]!["Work"] = 0; });
                RejectPlayerTestSave("outside-patch", json =>
                {
                    var patch = JsonNode.Parse(json["Job"]!["Patch"]!.GetValue<string>())!;
                    patch["heights_m"]![330] = 5; json["Job"]!["Patch"] = patch.ToJsonString();
                });
                RejectPlayerTestSave("unsafe-station", json => json["Job"]!["Station"] = json["Job"]!["Center"]!.DeepClone());
                RejectPlayerTestSave("blocked-route", json =>
                {
                    var source = TerrainDataCodec.ParseSnapshot(json["Terrain"]!.GetValue<string>());
                    var center = new Vector3(-22, 0, 0); var patch = GroundPatch(source, center.X, center.Z, 0, "level-1");
                    json["Job"]!["Patch"] = TerrainDataCodec.Serialize(patch); json["Job"]!["Stage"] = "Travelling";
                    json["Job"]!["Work"] = 0; json["Job"]!["Space"] = 0;
                    json["Job"]!["Center"] = new JsonArray(-22, 0, 0);
                    json["Job"]!["Station"] = new JsonArray(-16.525f, (float)SavedGroundHeight(source, -16.525f, 0), 0);
                });
                TestPlayerWriteFailure();
                File.WriteAllText(PlayerSavePath, "{\"Schema\":99}"); QueuePlayerAction("load"); break;
            case 11:
                GroundRequire(ReferenceEquals(_levelJob, _playerTestJob) && _liveTerrain!.Current.Version == 0 && _loadPending == null, "bad save leaves world/task unchanged");
                File.WriteAllBytes(PlayerSavePath, _playerTestSave); QueuePlayerAction("load"); break;
            case 12:
                if (_loadPending != null) return;
                GroundRequire(_userPaused && _levelJob!.Work.ElapsedSeconds == _playerTestWork && _playerTime == _playerTestTime && _groundVerified == 0, "working load preserves exact progress and pause");
                QueuePlayerAction("pause"); break;
            case 13:
                if (!_playerTestSavedCommit && _levelJob!.Stage == LevelStage.AwaitingPhysics)
                {
                    SavePlayer(); File.Copy(PlayerSavePath, PlayerSavePath + ".awaiting", false); _playerTestSavedCommit = true;
                    GD.Print("PLAYER_SAVE_AWAITING_PHYSICS PASS");
                }
                if (_levelJob!.Stage != LevelStage.Completed) { GroundRequire(_levelJob.Active, "restored task remains valid"); return; }
                GroundRequire(_liveTerrain!.Current.Version == 1 && _groundVerified == 1, "one real restored commit");
                QueuePlayerAction("save"); break;
            case 14:
                _playerTestSave = File.ReadAllBytes(PlayerSavePath); QueuePlayerAction("load"); break;
            case 15:
                if (_loadPending != null) return;
                GroundRequire(_levelJob!.Stage == LevelStage.Completed && _liveTerrain!.Current.Version == 1, "completed load no repeat commit");
                QueuePlayerAction("load"); break;
            case 16:
                if (_loadPending != null) return;
                GroundRequire(_levelJob!.Stage == LevelStage.Completed && _liveTerrain!.Current.Version == 1, "repeated load no reward/version replay");
                var second = new Vector3(19, 0, 18);
                GroundRequire(PreviewLevel(second).Legal, "second location remains legal"); QueueLevel(second, 1); break;
            case 17:
                GroundRequire(_levelJob!.Active && _levelJob.Center != _playerTestSite, "second player location task");
                QueuePlayerAction("cancel"); break;
            case 18:
                GroundRequire(_levelJob!.Stage == LevelStage.Cancelled && _liveTerrain!.Current.Version == 1, "cancel before commit preserves previous terrain");
                QueuePlayerAction("save"); break;
            case 19:
                var paused = JsonNode.Parse(File.ReadAllText(PlayerSavePath))!; paused["Paused"] = true; File.WriteAllText(PlayerSavePath, paused.ToJsonString());
                foreach (var visual in _playerVisuals.Values) visual.Visual.Apply("work", .6, false);
                QueuePlayerAction("load"); break;
            case 20:
                if (_loadPending != null) return;
                GroundRequire(_levelJob!.Stage == LevelStage.Cancelled && _liveTerrain!.Current.Version == 1 && !_levelJob.Worker!.HasOrder, "cancelled load no work/order replay");
                GroundRequire(_userPaused && _playerVisuals.Values.All(x => x.Visual.State == "idle"), "paused cancelled load resets previous work pose");
                GD.Print("PLAYER_VISUAL_RESTORE PASS");
                _playerTestInjectLoadFault = true; QueuePlayerAction("load"); break;
            case 21:
                if (_loadPending != null) return;
                GroundRequire(_loadRecovered && _levelJob!.Stage == LevelStage.Cancelled && _liveTerrain!.Current.Version == 1 && !_groundFault, "failed projection load restores original world and task");
                GD.Print("PLAYER_SELF_TEST PASS steps=22"); GetTree().Quit(); break;
        }
        GD.Print("PLAYER_TEST_STEP PASS " + _playerTestStep++);
    }
}
