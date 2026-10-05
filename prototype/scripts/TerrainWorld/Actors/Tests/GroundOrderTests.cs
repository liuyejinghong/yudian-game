using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// level-job-r1 GLM包A（GROUND-ORDER）实际引擎测试（只headless）。_Ready 跑身份核对与
/// SetOrder/ClearOrder 纯逻辑用例，并搭静态box世界：平面下单区（巡逻→下单→直线进场→到点
/// 保持→非法拒绝→清除回巡逻）、非法输入保留旧目标区、50°陡坡阻挡区、暂停空中正上方区
/// （订单目标在出生点正下方：空中每帧OrderReached=false、真实落地后true，SetOrder远目标
/// 后恢复到场）、飞行替换目标区；_PhysicsProcess 按帧推进五个
/// 真实 MoveAndSlide 场景，各自带deadline帧超时，超时按失败计。逐条打印 PASS/FAIL
/// （Console.WriteLine 走 stdout，不在 --log-file 里，验收须捕获完整 stdout/stderr），
/// 全过 SUMMARY 后退出0，任一失败退出1。所有地面/到场判定来自原生物理，不手动模拟高度、
/// 不传送、不寻路。
/// </summary>
public partial class GroundOrderTests : Node3D
{
    private const int LandingFrames = 30;
    private const int GlobalFrameLimit = 2400;

    private static int _pass;
    private static int _fail;

    private readonly List<Scenario> _scenarios = new();
    private int _frame;

    public override void _Ready()
    {
        try
        {
            Check("identity: CLR version, physics backend, loaded MVID == fresh Debug PE MVID, SHA256", Identity);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
        try
        {
            Check("order-api: SetOrder before Initialize throws without consuming Initialize, "
                + "defaults false, rejection matrix, boundary accept, idempotent ClearOrder", OrderApi);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
        try
        {
            BuildWorld();
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL world setup: {ex}");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 2)
            return; // 世界搭建后至少等一个完整物理步
        foreach (Scenario scenario in _scenarios)
        {
            if (scenario.Finished)
                continue;
            try
            {
                if (_frame > scenario.Deadline)
                    throw new ScenarioException(
                        $"{scenario.Name}: timeout frame {_frame} > deadline {scenario.Deadline}, {scenario.Describe()}");
                scenario.Step(_frame, (float)delta);
            }
            catch (ScenarioException ex)
            {
                scenario.MarkFailed();
                _fail++;
                Console.WriteLine($"FAIL {ex.Message}");
            }
            catch (Exception ex)
            {
                scenario.MarkFailed();
                _fail++;
                Console.WriteLine($"FAIL {scenario.Name}: {ex}");
            }
        }
        if (_frame >= GlobalFrameLimit || _scenarios.All(s => s.Finished))
        {
            foreach (Scenario scenario in _scenarios.Where(s => !s.Finished))
            {
                scenario.MarkFailed();
                _fail++;
                Console.WriteLine(
                    $"FAIL {scenario.Name}: global frame limit {_frame} reached, {scenario.Describe()}");
            }
            Console.WriteLine($"SUMMARY pass={_pass} fail={_fail}");
            GetTree().Quit(_fail == 0 ? 0 : 1);
        }
    }

    private static void Check(string name, Action body)
    {
        try
        {
            body();
            _pass++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static void Identity()
    {
        Console.WriteLine($"CLR {System.Environment.Version}");
        Console.WriteLine(
            $"physics/3d/physics_engine = {ProjectSettings.GetSetting("physics/3d/physics_engine")}");
        System.Reflection.Assembly assembly = typeof(GroundPatrol).Assembly;
        Console.WriteLine($"Assembly.FullName {assembly.FullName}");
        Console.WriteLine($"Assembly.Location {assembly.Location}");
        // Godot 4.7 经加载上下文装载，Location 为空；用 MVID 核对刚构建文件的模块身份，文件SHA另行记录。
        Guid loadedMvid = assembly.ManifestModule.ModuleVersionId;
        string expectedPath = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Yudian.dll");
        if (!File.Exists(expectedPath))
            throw new Exception($"fresh Debug build missing: {expectedPath}");
        byte[] freshBytes = File.ReadAllBytes(expectedPath);
        Console.WriteLine($"Fresh Debug Yudian.dll SHA256 {Convert.ToHexString(SHA256.HashData(freshBytes))}");
        Guid freshMvid = MvidOf(freshBytes);
        Console.WriteLine($"Loaded MVID  {loadedMvid}");
        Console.WriteLine($"Fresh  MVID  {freshMvid}");
        Expect(loadedMvid == freshMvid, "loaded Yudian.dll MVID differs from fresh Debug build (stale cache)");
    }

    private static Guid MvidOf(byte[] peBytes)
    {
        using var stream = new MemoryStream(peBytes);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        return reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }

    private static void OrderApi()
    {
        var robot = new GroundPatrol();
        try
        {
            ExpectInvalidOperation(() => robot.SetOrder(new Vector3(1, 0, 0)),
                "SetOrder before Initialize");
            Expect(!robot.HasOrder, "failed SetOrder must not create an order");
            robot.Initialize(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0) }, 1f);
            Expect(!robot.HasOrder, "HasOrder must default to false");
            Expect(!robot.OrderReached, "OrderReached must default to false");
            Expect(robot.TargetIndex == 0, "patrol TargetIndex must still start at 0");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(float.NaN, 0, 0)), "NaN x");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(0, float.NaN, 0)), "NaN y");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(0, 0, float.NaN)), "NaN z");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(float.PositiveInfinity, 0, 0)), "+inf x");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(0, float.NegativeInfinity, 0)), "-inf y");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(10000.5f, 0, 0)), "|x| > 10000");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(0, 10000.5f, 0)), "|y| > 10000");
            ExpectArgumentOrder(() => robot.SetOrder(new Vector3(0, 0, -10000.5f)), "|z| > 10000");
            Expect(!robot.HasOrder, "rejected SetOrder must not create an order");
            robot.SetOrder(new Vector3(10000f, 10000f, -10000f));
            Expect(robot.HasOrder, "boundary |coordinate| = 10000 must be accepted");
            int index = robot.TargetIndex;
            robot.ClearOrder();
            robot.ClearOrder();
            Expect(!robot.HasOrder, "ClearOrder must clear the order");
            Expect(robot.TargetIndex == index, "ClearOrder must not reset TargetIndex");
            Expect(robot.TravelledM == 0f, "ClearOrder must not touch TravelledM");
            Expect(!robot.Paused, "ClearOrder must not set Paused");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(robot))
                robot.Free();
        }
    }

    private static void ExpectInvalidOperation(Action action, string what)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected InvalidOperationException, got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception($"{what}: no InvalidOperationException thrown");
    }

    private static void ExpectArgumentOrder(Action action, string what)
    {
        try
        {
            action();
        }
        catch (ArgumentException ex)
        {
            Expect(ex.Message.Contains("target"), $"{what}: message missing 'target': {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
        }
        throw new Exception($"{what}: no ArgumentException thrown");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    // ---- 静态box物理世界：五个互不重叠的XZ区域，地面layer 1，机器人mask 1。 ----

    private void BuildWorld()
    {
        // A 平面区：巡逻→下单→直线进场→到点保持→非法拒绝→清除回巡逻（含Paused不被解除）。
        AddChild(FloorBox(new Vector3(16, 1, 12), new Vector3(4, -0.5f, 0)));
        GroundPatrol flowBot = SpawnRobot(new Vector3(0, 0.5f, 0),
            new[] { new Vector3(0, 0, 0), new Vector3(8, 0, 0), new Vector3(8, 0, 4), new Vector3(0, 0, 4) },
            2f);
        _scenarios.Add(new OrderFlowScenario(flowBot));

        // 非法输入区：下单后非法SetOrder被拒，机器人继续奔原目标。
        AddChild(FloorBox(new Vector3(12, 1, 10), new Vector3(50, -0.5f, 0)));
        GroundPatrol invalidBot = SpawnRobot(new Vector3(46, 0.5f, 0),
            new[] { new Vector3(48, 0, 0), new Vector3(52, 0, 0) }, 2f);
        invalidBot.SetOrder(new Vector3(52, 0, -2));
        _scenarios.Add(new InvalidKeepsTargetScenario(invalidBot));

        // 50°陡坡（>35°，墙语义）+ 平面接近段；低端埋入平面下避免缝隙。
        AddChild(FloorBox(new Vector3(8, 1, 8), new Vector3(100, -0.5f, 0)));
        AddChild(SlopeBox(new Vector3(8, 0.5f, 4), new Vector3(105.2f, 1.9f, 0), 50f));
        GroundPatrol blockedBot = SpawnRobot(new Vector3(98, 0.5f, 0),
            new[] { new Vector3(99, 0, 0), new Vector3(101, 0, 0) }, 2f);
        blockedBot.SetOrder(new Vector3(108, 0, 0));
        _scenarios.Add(new BlockedOrderScenario(blockedBot));

        // 暂停空中正上方区：首个订单目标=出生点正下方（XZ距离0），Paused悬空验证
        // 空中OrderReached=false（IsOnFloor保护）、真实落地后true，再SetOrder远目标恢复到场。
        AddChild(FloorBox(new Vector3(12, 1, 10), new Vector3(202, -0.5f, 0)));
        GroundPatrol airBot = SpawnRobot(new Vector3(198, 2f, 2),
            new[] { new Vector3(199, 0, 0), new Vector3(205, 0, 0) }, 2f);
        airBot.SetOrder(new Vector3(198, 0, 2));
        airBot.Paused = true;
        _scenarios.Add(new PausedAirOrderScenario(airBot));

        // 替换目标区：飞行途中SetOrder覆盖旧目标。
        AddChild(FloorBox(new Vector3(12, 1, 10), new Vector3(302, -0.5f, 0)));
        GroundPatrol swapBot = SpawnRobot(new Vector3(298, 0.5f, 0),
            new[] { new Vector3(299, 0, 0), new Vector3(305, 0, 0) }, 2f);
        swapBot.SetOrder(new Vector3(306, 0, 2));
        _scenarios.Add(new ReplaceOrderScenario(swapBot));
    }

    private static StaticBody3D FloorBox(Vector3 size, Vector3 center) => SlopeBox(size, center, 0f);

    private static StaticBody3D SlopeBox(Vector3 size, Vector3 center, float rotateZDeg)
    {
        var body = new StaticBody3D { Position = center, CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (rotateZDeg != 0f)
            body.RotationDegrees = new Vector3(0, 0, rotateZDeg);
        return body;
    }

    private GroundPatrol SpawnRobot(Vector3 feet, Vector3[] waypoints, float speed)
    {
        var robot = new GroundPatrol();
        robot.Initialize(waypoints, speed);
        AddChild(robot);
        robot.Position = feet;
        return robot;
    }

    // ---- 帧驱动场景骨架 ----

    private sealed class ScenarioException : Exception
    {
        public ScenarioException(string message) : base(message) { }
    }

    private abstract class Scenario
    {
        protected Scenario(string name, int deadline)
        {
            Name = name;
            Deadline = deadline;
        }

        public string Name { get; }
        public int Deadline { get; }
        public bool Finished { get; private set; }

        public void MarkFailed() => Finished = true;

        public abstract void Step(int frame, float delta);

        public virtual string Describe() => "";

        protected void Done(string detail)
        {
            Finished = true;
            _pass++;
            Console.WriteLine($"PASS {Name} {detail}");
        }

        protected void Expect(bool condition, string message)
        {
            if (!condition)
                throw new ScenarioException($"{Name}: {message}");
        }
    }

    private static float XzDistance(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        d.Y = 0f;
        return d.Length();
    }

    /// <summary>巡逻→下单→直线进场→到点保持→非法拒绝→清除回巡逻，全程TargetIndex冻结。</summary>
    private sealed class OrderFlowScenario : Scenario
    {
        private static readonly Vector3 OrderTarget = new(8f, 0f, -4f);
        private readonly GroundPatrol _robot;
        private int _phase;
        private int _local;
        private int _frozenIndex = -1;
        private float _lastDistance = float.MaxValue;
        private Vector3 _prev;
        private Vector3 _holdPos;
        private int _holdFrames;
        private int _indexAtClear;
        private float _travelAtClear;

        public OrderFlowScenario(GroundPatrol robot)
            : base("order flow: chase only via MoveAndSlide, arrival hold, invalid rejected, clear resumes patrol", 1800)
        {
            _robot = robot;
            _prev = robot.GlobalPosition;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} phase {_phase} idx {_robot.TargetIndex} "
            + $"hasOrder {_robot.HasOrder} reached {_robot.OrderReached} travelled {_robot.TravelledM:F2}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            Vector3 moved = pos - _prev;
            moved.Y = 0f;
            float step = moved.Length();
            _prev = pos;
            switch (_phase)
            {
                case 0:
                    // 先证明巡逻在跑，再下单覆盖。
                    if (_robot.TargetIndex != 0)
                    {
                        Expect(!_robot.HasOrder, "HasOrder must default false before SetOrder");
                        Expect(!_robot.OrderReached, "OrderReached must default false before SetOrder");
                        ExpectArgument(() => _robot.SetOrder(new Vector3(float.NaN, 0, 0)),
                            "invalid SetOrder while patrolling");
                        Expect(!_robot.HasOrder, "rejected SetOrder must not create an order");
                        _robot.SetOrder(OrderTarget);
                        Expect(_robot.HasOrder, "SetOrder must activate HasOrder");
                        _frozenIndex = _robot.TargetIndex;
                        _lastDistance = XzDistance(pos, OrderTarget);
                        _phase = 1;
                    }
                    break;
                case 1:
                {
                    Expect(_robot.TargetIndex == _frozenIndex,
                        $"TargetIndex {_robot.TargetIndex} must stay frozen at {_frozenIndex} while ordered");
                    float distance = XzDistance(pos, OrderTarget);
                    Expect(distance <= _lastDistance + 1e-3f,
                        $"order distance increased {distance:F3} > {_lastDistance:F3}, pos {pos}");
                    if (step > 1e-5f)
                    {
                        Vector3 toTarget = OrderTarget - pos;
                        toTarget.Y = 0f;
                        float dot = (moved / step).Dot(toTarget / toTarget.Length());
                        Expect(dot > 0.95f,
                            $"chase direction off straight line: dot {dot:F3}, moved {moved}, pos {pos}");
                    }
                    Expect(!_robot.OrderReached || distance <= 0.25f,
                        "OrderReached must be false while away from the order target");
                    _lastDistance = distance;
                    if (distance <= 0.25f)
                    {
                        Expect(_robot.OrderReached, "OrderReached must be true on arrival");
                        Expect(_robot.IsOnFloor(), "must be grounded on arrival");
                        _holdPos = pos;
                        _phase = 2;
                    }
                    break;
                }
                case 2:
                {
                    Expect(XzDistance(pos, OrderTarget) <= 0.25f, "drifted off order target during hold");
                    Expect(_robot.OrderReached, "OrderReached lost during hold");
                    Expect(_robot.IsOnFloor(), "must stay grounded during hold");
                    Expect(pos.DistanceTo(_holdPos) < 1e-3f,
                        $"position drifted during hold: {pos} vs {_holdPos}");
                    Expect(_robot.TargetIndex == _frozenIndex,
                        "TargetIndex must stay frozen during hold");
                    _holdFrames++;
                    if (_holdFrames >= 90)
                    {
                        _indexAtClear = _robot.TargetIndex;
                        _travelAtClear = _robot.TravelledM;
                        _robot.Paused = true;
                        _robot.ClearOrder();
                        _robot.ClearOrder(); // 幂等
                        Expect(!_robot.HasOrder, "ClearOrder must clear the order");
                        Expect(_robot.Paused, "ClearOrder must not lift Paused");
                        Expect(_robot.TargetIndex == _indexAtClear, "ClearOrder must not reset TargetIndex");
                        Expect(_robot.TravelledM == _travelAtClear, "ClearOrder must not reset TravelledM");
                        _robot.Paused = false;
                        Expect(!_robot.OrderReached, "OrderReached must be false after ClearOrder");
                        _phase = 3;
                    }
                    break;
                }
                default:
                    Expect(!_robot.HasOrder, "order resurrected after ClearOrder");
                    Expect(_robot.TravelledM >= _travelAtClear, "TravelledM was reset by ClearOrder");
                    if (_robot.TargetIndex != _indexAtClear)
                    {
                        Expect(_robot.TargetIndex == (_indexAtClear + 1) % 4,
                            $"patrol resumed from {_robot.TargetIndex}, expected preserved-index advance "
                            + $"{(_indexAtClear + 1) % 4}");
                        Done($"held {_holdFrames} frames at {_holdPos}, frozen idx {_frozenIndex}, "
                            + $"resumed idx {_robot.TargetIndex}, travelled {_robot.TravelledM:F2}m");
                    }
                    break;
            }
        }

        private static void ExpectArgument(Action action, string what)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new ScenarioException($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
            }
            throw new ScenarioException($"{what}: no ArgumentException thrown");
        }
    }

    /// <summary>下单后非法SetOrder被拒且原目标保留：继续奔原目标并到点。</summary>
    private sealed class InvalidKeepsTargetScenario : Scenario
    {
        private static readonly Vector3 OrderTarget = new(52f, 0f, -2f);
        private readonly GroundPatrol _robot;
        private int _phase;
        private int _local;
        private int _chaseFrames;
        private float _lastDistance = float.MaxValue;

        public InvalidKeepsTargetScenario(GroundPatrol robot)
            : base("invalid SetOrder keeps old target: rejected inputs, robot still reaches original", 900)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} phase {_phase} distance {XzDistance(_robot.GlobalPosition, OrderTarget):F3}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            float distance = XzDistance(pos, OrderTarget);
            if (_phase == 0)
            {
                Expect(_robot.HasOrder, "order lost before invalid input");
                Expect(distance <= _lastDistance + 1e-3f,
                    $"distance increased before invalid input: {distance:F3}");
                _lastDistance = distance;
                _chaseFrames++;
                if (_chaseFrames >= 20)
                {
                    ExpectArgument(() => _robot.SetOrder(new Vector3(float.NaN, 0, 0)), "NaN target");
                    ExpectArgument(() => _robot.SetOrder(new Vector3(10001f, 0, -2f)), "out-of-range target");
                    ExpectArgument(() => _robot.SetOrder(new Vector3(52f, float.PositiveInfinity, -2f)),
                        "infinite target");
                    Expect(_robot.HasOrder, "invalid SetOrder must not clear the live order");
                    _phase = 1;
                }
            }
            else
            {
                Expect(_robot.HasOrder, "invalid SetOrder cleared the order");
                Expect(distance <= _lastDistance + 1e-3f,
                    $"old target abandoned after invalid input: {distance:F3} > {_lastDistance:F3}");
                _lastDistance = distance;
                if (distance <= 0.25f)
                {
                    Expect(_robot.OrderReached, "OrderReached must be true on original target");
                    Expect(_robot.IsOnFloor(), "must be grounded on original target");
                    Done($"reached original target {OrderTarget} after 3 rejected SetOrder calls, "
                        + $"local frames {_local}");
                }
            }
        }

        private static void ExpectArgument(Action action, string what)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new ScenarioException($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
            }
            throw new ScenarioException($"{what}: no ArgumentException thrown");
        }
    }

    /// <summary>50°陡坡墙：订单目标在墙后，被挡不穿墙、永不 OrderReached，Blocked置位。</summary>
    private sealed class BlockedOrderScenario : Scenario
    {
        private readonly GroundPatrol _robot;
        private int _phase;
        private int _local;
        private int _contactFrames;
        private float _contactX = float.MaxValue;
        private int _frozenIndex = -1;

        public BlockedOrderScenario(GroundPatrol robot)
            : base("blocked order: wall between robot and target, never reached, Blocked set", 900)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} phase {_phase} contactX {_contactX:F3} "
            + $"blocked {_robot.Blocked} reached {_robot.OrderReached}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            if (_local <= LandingFrames)
                return;
            Expect(_robot.IsOnFloor(), $"not grounded at local frame {_local}, pos {pos}");
            Expect(_robot.HasOrder, "order lost while blocked");
            Expect(!_robot.OrderReached, "OrderReached must stay false behind a wall");
            if (_frozenIndex < 0)
                _frozenIndex = _robot.TargetIndex;
            Expect(_robot.TargetIndex == _frozenIndex, "TargetIndex advanced while ordered");
            if (_phase == 0)
            {
                if (_robot.IsOnWall() || _robot.Blocked)
                {
                    _contactX = pos.X;
                    _phase = 1;
                }
            }
            else
            {
                _contactFrames++;
                Expect(pos.X <= _contactX + 0.15f,
                    $"crept past contact X: {pos.X} > {_contactX:F3} + 0.15, pos {pos}");
                Expect(pos.X <= 104.2f, $"penetrated steep ramp: X {pos.X}, pos {pos}");
                if (_contactFrames >= 180)
                {
                    Expect(_robot.Blocked, "Blocked must be true while pressed into wall with live order");
                    Expect(_robot.IsOnWall(), "IsOnWall must hold at steep slope");
                    Done($"held against wall for {_contactFrames} frames at X {_contactX:F3}, "
                        + $"never reached, pos {pos}");
                }
            }
        }
    }

    /// <summary>
    /// 空中已Paused且订单目标在出生点正下方：悬空期间XZ<=.25但IsOnFloor=false，
    /// OrderReached必须逐帧false（杀掉移除IsOnFloor保护的变异）；真实重力落地稳定后true；
    /// SetOrder覆盖为远目标立即false，解除Paused后真实到场。
    /// </summary>
    private sealed class PausedAirOrderScenario : Scenario
    {
        private static readonly Vector3 BelowTarget = new(198f, 0f, 2f);
        private static readonly Vector3 FarTarget = new(206f, 0f, -2f);
        private readonly GroundPatrol _robot;
        private readonly Vector3 _spawn;
        private int _phase;
        private int _local;
        private int _groundedFrames;
        private int _airborneFrames;
        private bool _wasAirborne;
        private float _minY;
        private int _frozenIndex;

        public PausedAirOrderScenario(GroundPatrol robot)
            : base("paused airborne over target: OrderReached false every airborne frame, true after landing, far order reached", 900)
        {
            _robot = robot;
            _spawn = robot.GlobalPosition;
            _minY = _spawn.Y;
            _frozenIndex = robot.TargetIndex;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} phase {_phase} minY {_minY:F2} "
            + $"airborne {_airborneFrames} grounded {_groundedFrames}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            if (_phase == 0)
            {
                Expect(_robot.Paused, "Paused flag lost while airborne");
                Expect(_robot.HasOrder, "order lost while paused");
                Expect(_robot.TargetIndex == _frozenIndex,
                    $"TargetIndex {_robot.TargetIndex} advanced while paused");
                Expect(MathF.Abs(pos.X - _spawn.X) < 1e-4f && MathF.Abs(pos.Z - _spawn.Z) < 1e-4f,
                    $"XZ moved while paused airborne: {pos} vs spawn {_spawn}");
                if (!_robot.IsOnFloor())
                {
                    _wasAirborne = true;
                    _airborneFrames++;
                    _minY = MathF.Min(_minY, pos.Y);
                    _groundedFrames = 0; // 离地即归零，落地须连续贴地才算稳定。
                    // 变异杀手：正上方悬空 XZ<=.25，也必须因 IsOnFloor=false 而 false。
                    Expect(XzDistance(pos, BelowTarget) <= GroundPatrol.WaypointReachXzM,
                        $"not above the order target while airborne: {XzDistance(pos, BelowTarget):F3}");
                    Expect(!_robot.OrderReached,
                        "OrderReached must be false while airborne over the target (IsOnFloor guard)");
                }
                else
                {
                    Expect(_wasAirborne, "reported grounded before ever being airborne (no real fall)");
                    _groundedFrames++;
                    if (_groundedFrames >= 30)
                    {
                        Expect(_airborneFrames >= 10,
                            $"only {_airborneFrames} airborne frames observed, mutation coverage too thin");
                        Expect(_minY < _spawn.Y - 1f,
                            $"never really fell under gravity: minY {_minY:F2} vs spawn Y {_spawn.Y:F2}");
                        Expect(MathF.Abs(pos.Y) <= 0.05f, $"landed Y {pos.Y:F3} not at floor level");
                        Expect(XzDistance(pos, BelowTarget) <= GroundPatrol.WaypointReachXzM,
                            "drifted off the target XZ after landing");
                        Expect(_robot.OrderReached,
                            "OrderReached must be true once really grounded over the target");
                        _robot.SetOrder(FarTarget);
                        Expect(!_robot.OrderReached,
                            "OrderReached must be false right after SetOrder to a far target");
                        _robot.Paused = false;
                        _phase = 1;
                    }
                }
            }
            else
            {
                Expect(_robot.IsOnFloor(), $"lost ground after unpause at {pos}");
                Expect(_robot.TargetIndex == _frozenIndex, "TargetIndex advanced while ordered");
                float distance = XzDistance(pos, FarTarget);
                if (distance <= 0.25f)
                {
                    Expect(_robot.OrderReached, "OrderReached must be true on arrival after unpause");
                    Done($"OrderReached false on all {_airborneFrames} airborne frames over target, "
                        + $"true after landing, then far order {FarTarget} reached after unpause, "
                        + $"local frames {_local}");
                }
            }
        }
    }

    /// <summary>飞行途中SetOrder覆盖旧目标：改道奔新目标并到点，TargetIndex保持冻结。</summary>
    private sealed class ReplaceOrderScenario : Scenario
    {
        private static readonly Vector3 FirstTarget = new(306f, 0f, 2f);
        private static readonly Vector3 SecondTarget = new(298f, 0f, -2f);
        private readonly GroundPatrol _robot;
        private int _phase;
        private int _local;
        private int _frozenIndex = -1;
        private float _lastFirst = float.MaxValue;
        private float _lastSecond = float.MaxValue;

        public ReplaceOrderScenario(GroundPatrol robot)
            : base("replace order mid-chase: SetOrder overrides, robot reaches the new target", 900)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} phase {_phase} "
            + $"d1 {XzDistance(_robot.GlobalPosition, FirstTarget):F2} "
            + $"d2 {XzDistance(_robot.GlobalPosition, SecondTarget):F2}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            if (_frozenIndex < 0)
                _frozenIndex = _robot.TargetIndex;
            Expect(_robot.TargetIndex == _frozenIndex, "TargetIndex advanced while ordered");
            float d1 = XzDistance(pos, FirstTarget);
            float d2 = XzDistance(pos, SecondTarget);
            if (_phase == 0)
            {
                Expect(d1 <= _lastFirst + 1e-3f, $"not approaching first target: {d1:F3}");
                _lastFirst = d1;
                if (d1 <= 5f)
                {
                    Expect(_robot.HasOrder, "order lost before replacement");
                    _robot.SetOrder(SecondTarget);
                    Expect(_robot.HasOrder, "replacement SetOrder must keep order active");
                    _phase = 1;
                }
            }
            else
            {
                Expect(d1 >= _lastFirst - 1e-3f, $"still approaching old target after replacement: {d1:F3}");
                Expect(d2 <= _lastSecond + 1e-3f, $"not approaching replacement target: {d2:F3}");
                _lastFirst = d1;
                _lastSecond = d2;
                if (d2 <= 0.25f)
                {
                    Expect(_robot.OrderReached, "OrderReached must be true on replacement target");
                    Expect(_robot.IsOnFloor(), "must be grounded on replacement target");
                    Done($"switched at d1 {_lastFirst:F2}, reached new target {SecondTarget}, "
                        + $"local frames {_local}");
                }
            }
        }
    }
}
