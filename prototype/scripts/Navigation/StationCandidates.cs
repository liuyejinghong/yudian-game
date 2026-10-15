#nullable enable
using System;
using System.Collections.Generic;
using Yudian.Terrain;

namespace Yudian.Navigation;

// Finite, read-only endpoints. The caller must still validate every route and actual arrival.
internal static class StationCandidates
{
    internal const int Directions = 32;
    internal readonly record struct Candidate(NavPoint Point, string Kind, int RingIndex, int DirectionIndex, float RadiusM)
    {
        public double AngleDegrees => DirectionIndex * 360d / Directions;
        public string? Axis => DirectionIndex switch { 0 => "+X", 16 => "-X", 8 => "+Z", 24 => "-Z", _ => null };
    }

    // A construction radius opts in only the unfinished site's unloading call. Other docks keep four axes.
    internal static IEnumerable<Candidate> Enumerate(TerrainSnapshot terrain, float centerX, float centerZ,
        float radius, float? constructionRadius = null)
    {
        var seen = new HashSet<NavPoint>();
        foreach (int direction in new[] { 0, 16, 8, 24 })
        {
            var point = direction switch
            {
                0 => new NavPoint(centerX + radius, centerZ),
                16 => new NavPoint(centerX - radius, centerZ),
                8 => new NavPoint(centerX, centerZ + radius),
                _ => new NavPoint(centerX, centerZ - radius)
            };
            if (InField(terrain, point) && seen.Add(point)) yield return new(point, "original-axis", 0, direction, radius);
        }
        if (constructionRadius is not {} edge) yield break;
        for (int ring = 0; ring < (edge > radius ? 2 : 1); ring++)
        {
            float distance = ring == 0 ? radius : edge;
            for (int direction = 0; direction < Directions; direction++)
            {
                if (ring == 0 && direction % 8 == 0) continue;
                double angle = direction * 2 * Math.PI / Directions;
                // Exact axes avoid trigonometric noise; float endpoints round inward, never beyond the ring.
                double dx = direction switch { 0 => distance, 16 => -distance, 8 or 24 => 0, _ => distance * Math.Cos(angle) };
                double dz = direction switch { 8 => distance, 24 => -distance, 0 or 16 => 0, _ => distance * Math.Sin(angle) };
                var point = new NavPoint(InwardCoordinate(centerX, dx), InwardCoordinate(centerZ, dz));
                if (InField(terrain, point) && seen.Add(point))
                    yield return new(point, ring == 0 ? "same-radius" : "construction-edge", ring, direction, distance);
            }
        }
    }

    private static float InwardCoordinate(float center, double offset)
    {
        float point = (float)(center + offset);
        if (Math.Abs((double)point - center) > Math.Abs(offset))
            point = offset > 0 ? MathF.BitDecrement(point) : MathF.BitIncrement(point);
        return point;
    }

    private static bool InField(TerrainSnapshot terrain, NavPoint point)
        => point.X >= terrain.OriginXM && point.X <= terrain.OriginXM + (terrain.Columns - 1) * terrain.SpacingM
            && point.Z >= terrain.OriginZM && point.Z <= terrain.OriginZM + (terrain.Rows - 1) * terrain.SpacingM;
}
