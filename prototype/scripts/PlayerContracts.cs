#nullable enable
using Godot;
namespace Yudian;

public sealed record PlayerRobotView(string Id, string Name, Vector3 Position, float Radius);
public sealed record PlayerJobView(string Id, string Stage, string WorkerId, double Progress, Vector3 Center, bool Active);
public sealed record PlayerReadModel(bool Ready, bool Paused, string Notice, PlayerJobView? Job, double TimeSeconds, long Version, float ExtentM, bool SaveExists);
public sealed record PlayerSitePreview(bool Legal, string Reason, Vector3 Center, long Version, double TargetHeight);
