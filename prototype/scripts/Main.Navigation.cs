#nullable enable
using System;
using System.Linq;
using Godot;
using Yudian.Navigation;
using Yudian.Terrain;
namespace Yudian;

public partial class Main
{
    // GroundPatrol may turn .25m before a waypoint. Keep that entire possible
    // corridor inside safe terrain/obstacle geometry, without changing body physics.
    private float RouteFootprint(GroundPatrol actor) => actor.BodyRadius + GroundPatrol.WaypointReachXzM;
    private RouteResult StrictRoute(GroundPatrol actor, Vector3 start, Vector3 destination)
        => BoundedRoute.FindWithTerrainMargin(_liveTerrain!.Current, new(start.X, start.Z), new(destination.X, destination.Z), actor.BodyRadius, RouteObstacles(actor.Name.ToString()), GroundPatrol.WaypointReachXzM);
    private bool NativeEscapeSweep(GroundPatrol actor, Vector3 motion)
    {
        var terrain = _liveTerrain!.Current;
        var start = actor.GlobalPosition; var prediction = actor.GlobalTransform;
        var horizontal = new Vector2(motion.X, motion.Z);
        if (horizontal.Length() < .1f) return false;
        int segments = Math.Max(1, (int)Math.Ceiling(horizontal.Length() / (terrain.SpacingM / 4)));
        double floorOffset = start.Y - SavedGroundHeight(terrain, start.X, start.Z);
        float floorCosine = Mathf.Cos(Mathf.DegToRad(GroundPatrol.MaxFloorAngleDeg));
        bool diagnose = TutorialTest && TutorialTestPhase is "recovery-retry" or "escape-probe" && _tutorialTestStep is 205 or 208;
        for (int segment = 1; segment <= segments; segment++)
        {
            var desired = start + new Vector3(motion.X, 0, motion.Z) * (segment / (float)segments);
            desired.Y = (float)(SavedGroundHeight(terrain, desired.X, desired.Z) + floorOffset);
            var remaining = desired - prediction.Origin;
            for (int slide = 0; slide < 4 && remaining.LengthSquared() > .0000001f; slide++)
            {
                using var parameters = new PhysicsTestMotionParameters3D { From = prediction, Motion = remaining, Margin = actor.SafeMargin, MaxCollisions = 8, RecoveryAsCollision = false };
                using var result = new PhysicsTestMotionResult3D();
                bool collision = PhysicsServer3D.BodyTestMotion(actor.GetRid(), parameters, result);
                var travel = result.GetTravel(); prediction.Origin += travel;
                if (!collision) { remaining = Vector3.Zero; break; }
                var rest = result.GetRemainder(); bool terrainFloor = false;
                for (int index = 0; index < result.GetCollisionCount(); index++)
                {
                    var normal = result.GetCollisionNormal(index);
                    bool floor = result.GetColliderRid(index) == _liveTerrain.Body.GetRid() && normal.Y >= floorCosine;
                    if (diagnose) GD.Print($"NAV_ESCAPE_SLIDE_QUERY segment={segment} slide={slide} normal={normal} floor={floor} rid={result.GetColliderRid(index)} terrain={_liveTerrain.Body.GetRid()} travel={travel} remaining={rest}");
                    if (!floor)
                    {
                        // Departing an existing steep contact may be reported by recovery.
                        // Only motion strictly away from that identified terrain wall can continue.
                        var away = new Vector3(normal.X, 0, normal.Z);
                        if (result.GetColliderRid(index) != _liveTerrain.Body.GetRid() || rest.Dot(away) < -.0001f) return false;
                        continue;
                    }
                    terrainFloor = true; rest = rest.Slide(normal);
                }
                if (!terrainFloor || rest.LengthSquared() >= remaining.LengthSquared() && travel.LengthSquared() < .0000001f) return false;
                remaining = rest;
            }
            if (XzDistance(prediction.Origin, desired) > .035f) return false;
        }
        var predicted = prediction.Origin;
        bool supported;
        using (var query = PhysicsRayQueryParameters3D.Create(predicted + Vector3.Up * .15f, predicted + Vector3.Down * .65f, 1))
        using (var hit = GetWorld3D().DirectSpaceState.IntersectRay(query))
            supported = hit.Count > 0 && hit["collider"].AsGodotObject() == _liveTerrain.Body && hit["normal"].AsVector3().Y >= floorCosine;
        bool success = supported && new Vector2(predicted.X - start.X, predicted.Z - start.Z).Dot(horizontal.Normalized()) >= horizontal.Length() - .035f;
        if (diagnose) GD.Print($"NAV_ESCAPE_PROJECTED start={start} motion={motion} predicted={predicted} ground_y={SavedGroundHeight(terrain, predicted.X, predicted.Z)} supported={supported} success={success}");
        return success;
    }
    private (RouteResult Route, Vector3? Escape) PlanBaseRoute(GroundPatrol actor, Vector3 destination)
    {
        var strict = StrictRoute(actor, actor.GlobalPosition, destination);
        if (strict.Found) return (strict, null);
        var terrain = _liveTerrain!.Current; var position = actor.GlobalPosition;
        if (!Operational(actor) || !actor.IsOnFloor() || BoundedRoute.FootprintClear(terrain, new(position.X, position.Z), RouteFootprint(actor))) return (strict, null);
        // A pure native query includes existing contact even after save/reload, where
        // CharacterBody's last-frame wall list has not yet been repopulated.
        using var contactParameters = new PhysicsTestMotionParameters3D { From = actor.GlobalTransform, Motion = Vector3.Down * .02f, Margin = actor.SafeMargin + .01f, MaxCollisions = 8, RecoveryAsCollision = true };
        using var contact = new PhysicsTestMotionResult3D();
        PhysicsServer3D.BodyTestMotion(actor.GetRid(), contactParameters, contact);
        var away = Vector3.Zero;
        for (int index = 0; index < contact.GetCollisionCount(); index++)
        {
            var normal = contact.GetCollisionNormal(index);
            if (normal.Y >= Mathf.Cos(Mathf.DegToRad(GroundPatrol.MaxFloorAngleDeg)) || contact.GetCollider(index) != _liveTerrain.Body) continue;
            away = normal; break;
        }
        bool diagnose = TutorialTest && TutorialTestPhase == "recovery-retry" && _tutorialTestStep == 205;
        if (diagnose)
        {
            GD.Print($"NAV_ESCAPE_CONTACT floor={actor.IsOnFloor()} operational={Operational(actor)} ready={ReadPlayerState().Ready} collisions={contact.GetCollisionCount()} slides={actor.GetSlideCollisionCount()}");
            for (int index = 0; index < contact.GetCollisionCount(); index++) GD.Print($"NAV_ESCAPE_CONTACT_ITEM collider={contact.GetColliderRid(index)} terrain={_liveTerrain.Body.GetRid()} normal={contact.GetCollisionNormal(index)} travel={contact.GetTravel()}");
        }
        if (away == Vector3.Zero)
            for (int index = 0; index < actor.GetSlideCollisionCount(); index++)
            {
                using var collision = actor.GetSlideCollision(index);
                if (collision.GetColliderRid() != _liveTerrain.Body.GetRid()) continue;
                var normal = collision.GetNormal();
                if (normal.Y >= Mathf.Cos(Mathf.DegToRad(GroundPatrol.MaxFloorAngleDeg))) continue;
                if (diagnose) GD.Print($"NAV_ESCAPE_SLIDE collider={collision.GetColliderRid()} terrain={_liveTerrain.Body.GetRid()} normal={normal}");
                away = normal; break;
            }
        away.Y = 0;
        if (away.LengthSquared() < .01f) return (strict, null);
        away = away.Normalized();
        float maximumDistance = actor.BodyRadius * 3 + GroundPatrol.WaypointReachXzM;
        for (float distance = .5f; distance <= maximumDistance + .001f; distance += .25f)
            foreach (float angle in new[] { 0f, -.35f, .35f, -.7f, .7f, -1.05f, 1.05f, -1.3962634f, 1.3962634f, -1.4835299f, 1.4835299f, -Mathf.Pi / 2, Mathf.Pi / 2 })
            {
                var direction = away.Rotated(Vector3.Up, angle);
                if (direction.Dot(away) < -.00001f) continue;
                var escape = position + direction * distance;
                if (Math.Abs(escape.X) + RouteFootprint(actor) >= _cfg.Terrain.Size / 2 || Math.Abs(escape.Z) + RouteFootprint(actor) >= _cfg.Terrain.Size / 2) continue;
                escape.Y = (float)SavedGroundHeight(terrain, escape.X, escape.Z);
                var route = StrictRoute(actor, escape, destination);
                bool movable = route.Found && NativeEscapeSweep(actor, direction * distance);
                if (diagnose) GD.Print($"NAV_ESCAPE_CANDIDATE angle={angle} distance={distance} point={escape} post_route={route.Found}/{route.Reason} native={movable}");
                if (!movable) continue;
                return (route with { Reason = "原位有限退让后严格足迹路线", Points = new[] { new NavPoint(escape.X, escape.Z) }.Concat(route.Points).ToArray(), LengthM = route.LengthM + distance }, escape);
            }
        return (strict, null);
    }
    private void AssignPlannedRoute(GroundPatrol actor, Vector3 destination, RouteResult route, Vector3? escape)
    {
        actor.SetRoute(route.Points.Select(p => new Vector3((float)p.X, (float)SavedGroundHeight(_liveTerrain!.Current, (float)p.X, (float)p.Z), (float)p.Z)).ToArray());
        _routes[actor.Name.ToString()] = new() { Destination = destination, WorldVersion = route.WorldVersion, FacilityRevision = _baseRevision,
            Escaping = escape != null, EscapeDestination = escape ?? Vector3.Zero };
        if (escape != null) _health[actor.Name.ToString()].Reason = "按原生胶囊查询有限退让，货物与原目的地保留";
    }
}
