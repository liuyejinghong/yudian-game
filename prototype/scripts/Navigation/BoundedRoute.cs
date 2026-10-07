using System;
using System.Collections.Generic;
using Yudian.Terrain;

namespace Yudian.Navigation;

/// <summary>导航平面点（世界 XZ 坐标，米）。</summary>
public readonly record struct NavPoint(double X, double Z);

/// <summary>圆形障碍代理。机器人自身不在此列，由调用者提供并排除。</summary>
public readonly record struct NavObstacle(NavPoint Center, double Radius);

/// <summary>
/// 路线结果。失败时 Points 为空数组且 LengthM=0；成功时 Points 不含 start、
/// 末端严格为 destination，LengthM 为从 start 起的全程三维折线长度。
/// WorldVersion 恒等于输入快照版本。
/// </summary>
public sealed record RouteResult(bool Found, string Reason, long WorldVersion, NavPoint[] Points, double LengthM);

/// <summary>
/// D1.1 导航子票冻结实现：现有高度网格上的有界 A*。结点扩展上限 rows*columns；
/// 网格段与 start/destination 连接段统一执行同一段校验（全程场内、避开按 bodyRadius
/// 膨胀的圆障碍、采样坡度步长 &lt;= spacing/4）；对角移动禁止切角。场内按 bodyRadius
/// 收缩：中心距任一场界不足 bodyRadius 即视为场外（整个圆代理必须留在场内），
/// 越界采样一律非法，不被收敛为合法。连续高度按 StageMeshBuilder 冻结的 a-d
/// 三角对角线插值（a=左上，b=右上，c=左下，d=右下）。
/// 只读、无缓存、确定性：相同输入产生相同输出，不修改快照。
/// </summary>
public static class BoundedRoute
{
    private const double DegreesPerRadian = 180.0 / Math.PI;
    private const double MaxAllowedSlopeDegrees = 35.0;

    public static RouteResult Find(
        TerrainSnapshot terrain,
        NavPoint start,
        NavPoint destination,
        double bodyRadius,
        NavObstacle[] obstacles,
        double maxSlopeDegrees = 30)
    {
        ValidateInput(terrain, start, destination, bodyRadius, obstacles, maxSlopeDegrees);

        int rows = terrain.Rows;
        int columns = terrain.Columns;
        double originX = terrain.OriginXM;
        double originZ = terrain.OriginZM;
        double spacing = terrain.SpacingM;

        var inflated = new double[obstacles.Length];
        for (int i = 0; i < obstacles.Length; i++)
            inflated[i] = obstacles[i].Radius + bodyRadius;

        var centerX = new double[obstacles.Length];
        var centerZ = new double[obstacles.Length];
        for (int i = 0; i < obstacles.Length; i++)
        {
            centerX[i] = obstacles[i].Center.X;
            centerZ[i] = obstacles[i].Center.Z;
        }

        var context = new GridContext(terrain, originX, originZ, spacing, bodyRadius, centerX, centerZ, inflated, maxSlopeDegrees);

        // 结点占位预判只作剪枝；段级校验仍是唯一裁决（端点采样同样检查障碍与收缩场界）。
        bool[] free = new bool[rows * columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double x = originX + column * spacing;
                double z = originZ + row * spacing;
                free[row * columns + column] = context.CenterInField(x, z) && !context.BlockedAt(x, z);
            }
        }

        List<int> entries = CollectConnectionNodes(context, start, isStart: true);
        if (entries.Count == 0)
            return Fail(terrain, "起点无法全程在场内连接到通行网格结点");

        List<int> exits = CollectConnectionNodes(context, destination, isStart: false);
        if (exits.Count == 0)
            return Fail(terrain, "终点无法全程在场内连接到通行网格结点");

        var exitSet = new bool[rows * columns];
        foreach (int node in exits)
            exitSet[node] = true;

        RouteResult route = Search(context, free, entries, exitSet, start, destination);
        return route;
    }

    // ---- 输入校验：全部先行，非法输入无副作用 ----

    private static void ValidateInput(
        TerrainSnapshot terrain,
        NavPoint start,
        NavPoint destination,
        double bodyRadius,
        NavObstacle[] obstacles,
        double maxSlopeDegrees)
    {
        if (terrain is null)
            throw new ArgumentException("terrain must not be null", nameof(terrain));
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Z))
            throw new ArgumentException("start coordinates must be finite", nameof(start));
        if (!double.IsFinite(destination.X) || !double.IsFinite(destination.Z))
            throw new ArgumentException("destination coordinates must be finite", nameof(destination));
        if (!double.IsFinite(bodyRadius) || bodyRadius <= 0)
            throw new ArgumentException("bodyRadius must be finite and greater than zero", nameof(bodyRadius));
        if (obstacles is null)
            throw new ArgumentException("obstacles must not be null", nameof(obstacles));
        for (int i = 0; i < obstacles.Length; i++)
        {
            NavObstacle obstacle = obstacles[i];
            if (!double.IsFinite(obstacle.Center.X) || !double.IsFinite(obstacle.Center.Z))
                throw new ArgumentException($"obstacles[{i}] center must be finite", nameof(obstacles));
            if (!double.IsFinite(obstacle.Radius) || obstacle.Radius <= 0)
                throw new ArgumentException($"obstacles[{i}] radius must be finite and greater than zero", nameof(obstacles));
        }
        if (!double.IsFinite(maxSlopeDegrees) || maxSlopeDegrees <= 0 || maxSlopeDegrees > MaxAllowedSlopeDegrees)
            throw new ArgumentException(
                $"maxSlopeDegrees must be a finite angle in (0,{MaxAllowedSlopeDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture)}]",
                nameof(maxSlopeDegrees));
    }

    private static RouteResult Fail(TerrainSnapshot terrain, string reason)
        => new(false, reason, terrain.Version, Array.Empty<NavPoint>(), 0d);

    // ---- start/destination 与网格的连接：包含所在格 3x3 邻域，逐段完整校验 ----

    private static List<int> CollectConnectionNodes(GridContext context, NavPoint point, bool isStart)
    {
        int column0 = Clamp((int)Math.Floor((point.X - context.OriginX) / context.Spacing), 0, context.Columns - 1);
        int row0 = Clamp((int)Math.Floor((point.Z - context.OriginZ) / context.Spacing), 0, context.Rows - 1);

        var connected = new List<int>();
        for (int row = row0 - 1; row <= row0 + 1; row++)
        {
            if (row < 0 || row >= context.Rows)
                continue;
            for (int column = column0 - 1; column <= column0 + 1; column++)
            {
                if (column < 0 || column >= context.Columns)
                    continue;
                double x = context.OriginX + column * context.Spacing;
                double z = context.OriginZ + row * context.Spacing;
                bool clear = isStart
                    ? context.SegmentClear(point.X, point.Z, x, z)
                    : context.SegmentClear(x, z, point.X, point.Z);
                if (clear)
                    connected.Add(row * context.Columns + column);
            }
        }
        return connected;
    }

    // ---- 有界 A*：闭集保证扩展数不超过 rows*columns ----

    private static RouteResult Search(
        GridContext context,
        bool[] free,
        List<int> entries,
        bool[] exitSet,
        NavPoint start,
        NavPoint destination)
    {
        int nodeCount = context.Rows * context.Columns;
        var gScore = new double[nodeCount];
        var cameFrom = new int[nodeCount];
        var closed = new bool[nodeCount];
        Array.Fill(gScore, double.PositiveInfinity);
        Array.Fill(cameFrom, -1);

        // .NET8 PriorityQueue + (f, node) 全序优先级：优先级含 node 决胜，出队序确定。
        var heap = new PriorityQueue<int, (double F, int Node)>(nodeCount, NodePriorityComparer.Instance);
        foreach (int entry in entries)
        {
            double x = context.NodeX(entry);
            double z = context.NodeZ(entry);
            gScore[entry] = context.Distance3D(start.X, start.Z, x, z);
            heap.Enqueue(entry, (gScore[entry] + context.Heuristic(x, z, destination), entry));
        }

        int expansions = 0;
        while (heap.Count > 0)
        {
            int node = heap.Dequeue();
            if (closed[node])
                continue;
            closed[node] = true;
            expansions++;
            if (expansions > nodeCount)
                return Fail(context.Terrain, "网格结点扩展超出上限，终止搜索");

            if (exitSet[node])
                return BuildSuccess(context, cameFrom, gScore, node, start, destination);

            double nodeX = context.NodeX(node);
            double nodeZ = context.NodeZ(node);
            int row = node / context.Columns;
            int column = node % context.Columns;

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0)
                        continue;
                    int neighborRow = row + dr;
                    int neighborColumn = column + dc;
                    if (neighborRow < 0 || neighborRow >= context.Rows
                        || neighborColumn < 0 || neighborColumn >= context.Columns)
                        continue;
                    int neighbor = neighborRow * context.Columns + neighborColumn;
                    if (closed[neighbor] || !free[neighbor])
                        continue;

                    // 对角移动禁止切角：两正交肩结点必须通行。
                    if (dr != 0 && dc != 0
                        && (!free[row * context.Columns + neighborColumn]
                            || !free[neighborRow * context.Columns + column]))
                        continue;

                    double neighborX = context.NodeX(neighbor);
                    double neighborZ = context.NodeZ(neighbor);
                    if (!context.SegmentClear(nodeX, nodeZ, neighborX, neighborZ))
                        continue;

                    double candidate = gScore[node] + context.Distance3D(nodeX, nodeZ, neighborX, neighborZ);
                    if (candidate < gScore[neighbor])
                    {
                        gScore[neighbor] = candidate;
                        cameFrom[neighbor] = node;
                        heap.Enqueue(neighbor, (candidate + context.Heuristic(neighborX, neighborZ, destination), neighbor));
                    }
                }
            }
        }

        return Fail(context.Terrain, "在结点扩展上限内未找到可通行路线");
    }

    private static RouteResult BuildSuccess(
        GridContext context,
        int[] cameFrom,
        double[] gScore,
        int exit,
        NavPoint start,
        NavPoint destination)
    {
        var path = new List<int>();
        for (int node = exit; node >= 0; node = cameFrom[node])
            path.Add(node);
        path.Reverse();

        var points = new List<NavPoint>(path.Count + 1);
        foreach (int node in path)
        {
            var point = new NavPoint(context.NodeX(node), context.NodeZ(node));
            if (points.Count == 0 && point.X == start.X && point.Z == start.Z)
                continue; // start 恰为网格结点时不得重复出现在 Points 中
            points.Add(point);
        }
        if (points.Count == 0 || !Equals(points[points.Count - 1], destination))
            points.Add(destination); // 末端严格 destination，而非吸附格点

        NavPoint lastNode = new(context.NodeX(exit), context.NodeZ(exit));
        double length = gScore[exit] + context.Distance3D(lastNode.X, lastNode.Z, destination.X, destination.Z);
        return new RouteResult(true, "已找到路线", context.Terrain.Version, points.ToArray(), length);
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : (value > max ? max : value);

    // ---- 网格几何与段校验的共享上下文 ----

    private sealed class GridContext
    {
        private readonly TerrainSnapshot _terrain;
        private readonly double[] _centerX;
        private readonly double[] _centerZ;
        private readonly double[] _inflated;
        private readonly double _maxSlopeDegrees;

        public GridContext(
            TerrainSnapshot terrain,
            double originX,
            double originZ,
            double spacing,
            double bodyRadius,
            double[] centerX,
            double[] centerZ,
            double[] inflated,
            double maxSlopeDegrees)
        {
            _terrain = terrain;
            OriginX = originX;
            OriginZ = originZ;
            Spacing = spacing;
            // 场界按 bodyRadius 收缩：中心可活动范围保证整个圆代理在场内。
            // 采样网格原点与高度插值坐标不受收缩影响，仍用原始场界。
            MinX = originX + bodyRadius;
            MaxX = originX + (terrain.Columns - 1) * spacing - bodyRadius;
            MinZ = originZ + bodyRadius;
            MaxZ = originZ + (terrain.Rows - 1) * spacing - bodyRadius;
            _centerX = centerX;
            _centerZ = centerZ;
            _inflated = inflated;
            _maxSlopeDegrees = maxSlopeDegrees;
        }

        public TerrainSnapshot Terrain => _terrain;
        public double OriginX { get; }
        public double OriginZ { get; }
        public double Spacing { get; }
        public double MinX { get; }
        public double MaxX { get; }
        public double MinZ { get; }
        public double MaxZ { get; }
        public int Rows => _terrain.Rows;
        public int Columns => _terrain.Columns;

        public double NodeX(int node) => OriginX + (node % Columns) * Spacing;
        public double NodeZ(int node) => OriginZ + (node / Columns) * Spacing;

        /// <summary>中心是否在 bodyRadius 收缩后的场内；等于收缩边界（圆与场界相切）视为在场。</summary>
        public bool CenterInField(double x, double z)
            => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public double Heuristic(double x, double z, NavPoint destination)
        {
            double dx = destination.X - x;
            double dz = destination.Z - z;
            return Math.Sqrt(dx * dx + dz * dz); // 2D 距离是三维段长的下界，可采纳且一致
        }

        public double Distance3D(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1;
            double dz = z2 - z1;
            double dh = HeightAt(x2, z2) - HeightAt(x1, z1);
            return Math.Sqrt(dx * dx + dz * dz + dh * dh);
        }

        public bool BlockedAt(double x, double z)
        {
            for (int i = 0; i < _inflated.Length; i++)
            {
                double dx = x - _centerX[i];
                double dz = z - _centerZ[i];
                if (dx * dx + dz * dz <= _inflated[i] * _inflated[i])
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 段校验：按步长 spacing/4 采样，逐点检查收缩场内（中心距场界 ≥ bodyRadius，
        /// 越界采样非法、不 clamp）、膨胀圆障碍与相邻采样点坡度；另做圆-线段精确距离
        /// 复验，防止采样间隙漏检。端点即采样点，一并受检。
        /// </summary>
        public bool SegmentClear(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1;
            double dz = z2 - z1;
            double length = Math.Sqrt(dx * dx + dz * dz);
            double step = Spacing / 4.0;
            int samples = (int)Math.Ceiling(length / step);
            if (samples < 1)
                samples = 1;

            double previousHeight = 0;
            for (int i = 0; i <= samples; i++)
            {
                double t = (double)i / samples;
                double x = x1 + dx * t;
                double z = z1 + dz * t;
                if (!CenterInField(x, z))
                    return false;
                if (BlockedAt(x, z))
                    return false;
                double height = HeightAt(x, z);
                if (i > 0)
                {
                    double dh = height - previousHeight;
                    double horizontal = length / samples;
                    double slopeDegrees = Math.Atan2(Math.Abs(dh), horizontal) * DegreesPerRadian;
                    if (slopeDegrees > _maxSlopeDegrees)
                        return false;
                }
                previousHeight = height;
            }

            for (int i = 0; i < _inflated.Length; i++)
            {
                double distanceSquared = PointSegmentDistanceSquared(_centerX[i], _centerZ[i], x1, z1, x2, z2);
                if (distanceSquared < _inflated[i] * _inflated[i])
                    return false;
            }
            return true;
        }

        /// <summary>现有网格的连续高度：cell 内按冻结的 a-d 对角线分三角平面插值。</summary>
        public double HeightAt(double x, double z)
        {
            double fractionX = (x - OriginX) / Spacing;
            double fractionZ = (z - OriginZ) / Spacing;
            int column = Clamp((int)Math.Floor(fractionX), 0, Columns - 2);
            int row = Clamp((int)Math.Floor(fractionZ), 0, Rows - 2);
            double u = fractionX - column;
            double v = fractionZ - row;
            double ha = _terrain.GetHeight(row, column);         // a 左上
            double hb = _terrain.GetHeight(row, column + 1);     // b 右上
            double hc = _terrain.GetHeight(row + 1, column);     // c 左下
            double hd = _terrain.GetHeight(row + 1, column + 1); // d 右下
            return u >= v
                ? (1 - u) * ha + (u - v) * hb + v * hd      // 三角 a-d-b
                : (1 - v) * ha + (v - u) * hc + u * hd;     // 三角 a-c-d
        }

        private static double PointSegmentDistanceSquared(double px, double pz, double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1;
            double dz = z2 - z1;
            double lengthSquared = dx * dx + dz * dz;
            double t = lengthSquared == 0
                ? 0
                : Clamp01(((px - x1) * dx + (pz - z1) * dz) / lengthSquared);
            double nearestX = x1 + dx * t;
            double nearestZ = z1 + dz * t;
            double ex = px - nearestX;
            double ez = pz - nearestZ;
            return ex * ex + ez * ez;
        }

        private static double Clamp01(double value) => value < 0 ? 0 : (value > 1 ? 1 : value);
    }

    // ---- 优先级比较器：(f, node) 全序，同 f 按 node 序，出队序与输入无关地确定 ----

    private sealed class NodePriorityComparer : IComparer<(double F, int Node)>
    {
        public static readonly NodePriorityComparer Instance = new();

        public int Compare((double F, int Node) left, (double F, int Node) right)
        {
            int byKey = left.F.CompareTo(right.F);
            return byKey != 0 ? byKey : left.Node.CompareTo(right.Node);
        }
    }
}
