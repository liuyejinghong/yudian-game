using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Yudian.Navigation;
using Yudian.Terrain;

namespace Navigation.Tests;

/// <summary>D1.1 导航子票合同检查：无框架 console 自检，逐项打印 PASS/FAIL。</summary>
internal static class NavigationContractTests
{
    public static int RunAll()
    {
        FlatRoute_Found_And_IndependentlyValidated();
        OnGridStart_Excluded_And_Destination_EndExact();
        Detour_Around_Circle_Found();
        NarrowGap_Pass_SmallBody_Reject_LargeBody();
        SteepRidge_Rejected_AtDefaultSlope();
        ModerateSlope_Passes_Default_Blocked_Tighter();
        BodyOverBoundary_Rejected_ClearMargin_Found();
        InputValidation_Negatives_And_NoSideEffects();
        Version_Follows_Snapshot_And_SameGeometry_SameRoute();
        Start_Or_Destination_Outside_Field_Unreachable();
        StartEqualsDestination_OnGrid();
        Deterministic_Repeat_SameResult();
        return Check.Failed;
    }

    // ---- 合同正例 ----

    private static void FlatRoute_Found_And_IndependentlyValidated()
    {
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d, version: 3);
        var start = new NavPoint(2.5, 2.5);      // 非格点
        var destination = new NavPoint(8.25, 8.75); // 非格点
        RouteResult result = BoundedRoute.Find(terrain, start, destination, 0.4, Array.Empty<NavObstacle>());

        Check.Ok("flat route found", () =>
        {
            Check.True(result.Found, $"expected Found, got false ({result.Reason})");
            Check.True(result.Points.Length > 0, "Points must not be empty on success");
            Check.True(result.WorldVersion == 3, $"WorldVersion {result.WorldVersion} != 3");
            Check.True(double.IsFinite(result.LengthM) && result.LengthM > 0, "LengthM must be finite and positive");
            Check.True(!PointEquals(result.Points[0], start), "Points must not start with start");
            Check.True(PointEquals(result.Points[^1], destination), "last point must be exactly destination");
            double straight = Distance2D(start, destination);
            Check.True(result.LengthM >= straight - 1e-9, $"LengthM {result.LengthM} below 2D chord {straight}");
            AssertSegmentsValid(terrain, start, result.Points, 0.4, Array.Empty<NavObstacle>(), 30, "flat");
        });
    }

    private static void OnGridStart_Excluded_And_Destination_EndExact()
    {
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d);
        var start = new NavPoint(3, 3);
        var destination = new NavPoint(7, 7);
        RouteResult result = BoundedRoute.Find(terrain, start, destination, 0.4, Array.Empty<NavObstacle>());

        Check.Ok("on-grid start excluded, end exact", () =>
        {
            Check.True(result.Found, $"expected Found, got false ({result.Reason})");
            Check.True(!PointEquals(result.Points[0], start), "on-grid start must not appear as first waypoint");
            Check.True(PointEquals(result.Points[^1], destination), "on-grid destination must end exactly");
            for (int i = 0; i + 1 < result.Points.Length; i++)
            {
                double d = Distance2D(result.Points[i], result.Points[i + 1]);
                Check.True(d <= Math.Sqrt(2) * terrain.SpacingM + 1e-9, $"waypoints {i}->{i + 1} not grid moves ({d})");
            }
        });
    }

    private static void Detour_Around_Circle_Found()
    {
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d);
        var obstacles = new[] { new NavObstacle(new NavPoint(5, 5), 1.5) };
        var start = new NavPoint(1, 5);
        var destination = new NavPoint(9, 5);
        RouteResult result = BoundedRoute.Find(terrain, start, destination, 0.5, obstacles);

        Check.Ok("detour around circle", () =>
        {
            Check.True(result.Found, $"expected Found, got false ({result.Reason})");
            Check.True(!PointEquals(result.Points[0], start), "Points must not start with start");
            Check.True(PointEquals(result.Points[^1], destination), "last point must be exactly destination");
            Check.True(result.LengthM > 8.5, $"route must be longer than the blocked 8m straight (got {result.LengthM})");
            Check.True(result.LengthM < 20, $"route unreasonably long: {result.LengthM}");
            AssertSegmentsValid(terrain, start, result.Points, 0.5, obstacles, 30, "detour");
        });
    }

    private static void NarrowGap_Pass_SmallBody_Reject_LargeBody()
    {
        TerrainSnapshot terrain = MakeSnapshot(13, 11, (_, _) => 0d);
        var obstacles = new[]
        {
            new NavObstacle(new NavPoint(5, 0), 2.0),
            new NavObstacle(new NavPoint(5, 6), 2.0),
            new NavObstacle(new NavPoint(5, 12), 2.0),
        };
        var start = new NavPoint(1.5, 1.5);
        var destination = new NavPoint(8.5, 10.5);

        RouteResult pass = BoundedRoute.Find(terrain, start, destination, 0.75, obstacles);
        Check.Ok("narrow gap passes with small body", () =>
        {
            Check.True(pass.Found, $"expected Found through the gap, got false ({pass.Reason})");
            Check.True(pass.Points.Length > 0, "Points must not be empty on success");
            AssertSegmentsValid(terrain, start, pass.Points, 0.75, obstacles, 30, "gap-pass");
        });

        RouteResult reject = BoundedRoute.Find(terrain, start, destination, 1.05, obstacles);
        Check.Ok("narrow gap rejected with large body", () =>
        {
            Check.True(!reject.Found, "expected rejection with inflated body 1.05");
            Check.True(!string.IsNullOrWhiteSpace(reject.Reason), "failure reason must be non-empty");
            Check.True(reject.Points.Length == 0, "Points must be empty on failure");
            Check.True(reject.LengthM == 0, "LengthM must be 0 on failure");
            Check.True(reject.WorldVersion == terrain.Version, "WorldVersion must still match snapshot");
        });
    }

    private static void SteepRidge_Rejected_AtDefaultSlope()
    {
        TerrainSnapshot terrain = MakeSnapshot(5, 8, (_, column) => column <= 3 ? 0d : 20d);
        var start = new NavPoint(1, 2);
        var destination = new NavPoint(6, 2);
        RouteResult result = BoundedRoute.Find(terrain, start, destination, 0.3, Array.Empty<NavObstacle>());

        Check.Ok("steep ridge rejected", () =>
        {
            Check.True(!result.Found, "20m wall must be untraversable at 30 degrees");
            Check.True(!string.IsNullOrWhiteSpace(result.Reason), "failure reason must be non-empty");
            Check.True(result.Points.Length == 0 && result.LengthM == 0, "failure must carry empty Points and LengthM=0");
            Check.True(result.WorldVersion == terrain.Version, "WorldVersion must still match snapshot");
        });
    }

    private static void ModerateSlope_Passes_Default_Blocked_Tighter()
    {
        // 0.5m 台阶（atan(0.5) ≈ 26.57°）：默认 30° 可过，收紧到 10° 全拒绝。
        // 端点距场界 1m > bodyRadius 0.3（场界收缩语义下合法）。
        TerrainSnapshot terrain = MakeSnapshot(5, 6, (_, column) => column <= 2 ? 0d : 0.5);
        var start = new NavPoint(1, 2);
        var destination = new NavPoint(4, 2);

        RouteResult pass = BoundedRoute.Find(terrain, start, destination, 0.3, Array.Empty<NavObstacle>());
        RouteResult blocked = BoundedRoute.Find(terrain, start, destination, 0.3, Array.Empty<NavObstacle>(), maxSlopeDegrees: 10);

        Check.Ok("moderate slope default passes", () =>
        {
            Check.True(pass.Found, $"0.5m step must pass at 30 degrees ({pass.Reason})");
            AssertSegmentsValid(terrain, start, pass.Points, 0.3, Array.Empty<NavObstacle>(), 30, "slope-pass");
        });
        Check.Ok("moderate slope blocked at 10 degrees", () =>
        {
            Check.True(!blocked.Found, "0.5m step must fail at 10 degrees");
            Check.True(blocked.Points.Length == 0 && blocked.LengthM == 0, "failure must carry empty Points and LengthM=0");
        });
    }

    private static void BodyOverBoundary_Rejected_ClearMargin_Found()
    {
        // 场界按 bodyRadius 收缩：中心在场内但距场界 < radius（身体越界）即不可达；
        // 对照正例端点距场界 > radius，且全程按收缩边界独立复核。
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d); // 场界 x,z ∈ [0,10]

        RouteResult startHugging = BoundedRoute.Find(
            terrain, new NavPoint(0.3, 5), new NavPoint(9, 5), 0.5, Array.Empty<NavObstacle>());
        Check.Ok("start within radius of boundary (body crosses) unreachable", () =>
        {
            Check.True(!startHugging.Found, "center 0.3m from west boundary with radius 0.5 must not route");
            Check.True(startHugging.Reason.Contains("起点"), $"reason must mention 起点: {startHugging.Reason}");
            Check.True(startHugging.Points.Length == 0 && startHugging.LengthM == 0, "failure invariants");
            Check.True(startHugging.WorldVersion == terrain.Version, "WorldVersion must still match snapshot");
        });

        RouteResult destHugging = BoundedRoute.Find(
            terrain, new NavPoint(5, 5), new NavPoint(5, 9.75), 0.5, Array.Empty<NavObstacle>());
        Check.Ok("destination within radius of boundary (body crosses) unreachable", () =>
        {
            Check.True(!destHugging.Found, "destination 0.25m from north boundary with radius 0.5 must not route");
            Check.True(destHugging.Reason.Contains("终点"), $"reason must mention 终点: {destHugging.Reason}");
            Check.True(destHugging.Points.Length == 0 && destHugging.LengthM == 0, "failure invariants");
        });

        var start = new NavPoint(1, 5);
        var destination = new NavPoint(9, 5);
        RouteResult clear = BoundedRoute.Find(terrain, start, destination, 0.5, Array.Empty<NavObstacle>());
        Check.Ok("endpoints farther than radius from boundary found", () =>
        {
            Check.True(clear.Found, $"expected Found with margin 1m > radius 0.5 ({clear.Reason})");
            Check.True(PointEquals(clear.Points[^1], destination), "last point must be exactly destination");
            AssertSegmentsValid(terrain, start, clear.Points, 0.5, Array.Empty<NavObstacle>(), 30, "body-margin");
        });
    }

    // ---- 输入校验（统一 ArgumentException，无副作用）----

    private static void InputValidation_Negatives_And_NoSideEffects()
    {
        TerrainSnapshot terrain = MakeSnapshot(4, 4, (_, _) => 0d);
        var origin = new NavPoint(1, 1);
        var valid = Array.Empty<NavObstacle>();

        Check.Throws("null terrain", "terrain", () =>
            BoundedRoute.Find(null!, origin, origin, 0.5, valid));
        Check.Throws("start X NaN", "start", () =>
            BoundedRoute.Find(terrain, new NavPoint(double.NaN, 1), origin, 0.5, valid));
        Check.Throws("start Z +inf", "start", () =>
            BoundedRoute.Find(terrain, new NavPoint(1, double.PositiveInfinity), origin, 0.5, valid));
        Check.Throws("destination NaN", "destination", () =>
            BoundedRoute.Find(terrain, origin, new NavPoint(double.NaN, 1), 0.5, valid));
        Check.Throws("destination -inf", "destination", () =>
            BoundedRoute.Find(terrain, origin, new NavPoint(1, double.NegativeInfinity), 0.5, valid));

        foreach (double radius in new[] { 0d, -0.5, double.NaN, double.PositiveInfinity })
        {
            double captured = radius;
            Check.Throws($"bodyRadius {captured}", "bodyRadius", () =>
                BoundedRoute.Find(terrain, origin, origin, captured, valid));
        }

        Check.Throws("null obstacles", "obstacles", () =>
            BoundedRoute.Find(terrain, origin, origin, 0.5, null!));
        Check.Throws("obstacle center NaN", "obstacles", () =>
            BoundedRoute.Find(terrain, origin, origin, 0.5, new[] { new NavObstacle(new NavPoint(double.NaN, 0), 1) }));
        Check.Throws("obstacle center +inf", "obstacles", () =>
            BoundedRoute.Find(terrain, origin, origin, 0.5, new[] { new NavObstacle(new NavPoint(0, double.PositiveInfinity), 1) }));
        foreach (double radius in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
        {
            double captured = radius;
            Check.Throws($"obstacle radius {captured}", "obstacles", () =>
                BoundedRoute.Find(terrain, origin, origin, 0.5, new[] { new NavObstacle(origin, captured) }));
        }

        foreach (double slope in new[] { 0d, -5d, 35.0001, double.NaN, double.PositiveInfinity })
        {
            double captured = slope;
            Check.Throws($"maxSlopeDegrees {captured}", "maxSlopeDegrees", () =>
                BoundedRoute.Find(terrain, origin, origin, 0.5, valid, captured));
        }

        Check.Ok("boundary maxSlopeDegrees=35 is legal and side-effect free", () =>
        {
            RouteResult result = BoundedRoute.Find(terrain, origin, new NavPoint(2, 2), 0.5, valid, 35);
            Check.True(result.Found, "flat 35-degree call must succeed");
            Check.True(terrain.Version == result.WorldVersion, "version must be unchanged");
            Check.True(terrain.GetHeight(0, 0) == 0 && terrain.HeightsM.Count == 16, "snapshot heights untouched");
        });
    }

    private static void Version_Follows_Snapshot_And_SameGeometry_SameRoute()
    {
        Func<int, int, double> flat = (_, _) => 0d;
        TerrainSnapshot v7 = MakeSnapshot(9, 9, flat, version: 7);
        TerrainSnapshot v42 = MakeSnapshot(9, 9, flat, version: 42);
        var start = new NavPoint(1.5, 1.5);
        var destination = new NavPoint(7.5, 7.5);

        RouteResult r7 = BoundedRoute.Find(v7, start, destination, 0.4, Array.Empty<NavObstacle>());
        RouteResult r42 = BoundedRoute.Find(v42, start, destination, 0.4, Array.Empty<NavObstacle>());

        Check.Ok("version follows snapshot, geometry identical", () =>
        {
            Check.True(r7.Found && r42.Found, "both flat calls must succeed");
            Check.True(r7.WorldVersion == 7, $"WorldVersion {r7.WorldVersion} != 7");
            Check.True(r42.WorldVersion == 42, $"WorldVersion {r42.WorldVersion} != 42");
            Check.True(r7.Points.Length == r42.Points.Length, "same geometry must yield same waypoint count");
            for (int i = 0; i < r7.Points.Length; i++)
                Check.True(PointEquals(r7.Points[i], r42.Points[i]), $"waypoint {i} differs between versions");
            Check.True(r7.LengthM == r42.LengthM, "same geometry must yield identical LengthM");
        });
    }

    private static void Start_Or_Destination_Outside_Field_Unreachable()
    {
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d);
        var inside = new NavPoint(5, 5);

        RouteResult startOutside = BoundedRoute.Find(terrain, new NavPoint(-3, 2), inside, 0.4, Array.Empty<NavObstacle>());
        Check.Ok("start outside field unreachable", () =>
        {
            Check.True(!startOutside.Found, "out-of-field start must not route");
            Check.True(startOutside.Reason.Contains("起点"), $"reason must mention 起点: {startOutside.Reason}");
            Check.True(startOutside.Points.Length == 0 && startOutside.LengthM == 0, "failure invariants");
            Check.True(startOutside.WorldVersion == terrain.Version, "WorldVersion must still match");
        });

        RouteResult destOutside = BoundedRoute.Find(terrain, inside, new NavPoint(50, 5), 0.4, Array.Empty<NavObstacle>());
        Check.Ok("destination outside field unreachable", () =>
        {
            Check.True(!destOutside.Found, "out-of-field destination must not route");
            Check.True(destOutside.Reason.Contains("终点"), $"reason must mention 终点: {destOutside.Reason}");
            Check.True(destOutside.Points.Length == 0 && destOutside.LengthM == 0, "failure invariants");
        });

        TerrainSnapshot enclosed = MakeSnapshot(11, 11, (_, _) => 0d);
        var ring = new[] { new NavObstacle(new NavPoint(5, 5), 2.0) };
        RouteResult destInsideObstacle = BoundedRoute.Find(enclosed, new NavPoint(1, 1), new NavPoint(5, 5), 0.5, ring);
        Check.Ok("destination inside obstacle unreachable", () =>
        {
            Check.True(!destInsideObstacle.Found, "destination inside inflated circle must not route");
            Check.True(destInsideObstacle.Reason.Contains("终点"), $"reason must mention 终点: {destInsideObstacle.Reason}");
        });
    }

    private static void StartEqualsDestination_OnGrid()
    {
        TerrainSnapshot terrain = MakeSnapshot(9, 9, (_, _) => 0d);
        var same = new NavPoint(3, 3);
        RouteResult result = BoundedRoute.Find(terrain, same, same, 0.4, Array.Empty<NavObstacle>());

        Check.Ok("start equals destination on grid", () =>
        {
            Check.True(result.Found, $"trivial route must be found ({result.Reason})");
            Check.True(result.Points.Length == 1, $"expected single destination waypoint, got {result.Points.Length}");
            Check.True(PointEquals(result.Points[0], same), "waypoint must be exactly the destination");
            Check.True(result.LengthM == 0, "zero route must have zero length");
        });
    }

    private static void Deterministic_Repeat_SameResult()
    {
        TerrainSnapshot terrain = MakeSnapshot(11, 11, (_, _) => 0d);
        var obstacles = new[] { new NavObstacle(new NavPoint(5, 5), 1.5) };
        var start = new NavPoint(1, 5);
        var destination = new NavPoint(9, 5);

        RouteResult first = BoundedRoute.Find(terrain, start, destination, 0.5, obstacles);
        RouteResult second = BoundedRoute.Find(terrain, start, destination, 0.5, obstacles);

        Check.Ok("deterministic repeat", () =>
        {
            Check.True(first.Found && second.Found, "both calls must succeed");
            Check.True(first.Points.Length == second.Points.Length, "waypoint count must match");
            for (int i = 0; i < first.Points.Length; i++)
                Check.True(PointEquals(first.Points[i], second.Points[i]), $"waypoint {i} must match on repeat");
            Check.True(first.LengthM == second.LengthM, "LengthM must match on repeat");
            Check.True(first.Reason == second.Reason, "Reason must match on repeat");
        });
    }

    // ---- 独立段校验器：与实现分开重写的采样/插值，只用于复核成功结果 ----

    private static void AssertSegmentsValid(
        TerrainSnapshot terrain,
        NavPoint start,
        NavPoint[] points,
        double bodyRadius,
        NavObstacle[] obstacles,
        double maxSlope,
        string label)
    {
        var full = new List<NavPoint>(points.Length + 1) { start };
        full.AddRange(points);

        double originX = terrain.OriginXM;
        double originZ = terrain.OriginZM;
        double spacing = terrain.SpacingM;
        double endX = originX + (terrain.Columns - 1) * spacing;
        double endZ = originZ + (terrain.Rows - 1) * spacing;
        // 收缩场界：中心距任一场界至少 bodyRadius，整个圆代理在场内。
        double minX = originX + bodyRadius, maxX = endX - bodyRadius;
        double minZ = originZ + bodyRadius, maxZ = endZ - bodyRadius;

        for (int segment = 0; segment + 1 < full.Count; segment++)
        {
            (double x1, double z1) = full[segment];
            (double x2, double z2) = full[segment + 1];
            double dx = x2 - x1;
            double dz = z2 - z1;
            double length = Math.Sqrt(dx * dx + dz * dz);
            int samples = Math.Max(1, (int)Math.Ceiling(length / (spacing / 4.0)));
            double previousHeight = double.NaN;

            for (int i = 0; i <= samples; i++)
            {
                double t = (double)i / samples;
                double x = x1 + dx * t;
                double z = z1 + dz * t;
                Check.True(x >= minX - 1e-9 && x <= maxX + 1e-9 && z >= minZ - 1e-9 && z <= maxZ + 1e-9,
                    $"{label} segment {segment} sample {i} leaves the body-radius shrunk field");
                foreach (NavObstacle obstacle in obstacles)
                {
                    double ex = x - obstacle.Center.X;
                    double ez = z - obstacle.Center.Z;
                    double inflated = obstacle.Radius + bodyRadius;
                    Check.True(ex * ex + ez * ez > inflated * inflated,
                        $"{label} segment {segment} sample {i} inside inflated obstacle");
                }
                double height = IndependentHeight(terrain, x, z);
                if (i > 0)
                {
                    double slope = Math.Atan2(Math.Abs(height - previousHeight), length / samples) * 180.0 / Math.PI;
                    Check.True(slope <= maxSlope + 1e-9,
                        $"{label} segment {segment} sample {i} slope {slope} exceeds {maxSlope}");
                }
                previousHeight = height;
            }

            foreach (NavObstacle obstacle in obstacles)
            {
                double distanceSquared = PointSegmentDistanceSquared(
                    obstacle.Center.X, obstacle.Center.Z, x1, z1, x2, z2);
                double inflated = obstacle.Radius + bodyRadius;
                Check.True(distanceSquared >= inflated * inflated,
                    $"{label} segment {segment} clips inflated obstacle between samples");
            }
        }
    }

    /// <summary>独立重写的 a-d 三角插值（a=左上，三角 a-d-b / a-c-d），用于交叉复核。</summary>
    private static double IndependentHeight(TerrainSnapshot terrain, double x, double z)
    {
        double spacing = terrain.SpacingM;
        double fx = (x - terrain.OriginXM) / spacing;
        double fz = (z - terrain.OriginZM) / spacing;
        int column = Math.Clamp((int)Math.Floor(fx), 0, terrain.Columns - 2);
        int row = Math.Clamp((int)Math.Floor(fz), 0, terrain.Rows - 2);
        double u = fx - column;
        double v = fz - row;
        double ha = terrain.GetHeight(row, column);
        double hb = terrain.GetHeight(row, column + 1);
        double hc = terrain.GetHeight(row + 1, column);
        double hd = terrain.GetHeight(row + 1, column + 1);
        return u >= v
            ? (1 - u) * ha + (u - v) * hb + v * hd
            : (1 - v) * ha + (v - u) * hc + u * hd;
    }

    private static double PointSegmentDistanceSquared(double px, double pz, double x1, double z1, double x2, double z2)
    {
        double dx = x2 - x1;
        double dz = z2 - z1;
        double lengthSquared = dx * dx + dz * dz;
        double t = lengthSquared == 0 ? 0 : Math.Clamp(((px - x1) * dx + (pz - z1) * dz) / lengthSquared, 0, 1);
        double nearestX = x1 + dx * t;
        double nearestZ = z1 + dz * t;
        double ex = px - nearestX;
        double ez = pz - nearestZ;
        return ex * ex + ez * ez;
    }

    private static bool PointEquals(NavPoint left, NavPoint right)
        => left.X == right.X && left.Z == right.Z;

    private static double Distance2D(NavPoint a, NavPoint b)
    {
        double dx = b.X - a.X;
        double dz = b.Z - a.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>经 TerrainDataCodec 构造真实不可变快照（合同禁用其他构造入口）。</summary>
    private static TerrainSnapshot MakeSnapshot(
        int rows,
        int columns,
        Func<int, int, double> height,
        double spacing = 1.0,
        long version = 1)
    {
        var heights = new StringBuilder();
        for (int row = 0; row < rows; row++)
        {
            if (row > 0)
                heights.Append(',');
            for (int column = 0; column < columns; column++)
            {
                if (column > 0)
                    heights.Append(',');
                heights.Append(height(row, column).ToString(CultureInfo.InvariantCulture));
            }
        }

        string json = "{\"schema_version\":1,\"region_id\":\"nav-test\",\"version\":"
            + version.ToString(CultureInfo.InvariantCulture)
            + ",\"origin_x_m\":0,\"origin_z_m\":0,\"spacing_m\":"
            + spacing.ToString(CultureInfo.InvariantCulture)
            + ",\"rows\":" + rows.ToString(CultureInfo.InvariantCulture)
            + ",\"columns\":" + columns.ToString(CultureInfo.InvariantCulture)
            + ",\"heights_m\":[" + heights + "]}";
        return TerrainDataCodec.ParseSnapshot(json);
    }
}

/// <summary>最小检查器：非 0 退出码表示存在失败。</summary>
internal static class Check
{
    public static int Failed;

    public static void Ok(string name, Action check)
    {
        try
        {
            check();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            Failed++;
            Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>断言调用抛出精确 ArgumentException（合同统一异常类型），且消息包含指定参数名。</summary>
    public static void Throws(string name, string messageContains, Action call)
    {
        Ok(name, () =>
        {
            try
            {
                call();
            }
            catch (ArgumentException ex)
            {
                if (ex.GetType() != typeof(ArgumentException))
                    throw new Exception($"expected exact ArgumentException, got {ex.GetType().Name}");
                if (!ex.Message.Contains(messageContains, StringComparison.Ordinal))
                    throw new Exception($"message '{ex.Message}' does not contain '{messageContains}'");
                return;
            }
            throw new Exception("expected ArgumentException, but no exception was thrown");
        });
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
