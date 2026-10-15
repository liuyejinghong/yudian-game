using System;
using System.Linq;
using System.Text.Json;
using Yudian.Navigation;
using Yudian.Terrain;

namespace Navigation.Tests;

internal static class StationCandidateContractTests
{
    public static void RunAll()
    {
        var terrain = FlatTerrain(41);
        Check.Ok("station defaults keep exact original axes and first reachable axis", () =>
        {
            var original = StationCandidates.Enumerate(terrain, 20, 20, 5.5f).ToArray();
            Check.True(original.Select(c => c.Point).SequenceEqual(new NavPoint[]
                { new(25.5, 20), new(14.5, 20), new(20, 25.5), new(20, 14.5) }), "original offsets/order changed");
            Check.True(original.Select(c => c.Axis).SequenceEqual(new[] { "+X", "-X", "+Z", "-Z" }), "axis labels changed");
            var obstacle = new[] { new NavObstacle(original[0].Point, .5) };
            var start = new NavPoint(8, 20);
            var selected = StationCandidates.Enumerate(terrain, 20, 20, 5.5f, 6.175f)
                .First(c => BoundedRoute.FindWithTerrainMargin(terrain, start, c.Point, 1.4f, obstacle, .25f).Found);
            Check.True(selected == original[1], "fallback preempted the first reachable original axis");
        });
        Check.Ok("station fallback finite deterministic unique and capped after float conversion", () =>
        {
            var first = StationCandidates.Enumerate(terrain, 20.13f, 20.37f, 5.5f, 6.175f).ToArray();
            var repeated = StationCandidates.Enumerate(terrain, 20.13f, 20.37f, 5.5f, 6.175f).ToArray();
            Check.True(first.Length == 64 && first.Select(c => c.Point).Distinct().Count() == 64, "expected two unique 32-direction rings");
            Check.True(first.SequenceEqual(repeated), "candidate order/metadata changed on repeat");
            Check.True(first.Take(4).All(c => c.Kind == "original-axis") && first.Skip(4).Take(28).All(c => c.Kind == "same-radius")
                && first.Skip(32).All(c => c.Kind == "construction-edge"), "fallback ring order changed");
            Check.True(first.All(c => Distance(c.Point, new(20.13f, 20.37f)) <= 6.175f), "float endpoint exceeded construction edge");
            Check.True(first.Skip(4).All(c => Distance(c.Point, new(20.13f, 20.37f)) <= c.RadiusM), "float fallback endpoint exceeded its own ring");
            Check.True(first.All(c => (float)c.Point.X == c.Point.X && (float)c.Point.Z == c.Point.Z), "Main float conversion changed endpoint");
        });
        Check.Ok("station no additional ring at or below original radius", () =>
        {
            foreach (float edge in new[] { 5.5f, 5f })
            {
                var candidates = StationCandidates.Enumerate(terrain, 20, 20, 5.5f, edge).ToArray();
                Check.True(candidates.Length == 32 && candidates.All(c => c.RingIndex == 0), "redundant or smaller outer ring added");
            }
        });
        Check.Ok("station field clipping never clamps illegal endpoint into a legal one", () =>
        {
            var nearEdge = StationCandidates.Enumerate(terrain, 1, 20, 5.5f, 6.175f).ToArray();
            Check.True(nearEdge.Length < 64 && nearEdge.All(c => c.Point.X >= 0 && c.Point.X <= 40 && c.Point.Z >= 0 && c.Point.Z <= 40), "endpoint outside field");
            Check.True(!nearEdge.Any(c => c.Point.X == 0), "out-of-field endpoint clamped to boundary");
            var bodyCrossing = StationCandidates.Enumerate(terrain, 6.5f, 20, 5.5f).Single(c => c.Axis == "-X");
            var rejected = BoundedRoute.FindWithTerrainMargin(terrain, new(12, 20), bodyCrossing.Point, 1.4f, Array.Empty<NavObstacle>(), .25f);
            Check.True(!rejected.Found, "body-crossing field endpoint bypassed strict route rejection");
        });
    }

    private static TerrainSnapshot FlatTerrain(int size) => TerrainDataCodec.ParseSnapshot(JsonSerializer.Serialize(new
    {
        schema_version = 1, region_id = "station-test", version = 1, origin_x_m = 0, origin_z_m = 0,
        spacing_m = 1, rows = size, columns = size, heights_m = new double[size * size]
    }));
    private static double Distance(NavPoint a, NavPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
}
