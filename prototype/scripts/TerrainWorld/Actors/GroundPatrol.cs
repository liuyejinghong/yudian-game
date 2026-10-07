using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>脚底原点的固定航点巡逻；只用原生MoveAndSlide，不提供寻路或避让。</summary>
public partial class GroundPatrol : CharacterBody3D
{
    /// <summary>合同重力常量，m/s²。</summary>
    public const float Gravity = 9.8f;

    /// <summary>可走最大坡角（度），FloorMaxAngle。</summary>
    public const float MaxFloorAngleDeg = 35f;

    /// <summary>到点XZ容差，m。</summary>
    public const float WaypointReachXzM = 0.25f;

    private const float CoordinateLimit = 10000f;
    private const float SpeedLimit = 20f;

    private Vector3[] _waypoints;
    private float _speed;
    private int _targetIndex = -1;
    private Vector3 _orderTarget;
    private bool _hasOrder;
    private Vector3[] _route = [];
    private int _routeIndex;

    /// <summary>暂停时不推水平运动与航点，仍处理重力/地面。</summary>
    public bool Paused { get; set; }
    public bool PatrolEnabled { get; set; } = true;
    public float BodyRadius { get; set; } = .35f;

    /// <summary>当前目标航点索引；Initialize成功前为-1，成功后从0开始。</summary>
    public int TargetIndex => _targetIndex;

    /// <summary>累加的实际XZ位移，m。</summary>
    public float TravelledM { get; private set; }

    /// <summary>本帧希望水平移动但实际XZ位移&lt;1e-4且被墙/陡坡阻挡。</summary>
    public bool Blocked { get; private set; }

    /// <summary>存在活动单目标订单；不反映是否已到点（见OrderReached）。</summary>
    public bool HasOrder => _hasOrder;

    /// <summary>
    /// 订单已到点且真实站在地面：IsOnFloor且XZ距离&lt;=.25；离场/空中为false。
    /// 只在订单存活期间为true，ClearOrder后恒false。
    /// </summary>
    public bool OrderReached
    {
        get
        {
            if (!_hasOrder || (_route.Length > 0 && _routeIndex < _route.Length - 1) || !IsOnFloor())
                return false;
            Vector3 toTarget = _orderTarget - GlobalPosition;
            toTarget.Y = 0f;
            return toTarget.Length() <= WaypointReachXzM;
        }
    }

    /// <summary>
    /// 覆盖式设置单目标订单：须已Initialize成功；target各坐标须有限且绝对值&lt;=10000，
    /// 非法抛 ArgumentException 且不改变旧订单。保留巡逻航点与TargetIndex；有订单期间
    /// 巡逻推进暂停，到XZ&lt;=.25原地保持（重力/地面照常），不循环、不自动恢复巡逻。
    /// </summary>
    public void SetOrder(Vector3 target)
    {
        if (_waypoints == null)
            throw new InvalidOperationException(
                "GroundPatrol.SetOrder requires a successful Initialize first");
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z)
            || MathF.Abs(target.X) > CoordinateLimit || MathF.Abs(target.Y) > CoordinateLimit
            || MathF.Abs(target.Z) > CoordinateLimit)
            throw new ArgumentException(
                $"order target {target} must be finite with |coordinate| <= {CoordinateLimit}",
                nameof(target));
        _route = []; _routeIndex = 0;
        _orderTarget = target;
        _hasOrder = true;
    }

    public void SetRoute(Vector3[] points)
    {
        if (points == null || points.Length == 0 || points.Length > 10000 ||
            Array.Exists(points, p => !float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) || Math.Abs(p.X) > CoordinateLimit || Math.Abs(p.Y) > CoordinateLimit || Math.Abs(p.Z) > CoordinateLimit))
            throw new ArgumentException("route waypoints invalid");
        SetOrder(points[0]); _route = (Vector3[])points.Clone(); _routeIndex = 0;
    }

    /// <summary>
    /// 清除当前订单，幂等；只取消订单，不重置巡逻TargetIndex/TravelledM，不解除Paused。
    /// 清除后从原TargetIndex恢复巡逻。
    /// </summary>
    public void ClearOrder()
    {
        _route = []; _routeIndex = 0;
        _hasOrder = false;
    }

    /// <summary>
    /// 仅成功一次：先全量验证（非法即抛 ArgumentException，不建任何碰撞），
    /// 复制输入不依赖外部数组；成功后重复调用抛 InvalidOperationException。
    /// </summary>
    public void Initialize(Vector3[] waypoints, float speed)
    {
        if (_waypoints != null)
            throw new InvalidOperationException("GroundPatrol.Initialize can only succeed once");
        Validate(waypoints, speed);
        _waypoints = (Vector3[])waypoints.Clone();
        _speed = speed;
        _targetIndex = 0;
        BuildCollision();
    }

    public override void _PhysicsProcess(double delta)
    {
        Blocked = false;
        if (_waypoints == null)
            return;
        float dt = (float)delta;
        Vector3 before = GlobalPosition;

        Vector3 horizontal = Vector3.Zero;
        if (!Paused && (_hasOrder || PatrolEnabled))
        {
            if (_hasOrder && _route.Length > 0 && _routeIndex < _route.Length - 1 && IsOnFloor())
            {
                var next = _orderTarget - before; next.Y = 0;
                if (next.Length() <= WaypointReachXzM) _orderTarget = _route[++_routeIndex];
            }
            Vector3 target = _hasOrder ? _orderTarget : _waypoints[_targetIndex];
            Vector3 toTarget = target - before;
            toTarget.Y = 0f;
            float distance = toTarget.Length();
            bool advancePatrol = !_hasOrder && distance <= WaypointReachXzM;
            bool chase = distance > WaypointReachXzM && dt > 0f;
            if (advancePatrol)
            {
                _targetIndex = (_targetIndex + 1) % _waypoints.Length;
            }
            else if (chase)
            {
                horizontal = toTarget * (MathF.Min(_speed, distance / dt) / distance);
            }
            // 订单到点（<=.25）：原地保持，不推进航点、不恢复巡逻。
        }

        Vector3 velocity = Velocity;
        Velocity = new Vector3(horizontal.X, velocity.Y - Gravity * dt, horizontal.Z);
        MoveAndSlide();

        Vector3 moved = GlobalPosition - before;
        moved.Y = 0f;
        float xz = moved.Length();
        TravelledM += xz;
        Blocked = !Paused && horizontal != Vector3.Zero && xz < 1e-4f && IsOnWall();
    }

    private static void Validate(Vector3[] waypoints, float speed)
    {
        if (waypoints == null)
            throw new ArgumentException("waypoints must not be null", nameof(waypoints));
        if (waypoints.Length < 2)
            throw new ArgumentException(
                $"at least 2 waypoints required, got {waypoints.Length}", nameof(waypoints));
        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 p = waypoints[i];
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z))
                throw new ArgumentException(
                    $"waypoints[{i}] {p} has a non-finite coordinate", nameof(waypoints));
            if (MathF.Abs(p.X) > CoordinateLimit || MathF.Abs(p.Y) > CoordinateLimit
                || MathF.Abs(p.Z) > CoordinateLimit)
                throw new ArgumentException(
                    $"waypoints[{i}] {p} exceeds |coordinate| <= {CoordinateLimit}", nameof(waypoints));
        }
        for (int i = 0; i < waypoints.Length; i++)
        {
            int j = (i + 1) % waypoints.Length;
            if (waypoints[i].X == waypoints[j].X && waypoints[i].Z == waypoints[j].Z)
                throw new ArgumentException(
                    $"waypoints[{i}] and [{j}] share the same XZ", nameof(waypoints));
        }
        if (!float.IsFinite(speed) || speed <= 0f || speed > SpeedLimit)
            throw new ArgumentException(
                $"speed must be finite in (0, {SpeedLimit}], got {speed}", nameof(speed));
    }

    private void BuildCollision()
    {
        CollisionLayer = 2;
        CollisionMask = 1;
        MotionMode = MotionModeEnum.Grounded;
        UpDirection = Vector3.Up;
        FloorMaxAngle = Mathf.DegToRad(MaxFloorAngleDeg);
        FloorSnapLength = 0.5f;
        FloorStopOnSlope = true;
        FloorBlockOnWall = true;
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = BodyRadius, Height = MathF.Max(1.1f, BodyRadius * 2) },
            Position = new Vector3(0f, MathF.Max(.55f, BodyRadius), 0f),
        });
    }
}
