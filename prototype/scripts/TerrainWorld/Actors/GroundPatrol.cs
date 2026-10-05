using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// main-ground-r1 GLM包B：固定闭环航点贴地巡逻。脚底为节点原点，全部运动只经
/// _PhysicsProcess 的 Velocity+MoveAndSlide 原生地面解算（Grounded、35°坡、0.5m snap、
/// 9.8m/s² 重力），不直接写Y、不传送越墙、不补地形高度。无寻路/动态避让/建设/经济；
/// Blocked 只表示本帧被墙或陡坡挡住，不构成可达性算法。胶囊与速度等数值是本工程
/// 验证常量，不是正式机器人平衡或最终外形碰撞。
/// </summary>
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

    /// <summary>暂停时不推水平运动与航点，仍处理重力/地面。</summary>
    public bool Paused { get; set; }

    /// <summary>当前目标航点索引；Initialize成功前为-1，成功后从0开始。</summary>
    public int TargetIndex => _targetIndex;

    /// <summary>累加的实际XZ位移，m。</summary>
    public float TravelledM { get; private set; }

    /// <summary>本帧希望水平移动但实际XZ位移&lt;1e-4且被墙/陡坡阻挡。</summary>
    public bool Blocked { get; private set; }

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
        if (!Paused)
        {
            Vector3 toTarget = _waypoints[_targetIndex] - before;
            toTarget.Y = 0f;
            float distance = toTarget.Length();
            if (distance <= WaypointReachXzM)
            {
                _targetIndex = (_targetIndex + 1) % _waypoints.Length;
            }
            else if (dt > 0f)
            {
                horizontal = toTarget * (MathF.Min(_speed, distance / dt) / distance);
            }
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

    // 验证全部通过才走到这里；碰撞子节点一次建成，没有中间失败态。
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
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.1f },
            Position = new Vector3(0f, 0.55f, 0f),
        });
    }
}
