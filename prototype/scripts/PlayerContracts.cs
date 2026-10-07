#nullable enable
using Godot;
namespace Yudian;

public sealed record PlayerRobotView(string Id, string Name, Vector3 Position, float Radius);
public sealed record PlayerJobView(string Id, string Stage, string WorkerId, double Progress, Vector3 Center, bool Active);
public sealed record PlayerReadModel(bool Ready, bool Paused, string Notice, PlayerJobView? Job, double TimeSeconds, long Version, float ExtentM, bool SaveExists);
public sealed record PlayerSitePreview(bool Legal, string Reason, Vector3 Center, long Version, double TargetHeight, float Radius = 2);
public sealed record BuildBlueprintView(string Id, string Name, float Radius, string Cost);
public sealed record FacilityView(string Id, string Type, string Name, Vector3 Position, float Radius, bool Built, bool Powered, string? Source);
public sealed record RobotSupportView(string Id, double Energy, double Durability, double Capacity, int Cargo, string State, string Reason);
public sealed record BootstrapReadModel(bool Enabled, string Stock, string Power, BuildBlueprintView[] Blueprints, FacilityView[] Facilities, RobotSupportView[] Robots, long Revision);
