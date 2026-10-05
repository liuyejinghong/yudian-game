#nullable enable
using System;
using System.Linq;
using Godot;
using Yudian.Terrain;

namespace Yudian;

public partial class Main
{
    private int _levelTestStep, _levelTestFrame;
    private LevelJob? _levelTestJob;
    private GroundPatrol? _levelTestBlocker;
    private double _levelTestTravel;
    private Mesh? _levelTestMesh;
    private Shape3D? _levelTestShape;

    private void LevelTestStep(double delta)
    {
        GroundRequire(_groundFrame < 7200, "level test timeout");
        if (!_groundReady || _groundFrame < 120) return;
        var region = _liveTerrain!;
        var site = _cfg.Terrain.Mound.Position;
        switch (_levelTestStep)
        {
            case 0:
                GroundRequire(_groundRobots.Count == 12 && _groundRobots.All(x => x.IsOnFloor()), "initial 12 grounded");
                _levelTestMesh = region.MeshNode.Mesh; _levelTestShape = region.Collider.Shape;
                StartLevelJob(); _levelTestJob = _levelJob;
                GroundRequire(_levelJob?.Worker != null && _levelJob.Stage == LevelStage.Travelling, "assigned real worker");
                GroundRequire(!TouchesFootprint(_levelJob.Patch, _levelJob.Station.X, _levelJob.Station.Z, .35f), "outside changed cells");
                _levelTestTravel = _levelJob.Worker.TravelledM;
                StartLevelJob();
                GroundRequire(ReferenceEquals(_levelJob, _levelTestJob) && _levelJob.Work.ElapsedSeconds == 0 && region.Current.Version == 0, "duplicate keeps task; no work or terrain at dispatch");
                break;
            case 1:
                if (_levelJob!.Stage != LevelStage.Working)
                {
                    GroundRequire(_levelJob.Active && _levelJob.Work.ElapsedSeconds == 0 && region.Current.Version == 0, "travelling does not work"); return;
                }
                GroundRequire(WorkerAtStation(_levelJob) && _levelJob.Worker!.TravelledM > _levelTestTravel + .1, "real movement arrival");
                _levelJob.Worker.Paused = true; _levelTestFrame = _groundFrame;
                break;
            case 2:
                if (_groundFrame - _levelTestFrame < 30) return;
                GroundRequire(_levelJob!.Work.ElapsedSeconds == 0 && region.Current.Version == 0, "paused worker no progress");
                _levelJob.Worker!.Paused = false;
                break;
            case 3:
                if (_levelJob!.Work.ElapsedSeconds < .5) return;
                GroundRequire(_levelJob.Work.ElapsedSeconds < 3, "cancel before work finished");
                CancelLevelJob(); CancelLevelJob();
                GroundRequire(_levelJob.Stage == LevelStage.Cancelled && !_levelJob.Worker!.HasOrder && region.Current.Version == 0 &&
                    ReferenceEquals(_levelTestMesh, region.MeshNode.Mesh) && ReferenceEquals(_levelTestShape, region.Collider.Shape), "cancel no modification/resource replacement");
                StartLevelJob();
                // Seed only the timeout clock; no teleport and no claim of a full 60s blocked run.
                _levelJob!.TravelSeconds = 60; _levelJob.Worker!.SetOrder(new Vector3(-20, 0, 20));
                break;
            case 4:
                GroundRequire(_levelJob!.Stage == LevelStage.Failed && !_levelJob.Worker!.HasOrder && _levelJob.Work.ElapsedSeconds == 0 && region.Current.Version == 0, "not arrived timeout threshold fails without work");
                StartLevelJob(); region.PermissionGranted = false;
                break;
            case 5:
                GroundRequire(_levelJob!.Stage == LevelStage.Failed && region.Current.Version == 0 && !_levelJob.Worker!.HasOrder, "permission loss no modification");
                region.PermissionGranted = true; StartLevelJob();
                GroundRequire(SubmitGround(GroundPatch(-25, -25, .6)), "external test modification commits");
                break;
            case 6:
                GroundRequire(_levelJob!.Stage == LevelStage.Failed && _levelJob.Patch.Base.Version == 0 && region.Current.Version == 1 && !_levelJob.Worker!.HasOrder, "stale job keeps frozen base, releases worker");
                if (_groundVerified != 1) return;
                StartLevelJob(); _levelJob!.Worker!.Paused = true;
                _levelTestBlocker = _groundRobots.Where(x => x != _levelJob.Worker).OrderBy(x => XzDistance(x.GlobalPosition, new Vector3(site[0], 0, site[1]))).First();
                _levelTestBlocker.SetOrder(new Vector3(site[0], GroundHeight(site[0], site[1]), site[1]));
                break;
            case 7:
                if (!_levelTestBlocker!.OrderReached) return;
                GroundRequire(TouchesFootprint(_levelJob!.Patch, _levelTestBlocker.GlobalPosition.X, _levelTestBlocker.GlobalPosition.Z, .35f), "actual blocker arrived in patch");
                _levelJob.Worker!.Paused = false;
                break;
            case 8:
                if (_levelJob!.Stage != LevelStage.WaitingForSpace || _levelJob.SpaceSeconds < .05) return;
                GroundRequire(_levelJob.Active && _levelJob.Work.IsComplete && WorkerAtStation(_levelJob) && region.Current.Version == 1 && _levelJob.Worker!.HasOrder, "occupancy waits, worker order held, no commit");
                region.AfterMeshBoundForTest = () => throw new InvalidOperationException("synthetic level bind failure");
                _levelTestBlocker!.ClearOrder();
                break;
            case 9:
                if (_levelJob!.Stage != LevelStage.AwaitingPhysics) return;
                region.AfterMeshBoundForTest = null;
                GroundRequire(_levelJob.AppliedVersion == 2 && region.Current.Version == 2 && _groundFault && _groundVerified == null &&
                    _groundRobots.All(x => x.Paused), "commit fault not completed, authority retained");
                var replay = region.Submit(_levelJob.Id, _levelJob.Patch);
                GroundRequire(replay.Status == TerrainCommitStatus.AlreadyCommitted && replay.AppliedVersion == 2 && region.Current.Version == 2, "stable request replay no second commit");
                RecoverGround();
                GroundRequire(_groundVerified == null && _levelJob.Stage == LevelStage.AwaitingPhysics, "recovery waits later frame");
                break;
            case 10:
                GroundRequire(_levelJob!.Stage == LevelStage.Completed && _groundVerified == 2 && !_levelJob.Worker!.HasOrder && !_groundFault, "verified result only then completes");
                _levelTestJob = _levelJob; _levelTestTravel = _levelJob.Worker.TravelledM;
                StartLevelJob();
                GroundRequire(_levelJob!.Stage == LevelStage.Completed && _levelJob.Worker == null && region.Current.Version == 2, "already flat explicit no change");
                break;
            case 11:
                if (_levelTestJob!.Worker!.TravelledM < _levelTestTravel + .3) return;
                if (!SubmitGround(GroundPatch(site[0], site[1], 1))) return;
                break;
            case 12:
                GroundRequire(_groundVerified == 3, "test fixture remound synced");
                StartLevelJob();
                break;
            case 13:
                if (_levelJob!.Stage != LevelStage.AwaitingPhysics) return;
                GroundRequire(_levelJob.AppliedVersion == 4 && _groundVerified == null && region.Current.Version == 4, "committed still awaiting next frame");
                CancelLevelJob();
                GroundRequire(_levelJob.Stage == LevelStage.Cancelled && _levelJob.AppliedVersion == 4 && region.Current.Version == 4 && !_levelJob.Worker!.HasOrder, "postcommit cancellation preserves terrain");
                break;
            case 14:
                GroundRequire(_levelJob!.Stage == LevelStage.Cancelled && _groundVerified == 4 && region.Current.Version == 4 && _groundRobots.All(x => !x.Paused), "cancel remains cancelled; physics resumes retained result");
                GD.Print($"LEVEL_JOB_SELF_TEST PASS steps=15 version={region.Current.Version} robots={_groundRobots.Count} grounded={_groundRobots.Count(x => x.IsOnFloor())} continuous_seconds={_levelJob.Work.ElapsedSeconds:R}");
                GetTree().Quit(0); break;
        }
        GD.Print("LEVEL_JOB_TEST_STEP PASS " + _levelTestStep++);
    }
}
