#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Yudian.Navigation;
using Yudian.Terrain;

namespace Yudian;

public partial class Main
{
    private string? _deliveryDiagnosticSignature;
    private ulong _deliveryDiagnosticFrame;
    private sealed record DeliveryCandidateDiagnostic(Vector3 Station, RouteResult Route, StationCandidates.Candidate Candidate);
    private sealed record DeliveryObstacleDiagnostic(string Id, string Kind, string Type, NavObstacle Geometry);

    // Only the normal UI's private acceptance world may emit this evidence. No path is logged.
    private bool DeliveryDiagnosticsEnabled
    {
        get
        {
            if (!TutorialEnabled || System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") != "1") return false;
            string? root = System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_TEST_ROOT");
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return false;
            // Reuse the lifecycle's existing private-vs-formal save-root validation.
            return Path.GetFullPath(root) == TutorialSaveRoot;
        }
    }

    private List<DeliveryCandidateDiagnostic>? BeginDeliveryStationDiagnostics(BaseFacility facility, GroundPatrol actor, TerrainPatch? excluded)
        => excluded == null && _buildJob is {} job && facility.Id == job.Facility && actor.Name.ToString() == job.Hauler
            && DeliveryDiagnosticsEnabled ? new(4) : null;

    private void CompleteDeliveryStationDiagnostics(BaseFacility facility, GroundPatrol actor, List<DeliveryCandidateDiagnostic>? candidates)
    {
        if (candidates == null || _buildJob is not {} job) return;
        ulong frame = Engine.GetPhysicsFrames();
        // Gate before obstacle/signature construction, including the bounded fallback candidate list.
        if (_deliveryDiagnosticSignature != null && frame - _deliveryDiagnosticFrame < (ulong)Math.Max(1, Engine.PhysicsTicksPerSecond)) return;
        _deliveryDiagnosticFrame = frame;
        string actorId = actor.Name.ToString();
        var terrain = _liveTerrain!.Current;
        var start = actor.GlobalPosition;
        var obstacles = RouteObstacles(actorId);
        // Keep names in the exact facility-then-robot order of RouteObstacles (includeSites=true).
        var named = _baseFacilities.Where(f => !(!f.Built && f.Id == _buildJob?.Facility && _buildJob.Stage == "Preparing" && BuildClearingActor(actorId)))
            .Select(f => (Id: f.Id, Kind: "facility", Type: f.Type))
            .Concat(_groundRobots.Where(a => a.Name.ToString() != actorId)
                .Select(a => (Id: a.Name.ToString(), Kind: "robot", Type: RobotDefinitionId(a.Name.ToString())))).ToArray();
        var identified = obstacles.Select((obstacle, index) => new DeliveryObstacleDiagnostic(named[index].Id, named[index].Kind, named[index].Type, obstacle)).ToArray();
        // Ignore vertical settling noise and sub-centimetre XZ motion, while retaining exact coordinates in evidence.
        var signature = JsonSerializer.Serialize(new
        {
            _worldEpoch, _intentVersion, terrain.Version, _baseRevision, Build = job.Id, job.Stage, job.Trip, Goal = _development?.Id,
            Facility = facility.Id, Actor = actorId, actor.BodyRadius, Start = SignaturePoint(start.X, start.Z),
            Obstacles = identified.Select(o => new { o.Id, o.Geometry.Radius, Point = SignaturePoint(o.Geometry.Center.X, o.Geometry.Center.Z) }).ToArray(),
            Candidates = candidates.Select(c => new { Point = SignaturePoint(c.Station.X, c.Station.Z), c.Route.Found, c.Route.Reason,
                c.Route.WorldVersion, LengthM = Math.Round(c.Route.LengthM, 3) }).ToArray()
        });
        if (_deliveryDiagnosticSignature == signature) return;
        _deliveryDiagnosticSignature = signature;

        bool startFootprintClear = BoundedRoute.FootprintClear(terrain, new(start.X, start.Z), RouteFootprint(actor));
        var startConnections = BoundedRoute.DiagnoseConnections(terrain, new(start.X, start.Z), actor.BodyRadius,
            obstacles, GroundPatrol.WaypointReachXzM, isStart: true);
        GD.Print("DELIVERY_STATION_SET " + JsonSerializer.Serialize(new
        {
            Actor = actorId, Name = RobotName(actorId), Role = RobotDefinitionId(actorId), RoleText = RobotRole(actorId),
            BodyRadiusM = actor.BodyRadius, Start = DiagnosticPosition(start), actor.HasOrder,
            OnFloor = actor.IsOnFloor(), Operational = Operational(actor), StartFootprintClear = startFootprintClear,
            RouteSlopeLimitDeg = 30, CapsuleFloorLimitDeg = GroundPatrol.MaxFloorAngleDeg,
            TerrainMarginM = GroundPatrol.WaypointReachXzM, TerrainFootprintRadiusM = RouteFootprint(actor),
            TerrainVersion = terrain.Version, BaseRevision = _baseRevision, WorldEpoch = _worldEpoch,
            Facility = facility.Id, FacilityType = facility.Type, FacilityBuilt = facility.Built, FacilityCenter = DiagnosticPosition(LoadVector(facility.Position)),
            Build = job.Id, BuildStage = job.Stage, Goal = _development?.Id, GoalStage = _development?.Stage,
            job.Builder, job.Hauler, job.Trip, EvaluatedCandidates = candidates.Count,
            AllFourEvaluated = candidates.Count(c => c.Candidate.Kind == "original-axis") == 4,
            SelectedIndex = candidates.FindIndex(c => c.Route.Found),
            StartConnections = startConnections.Select(c => ConnectionEvidence(c, identified)).ToArray()
        }));
        for (int index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var endpoint = new NavPoint(candidate.Station.X, candidate.Station.Z);
            var connections = BoundedRoute.DiagnoseConnections(terrain, endpoint, actor.BodyRadius,
                obstacles, GroundPatrol.WaypointReachXzM, isStart: false);
            GD.Print("DELIVERY_STATION_CANDIDATE " + JsonSerializer.Serialize(new
            {
                Actor = actorId, Facility = facility.Id, Build = job.Id, BuildStage = job.Stage, Goal = _development?.Id,
                TerrainVersion = terrain.Version, BaseRevision = _baseRevision, Index = index, candidate.Candidate.Axis,
                candidate.Candidate.Kind, candidate.Candidate.RingIndex, candidate.Candidate.DirectionIndex,
                candidate.Candidate.RadiusM, candidate.Candidate.AngleDegrees,
                Station = DiagnosticPosition(candidate.Station), GroundHeightM = SavedGroundHeight(terrain, candidate.Station.X, candidate.Station.Z),
                candidate.Route.Found, candidate.Route.Reason, candidate.Route.WorldVersion, candidate.Route.LengthM,
                EndpointFootprintClear = BoundedRoute.FootprintClear(terrain, endpoint, RouteFootprint(actor)),
                Obstacles = identified.Select(o => new
                {
                    o.Id, o.Kind, o.Type, o.Geometry.Center, ObstacleRadiusM = o.Geometry.Radius,
                    InflatedRadiusM = o.Geometry.Radius + actor.BodyRadius,
                    EndpointDistanceM = Math.Sqrt(Math.Pow(endpoint.X - o.Geometry.Center.X, 2) + Math.Pow(endpoint.Z - o.Geometry.Center.Z, 2))
                }).ToArray(),
                Connections = connections.Select(c => ConnectionEvidence(c, identified)).ToArray()
            }));
        }
    }

    private static object ConnectionEvidence(BoundedRoute.ConnectionDiagnostic connection, DeliveryObstacleDiagnostic[] obstacles)
        => new
        {
            connection.Row, connection.Column, connection.Node, connection.Clear, connection.FirstReject,
            ObstacleId = connection.FirstReject?.ObstacleIndex is int index ? obstacles[index].Id : null
        };
    private static object DiagnosticPosition(Vector3 point) => new { point.X, point.Y, point.Z };
    private static object SignaturePoint(double x, double z) => new { X = Math.Round(x, 2), Z = Math.Round(z, 2) };
}
