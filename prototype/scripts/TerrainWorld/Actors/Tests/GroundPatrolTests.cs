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
/// main-ground-r1 GLM包B实际引擎测试（只headless）。_Ready 跑身份核对与输入验证/一次性
/// 初始化纯逻辑用例，并搭静态box世界：平面、20°可走坡、50°陡坡、暂停区、微小航点区；
/// _PhysicsProcess 按帧推进五个真实 MoveAndSlide 巡逻场景，各自带deadline帧超时，
/// 超时按失败计。逐条打印 PASS/FAIL，全过 SUMMARY 后退出0，任一失败退出1。
/// 所有地面判定来自原生物理，不手动模拟高度、不直接调用C#运动函数冒充 MoveAndSlide。
/// </summary>
public partial class GroundPatrolTests : Node3D
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
            Check("initialize: defaults, target 0, contract capsule/layer/mask/floor settings", ContractShape);
            Check("lifecycle: failed Initialize builds nothing and stays usable, success once then locked", Lifecycle);
            Check("rejections: null/single/non-finite/out-of-range/duplicate-XZ incl last-to-first/speed (0,20]", Rejections);
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

    private static void ContractShape()
    {
        var robot = new GroundPatrol();
        try
        {
            robot.Initialize(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0) }, 1f);
            Expect(robot.TargetIndex == 0, $"initial TargetIndex {robot.TargetIndex} != 0");
            Expect(!robot.Paused, "Paused must default to false");
            Expect(robot.TravelledM == 0f, "TravelledM must start at 0");
            Expect(!robot.Blocked, "Blocked must start false");
            Expect(robot.CollisionLayer == 2u, $"CollisionLayer {robot.CollisionLayer} != 2");
            Expect(robot.CollisionMask == 1u, $"CollisionMask {robot.CollisionMask} != 1");
            Expect(robot.MotionMode == CharacterBody3D.MotionModeEnum.Grounded, "MotionMode must be Grounded");
            Expect(robot.UpDirection.IsEqualApprox(Vector3.Up), "UpDirection must be Up");
            Expect(MathF.Abs(robot.FloorMaxAngle - Mathf.DegToRad(GroundPatrol.MaxFloorAngleDeg)) < 1e-5f,
                $"FloorMaxAngle {robot.FloorMaxAngle} != 35 deg");
            Expect(MathF.Abs(robot.FloorSnapLength - 0.5f) < 1e-6f, $"FloorSnapLength {robot.FloorSnapLength} != 0.5");
            Expect(robot.FloorStopOnSlope, "FloorStopOnSlope must be true");
            Expect(robot.FloorBlockOnWall, "FloorBlockOnWall must be true");
            CollisionShape3D shape = SingleCollisionShape(robot);
            Expect(shape.Position.IsEqualApprox(new Vector3(0f, 0.55f, 0f)),
                $"collision shape origin {shape.Position} != (0, 0.55, 0) above foot origin");
            if (shape.Shape is not CapsuleShape3D capsule)
                throw new Exception("shape must be a capsule");
            Expect(MathF.Abs(capsule.Radius - 0.35f) < 1e-6f, $"capsule radius {capsule.Radius} != 0.35");
            Expect(MathF.Abs(capsule.Height - 1.1f) < 1e-6f, $"capsule height {capsule.Height} != 1.1");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(robot))
                robot.Free();
        }
    }

    private static void Lifecycle()
    {
        var robot = new GroundPatrol();
        try
        {
            ExpectArgument(() => robot.Initialize(new[] { Vector3.Zero, new Vector3(1, 0, 0) }, 0f),
                "speed", "speed 0 on fresh instance");
            Expect(CollisionShapeCount(robot) == 0, "rejected Initialize must not build a collider");
            robot.Initialize(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0) }, 1f);
            Expect(CollisionShapeCount(robot) == 1,
                "valid Initialize after a rejected call must build exactly one collider");
            ExpectInvalidOperation(
                () => robot.Initialize(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0) }, 1f),
                "second successful Initialize");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(robot))
                robot.Free();
        }
    }

    private static void Rejections()
    {
        ExpectArgumentWaypoints(null, "waypoints", "null array");
        ExpectArgumentWaypoints(new[] { Vector3.Zero }, "2 waypoints", "single waypoint");
        ExpectArgumentWaypoints(new[] { Vector3.Zero, new Vector3(1, float.NaN, 0) },
            "waypoints[1]", "NaN coordinate");
        ExpectArgumentWaypoints(new[] { Vector3.Zero, new Vector3(1, 0, float.PositiveInfinity) },
            "waypoints[1]", "infinite coordinate");
        ExpectArgumentWaypoints(new[] { Vector3.Zero, new Vector3(10001, 0, 0) },
            "waypoints[1]", "|x| > 10000");
        ExpectArgumentWaypoints(new[] { Vector3.Zero, Vector3.Zero },
            "waypoints[0] and [1]", "duplicate adjacent XZ");
        ExpectArgumentWaypoints(
            new[] { new Vector3(-1, 0, -1), new Vector3(2, 0, -1), new Vector3(5, 0, -1), new Vector3(-1, 0, -1) },
            "waypoints[3] and [0]", "duplicate last-to-first XZ");
        ExpectArgumentSpeed(0f, "speed 0");
        ExpectArgumentSpeed(-1f, "negative speed");
        ExpectArgumentSpeed(float.NaN, "NaN speed");
        ExpectArgumentSpeed(float.PositiveInfinity, "infinite speed");
        ExpectArgumentSpeed(20.0001f, "speed > 20");
        var limit = new GroundPatrol();
        try
        {
            limit.Initialize(new[] { Vector3.Zero, new Vector3(1, 0, 0) }, 20f);
            Expect(limit.TargetIndex == 0, "speed 20 boundary must initialize");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(limit))
                limit.Free();
        }
    }

    private static void ExpectArgumentWaypoints(Vector3[] waypoints, string messagePart, string what)
    {
        var robot = new GroundPatrol();
        try
        {
            robot.Initialize(waypoints, 1f);
        }
        catch (ArgumentException ex)
        {
            Expect(ex.Message.Contains(messagePart), $"{what}: message missing '{messagePart}': {ex.Message}");
            Expect(CollisionShapeCount(robot) == 0, $"{what}: rejected Initialize must not build a collider");
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(robot))
                robot.Free();
        }
        throw new Exception($"{what}: no ArgumentException thrown");
    }

    private static void ExpectArgumentSpeed(float speed, string what)
    {
        var robot = new GroundPatrol();
        try
        {
            robot.Initialize(new[] { Vector3.Zero, new Vector3(1, 0, 0) }, speed);
        }
        catch (ArgumentException ex)
        {
            Expect(ex.Message.Contains("speed"), $"{what}: message missing 'speed': {ex.Message}");
            Expect(CollisionShapeCount(robot) == 0, $"{what}: rejected Initialize must not build a collider");
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{what}: expected ArgumentException, got {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(robot))
                robot.Free();
        }
        throw new Exception($"{what}: no ArgumentException thrown");
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

    private static void ExpectArgument(Action action, string messagePart, string what)
    {
        try
        {
            action();
        }
        catch (ArgumentException ex)
        {
            Expect(ex.Message.Contains(messagePart), $"{what}: message missing '{messagePart}': {ex.Message}");
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

    private static int CollisionShapeCount(GroundPatrol robot) =>
        robot.GetChildren().OfType<CollisionShape3D>().Count();

    private static CollisionShape3D SingleCollisionShape(GroundPatrol robot)
    {
        List<CollisionShape3D> shapes = robot.GetChildren().OfType<CollisionShape3D>().ToList();
        Expect(shapes.Count == 1, $"expected exactly 1 CollisionShape3D child, got {shapes.Count}");
        return shapes[0];
    }

    // ---- 静态box物理世界：五个互不重叠的XZ区域，地面layer 1，机器人mask 1。 ----

    private void BuildWorld()
    {
        // 平面区：上表面Y=0；初始化后篡改外部数组验证防御复制。
        AddChild(FloorBox(new Vector3(12, 1, 10), new Vector3(2, -0.5f, 2)));
        Vector3[] flat =
            { new(2, 0, 0), new(6, 0, 0), new(6, 0, 4), new(2, 0, 4) };
        GroundPatrol flatBot = SpawnRobot(new Vector3(0, 0.5f, 0), flat, 2f);
        flat[0] = new Vector3(999, 0, 999);
        flat[1] = new Vector3(999, 0, 999);
        _scenarios.Add(new FlatPatrolScenario(flatBot));

        // 20°可走坡（<35° FloorMaxAngle），绕Z转+20°，+X为上坡方向。
        AddChild(SlopeBox(new Vector3(10, 0.5f, 4), new Vector3(41, 1.4f, 0), 20f));
        GroundPatrol rampBot = SpawnRobot(new Vector3(37, 0.5f, 0),
            new[] { new Vector3(38.5f, 0, 0), new Vector3(44.5f, 0, 0) }, 2f);
        _scenarios.Add(new RampPatrolScenario(rampBot));

        // 50°陡坡（>35° FloorMaxAngle，墙语义）+ 平面接近段；低端埋入平面下避免缝隙。
        AddChild(FloorBox(new Vector3(8, 1, 8), new Vector3(100, -0.5f, 0)));
        AddChild(SlopeBox(new Vector3(8, 0.5f, 4), new Vector3(105.2f, 1.9f, 0), 50f));
        GroundPatrol steepBot = SpawnRobot(new Vector3(98, 0.5f, 0),
            new[] { new Vector3(108, 0, 0), new Vector3(98, 0, 0) }, 2f);
        _scenarios.Add(new SteepSlopeScenario(steepBot));

        // 暂停区。
        AddChild(FloorBox(new Vector3(12, 1, 10), new Vector3(202, -0.5f, 0)));
        GroundPatrol pauseBot = SpawnRobot(new Vector3(198, 0.5f, 0),
            new[] { new Vector3(200, 0, 0), new Vector3(206, 0, 0) }, 2f);
        _scenarios.Add(new PauseScenario(pauseBot));

        // 微小航点差区（1mm XZ，合法相邻差）。
        AddChild(FloorBox(new Vector3(8, 1, 8), new Vector3(300, -0.5f, 0)));
        GroundPatrol tinyBot = SpawnRobot(new Vector3(298, 0.5f, 0),
            new[] { new Vector3(299.999f, 0, 0), new Vector3(300f, 0, 0) }, 2f);
        _scenarios.Add(new TinyLoopScenario(tinyBot));
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

        protected void ExpectFinite(Vector3 v, string what)
        {
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
                throw new ScenarioException($"{Name}: non-finite {what} {v}");
        }
    }

    /// <summary>平面闭环：贴地、逐tick限速、依序换点、防御复制目标。</summary>
    private sealed class FlatPatrolScenario : Scenario
    {
        private const float Speed = 2f;
        private static readonly Vector3[] Original = { new(2, 0, 0), new(6, 0, 0), new(6, 0, 4) };
        private readonly GroundPatrol _robot;
        private Vector3 _prev;
        private int _local;
        private int _lastIndex;

        public FlatPatrolScenario(GroundPatrol robot)
            : base("flat loop: grounded walk, per-tick speed cap, ordered waypoints, defensive copy", 900)
        {
            _robot = robot;
            _prev = robot.GlobalPosition;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} idx {_robot.TargetIndex} travelled {_robot.TravelledM:F2}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            Vector3 moved = pos - _prev;
            moved.Y = 0f;
            float xz = moved.Length();
            Expect(xz <= Speed * delta + 2e-3f,
                $"per-tick XZ {xz:F4} exceeds speed cap at local frame {_local}, pos {pos}");
            if (_local > LandingFrames)
            {
                Expect(_robot.IsOnFloor(), $"not grounded at local frame {_local}, pos {pos}");
                Expect(MathF.Abs(pos.Y) <= 0.05f, $"feet Y {pos.Y} off plane at local frame {_local}, pos {pos}");
                if (_robot.TargetIndex <= 1)
                    Expect(pos.Z <= 0.05f,
                        $"Z {pos.Z} drifts toward mutated waypoint (defensive copy failed), pos {pos}");
            }
            if (_robot.TargetIndex != _lastIndex)
            {
                int reached = _robot.TargetIndex - 1;
                if (reached >= 0 && reached < Original.Length)
                {
                    Vector2 planar = new(pos.X - Original[reached].X, pos.Z - Original[reached].Z);
                    Expect(planar.Length() <= 0.26f,
                        $"advanced to index {_robot.TargetIndex} but waypoint[{reached}] miss distance {planar.Length():F3}");
                }
                _lastIndex = _robot.TargetIndex;
            }
            _prev = pos;
            if (_robot.TargetIndex >= 3 && _local > LandingFrames)
            {
                Expect(_robot.TravelledM >= 9f && _robot.TravelledM <= 10f,
                    $"TravelledM {_robot.TravelledM:F2} out of expected ~9.3m band");
                Expect(!_robot.Blocked, $"Blocked on open plane, pos {pos}");
                Done($"local frames {_local}, pos {pos}, travelled {_robot.TravelledM:F2}m");
            }
        }
    }

    /// <summary>20°可走坡：上坡到顶并折返，全程贴地不滑落。</summary>
    private sealed class RampPatrolScenario : Scenario
    {
        private static readonly Vector3 Bottom = new(38.5f, 0, 0);
        private static readonly Vector3 Top = new(44.5f, 0, 0);
        private readonly GroundPatrol _robot;
        private int _local;
        private int _lastIndex;
        private int _advances;
        private float _maxY;

        public RampPatrolScenario(GroundPatrol robot)
            : base("walkable 20 deg slope: climbs to top and returns, stays grounded", 900)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} idx {_robot.TargetIndex} maxY {_maxY:F2} travelled {_robot.TravelledM:F2}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            if (_local > LandingFrames)
            {
                Expect(_robot.IsOnFloor(), $"not grounded on ramp at local frame {_local}, pos {pos}");
                Expect(pos.Y >= 0.1f, $"fell below ramp surface: Y {pos.Y} at {pos}");
                Expect(MathF.Abs(pos.Z) <= 0.05f, $"Z drift {pos.Z} at {pos}");
            }
            _maxY = MathF.Max(_maxY, pos.Y);
            if (_robot.TargetIndex != _lastIndex)
            {
                Vector3 reached = _robot.TargetIndex == 1 ? Bottom : Top;
                Vector2 planar = new(pos.X - reached.X, pos.Z - reached.Z);
                Expect(planar.Length() <= 0.26f,
                    $"advance #{_advances + 1} but target miss distance {planar.Length():F3}, pos {pos}");
                _advances++;
                _lastIndex = _robot.TargetIndex;
            }
            if (_advances >= 2 && _local > LandingFrames)
            {
                Expect(_maxY >= 2.6f, $"never climbed the slope, maxY {_maxY:F2} (top ~2.94)");
                Done($"local frames {_local}, pos {pos}, maxY {_maxY:F2}, travelled {_robot.TravelledM:F2}m");
            }
        }
    }

    /// <summary>50°陡坡（&gt;35°，墙语义）：被挡不穿过、不爬坡，Blocked/IsOnWall 置位。</summary>
    private sealed class SteepSlopeScenario : Scenario
    {
        private const float FloorTopY = 0f;
        private readonly GroundPatrol _robot;
        private int _local;
        private bool _contact;
        private float _xAtContact;
        private int _contactFrames;

        public SteepSlopeScenario(GroundPatrol robot)
            : base("50 deg steep slope: blocked with wall semantics, never passes through", 600)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} idx {_robot.TargetIndex} contact {_contact} at X {_xAtContact:F3}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            if (_local <= LandingFrames)
                return;
            Expect(_robot.IsOnFloor(), $"not grounded at local frame {_local}, pos {pos}");
            if (!_contact && _robot.IsOnWall())
            {
                _contact = true;
                _xAtContact = pos.X;
            }
            if (!_contact)
                return;
            _contactFrames++;
            Expect(pos.X <= _xAtContact + 0.15f,
                $"crept past contact X: {pos.X} > {_xAtContact:F3} + 0.15, pos {pos}");
            Expect(pos.X <= 104.2f, $"penetrated steep ramp: X {pos.X}, pos {pos}");
            Expect(pos.Y <= FloorTopY + 0.1f, $"climbing steep ramp: Y {pos.Y} at {pos}");
            Expect(MathF.Abs(pos.Z) <= 0.05f, $"Z drift {pos.Z} at {pos}");
            if (_contactFrames >= 120)
            {
                Expect(_robot.Blocked, $"Blocked must be true while pressed into steep slope, pos {pos}");
                Expect(_robot.IsOnWall(), $"IsOnWall must hold at steep slope, pos {pos}");
                Done($"contact X {_xAtContact:F3}, pos {pos}, travelled {_robot.TravelledM:F2}m");
            }
        }
    }

    /// <summary>Paused：暂停冻结水平运动与航点，仍贴地；恢复后继续前进。</summary>
    private sealed class PauseScenario : Scenario
    {
        private readonly GroundPatrol _robot;
        private int _phase;
        private int _local;
        private int _pausedFrames;
        private Vector3 _snapshot;
        private float _travelledSnapshot;

        public PauseScenario(GroundPatrol robot)
            : base("paused: no horizontal or waypoint advance, still grounded, resumes", 900)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} idx {_robot.TargetIndex} phase {_phase} paused {_robot.Paused}";

        public override void Step(int frame, float delta)
        {
            _local++;
            if (_local <= LandingFrames)
                return;
            Vector3 pos = _robot.GlobalPosition;
            switch (_phase)
            {
                case 0:
                    if (_robot.IsOnFloor() && pos.X >= 198.3f && _robot.TargetIndex == 0)
                    {
                        _robot.Paused = true;
                        _snapshot = pos;
                        _travelledSnapshot = _robot.TravelledM;
                        _phase = 1;
                    }
                    break;
                case 1:
                    Expect(_robot.Paused, "Paused flag lost");
                    Expect(MathF.Abs(pos.X - _snapshot.X) < 1e-5f && MathF.Abs(pos.Z - _snapshot.Z) < 1e-5f,
                        $"moved while paused: {pos} vs snapshot {_snapshot}");
                    Expect(MathF.Abs(_robot.TravelledM - _travelledSnapshot) < 1e-6f,
                        $"TravelledM advanced while paused: {_robot.TravelledM} vs {_travelledSnapshot}");
                    Expect(_robot.TargetIndex == 0, $"TargetIndex advanced while paused: {_robot.TargetIndex}");
                    Expect(_robot.IsOnFloor(), "lost ground while paused (gravity/ground must still apply)");
                    Expect(MathF.Abs(pos.Y - _snapshot.Y) <= 0.05f,
                        $"Y drifted while paused: {pos.Y} vs {_snapshot.Y}");
                    _pausedFrames++;
                    if (_pausedFrames >= 90)
                    {
                        _robot.Paused = false;
                        _phase = 2;
                    }
                    break;
                default:
                    if (pos.X >= _snapshot.X + 1.5f)
                        Done($"paused {_pausedFrames} frames at {_snapshot}, resumed to {pos}, "
                            + $"travelled {_robot.TravelledM:F2}m");
                    break;
            }
        }
    }

    /// <summary>微小航点差（1mm XZ）：目标持续轮换，位置/速度/TravelledM 不得 NaN。</summary>
    private sealed class TinyLoopScenario : Scenario
    {
        private readonly GroundPatrol _robot;
        private int _local;
        private int _nearFrames;
        private int _toggles;
        private int _lastIndex;

        public TinyLoopScenario(GroundPatrol robot)
            : base("tiny waypoint difference: no NaN, keeps cycling targets", 600)
        {
            _robot = robot;
        }

        public override string Describe() =>
            $"pos {_robot.GlobalPosition} idx {_robot.TargetIndex} toggles {_toggles} travelled {_robot.TravelledM:F3}";

        public override void Step(int frame, float delta)
        {
            _local++;
            Vector3 pos = _robot.GlobalPosition;
            ExpectFinite(pos, "position");
            ExpectFinite(_robot.Velocity, "velocity");
            Expect(float.IsFinite(_robot.TravelledM), $"non-finite TravelledM {_robot.TravelledM}");
            Expect(_robot.TargetIndex is 0 or 1, $"TargetIndex {_robot.TargetIndex} out of range");
            if (_robot.TargetIndex != _lastIndex)
            {
                _toggles++;
                _lastIndex = _robot.TargetIndex;
            }
            if (pos.X >= 299.5f)
            {
                _nearFrames++;
                if (_nearFrames >= 120)
                {
                    Expect(_toggles >= 20, $"target index cycled only {_toggles} times in {_nearFrames} near frames");
                    Done($"local frames {_local}, toggles {_toggles}, pos {pos}, "
                        + $"travelled {_robot.TravelledM:F3}m");
                }
            }
        }
    }
}
