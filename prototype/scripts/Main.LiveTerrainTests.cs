#nullable enable
using System;
using System.Linq;
using Godot;
using Yudian.Terrain;

namespace Yudian;

public partial class Main
{
    private int _groundTestStep;
    private double _groundTestTravel;
    private long _groundTestVersion;
    private Mesh? _groundTestMesh;
    private Shape3D? _groundTestShape;

    private void LogLongRayError()
    {
        var s = _liveTerrain!.Current;
        for (int i = 0; i < s.HeightsM.Count; i++)
        {
            float x = (float)(s.OriginXM + i % s.Columns * s.SpacingM);
            float z = (float)(s.OriginZM + i / s.Columns * s.SpacingM);
            using var query = PhysicsRayQueryParameters3D.Create(new Vector3(x, 1100, z), new Vector3(x, -1100, z), 1);
            using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0) continue;
            float oldRay = hit["position"].AsVector3().Y;
            if (Math.Abs(oldRay - s.HeightsM[i]) <= .0001) continue;
            GD.Print($"MAIN_GROUND_LONG_RAY_PRECISION index={i} expected={s.HeightsM[i]:R} long={oldRay:R} bounded={GroundHeight(x, z):R}");
            break;
        }
    }

    private void GroundTestStep()
    {
        GroundRequire(_groundFrame < 900, "test timeout");
        if (!_groundReady || _groundFrame < 120) return;
        var region = _liveTerrain!;
        switch (_groundTestStep)
        {
            case 0:
                GroundRequire(_groundRobots.Count == 12 && _groundRobots.All(x => x.IsOnFloor() && x.TravelledM > .1), "12 real grounded moving bodies");
                var s = region.Current; var values = s.HeightsM.ToArray();
                int row = s.Rows / 2, col = s.Columns / 2; values[row * s.Columns + col] += .2;
                var edgePatch = GroundPatchFromHeights(s, values, "edge-footprint");
                float vertexX = (float)(s.OriginXM + col * s.SpacingM), vertexZ = (float)(s.OriginZM + row * s.SpacingM);
                GroundRequire(TouchesFootprint(edgePatch, vertexX + (float)s.SpacingM + .1f, vertexZ, .35f), "adjacent cell/capsule edge overlap");
                GroundRequire(!TouchesFootprint(GroundPatchFromHeights(s, s.HeightsM.ToArray(), "nochange"), vertexX, vertexZ, 2), "no change no occupied cell");
                var mesh = region.MeshNode.Mesh; var shape = region.Collider.Shape;
                var facility = _facilityPositions[0];
                GroundRequire(!SubmitGround(GroundPatch(facility.X, facility.Z, .7)), "occupied facility rejected");
                TerrainPatch? occupied = null;
                foreach (var actor in _groundRobots)
                {
                    var robot = actor.GlobalPosition; var candidate = GroundPatch(robot.X, robot.Z, .8);
                    if (_facilityPositions.Select((p, i) => TouchesFootprint(candidate, p.X, p.Z, FacilityRadii[i % 6])).Any(x => x)) continue;
                    GroundRequire(TouchesFootprint(candidate, robot.X, robot.Z, .35f), "capsule touched");
                    occupied = candidate; break;
                }
                GroundRequire(occupied != null && !SubmitGround(occupied), "robot-only occupancy rejected");
                LogLongRayError();
                GroundRequire(region.Current.Version == 0 && ReferenceEquals(mesh, region.MeshNode.Mesh) && ReferenceEquals(shape, region.Collider.Shape) && _groundRobots.All(x => !x.Paused), "reject preserves authority/resources and resumes");
                break;
            case 1:
                if (!SubmitGround(GroundPatch(_cfg.Terrain.Mound.Position[0], _cfg.Terrain.Mound.Position[1], 0))) return;
                GroundRequire(_groundVerified == null && _groundRobots.All(x => x.Paused), "commit waits for physics");
                break;
            case 2:
                GroundRequire(_groundVerified == 1 && _groundRobots.All(x => !x.Paused), "mound version 1 synced/resumed");
                if (!SubmitGround(GroundPatch(_cfg.Terrain.MineralPit.Position[0], _cfg.Terrain.MineralPit.Position[1], -3))) return;
                break;
            case 3:
                GroundRequire(_groundVerified == 2, "pit version 2 synced");
                _groundTestMesh = region.MeshNode.Mesh; _groundTestShape = region.Collider.Shape;
                region.AfterMeshBoundForTest = () => throw new InvalidOperationException("synthetic main bind failure");
                try { GroundRequire(SubmitGround(GroundPatch(-20, -20, .6)), "fault still commits"); }
                finally { region.AfterMeshBoundForTest = null; }
                GroundRequire(region.Current.Version == 3 && region.ProjectionVersion == null && _groundVerified == null && _groundRobots.All(x => x.Paused), "authority survives fault, motion paused");
                GroundRequire(ReferenceEquals(_groundTestMesh, region.MeshNode.Mesh) && ReferenceEquals(_groundTestShape, region.Collider.Shape), "fault retains old native pair");
                break;
            case 4:
                GroundRequire(_groundFault && _groundVerified == null && _groundRobots.All(x => x.Paused), "fault cannot silently resume");
                region.AfterMeshBoundForTest = () => throw new InvalidOperationException("synthetic recovery bind failure");
                try
                {
                    RecoverGround();
                    GroundRequire(_groundFault && _groundMessage.Contains("synthetic recovery bind failure") && _groundRobots.All(x => x.Paused), "failed recovery remains paused and reports reason");
                }
                finally { region.AfterMeshBoundForTest = null; }
                RecoverGround();
                GroundRequire(region.Current.Version == 3 && _groundVerified == null && _groundRobots.All(x => x.Paused), "Current recovery still waits");
                break;
            case 5:
                GroundRequire(_groundVerified == 3 && !_groundFault && _groundRobots.All(x => !x.Paused), "recovery synced/resumed same version");
                GroundRequire(!GodotObject.IsInstanceValid(_groundTestMesh) && !GodotObject.IsInstanceValid(_groundTestShape), "old resources released on recovery");
                _groundTestTravel = _groundRobots.Sum(x => x.TravelledM); _groundTestVersion = region.Current.Version;
                break;
            case 6:
                if (_groundFrame < 180) return;
                GroundRequire(region.Current.Version == _groundTestVersion && _groundRobots.Sum(x => x.TravelledM) > _groundTestTravel + 1, "actual motion after changed terrain/recovery");
                GD.Print($"MAIN_GROUND_SELF_TEST PASS version={region.Current.Version} robots={_groundRobots.Count} travelled={_groundRobots.Sum(x => x.TravelledM):F3} grounded={_groundRobots.Count(x => x.IsOnFloor())}");
                GetTree().Quit(0); break;
        }
        GD.Print("MAIN_GROUND_TEST_STEP PASS " + _groundTestStep++);
    }
}
