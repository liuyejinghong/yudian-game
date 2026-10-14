#nullable enable
using Godot;
namespace Yudian;

public sealed record PlayerRobotView(string Id, string Name, Vector3 Position, float Radius);
public sealed record PlayerJobView(string Id, string Stage, string WorkerId, double Progress, Vector3 Center, bool Active);
public sealed record PlayerReadModel(bool Ready, bool Paused, string Notice, PlayerJobView? Job, double TimeSeconds, long Version, float ExtentM, bool SaveExists, bool EntryOpen = false, bool EntryLoading = false);
public sealed record PlayerSitePreview(bool Legal, string Reason, Vector3 Center, long Version, double TargetHeight, float Radius = 2);
public sealed record BuildBlueprintView(string Id, string Name, float Radius, string Cost);
public sealed record FacilityView(string Id, string Type, string Name, Vector3 Position, float Radius, bool Built, bool Powered, string? Source, bool ServiceConnected = false);
public sealed record RobotSupportView(string Id, double Energy, double Durability, double Capacity, int Cargo, string State, string Reason);
public sealed record BootstrapReadModel(bool Enabled, string Stock, string Power, BuildBlueprintView[] Blueprints, FacilityView[] Facilities, RobotSupportView[] Robots, long Revision);

public sealed record DevelopmentDirectionView(string Id, string Name, string Cost, string Need, string Consequence, bool Feasible, string Reason);
public sealed record DevelopmentReadModel(bool Enabled, string Provider, string Goal, string Stage, string Need, string Reason, string Choices, string Mines,
    string Supply = "", string Prepared = "", string Transit = "", string InProcess = "", DevelopmentDirectionView[]? Directions = null);

// Detached values only. UI never receives a ledger, mutable tutorial history, or a file path.
public sealed record PlayerSlotView(int Slot, string Label, bool Exists, bool Loadable, string SavedAtUtc, string TutorialStage, string Summary, string Error);
public sealed record PlayerLifecycleView(string Page, string BackPage, bool InGame, bool MenuPaused, bool UserPaused, bool Busy,
    int ActiveSlot, int SelectedSlot, string PendingAction, string Notice, PlayerSlotView[] Slots, long WorldEpoch);
public sealed record TutorialMilestoneView(string Id, string Title, bool Achieved);
public sealed record TutorialView(bool Enabled, bool LegacyPractice, string StageId, string Title, string Instruction, string Why,
    string NextAction, string? NeededType, string? FacilityId, string? SourceId, int Achieved, int Total, bool Completed, bool ShowConclusion,
    string? FirstDirection, string? FirstGoalId, string? SecondGoalId, string? StorageId, string? ExpansionSolarId, string? SecondChargerId,
    TutorialMilestoneView[] Milestones);
public sealed record PlayerSettingsView(double UiScale, double CameraSpeed, string HelpDetail, long Revision);
public sealed record MaterialAmountView(string Id, string Name, int Quantity);
public sealed record MaterialStockView(string Id, string Name, int Stored, int Available, int InTransit, int InProcess);
public sealed record PlayerEconomyView(MaterialStockView[] Materials, bool Daylight, double GenerationPerSecond, double ConnectionRangeM);
public sealed record SupplyRouteView(string SupplyId, string Name, Vector3 Position, bool RouteFound, double RouteDistanceM, string Consequence);
public sealed record PowerSourceView(string SourceId, string Name, Vector3 Position, double DistanceM, double RangeM, bool InRange, bool Powered);
public sealed record BuildPlanView(string Type, string Name, Vector3 Center, float Yaw, float Radius, bool CandidateLegal, bool CanSubmit,
    string CandidateReason, string SubmitReason, MaterialAmountView[] Cost, MaterialAmountView[] Gap, MaterialStockView[] Stock,
    SupplyRouteView[] SupplyRoutes, string? SupplyId, PowerSourceView[] PowerSources, string? SourceId, double? PowerDistanceM, double PowerRangeM,
    string Consequence, bool Recovery, bool IsDraft, string? CurrentGoalId, string CurrentStage, long ObservedVersion, long TerrainVersion);
public sealed record ConnectionOptionView(string FacilityId, string FacilityName, string SourceId, string SourceName, Vector3 FacilityPosition,
    Vector3 SourcePosition, double DistanceM, double RangeM, bool CandidateLegal, bool CanSubmit, string CandidateReason, string SubmitReason,
    MaterialAmountView[] Cost, MaterialAmountView[] Gap, string? SupplyId, bool Recovery, long ObservedVersion);
