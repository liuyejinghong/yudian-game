#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using Yudian.Presentation;
using Yudian.Terrain;

namespace Yudian;
public partial class Main
{
    private readonly Dictionary<GroundPatrol, (ZhuleiVisual Visual, Vector3 Previous)> _playerVisuals = new();
    private void AttachPlayerVisual(GroundPatrol actor)
    {
        var visual = new ZhuleiVisual(); actor.AddChild(visual); visual.Initialize();
        visual.Apply("idle", 0, false); _playerVisuals.Add(actor, (visual, actor.GlobalPosition));
    }
    private void TickPlayerVisuals(double delta)
    {
        UpdatePlayerSurface();
        bool frozen = _userPaused || _projectionPaused || _loadPending != null || _groundFault;
        foreach (var actor in _groundRobots)
        {
            if (!_playerVisuals.TryGetValue(actor, out var item)) continue;
            var (visual, previous) = item;
            var moved = actor.GlobalPosition - previous; moved.Y = 0;
            bool working = _levelJob?.Worker == actor && _levelJob.Stage == LevelStage.Working && WorkerAtStation(_levelJob);
            bool moving = moved.Length() > .0001f;
            if (!frozen)
            {
                var direction = working ? _levelJob!.Center - actor.GlobalPosition : moved;
                if (direction.X * direction.X + direction.Z * direction.Z > .0001f)
                    actor.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
            }
            string state = frozen ? visual.State : working ? "work" : moving ? "move" : "idle";
            double step = state == "move" ? moved.Length() / 1.6 : delta;
            visual.Apply(state, step, frozen);
            _playerVisuals[actor] = (visual, actor.GlobalPosition);
        }
    }
    private void RestorePlayerVisuals()
    {
        foreach (var actor in _groundRobots)
        {
            if (!_playerVisuals.TryGetValue(actor, out var item)) continue;
            var job = _levelJob?.Worker == actor ? _levelJob : null;
            string state = job?.Stage == LevelStage.Working ? "work" : job?.Stage == LevelStage.Travelling ? "move" : "idle";
            item.Visual.Apply("idle", 0, false); item.Visual.Apply(state, job?.Stage == LevelStage.Working ? job.Work.ElapsedSeconds : 0, false);
            _playerVisuals[actor] = (item.Visual, actor.GlobalPosition);
        }
    }
    private ShaderMaterial? _playerSurface;
    private ImageTexture? _committedMask;
    private TerrainRegionView? _surfaceRegion;
    private long _surfaceVersion = -1;
    public bool PlayerJobApplied => _levelJob?.AppliedVersion != null;
    public float SamplePlayerGround(float x, float z)
    {
        if (_liveTerrain == null || _groundVerified != _liveTerrain.Current.Version || _groundFault) return float.NaN;
        var t = _liveTerrain.Current;
        if (!float.IsFinite(x) || !float.IsFinite(z) || x < t.OriginXM || z < t.OriginZM ||
            x > t.OriginXM + (t.Columns - 1) * t.SpacingM || z > t.OriginZM + (t.Rows - 1) * t.SpacingM) return float.NaN;
        return (float)SavedGroundHeight(t, x, z);
    }
    private void UpdatePlayerSurface()
    {
        if (_liveTerrain == null) return;
        _playerSurface ??= new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://assets/d1-art/surface.gdshader") };
        var t = _liveTerrain.Current;
        if (_surfaceRegion != _liveTerrain || _surfaceVersion != t.Version)
        {
            var pixels = new byte[t.HeightsM.Count];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = t.HeightsM[i] != _liveInitial.HeightsM[i] ? (byte)255 : (byte)0;
            using var image = Image.CreateFromData(t.Columns, t.Rows, false, Image.Format.R8, pixels);
            var next = ImageTexture.CreateFromImage(image);
            _playerSurface.SetShaderParameter("committed_mask", next);
            _committedMask?.Dispose(); _committedMask = next;
            _playerSurface.SetShaderParameter("use_committed_mask", true);
            _playerSurface.SetShaderParameter("world_origin", new Vector2((float)t.OriginXM, (float)t.OriginZM));
            _playerSurface.SetShaderParameter("world_span", new Vector2((float)((t.Columns - 1) * t.SpacingM), (float)((t.Rows - 1) * t.SpacingM)));
            _liveTerrain.MeshNode.MaterialOverride = _playerSurface;
            _surfaceRegion = _liveTerrain; _surfaceVersion = t.Version;
        }
        bool working = _levelJob?.Stage is LevelStage.Working or LevelStage.WaitingForSpace;
        _playerSurface.SetShaderParameter("stage", working ? 1 : 0);
        if (_levelJob != null) _playerSurface.SetShaderParameter("region_center", new Vector2(_levelJob.Center.X, _levelJob.Center.Z));
    }
}
