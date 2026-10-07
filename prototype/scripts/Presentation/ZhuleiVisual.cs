#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Yudian.Presentation;

/// <summary>
/// E03 筑垒视觉适配：把主控给的状态键映射到现役 canonical
/// res://assets/lowfi-batch-r1/models/zhulei.glb（ART-U02 rev1）的源动作。挂到物理 actor
/// 下作视觉子节点，根保持 identity/1m/脚底原点，只动本子树局部节点，不动 actor 与游戏事实。
/// 动画时间由 Apply 的 delta 累加驱动（Play→Seek→Pause，引擎不自行推进）；合法切换先复位
/// 基线 pose 防上一个动作的关节串扰；非法键（含 manifest 有而合同没有的 towed）与非法
/// delta 先验后拒、保旧有效状态；idle 是 manifest 静态轮廓，无 clip。
/// </summary>
public partial class ZhuleiVisual : Node3D
{
    /// <summary>Apply 接受的单次 delta 上限（s）；超限按非法拒绝，视觉保持原状态。</summary>
    public const double DeltaLimit = 10.0;

    private const string ModelPath = "res://assets/lowfi-batch-r1/models/zhulei.glb";
    private const string ModelNodeName = "Model";

    /// <summary>键→(clip,循环) 映射，来源 manifest zhulei.json states（rev1）；idle 静态无 clip。</summary>
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

    /// <summary>实例化 canonical 模型并硬校验（唯一 AnimationPlayer、五 clip 时长为正，
    /// 缺失即抛不静默降级）；校验全过才落子节点，失败不残留半成品；仅成功一次。</summary>
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

    /// <summary>true=接受；false=非法输入（键不在六键内，或 delta 非有限/[0,DeltaLimit] 外）
    /// 被拒且保旧有效状态。paused=true 定格当前键不推进时间，但状态切换仍立即生效
    /// （新动作定格在 t0，不伪造运动）。须先成功 Initialize。</summary>
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
