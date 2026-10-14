#nullable enable
using Godot;
namespace Yudian;

public sealed record PlayerRobotView(string Id, string Name, Vector3 Position, float Radius);
public sealed record PlayerJobView(string Id, string Stage, string WorkerId, double Progress, Vector3 Center, bool Active);
public sealed record PlayerReadModel(bool Ready, bool Paused, string Notice, PlayerJobView? Job, double TimeSeconds, long Version, float ExtentM, bool SaveExists, bool EntryOpen = false, bool EntryLoading = false, bool ManagementPaused = false, long WorldEpoch = 0);
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
    int ActiveSlot, int SelectedSlot, string PendingAction, string Notice, PlayerSlotView[] Slots, long WorldEpoch,
    long WorldTransitionRevision = 0);
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
    string Consequence, bool Recovery, bool IsDraft, string? CurrentGoalId, string CurrentStage, long ObservedVersion, long TerrainVersion,
    MaterialAmountView[]? EligibleStock = null, long WorldEpoch = 0);
public sealed record ConnectionOptionView(string FacilityId, string FacilityName, string SourceId, string SourceName, Vector3 FacilityPosition,
    Vector3 SourcePosition, double DistanceM, double RangeM, bool CandidateLegal, bool CanSubmit, string CandidateReason, string SubmitReason,
    MaterialAmountView[] Cost, MaterialAmountView[] Gap, string? SupplyId, bool Recovery, long ObservedVersion);

public sealed record MaterialDefinitionView(string Id, string Name, string Description, string Category, string Unit, string VisualRef);
public sealed record RecipeDefinitionView(string Id, string Name, string Description, string Category, string Unit, string VisualRef,
    MaterialAmountView[] Input, MaterialAmountView[] Output, double WorkSeconds, double Power);
public sealed record BuildingDefinitionView(string Id, string Name, string Description, string Category, string Unit, string VisualRef,
    MaterialAmountView[] Cost, double Radius, double WorkSeconds);
public sealed record PlayerDefinitionsView(MaterialDefinitionView[] Materials, RecipeDefinitionView[] Recipes, BuildingDefinitionView[] Buildings);

public enum WorldObjectKind { Robot, Mine, Facility }
public sealed record WorldObjectView(long WorldEpoch, WorldObjectKind Kind, string Id, string DefinitionId, string Name, Vector3 Position, float Radius);
public sealed record MineDetailView(string MaterialId, int Remaining, MaterialAmountView[] Products, string Prerequisite);
public sealed record RobotDetailView(double Energy, double Durability, double Capacity, string Role, string State, string Reason,
    string? GoalId, string? TaskId, MaterialAmountView[] Cargo);
public sealed record FacilityDetailView(string Type, bool Built, bool Connected, bool Powered, string? SourceId,
    string OperatingState, MaterialAmountView[] Stored);
public sealed record PlayerObjectDetailView(WorldObjectView Object, string Description, MineDetailView? Mine = null,
    RobotDetailView? Robot = null, FacilityDetailView? Facility = null);

public enum PlayerWorkKind { Build, Connection, Restock, Mine, Recipe, Haul, Level }
public enum PlayerWorkStage { Authorized, PreparingMaterials, Travelling, Mining, Processing, Transporting, WaitingForRoute,
    Levelling, AwaitingPhysics, Constructing, Completed, Cancelled, Blocked, Unknown }
public enum PlayerWorkStatus { Running, Waiting, Blocked, Completed, Cancelled, Failed }
public enum PlayerProgressBasis { None, MiningWork, RecipeWork, ConstructionWork, LevellingWork }
public enum PlayerReasonCode { None, WaitingForWorker, WaitingForStation, WaitingForRoute, WaitingForPower,
    WaitingForProjection, Unknown, Completed, Cancelled, Blocked }
public sealed record PlayerWorkView(long WorldEpoch, string GoalId, string? TaskId, PlayerWorkKind Kind, PlayerWorkStage Stage,
    PlayerWorkStatus Status, string Name, PlayerReasonCode ReasonCode, string Reason, PlayerProgressBasis ProgressBasis,
    double? Progress, string[] WorkerIds, Vector3 Center, bool Active, long ObservedVersion);

public enum MaterialContainerKind { Warehouse, RobotCargo, Mine, Worksite, Processing, Consumed, Other }
public sealed record MaterialLocationAmountView(string Id, string Name, int Quantity, int Reserved, int Unreserved);
public sealed record MaterialContainerView(string Id, string Name, MaterialContainerKind Kind, string? OwnerId, Vector3? Position,
    int Capacity, MaterialLocationAmountView[] Materials);
public sealed record MaterialReservationView(string GoalId, string ContainerId, string MaterialId, string Name, int Quantity);
public sealed record ProductionBatchView(string TaskId, string GoalId, string RecipeId, string FacilityId, bool Paid,
    PlayerWorkStage Stage, MaterialAmountView[] Input, MaterialAmountView[] ExpectedOutput, double? Progress);
public sealed record PlayerMaterialsView(MaterialStockView[] Summary, MaterialContainerView[] Locations,
    MaterialReservationView[] Reservations, ProductionBatchView[] Batches, long ObservedVersion, long WorldEpoch);
