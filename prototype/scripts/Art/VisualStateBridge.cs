#nullable enable
using System;
using Godot;
namespace Yudian;

// 只消费外部给出的状态；模型动作不能改变世界事实。
public sealed class VisualStateBridge
{
    public static readonly string[] States = ["idle", "move", "work", "charge", "disabled", "towed", "maintenance"];
    public string State { get; private set; } = "idle";
    private readonly Node3D _visual;
    private readonly Node3D? _workPart;
    private readonly AnimationPlayer? _player;

    public VisualStateBridge(Node3D visual)
    {
        _visual = visual;
        _workPart = visual.FindChild("WorkPart", true, false) as Node3D;
        _player = visual.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
    }

    public void SetState(string state)
    {
        if (Array.IndexOf(States, state) < 0) throw new ArgumentException("未知视觉状态", nameof(state));
        State = state;
        _player?.Stop();
        if (_workPart != null) _workPart.Rotation = Vector3.Zero;
        if (_player != null && _player.HasAnimation(state)) _player.Play(state);
        _visual.SetMeta("visual_state", state);
    }

    public void Preview(double delta)
    {
        if (State == "work" && _workPart != null) _workPart.RotateY((float)delta);
    }
}
