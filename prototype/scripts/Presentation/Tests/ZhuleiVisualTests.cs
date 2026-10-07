#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Godot;

namespace Yudian.Presentation;

/// <summary>
/// D1.0 E03 ZhuleiVisual 实机测试（headless，Godot 进程内跑真资产 zhulei.glb）。
/// _Ready 一次跑完全部检查：启动身份核对（CLR/物理后端/MVID==刚构建 PE）、生命周期、
/// canonical 资产与 manifest 时长核对、根 identity/脚底原点/静态包络、六键消费路由、
/// move/work 循环回绕、单次动作定格、paused 冻结不伪造、切换复位防串扰（按 manifest
/// changed_targets 逐名核验）、非法键/非法 delta 先验后拒保旧态、delta 极值与父节点不动。
/// 逐条 PASS/FAIL（Console.WriteLine 走 stdout，验收须捕获完整 stdout+stderr 与退出码），
/// 全过 SUMMARY 后退出 0，任一失败退出 1。不做渲染判定，不伪造充电/维修能力。
/// </summary>
public partial class ZhuleiVisualTests : Node3D
{
    private static int _pass;
    private static int _fail;

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
            Check("lifecycle: Apply before Initialize throws, Initialize succeeds once then locked", Lifecycle);
            Check("contract: canonical model, exactly one AnimationPlayer, five clips at manifest durations", ContractAssets);
            Check("units: adapter root identity, model root foot origin, static bounds per manifest", UnitsAndRootIdentity);
            Check("keys: six keys accepted with manifest clip routing, unknown key rejected", SixKeys);
            Check("loops: move wraps at clip length, work wraps at clip length", LoopWrap);
            Check("oneshot: charge/disabled/maintenance hold at clip end", OneShotHold);
            Check("pause: time frozen, pose frozen, no faked motion, resumes from frozen time", PauseFreeze);
            Check("pause switch: new key shown frozen at t0, advances only after unpause", PauseSwitch);
            Check("rejections: bad key / bad delta fully validated before mutation, old state+pose kept", Rejections);
            Check("crosstalk: every legal switch restores previous clip's changed targets to baseline", CrosstalkReset);
            Check("boundary: delta 0 and DeltaLimit accepted, parent transform untouched", BoundaryAndParent);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FATAL {ex}");
        }
        Console.WriteLine($"SUMMARY pass={_pass} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
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
        System.Reflection.Assembly assembly = typeof(ZhuleiVisual).Assembly;
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

    private static void Lifecycle()
    {
        var visual = new ZhuleiVisual();
        try
        {
            ExpectInvalidOperationException(() => visual.Apply("idle", 0.016, false),
                "Apply before Initialize");
            ExpectInvalidOperationException(() => visual.Apply(null, 0.016, false),
                "Apply(null) before Initialize");
            visual.Initialize();
            ExpectInvalidOperationException(visual.Initialize, "second Initialize");
            Expect(visual.State == "idle", $"initial State {visual.State} != idle");
            Expect(visual.StateTime == 0d, "initial StateTime must be 0");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
        }
    }

    private void ContractAssets()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            Node? model = visual.GetNodeOrNull("Model");
            Expect(model is Node3D, "canonical model must be instantiated as child node 'Model'");
            AnimationPlayer player = PlayerOf(visual);
            // manifest rev1 durations: move 1.0, work 2.0, charge/disabled/maintenance 1.0, idle static。
            foreach ((string clip, double expected) in new[]
                     {
                         ("move", 1.0), ("work", 2.0), ("charge", 1.0), ("disabled", 1.0), ("maintenance", 1.0),
                     })
            {
                Expect(player.HasAnimation(clip), $"canonical clip missing: {clip}");
                double length = player.GetAnimation(clip).Length;
                Console.WriteLine($"clip {clip} length={length}");
                Expect(Math.Abs(length - expected) <= 1e-6,
                    $"clip {clip} length {length} != manifest {expected}");
            }
            Expect(player.GetAnimation("move").GetTrackCount() > 0, "move clip must drive tracks");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void UnitsAndRootIdentity()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            Expect(Same(visual.Transform, Transform3D.Identity),
                $"adapter root must stay identity, got {visual.Transform}");
            var model = (Node3D)visual.GetNode("Model");
            Console.WriteLine($"model root transform {model.Transform}");
            Expect(Diff(model.Transform, Transform3D.Identity) <= 1e-4,
                $"model root should be identity, got {model.Transform}");

            // 1m 单位与脚底原点意图：manifest bounds.static 是美术参考面（bounds_scope 明示
            // 不作包络认证），Godot 导入 rest pose 与参考面有厘米级差，故用 ±0.05 容差档。
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
            int meshCount = 0;
            CollectMeshes(model, ref min, ref max, ref meshCount);
            Expect(meshCount > 0, "no MeshInstance3D found under model");
            Console.WriteLine($"static AABB min={min} max={max} meshes={meshCount}");
            Expect(Math.Abs(min.Y) <= 0.05f, $"foot origin: AABB min.Y {min.Y} must be ~0");
            Expect(min.X > -0.97f && min.X < -0.77f, $"AABB min.X {min.X} vs manifest -0.87");
            Expect(max.X > 0.77f && max.X < 0.97f, $"AABB max.X {max.X} vs manifest 0.87");
            Expect(max.Y > 1.0f && max.Y < 1.2f, $"AABB max.Y {max.Y} vs manifest 1.1034 (1m unit scale)");
            Expect(min.Z > -0.91f && min.Z < -0.71f, $"AABB min.Z {min.Z} vs manifest -0.8106");
            Expect(max.Z > 0.70f && max.Z < 0.90f, $"AABB max.Z {max.Z} vs manifest 0.80");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private static void CollectMeshes(Node node, ref Vector3 min, ref Vector3 max, ref int meshCount)
    {
        if (node is MeshInstance3D mesh)
        {
            meshCount++;
            Aabb local = mesh.GetAabb();
            for (int i = 0; i < 8; i++)
            {
                var corner = local.Position
                    + new Vector3(local.Size.X * (i & 1), local.Size.Y * ((i >> 1) & 1), local.Size.Z * ((i >> 2) & 1));
                Vector3 world = mesh.GlobalTransform * corner;
                min = min.Min(world);
                max = max.Max(world);
            }
        }
        foreach (Node child in node.GetChildren())
            CollectMeshes(child, ref min, ref max, ref meshCount);
    }

    private void SixKeys()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            foreach ((string key, string clip) in new[]
                     {
                         ("idle", ""), ("move", "move"), ("work", "work"),
                         ("charge", "charge"), ("disabled", "disabled"), ("maintenance", "maintenance"),
                     })
            {
                Expect(visual.Apply(key, 0.016, false), $"Apply({key}) must be accepted");
                Expect(visual.State == key, $"State {visual.State} != {key}");
                if (clip.Length > 0)
                    Expect(player.AssignedAnimation == clip,
                        $"key {key} must route to clip {clip}, got '{player.AssignedAnimation}'");
            }
            Expect(!visual.Apply("towed", 0.016, false), "manifest-only key 'towed' must be rejected in D1.0");
            Expect(visual.State == "maintenance", "rejected key must not change State");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void LoopWrap()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            Dictionary<Node3D, Transform3D> rest = Snapshot(visual);

            Node3D wheel = TrackNode(player, "move", 0);
            Expect(visual.Apply("move", 0.25, false), "move first apply must be accepted");
            Expect(Moved(wheel.Transform, rest[wheel]), "move must visibly rotate wheels");
            Transform3D atQuarter = wheel.Transform;
            Expect(Math.Abs(visual.StateTime - 0.25) <= 1e-9, "move StateTime must be 0.25");
            Expect(visual.Apply("move", 1.0, false), "move second apply must be accepted");
            Expect(Math.Abs(visual.StateTime - 1.25) <= 1e-9, "move StateTime must accumulate to 1.25");
            Expect(Same(TrackNode(player, "move", 0).Transform, atQuarter),
                "move pose at t=1.25 must equal t=0.25 (1.0s loop wrap)");

            Expect(visual.Apply("work", 0.5, false), "work first apply must be accepted");
            Transform3D atHalf = TrackNode(player, "work", 0).Transform;
            Expect(visual.Apply("work", 2.0, false), "work second apply must be accepted");
            // 切换键时 _time 归零：0.5 + 2.0 = 2.5，position = 2.5 % 2.0 = 0.5。
            Expect(Math.Abs(visual.StateTime - 2.5) <= 1e-9, "work StateTime must be 2.5 (reset on switch)");
            Expect(Math.Abs(player.CurrentAnimationPosition - 0.5) <= 1e-9,
                "work playback position must wrap to 0.5");
            Expect(Same(TrackNode(player, "work", 0).Transform, atHalf),
                "work pose at t=2.5 must equal t=0.5 (2.0s loop wrap)");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void OneShotHold()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            foreach (string key in new[] { "charge", "disabled", "maintenance" })
            {
                Expect(visual.Apply(key, 5.0, false), $"{key} must accept 5s delta");
                double length = player.GetAnimation(key).Length;
                Expect(Math.Abs(player.CurrentAnimationPosition - length) <= 1e-9,
                    $"{key} must hold at clip end {length}, got {player.CurrentAnimationPosition}");
                Transform3D held = TrackNode(player, key, 0).Transform;
                Expect(visual.Apply(key, 3.0, false), $"{key} must keep accepting while held");
                Expect(Same(TrackNode(player, key, 0).Transform, held),
                    $"{key} pose must stay frozen at held end");
            }
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void PauseFreeze()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            Node3D shoulder = TrackNode(player, "work", 0);
            Expect(visual.Apply("work", 0.3, false), "work must be accepted");
            Transform3D pose = shoulder.Transform;
            double time = visual.StateTime;
            for (int i = 0; i < 5; i++)
            {
                Expect(visual.Apply("work", 1.0 / 60.0, true), "paused Apply must be accepted");
                Expect(visual.StateTime == time, $"paused must not advance StateTime, got {visual.StateTime}");
                Expect(Same(shoulder.Transform, pose), "paused must not move pose");
                Expect(Math.Abs(player.CurrentAnimationPosition - 0.3) <= 1e-9,
                    $"paused playback position must stay 0.3, got {player.CurrentAnimationPosition}");
            }
            Expect(visual.Apply("work", 1.0 / 60.0, false), "resume must be accepted");
            Expect(Math.Abs(visual.StateTime - (time + 1.0 / 60.0)) <= 1e-9,
                $"resume must continue from frozen time, got {visual.StateTime}");
            Expect(Math.Abs(player.CurrentAnimationPosition - (0.3 + 1.0 / 60.0)) <= 1e-9,
                $"resume must advance playback position, got {player.CurrentAnimationPosition}");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void PauseSwitch()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            Node3D shoulder = TrackNode(player, "work", 0);
            Expect(visual.Apply("idle", 0.5, false), "idle must be accepted first");
            Expect(visual.Apply("work", 1.0 / 60.0, true), "paused switch to work must be accepted");
            Expect(visual.State == "work", "state key must switch even while paused");
            Expect(player.AssignedAnimation == "work", "paused switch must route to the new clip");
            Expect(Math.Abs(player.CurrentAnimationPosition) <= 1e-9,
                $"paused switch must show new clip at t0, got {player.CurrentAnimationPosition}");
            Expect(visual.StateTime == 0d, "paused switch must not advance time");
            Transform3D pose = shoulder.Transform;
            for (int i = 0; i < 3; i++)
            {
                Expect(visual.Apply("work", 0.2, true), "paused Apply must be accepted");
                Expect(Same(shoulder.Transform, pose), "paused frames must not move the new clip");
            }
            Expect(visual.Apply("work", 1.0 / 60.0, false), "resume must be accepted");
            Expect(Math.Abs(player.CurrentAnimationPosition - 1.0 / 60.0) <= 1e-9,
                $"unpaused frame must advance playback position, got {player.CurrentAnimationPosition}");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void Rejections()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            AnimationPlayer player = PlayerOf(visual);
            Node3D wheel = TrackNode(player, "move", 0);
            Expect(visual.Apply("move", 0.37, false), "move must be accepted first");
            Transform3D pose = wheel.Transform;
            double time = visual.StateTime;

            (string? Key, double Delta)[] rejected =
            {
                ("bogus", 0.016), (null, 0.016), ("", 0.016), ("towed", 0.016), ("Idle", 0.016),
                ("move", double.NaN), ("move", double.PositiveInfinity), ("move", double.NegativeInfinity),
                ("move", -0.01), ("move", 10.0000001), ("work", double.NaN),
            };
            foreach ((string? key, double delta) in rejected)
            {
                Expect(!visual.Apply(key, delta, false),
                    $"Apply({key ?? "null"}, {delta}) must be rejected");
                Expect(visual.State == "move", "rejected Apply must keep old state");
                Expect(visual.StateTime == time, "rejected Apply must keep old StateTime");
                Expect(Same(wheel.Transform, pose), "rejected Apply must keep old pose");
            }

            Expect(visual.Apply("move", 1.0 / 60.0, false), "valid Apply after rejections must be accepted");
            Expect(visual.State == "move" && visual.StateTime > time,
                "old valid state must survive and keep advancing after rejections");
            Expect(Moved(wheel.Transform, pose), "valid Apply must advance pose after rejections");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    private void CrosstalkReset()
    {
        ZhuleiVisual visual = NewVisual();
        try
        {
            Dictionary<Node3D, Transform3D> baseline = Snapshot(visual);
            Console.WriteLine($"baseline nodes={baseline.Count}");

            Expect(visual.Apply("idle", 0, false), "idle accepted");

            // work 改肩/肘/双滑柱（manifest changed_targets）；各自在样本内必须可见移动。
            Expect(visual.Apply("work", 0, false), "work accepted");
            foreach (string part in new[] { "Shoulder", "Elbow", "SupportSlideL", "SupportSlideR" })
                Expect(MovedSomewhere(visual, "work", part, new[] { 0.3, 0.7, 1.1, 1.5, 1.9 }, baseline),
                    $"work must visibly move {part}");

            Expect(visual.Apply("charge", 5.0, false), "work->charge accepted");
            foreach (string part in new[] { "Shoulder", "Elbow", "SupportSlideL", "SupportSlideR" })
            {
                Node3D partNode = ByName(baseline, part);
                Console.WriteLine(
                    $"crosstalk work->charge {part} diff={Diff(partNode.Transform, baseline[partNode]):E3}");
                Expect(SamePose(partNode.Transform, baseline[partNode]),
                    $"switching work->charge must restore {part} to baseline");
            }
            Expect(MovedSomewhere(visual, "charge", "ChargePivot", new[] { 0.25, 0.5, 0.75, 1.0 }, baseline),
                "charge must visibly move ChargePivot");

            Expect(visual.Apply("maintenance", 5.0, false), "charge->maintenance accepted");
            Expect(SamePose(ByName(baseline, "ChargePivot").Transform, baseline[ByName(baseline, "ChargePivot")]),
                "switching charge->maintenance must restore ChargePivot to baseline");
            Expect(MovedSomewhere(visual, "maintenance", "HoodLid", new[] { 0.25, 0.5, 0.75, 1.0 }, baseline),
                "maintenance must visibly move HoodLid");

            Expect(visual.Apply("disabled", 5.0, false), "maintenance->disabled accepted");
            Expect(SamePose(ByName(baseline, "HoodLid").Transform, baseline[ByName(baseline, "HoodLid")]),
                "switching maintenance->disabled must restore HoodLid to baseline");
            Expect(MovedSomewhere(visual, "disabled", "Shoulder", new[] { 0.25, 0.5, 0.75, 1.0 }, baseline),
                "disabled must visibly move Shoulder");

            Expect(visual.Apply("idle", 0, false), "disabled->idle accepted");
            foreach (KeyValuePair<Node3D, Transform3D> entry in baseline)
                Expect(SamePose(entry.Key.Transform, entry.Value),
                    "back to idle must restore the whole baseline pose");
        }
        finally
        {
            visual.QueueFree();
        }
    }

    /// <summary>沿 clip 逐样本推进，任一样本下该部件离开基线即为真（对关键帧相位稳健）。</summary>
    private bool MovedSomewhere(ZhuleiVisual visual, string clip, string part, double[] samples,
        Dictionary<Node3D, Transform3D> baseline)
    {
        Node3D node = ByName(baseline, part);
        double previous = 0;
        foreach (double t in samples)
        {
            Expect(visual.Apply(clip, t - previous, false), $"{clip} sample at {t} must be accepted");
            previous = t;
            if (Moved(node.Transform, baseline[node]))
                return true;
        }
        return false;
    }

    private static Node3D ByName(Dictionary<Node3D, Transform3D> baseline, string name)
    {
        foreach (Node3D node in baseline.Keys)
            if (node.Name == name)
                return node;
        throw new Exception($"expected animated part '{name}' not found under model");
    }

    private void BoundaryAndParent()
    {
        var expectedParent = new Transform3D(new Basis(Vector3.Up, 0.5f), new Vector3(3f, 1f, -2f));
        var parent = new Node3D { Transform = expectedParent };
        AddChild(parent);
        try
        {
            var visual = new ZhuleiVisual();
            parent.AddChild(visual);
            try
            {
                visual.Initialize();
                AnimationPlayer player = PlayerOf(visual);
                Node3D wheel = TrackNode(player, "move", 0);
                Transform3D baselineWheel = wheel.Transform;

                Expect(visual.Apply("move", 0d, false), "delta 0 must be accepted");
                Expect(visual.StateTime == 0d, "delta 0 must not advance");
                Expect(SamePose(wheel.Transform, baselineWheel), "delta 0 must not move pose");

                Expect(visual.Apply("move", ZhuleiVisual.DeltaLimit, false),
                    "delta at DeltaLimit must be accepted");
                Expect(Math.Abs(visual.StateTime - ZhuleiVisual.DeltaLimit) <= 1e-9,
                    "accepted DeltaLimit delta must accumulate");
                Expect(SamePose(wheel.Transform, baselineWheel),
                    "10s on a 1.0s loop must wrap back to the same pose");

                Expect(Same(parent.Transform, expectedParent),
                    "parent (physics actor stand-in) transform must never be touched");
                Expect(Same(visual.Transform, Transform3D.Identity),
                    "adapter root must stay identity after all applies");
            }
            finally
            {
                visual.QueueFree();
            }
        }
        finally
        {
            parent.QueueFree();
        }
    }

    private ZhuleiVisual NewVisual()
    {
        var visual = new ZhuleiVisual();
        AddChild(visual);
        visual.Initialize();
        return visual;
    }

    private static AnimationPlayer PlayerOf(ZhuleiVisual visual)
    {
        var found = new List<AnimationPlayer>();
        Collect(visual, found);
        Expect(found.Count == 1, $"expected exactly one AnimationPlayer, found {found.Count}");
        return found[0];

        static void Collect(Node node, List<AnimationPlayer> found)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is AnimationPlayer player)
                    found.Add(player);
                Collect(child, found);
            }
        }
    }

    private static Node3D TrackNode(AnimationPlayer player, string clip, int track)
    {
        // track 路径相对 player.root_node（其父=实例根），不含 player 自身，故先上到 root_node。
        NodePath path = player.GetAnimation(clip).TrackGetPath(track);
        Node rootTarget = player.GetNode(player.RootNode);
        Node3D? node = rootTarget.GetNode(new NodePath(path.GetConcatenatedNames())) as Node3D
            ?? throw new Exception($"clip {clip} track {track} ({path}) must resolve to a Node3D");
        return node;
    }

    private static Dictionary<Node3D, Transform3D> Snapshot(Node3D root)
    {
        var map = new Dictionary<Node3D, Transform3D>();
        Collect(root, map);
        return map;

        static void Collect(Node3D node, Dictionary<Node3D, Transform3D> map)
        {
            map[node] = node.Transform;
            foreach (Node child in node.GetChildren())
                if (child is Node3D node3d)
                    Collect(node3d, map);
        }
    }

    private static double Diff(in Transform3D a, in Transform3D b)
    {
        double d = (a.Origin - b.Origin).Length();
        d = Math.Max(d, Diff(a.Basis.X, b.Basis.X));
        d = Math.Max(d, Diff(a.Basis.Y, b.Basis.Y));
        return Math.Max(d, Diff(a.Basis.Z, b.Basis.Z));

        static double Diff(in Vector3 ca, in Vector3 cb)
        {
            return Math.Max(Math.Abs(ca.X - cb.X),
                Math.Max(Math.Abs(ca.Y - cb.Y), Math.Abs(ca.Z - cb.Z)));
        }
    }

    private static bool Same(in Transform3D a, in Transform3D b) => Diff(a, b) <= 1e-9;

    /// <summary>动画系统按 float32 写 rest 值时有 ~1e-7 量化噪声；"视觉上没动"用它判。</summary>
    private static bool SamePose(in Transform3D a, in Transform3D b) => Diff(a, b) <= 1e-6;

    private static bool Moved(in Transform3D a, in Transform3D b) => Diff(a, b) > 1e-5;

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void ExpectInvalidOperationException(Action action, string what)
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
            throw new Exception($"{what} must throw InvalidOperationException, got {ex.GetType().Name}");
        }
        throw new Exception($"{what} must throw InvalidOperationException, but returned normally");
    }
}
