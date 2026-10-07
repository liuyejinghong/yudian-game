#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Yudian.Presentation;

/// <summary>
/// D1.0 E03 筑垒视觉适配（现役 canonical 资产 res://assets/lowfi-batch-r1/models/zhulei.glb，
/// ART-U02 manifest rev1）。由主控挂到物理 actor（如 GroundPatrol）下作视觉子节点：本节点根
/// 保持 identity、1m 单位、脚底原点；Initialize() 实例化并校验 canonical 模型、唯一
/// AnimationPlayer 与五个源 clip；Apply(state,delta,paused) 只驱动本子树内的局部节点，消费六键
/// idle/move/work/charge/disabled/maintenance（manifest states；towed 不在 D1.0 合同，按非法键
/// 拒绝）。动画位置由主控传入的 delta 累加并 seek，不依赖引擎帧时序，也不改变物理 actor 的
/// root/GlobalPosition 或任何游戏事实。paused 只定格当前键、不推进时间、不伪造动作；合法切换
/// 先复位基线 pose 再起新动作，防工作/盖板/充电串扰；非法键/非法 delta 先完整校验再拒绝并保旧
/// 有效状态。move 的时间推进来自主控真实位移节拍，work 来自真实工段，服务状态只测适配，
/// 不在 D1.0 伪造充电维修能力。
/// </summary>
public partial class ZhuleiVisual : Node3D
{
    /// <summary>Apply 接受的单次 delta 上限（s）；超限按非法拒绝，视觉保持原状态。</summary>
    public const double DeltaLimit = 10.0;

    private const string ModelPath = "res://assets/lowfi-batch-r1/models/zhulei.glb";
    private const string ModelNodeName = "Model";

    /// <summary>状态键 → (源 clip, 是否循环)；idle 是静态基线轮廓，无 clip。对照
    /// art/manifests/lowfi-batch-r1/zhulei.json 的 states（rev1）。</summary>
    private static readonly Dictionary<string, (string Clip, bool Loop)> States = new()
    {
        ["idle"] = ("", false),
        ["move"] = ("move", true),
        ["work"] = ("work", true),
        ["charge"] = ("charge", false),
        ["disabled"] = ("disabled", false),
        ["maintenance"] = ("maintenance", false),
    };

    private AnimationPlayer? _player;
    private Dictionary<Node3D, Transform3D>? _baseline;
    private string _state = "idle";
    private double _time;
    private bool _startPending = true;

    /// <summary>当前生效状态键；被拒绝的 Apply 不改变它。</summary>
    public string State => _state;

    /// <summary>当前状态内累计的动画时间（s）。paused 不推进；单次动作按 clip 长度钳制呈现，
    /// 该累计值本身继续累加（可诊断过长停留）。</summary>
    public double StateTime => _time;

    /// <summary>
    /// 实例化现役 canonical 模型并完整校验：资源存在、根是 Node3D、唯一 AnimationPlayer、
    /// 五个源 clip 存在且时长为正、子树可缓存基线。节点/clip 缺失直接抛异常，不静默降级；
    /// 校验全部通过才落子节点，失败不残留半成品。成功后重复调用抛 InvalidOperationException。
    /// </summary>
    public void Initialize()
    {
        if (_baseline != null)
            throw new InvalidOperationException("ZhuleiVisual.Initialize can only succeed once");
        PackedScene? scene = GD.Load<PackedScene>(ModelPath);
        if (scene == null)
            throw new InvalidOperationException($"canonical model missing: {ModelPath}");
        Node3D? model = scene.Instantiate<Node3D>();
        if (model == null)
            throw new InvalidOperationException($"{ModelPath} root is not a Node3D");
        try
        {
            model.Name = ModelNodeName;
            AddChild(model);
            AnimationPlayer[] players = FindAnimationPlayers(model);
            if (players.Length != 1)
                throw new InvalidOperationException(
                    $"{ModelPath} must expose exactly one AnimationPlayer, found {players.Length}");
            _player = players[0];
            foreach ((string _, (string clip, bool _)) in States)
            {
                if (clip.Length == 0)
                    continue;
                if (!_player.HasAnimation(clip))
                    throw new InvalidOperationException($"{ModelPath} source clip missing: {clip}");
                if (_player.GetAnimation(clip).Length <= 0d)
                    throw new InvalidOperationException($"{ModelPath} clip has no duration: {clip}");
            }
            _baseline = CaptureBaseline(model);
        }
        catch
        {
            _player = null;
            _baseline = null;
            model.QueueFree();
            throw;
        }
    }

    /// <summary>
    /// 消费主控给出的真实状态键。返回 true=接受；false=非法输入被拒绝且视觉保持原有效状态
    /// （键不在六键内含 null/大小写不符，或 delta 不为 [0, DeltaLimit] 内有限值）。
    /// paused=true 只呈现当前键的定格 pose、不推进时间；期间的状态切换仍立即生效
    /// （新动作从 0 定格，不伪造运动）。须先成功 Initialize。
    /// </summary>
    public bool Apply(string? state, double delta, bool paused)
    {
        if (_baseline == null || _player == null)
            throw new InvalidOperationException(
                "ZhuleiVisual.Apply requires a successful Initialize first");
        if (state == null || !States.ContainsKey(state))
            return false;
        if (!double.IsFinite(delta) || delta < 0d || delta > DeltaLimit)
            return false;

        if (state != _state)
        {
            // 合法切换：先完整复位基线 pose，防上一动作（盖板/充电/工作）残留串扰。
            _player.Stop();
            foreach (KeyValuePair<Node3D, Transform3D> entry in _baseline)
                entry.Key.Transform = entry.Value;
            _state = state;
            _time = 0d;
            _startPending = true;
        }
        if (!paused)
            _time += delta;

        (string clip, bool loop) = States[_state];
        if (clip.Length == 0)
        {
            // idle：manifest mode=static，静态基线轮廓，无 clip。
            if (_startPending)
            {
                _player.Stop();
                _startPending = false;
            }
            return true;
        }
        double length = _player.GetAnimation(clip).Length;
        double position = loop ? _time % length : Math.Min(_time, length);
        // 播放→定位→暂停：位置完全由主控 delta 驱动，引擎不自行推进（preview.gd 同款办法）。
        if (_startPending)
        {
            _player.Play(clip, 0f);
            _startPending = false;
        }
        _player.Seek(position, true);
        _player.Pause();
        return true;
    }

    private static AnimationPlayer[] FindAnimationPlayers(Node root)
    {
        var found = new List<AnimationPlayer>();
        Collect(root, found);
        return found.ToArray();

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

    private static Dictionary<Node3D, Transform3D> CaptureBaseline(Node3D root)
    {
        var baseline = new Dictionary<Node3D, Transform3D> { [root] = root.Transform };
        Collect(root, baseline);
        return baseline;

        static void Collect(Node node, Dictionary<Node3D, Transform3D> baseline)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Node3D node3d)
                    baseline[node3d] = node3d.Transform;
                Collect(child, baseline);
            }
        }
    }
}
