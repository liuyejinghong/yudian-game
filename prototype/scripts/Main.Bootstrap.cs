#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Yudian.Resources;
using Yudian.Navigation;
using Yudian.Terrain;

namespace Yudian;
public partial class Main
{
    private bool BootstrapEnabled => _playerMode && System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_SELF_TEST") != "1";
    private BootstrapConfig _bootstrapConfig = null!;
    private MaterialLedger _ledger = null!;
    private readonly List<BaseFacility> _baseFacilities = new();
    private readonly Dictionary<string, RobotHealth> _health = new();
    private readonly Dictionary<string, string> _stations = new();
    private readonly Dictionary<string, RouteExecution> _routes = new();
    private readonly Dictionary<string, ServiceJob> _services = new();
    private readonly Dictionary<string, Node3D> _facilityNodes = new();
    private long _baseRevision;
    private int _buildSequence, _serviceSequence;
    private BuildJob? _buildJob;
    private sealed class BaseFacility
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required float[] Position { get; init; }
        public float Yaw { get; init; }
        public bool Built { get; set; }
        public string? Source { get; set; }
        public string? ConnectionId { get; set; }
    }
    private sealed class RobotHealth
    {
        public double Energy { get; set; }
        public double Durability { get; set; }
        public double Travel { get; set; }
        public string Reason { get; set; } = "";
    }
    private sealed class RouteExecution
    {
        public required Vector3 Destination;
        public long WorldVersion, FacilityRevision;
        public double Seconds, Stalled;
        public bool Escaping;
        public Vector3 EscapeDestination;
    }
    private sealed class ServiceJob
    {
        public required string Id { get; init; }
        public required string Robot { get; init; }
        public required string Facility { get; init; }
        public required string Kind { get; init; }
        public required float[] Station { get; init; }
        public float[]? ReturnTo { get; set; }
        public double Progress { get; set; }
        public double Waiting { get; set; }
        public bool Paid { get; set; }
        public bool Returning { get; set; }
        public bool Blocked { get; set; }
    }
    private sealed class BuildJob
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required string Facility { get; init; }
        public string? Source { get; init; }
        public string Supply { get; init; } = "lander";
        public required float[] Center { get; init; }
        public float Yaw { get; init; }
        public required Dictionary<string,int> Cost { get; init; }
        public required string Builder { get; init; }
        public required string Hauler { get; init; }
        public required float[] Station { get; init; }
        public float[]? HaulStation { get; set; }
        public Dictionary<string,float[]> Clearance { get; init; } = new();
        public required string Patch { get; init; }
        public string Stage { get; set; } = "Preparing";
        public double Work { get; set; }
        public double Waiting { get; set; }
        public int Trip { get; set; }
        public string Reason { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public PlayerReasonCode FeedbackCode { get; set; } = PlayerReasonCode.Unknown;
        public bool Active => Stage is not ("Completed" or "Cancelled" or "Blocked");
    }
    private BuildingDefinition Definition(string type) => _bootstrapConfig.Buildings.Single(x => x.Id == type);
    private float BaseRadius(BaseFacility f) => f.Type == "lander" ? 2.3f : (float)Definition(f.Type).Radius;
    private string BaseName(string type) => type == "lander" ? "着陆器" : type=="connection"?"供电连接":Definition(type).Name;
    private GroundPatrol Actor(string id) => _groundRobots.Single(x => x.Name.ToString() == id);
    private static string CargoContainer(string id) => "cargo:" + id;
    private bool Operational(GroundPatrol a) => !BootstrapEnabled || _health.TryGetValue(a.Name.ToString(),out var h) && h.Energy > 0 && h.Durability > 0;
    private bool Servicing(GroundPatrol a) => BootstrapEnabled && _services.ContainsKey(a.Name.ToString());
    private bool Daylight => _playerTime % _bootstrapConfig.DaySeconds < _bootstrapConfig.DaylightSeconds;
    private bool ServiceConnected(BaseFacility f) => f.Built && f.Source != null && _baseFacilities.Any(x=>x.Id==f.Source && x.Built && x.Type=="solar");
    private bool Powered(BaseFacility f) => f.Built && (f.Type == "solar" ? Daylight : f.Source != null && _baseFacilities.Any(x => x.Id == f.Source && x.Built && x.Type == "solar") && Daylight);

    private void InitializeBootstrap()
    {
        _bootstrapConfig = LoadBootstrapConfig(DevelopmentEnabled ? "d12-firstplay-1" : "d11-bootstrap-1");
        _bootstrapConfig.Validate();
        _ledger = new(new([new("lander",500,new(_bootstrapConfig.Initial)),new("spent",MaterialLedger.Limit,new())],[],new()));
        _baseFacilities.Add(new(){Id="lander",Type="lander",Position=[7,0,-22],Built=true});
        RebuildBaseFacilities();
        if(DevelopmentEnabled)InitializeDevelopment();
    }
    private void InitializeRobotHealth()
    {
        foreach(var a in _groundRobots)
        {
            string id = a.Name.ToString(); _ledger.AddContainer(CargoContainer(id),_bootstrapConfig.CargoCapacity);
            _health.Add(id,new(){Energy=_bootstrapConfig.Capacity,Durability=_bootstrapConfig.Capacity,Travel=a.TravelledM});
            a.CollisionMask = 3;
        }
    }
    private void RebuildBaseFacilities()
    {
        foreach(var n in _facilityNodes.Values){RemoveChild(n);n.QueueFree();}
        _facilityNodes.Clear();_facilityPositions.Clear();
        foreach(var f in _baseFacilities)
        {
            var p = LoadVector(f.Position); p.Y = (float)SavedGroundHeight(_liveTerrain!.Current,p.X,p.Z);
            _facilityPositions.Add(p);
            var root = new Node3D{Name="Base_"+f.Id,Position=p,Rotation=new(0,f.Yaw,0)};AddChild(root);_facilityNodes.Add(f.Id,root);
            if(f.Built)
            {
                string path = f.Type switch {"solar"=>"facilities/solar-r1/solar-r1.glb","processor"=>DevelopmentEnabled?"facilities/processor-r1/preview.tscn":"facilities/processor-r1/processor-r1.glb",_=>"lowfi-batch-r1/models/"+f.Type+".glb"};
                root.AddChild(GD.Load<PackedScene>("res://assets/"+path).Instantiate<Node3D>());
                root.AddChild(new StaticBody3D { CollisionLayer=2, CollisionMask=0 });
                var body=(StaticBody3D)root.GetChild(root.GetChildCount()-1);
                body.AddChild(new CollisionShape3D{Shape=new CylinderShape3D{Radius=BaseRadius(f),Height=2},Position=new(0,1,0)});
            }
            else
            {
                MeshPart(root,new CylinderMesh{TopRadius=BaseRadius(f),BottomRadius=BaseRadius(f),Height=.04f},new Color(.8f,.55f,.2f,.45f),new(0,.05f,0));
            }
            if(f.Source is {} source)
            {
                var from=_baseFacilities.Single(x=>x.Id==source);var a=LoadVector(from.Position);var b=LoadVector(f.Position);
                var line=new ImmediateMesh();line.SurfaceBegin(Mesh.PrimitiveType.Lines);line.SurfaceSetColor(new Color(.9f,.65f,.15f));
                line.SurfaceAddVertex(a-p+Vector3.Up*.15f);line.SurfaceAddVertex(b-p+Vector3.Up*.15f);line.SurfaceEnd();
                root.AddChild(new MeshInstance3D{Mesh=line,MaterialOverride=new StandardMaterial3D{VertexColorUseAsAlbedo=true,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}});
            }
        }
    }
    public BootstrapReadModel ReadBootstrap()
    {
        if(!BootstrapEnabled || _bootstrapConfig==null || !_groundReady) return new(false,"","",[],[],[],0);
        var depots=_baseFacilities.Where(f=>f.Built&&f.Type is "lander" or "storage").Select(f=>f.Id).ToArray();
        string stock=string.Join(" · ",MaterialLedger.Materials.Where(m=>depots.Sum(c=>_ledger.Count(c,m))>0||_bootstrapConfig.Initial[m]>0).Select(m=>$"{MaterialName(m)} {depots.Sum(c=>_ledger.Count(c,m))}（可用{depots.Sum(c=>_ledger.Available(c,m))}）"));
        return new(true,stock,$"{(Daylight?"日照":"夜间")} · 发电 {_baseFacilities.Count(f=>f.Type=="solar"&&Powered(f))*_bootstrapConfig.SolarOutput:0.#} /s · 电缆连接 ≤{_bootstrapConfig.ConnectionRangeM:0}m，每段{_bootstrapConfig.ConnectionCables}线缆",
            _bootstrapConfig.Buildings.Select(b=>new BuildBlueprintView(b.Id,b.Name,(float)b.Radius,FormatMaterials(BuildCost(b.Id)))).ToArray(),
            _baseFacilities.Select(f=>new FacilityView(f.Id,f.Type,BaseName(f.Type),LoadVector(f.Position),BaseRadius(f),f.Built,Powered(f),f.Source,ServiceConnected(f))).ToArray(),
            _health.Select(x=>new RobotSupportView(x.Key,x.Value.Energy,x.Value.Durability,_bootstrapConfig.Capacity,_ledger.Load(CargoContainer(x.Key)),
                x.Value.Energy<=0&&x.Value.Durability<=0?"零电且机械停机":x.Value.Energy<=0?"零电停机":x.Value.Durability<=0?"机械停机":_services.TryGetValue(x.Key,out var s)?s.Blocked?"保障阻塞，可重试":s.Returning?"保障返程":s.Kind=="repair"?"维修保障":"回充保障":"可用",x.Value.Reason)).ToArray(),_baseRevision);
    }
    private static string MaterialName(string m) => DefinitionCatalog.Material(m).Name;
    private static string FormatMaterials(Dictionary<string,int> amounts)=>string.Join("、",amounts.Select(x=>$"{MaterialName(x.Key)}{x.Value}"));
    private TerrainPatch BuildPatch(TerrainSnapshot t, Vector3 center, float radius, string id)
    {
        var heights=t.HeightsM.ToArray();
        double reach=radius+t.SpacingM;
        for(int r=1;r<t.Rows-1;r++)for(int c=1;c<t.Columns-1;c++)
        {double x=t.OriginXM+c*t.SpacingM-center.X,z=t.OriginZM+r*t.SpacingM-center.Z;if(x*x+z*z<=reach*reach)heights[r*t.Columns+c]=0;}
        return GroundPatchFromHeights(t,heights,id);
    }
    private bool BuildClearingActor(string actor)=>_buildJob is {} j&&(actor==j.Hauler||DevelopmentEnabled&&_productionTasks.Any(t=>t.Goal==_development?.Id&&t.Kind=="haul"&&t.Robot==actor));
    private NavObstacle[] RouteObstacles(string actor, bool includeSites=true)=>_baseFacilities.Where(f=>(f.Built||includeSites)&&!(!f.Built&&f.Id==_buildJob?.Facility&&_buildJob.Stage=="Preparing"&&BuildClearingActor(actor))).Select(f=>new NavObstacle(new(LoadVector(f.Position).X,LoadVector(f.Position).Z),BaseRadius(f)))
        .Concat(_groundRobots.Where(a=>a.Name.ToString()!=actor).Select(a=>new NavObstacle(new(a.GlobalPosition.X,a.GlobalPosition.Z),a.BodyRadius))).ToArray();
    private RouteResult FindRoute(GroundPatrol a, Vector3 to) => PlanBaseRoute(a, to).Route;
    private bool OrderBase(GroundPatrol actor,Vector3 destination)
    {
        if (_managementPaused) return false;
        var planned = PlanBaseRoute(actor, destination);
        if(!planned.Route.Found){_health[actor.Name.ToString()].Reason=planned.Route.Reason;return false;}
        AssignPlannedRoute(actor, destination, planned.Route, planned.Escape); return true;
    }
    private Vector3? FreeStation(BaseFacility f,GroundPatrol a,TerrainPatch? excluded=null)
    {
        var p=LoadVector(f.Position);float d=BaseRadius(f)+a.BodyRadius+1.2f;
        foreach(var offset in new[]{new Vector3(d,0,0),new Vector3(-d,0,0),new Vector3(0,0,d),new Vector3(0,0,-d)})
        {var station=p+offset;if(excluded!=null&&(TouchesFootprint(excluded,station.X,station.Z,a.BodyRadius)||_buildJob!.Clearance.Any(x=>x.Key!=a.Name.ToString()&&XzDistance(station,LoadVector(x.Value))<=a.BodyRadius+Actor(x.Key).BodyRadius+.5f)))continue;if(FindRoute(a,station).Found){station.Y=(float)SavedGroundHeight(_liveTerrain!.Current,station.X,station.Z);return station;}}
        return null;
    }
    private bool TakeStation(string facility,string robot)=>!_stations.TryGetValue(facility,out var owner)?_stations.TryAdd(facility,robot):owner==robot;
    private void ReleaseStations(string robot){foreach(var key in _stations.Where(x=>x.Value==robot).Select(x=>x.Key).ToArray())_stations.Remove(key);}
    private static bool Arrived(GroundPatrol actor,Vector3 station)=>actor.IsOnFloor()&&actor.OrderReached&&XzDistance(actor.GlobalPosition,station)<=.25f&&new Vector2(actor.Velocity.X,actor.Velocity.Z).Length()<=.05f;
    public PlayerSitePreview PreviewBuild(string type, Vector3 center, float yaw = 0)
    {
        var plan = ReadBuildPlan(type, center, yaw);
        return new(plan.CanSubmit, plan.CanSubmit ? "可建设 · " + plan.Name + " · " + FormatMaterials(BuildCost(type)) : plan.SubmitReason,
            plan.Center, plan.ObservedVersion, 0, plan.Radius);
    }
    public void QueueBuild(string type,Vector3 center,float yaw,long observedVersion)
        =>QueuePlayer(new("build:"+type,center,observedVersion,null,yaw));
    private void StartBaseBuild(string type,Vector3 center,float yaw,string? connectionTarget=null,string? source=null,string supply="lander")
    {
        if(_buildSequence==int.MaxValue)throw new InvalidOperationException("工程编号已达上限");
        bool wire=type=="connection";
        var builder=_groundRobots.Where(a=>a.Name.ToString().StartsWith("Robot_Zhulei_")&&Operational(a)&&!Servicing(a)).OrderBy(a=>XzDistance(a.GlobalPosition,center)).FirstOrDefault();
        var hauler=_groundRobots.Where(a=>a.Name.ToString().StartsWith("Robot_Tuoyun_")&&Operational(a)&&!Servicing(a)).OrderBy(a=>_ledger.Load(CargoContainer(a.Name.ToString()))==0?1:0).FirstOrDefault();
        if(builder==null||hauler==null)throw new InvalidOperationException("需要可行动的筑垒和驮运");
        string id="build-"+(_buildSequence+1);var cost=BuildCost(type);
        var f=wire?_baseFacilities.Single(f=>f.Id==connectionTarget):new BaseFacility{Id=id,Type=type,Position=SavedVector(new(center.X,0,center.Z)),Yaw=yaw};
        var patch=wire?GroundPatchFromHeights(_liveTerrain!.Current,_liveTerrain.Current.HeightsM.ToArray(),id):BuildPatch(_liveTerrain!.Current,center,BaseRadius(f),id);
        var station=FindBuildStation(builder,center,patch,BaseRadius(f));
        if(station==null)throw new InvalidOperationException("没有可达且不触及整平范围的施工站");
        var nextLedger = new MaterialLedger(_ledger.Snapshot());
        nextLedger.AddContainer(wire ? id : f.Id, 100);
        nextLedger.Reserve(id,supply,cost);
        _ledger = nextLedger; _buildSequence++;
        _levelJob=null;
        _buildJob=new(){Id=id,Type=type,Facility=f.Id,Source=source,Supply=supply,Center=SavedVector(new(center.X,0,center.Z)),Yaw=yaw,Cost=cost,Builder=builder.Name.ToString(),Hauler=hauler.Name.ToString(),Station=SavedVector(station.Value),Patch=TerrainDataCodec.Serialize(patch)};
        if(!wire){_baseFacilities.Add(f);_baseRevision++;RebuildBaseFacilities();}
        if (!wire && !TutorialEnabled) _intentVersion++;
        _playerNotice="已登记工程；运输和实际工段后落成";
    }
    private Vector3? FindBuildStation(GroundPatrol actor,Vector3 center,TerrainPatch patch,float radius)
    {
        float d=radius+(float)patch.Base.SpacingM*2+1.4f;
        foreach(var p in new[]{center+new Vector3(d,0,0),center+new Vector3(-d,0,0),center+new Vector3(0,0,d),center+new Vector3(0,0,-d)})
        {if(TouchesFootprint(patch,p.X,p.Z,actor.BodyRadius)||!FindRoute(actor,p).Found)continue;var station=p;station.Y=(float)SavedGroundHeight(patch.Base,p.X,p.Z);return station;}
        return null;
    }
    private void ConnectNextFacility()
    {
        var option = ReadConnectionOptions().Where(o => o.CanSubmit).OrderBy(o => o.DistanceM).FirstOrDefault()
            ?? throw new InvalidOperationException("没有可付费连接且位于已建阵列12m内的未接线设施");
        ProcessExplicitConnection(new("connection", Vector3.Zero, option.ObservedVersion, option.FacilityId, 0, option.SourceId));
    }
    private void CancelBaseBuild()
    {
        if(_buildJob is not {} j || (!j.Active && j.Stage != "Blocked"))return;
        j.Stage="Cancelled";j.Reason="已取消；地形、现场物料与在途货物保留";j.FeedbackCode=PlayerReasonCode.Cancelled;_ledger.Release(j.Id);
        foreach(var id in new[]{j.Builder,j.Hauler}.Concat(j.Clearance.Keys).Distinct())
        ClearBaseWorkOrder(id);
        _playerNotice=j.Reason;
    }
    private void ClearBaseWorkOrder(string id)
    {
        if(_services.TryGetValue(id,out var service)&&!service.Returning){service.ReturnTo=null;return;}
        _services.Remove(id);StopBase(Actor(id));ReleaseStations(id);
    }
    private bool OrderDelivery(BuildJob j,GroundPatrol hauler)
    {
        var site=_baseFacilities.Single(f=>f.Id==j.Facility);
        var dock=FreeStation(site,hauler);
        if(dock==null){j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="已取货并保留载荷；当前未找到可达卸货工位候选";return false;}
        if(!TakeStation(site.Id,j.Hauler)){j.FeedbackCode=PlayerReasonCode.WaitingForStation;j.Reason="已取货并保留载荷；等待卸货工位占用解除";return false;}
        j.HaulStation=SavedVector(dock.Value);
        if(!OrderBase(hauler,dock.Value)){j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="已取货并保留载荷；卸货路线重验未找到路线";return false;}
        j.FeedbackCode=PlayerReasonCode.None;j.Reason="已真实取货，驮运载货前往工地";return true;
    }
    private static string BaseStageText(string stage)=>stage switch
    {
        "Preparing"=>"准备建设", "LevelTravel"=>"前往整平", "Levelling"=>"整平作业", "LevelPhysics"=>"验证地形",
        "Fetching"=>"安排运输", "Pickup"=>"前往取货", "CargoWaiting"=>"载货等待路线", "Delivering"=>"载货运输",
        "BuilderTravel"=>"前往施工", "Building"=>"施工", "Completed"=>"已完成", "Cancelled"=>"已取消", _=>"阻塞，可重试"
    };
    private Dictionary<string,int> BuildCost(string type)
    {
        if(type=="connection")return new(){{"cable",_bootstrapConfig.ConnectionCables}};
        var cost=new Dictionary<string,int>(Definition(type).Cost);
        if(type=="repair")cost["parts"]+=_bootstrapConfig.RepairParts*2;
        return cost;
    }
    private void StopBase(GroundPatrol actor){actor.ClearOrder();_routes.Remove(actor.Name.ToString());}
    private void RetryBaseBuild()
    {
        var blocked=_services.Values.Where(s=>s.Blocked).ToArray();
        if(blocked.Length>0)
        {
            foreach(var service in blocked)
            {
                var actor=Actor(service.Robot);var destination=LoadVector(service.Returning?service.ReturnTo!:service.Station);
                if(!Operational(actor)||service.Kind=="repair"&&!service.Returning&&!service.Paid&&_ledger.Available(service.Facility,"parts")<_bootstrapConfig.RepairParts||!service.Returning&&!TakeStation(service.Facility,service.Robot))continue;
                if(OrderBase(actor,destination)){service.Blocked=false;_health[service.Robot].Reason="重新前往保障或原工作站";}
                else ReleaseStations(service.Robot);
            }
            _playerNotice=blocked.Any(s=>s.Blocked)?"保障路线或工位仍受阻；原事实保留":"保障路线已重试；原工程继续";return;
        }
        if(_buildJob is not {} j || j.Stage is not ("Blocked" or "Cancelled") || _levelJob?.Active==true)
            throw new InvalidOperationException("没有可重试的当前工程");
        _ledger.Release(j.Id);
        string buffer=j.Type=="connection"?j.Id:j.Facility;
        var missing=j.Cost.Where(x=>x.Value>_ledger.Count(buffer,x.Key)+_ledger.Count(CargoContainer(j.Hauler),x.Key))
            .ToDictionary(x=>x.Key,x=>x.Value-_ledger.Count(buffer,x.Key)-_ledger.Count(CargoContainer(j.Hauler),x.Key));
        if(missing.Count>0)_ledger.Reserve(j.Id,j.Supply,missing);
        var patch=TerrainDataCodec.ParsePatch(j.Patch);
        j.Stage=patch.HeightsM.SequenceEqual(_liveTerrain!.Current.HeightsM)||j.Type=="connection"?"Fetching":"Preparing";
        if(j.Stage=="Preparing"&&patch.Base.Version!=_liveTerrain.Current.Version)throw new InvalidOperationException("地形已变化，保留工程；需另选位置");
        j.Waiting=0;j.FeedbackCode=PlayerReasonCode.None;j.Reason=j.Stage=="Fetching"?"重新安排余料运输，保留现场与载货":"重新安排让位与施工，保留已结算物料";
        _playerNotice="继续原工程；不会重扣已消耗物料";
    }

    private bool TickBuildWork(GroundPatrol actor,BuildJob j,double delta,double duration)
    {
        if(!Arrived(actor,LoadVector(j.Station)))return false;
        double step=Math.Min(Math.Max(0,duration-j.Work),WorkBudget(actor,delta));
        j.Work+=step;SpendWork(actor,step);return j.Work>=duration;
    }
    private void TickBaseBuild(double delta)
    {
        var j=_buildJob;if(_managementPaused || j?.Active!=true)return;
        j.FeedbackCode=PlayerReasonCode.None;
        var builder=Actor(j.Builder);var hauler=Actor(j.Hauler);var station=LoadVector(j.Station);var center=LoadVector(j.Center);var cargo=CargoContainer(j.Hauler);string buffer=j.Type=="connection"?j.Id:j.Facility;
        if(Servicing(builder)||Servicing(hauler)){j.Reason="保障中，原工程、载荷与进度保留";return;}
        if(!Operational(builder)||!Operational(hauler)){j.Reason="执行者停机；货物与工程保留，救援尚待D2";return;}
        j.Waiting+=delta;if(j.Waiting>180){j.Stage="Blocked";_ledger.Release(j.Id);j.Reason="180秒未完成当前阶段；事实保留，可重试";foreach(var id in new[]{j.Builder,j.Hauler}.Concat(j.Clearance.Keys).Distinct())ClearBaseWorkOrder(id);return;}
        string previous=j.Stage;
        switch(j.Stage)
        {
            case "Preparing":
                var patch=TerrainDataCodec.ParsePatch(j.Patch);
                if(patch.HeightsM.SequenceEqual(patch.Base.HeightsM)){j.Stage="Fetching";break;}
                var clearing=_groundRobots.Where(a=>BuildClearingActor(a.Name.ToString()));
                bool waiting=false;
                foreach(var actor in clearing)
                {
                    string actorId=actor.Name.ToString();
                    if(!j.Clearance.ContainsKey(actorId)&&TouchesFootprint(patch,actor.GlobalPosition.X,actor.GlobalPosition.Z,actor.BodyRadius))
                    {
                        if(!Operational(actor)||Servicing(actor)){waiting=true;continue;}
                        var safeDock=FreeStation(_baseFacilities.Single(f=>f.Id==j.Supply),actor,patch);
                        if(safeDock==null||!OrderBase(actor,safeDock.Value)){waiting=true;continue;}
                        j.Clearance.Add(actorId,SavedVector(safeDock.Value));
                    }
                    if(!j.Clearance.TryGetValue(actorId,out var safePoint))continue;
                    var safe=LoadVector(safePoint);
                    if(!Operational(actor)||Servicing(actor)){waiting=true;continue;}
                    if(!_routes.TryGetValue(actorId,out var escape)||escape.Destination!=safe)OrderBase(actor,safe);
                    if(!Arrived(actor,safe))waiting=true;
                }
                if(waiting){j.Reason="驮运先到范围外取货位停稳，再整平";break;}
                foreach(var id in j.Clearance.Keys)StopBase(Actor(id));j.Clearance.Clear();
                if(TakeStation(j.Facility,j.Builder)&&OrderBase(builder,station)){j.Stage="LevelTravel";j.Reason="筑垒前往整平施工站";}break;
            case "LevelTravel":if(Arrived(builder,station)){j.Stage="Levelling";j.Work=0;}break;
            case "Levelling":
                if(TickBuildWork(builder,j,delta,3))
                {
                    var result=SubmitGroundResult(TerrainDataCodec.ParsePatch(j.Patch));
                    if(result?.Status is TerrainCommitStatus.Committed or TerrainCommitStatus.AlreadyCommitted or TerrainCommitStatus.NoChange){j.Stage="LevelPhysics";j.Reason="地形已提交，等待物理验证";}
                }break;
            case "LevelPhysics":
                if(_groundVerified==_liveTerrain!.Current.Version){StopBase(builder);ReleaseStations(j.Builder);j.Work=0;j.Stage="Fetching";station.Y=(float)SavedGroundHeight(_liveTerrain.Current,station.X,station.Z);j.Station[1]=station.Y;}
                break;
            case "Fetching":
                StopBase(builder);ReleaseStations(j.Builder);
                var depot=_baseFacilities.Single(f=>f.Id==j.Supply);var dock=FreeStation(depot,hauler);
                if(dock==null){j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="等待取货；当前未找到可达"+BaseName(depot.Type)+"取货工位候选";break;}
                if(!TakeStation(j.Supply,j.Hauler)){j.FeedbackCode=PlayerReasonCode.WaitingForStation;j.Reason="等待"+BaseName(depot.Type)+"取货工位占用解除";break;}
                if(OrderBase(hauler,dock.Value)){j.Stage="Pickup";j.Reason="驮运前往"+BaseName(depot.Type)+"取货";}
                else{j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="取货路线重验未找到路线；尚未交接物料";}break;
            case "Pickup":
                if(!TakeStation(j.Supply,j.Hauler)){j.FeedbackCode=PlayerReasonCode.WaitingForStation;j.Reason="等待取货工位占用解除；尚未交接物料";break;}
                if(!_routes.TryGetValue(j.Hauler,out var pickup)||!Arrived(hauler,pickup.Destination)){if(!hauler.HasOrder){j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="尚未到达取货位；当前路线未继续执行，物料仍在仓内";}break;}
                if(_ledger.Load(cargo)>0)TransferMaterials(j.Id+"-return-"+j.Trip,j.Id,cargo,j.Supply,new(_ledger.Snapshot().Containers.Single(c=>c.Id==cargo).Items.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value)));
                var need=j.Cost.ToDictionary(x=>x.Key,x=>Math.Max(0,x.Value-_ledger.Count(buffer,x.Key)));int remaining=_bootstrapConfig.CargoCapacity;
                var load=new Dictionary<string,int>();foreach(var pair in need){int n=Math.Min(remaining,pair.Value);if(n>0){load.Add(pair.Key,n);remaining-=n;}}
                if(load.Count==0){ReleaseStations(j.Hauler);j.Stage="BuilderTravel";OrderBase(hauler,hauler.GlobalPosition+new Vector3(0,0,3));break;}
                _ledger.Release(j.Id); var outstanding=need.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value);
                _ledger.Reserve(j.Id,j.Supply,outstanding);
                j.Trip++;
                TransferMaterials(j.Id+"-take-"+j.Trip,j.Id,j.Supply,cargo,load);ReleaseStations(j.Hauler);
                if(OrderDelivery(j,hauler)){j.Stage="Delivering";j.Reason="已真实取货，驮运载货前往工地";}
                else j.Stage="CargoWaiting";
                break;
            case "CargoWaiting":if(OrderDelivery(j,hauler))j.Stage="Delivering";break;
            case "Delivering":
                if(!TakeStation(j.Facility,j.Hauler)){j.FeedbackCode=PlayerReasonCode.WaitingForStation;j.Reason="载货保留；等待卸货工位占用解除";break;}
                if(j.HaulStation==null||!Arrived(hauler,LoadVector(j.HaulStation))){if(!hauler.HasOrder){j.FeedbackCode=PlayerReasonCode.WaitingForRoute;j.Reason="载货未到卸货位；当前路线未继续执行";}break;}
                var items=_ledger.Snapshot().Containers.Single(c=>c.Id==cargo).Items.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value);
                TransferMaterials(j.Id+"-unload-"+j.Trip,j.Id,cargo,buffer,items);j.Trip++;ReleaseStations(j.Hauler);j.Stage="Fetching";j.Reason="物料已在现场交接；继续补足余料";break;
            case "BuilderTravel":
                if(TakeStation(j.Facility,j.Builder)&&OrderBase(builder,station)){j.Stage="Building";j.Work=0;j.Reason="物料齐备，筑垒前往并实际施工";}break;
            case "Building":
                if(!TakeStation(j.Facility,j.Builder))break;
                double seconds=j.Type=="connection"?3:Definition(j.Type).WorkSeconds;
                if(!TickBuildWork(builder,j,delta,seconds))break;
                var f=_baseFacilities.Single(f=>f.Id==j.Facility);
                if(j.Type!="connection"&&_groundRobots.Any(a=>XzDistance(a.GlobalPosition,center)<BaseRadius(f)+a.BodyRadius)){j.Reason="等待落成占地空闲";break;}
                TransferMaterials(j.Id+"-consume",j.Id,buffer,"spent",j.Type=="connection"?j.Cost:Definition(j.Type).Cost);
                if(j.Type=="connection"){f.Source=j.Source;f.ConnectionId=j.Id;}else f.Built=true;
                _baseRevision++;RebuildBaseFacilities();StopBase(builder);ReleaseStations(j.Builder);_ledger.Release(j.Id);j.Stage="Completed";j.Reason=j.Type=="connection"?"电缆已铺设；按日照与实际出力供电":"已落成："+BaseName(j.Type);_playerNotice=j.Reason;
                RecordTutorialBuild(j); _intentVersion++; break;
        }
        if(j.Stage!=previous)j.Waiting=0;
    }
    private double WorkBudget(GroundPatrol actor,double delta)
    {
        var h=_health[actor.Name.ToString()];
        return Math.Min(delta,Math.Min(h.Energy/_bootstrapConfig.WorkEnergyPerSecond,h.Durability/_bootstrapConfig.WorkWearPerSecond));
    }
    private void SpendWork(GroundPatrol actor,double delta)
    {var h=_health[actor.Name.ToString()];h.Energy=Math.Max(0,h.Energy-_bootstrapConfig.WorkEnergyPerSecond*delta);h.Durability=Math.Max(0,h.Durability-_bootstrapConfig.WorkWearPerSecond*delta);}
    private void SettleBaseMovement()
    {
        if (_managementPaused) return;
        foreach(var actor in _groundRobots)
        {
            var h=_health[actor.Name.ToString()];double moved=Math.Max(0,actor.TravelledM-h.Travel);h.Travel=actor.TravelledM;
            h.Energy=Math.Max(0,h.Energy-moved*_bootstrapConfig.MoveEnergyPerM);h.Durability=Math.Max(0,h.Durability-moved*_bootstrapConfig.MoveWearPerM);
            }
    }
    private void TickBootstrap(double delta)
    {
        if (_managementPaused) return;
        _recipeWorked=false;TickBaseRoutes(delta);TickBaseServices(delta);TickBaseBuild(delta);TickDevelopment(delta);
    }
    private void TickBaseRoutes(double delta)
    {
        if (_managementPaused) return;
        foreach(var pair in _routes.ToArray())
        {
            var a=Actor(pair.Key);var r=pair.Value;if(_services.TryGetValue(pair.Key,out var waiting)&&waiting.Blocked)continue;
            if(!Operational(a)||a.Paused||Arrived(a,r.Destination))continue;
            r.Seconds+=delta;r.Stalled=a.Blocked||!a.HasOrder?r.Stalled+delta:0;
            bool changed = r.WorldVersion != _liveTerrain!.Current.Version || r.FacilityRevision != _baseRevision;
            if (r.Escaping && XzDistance(a.GlobalPosition, r.EscapeDestination) <= GroundPatrol.WaypointReachXzM
                && BoundedRoute.FootprintClear(_liveTerrain!.Current, new(a.GlobalPosition.X, a.GlobalPosition.Z), RouteFootprint(a))) r.Escaping = false;
            if(changed || !r.Escaping && r.Stalled>.6)
            {double elapsed=r.Seconds;if(OrderBase(a,r.Destination))_routes[pair.Key].Seconds=elapsed;else{a.ClearOrder();r.Stalled=0;}}
            if(r.Seconds>120)
            {
                a.ClearOrder();_health[pair.Key].Reason="路线120秒未到达；可重试，原进度保留";
                if(_services.TryGetValue(pair.Key,out var service)){service.Blocked=true;ReleaseStations(pair.Key);}
            }
        }
    }
    private void TickBaseServices(double delta)
    {
        if (_managementPaused) return;
        foreach(var actor in _groundRobots)
        {
            string id=actor.Name.ToString();var h=_health[id];
            if(!Operational(actor)||_services.TryGetValue(id,out var pending)&&(!pending.Returning||pending.Blocked)||h.Energy>40&&h.Durability>40)continue;
            string? kind=h.Durability<=40?"repair":null;
            var charger=_baseFacilities.Where(f=>f.Built&&f.Type=="charger"&&ServiceConnected(f)).Select(f=>(Facility:f,Station:FreeStation(f,actor))).Where(x=>x.Station!=null).OrderBy(x=>XzDistance(actor.GlobalPosition,x.Station!.Value)).FirstOrDefault();
            double budget=charger.Facility==null?10:Math.Max(10,FindRoute(actor,charger.Station!.Value).LengthM*_bootstrapConfig.MoveEnergyPerM+5);
            if(kind==null&&h.Energy<=budget)kind="charge";
            if(kind==null)continue;
            var candidates=_baseFacilities.Where(f=>f.Built&&ServiceConnected(f)&&f.Type==(kind=="repair"?"repair":"charger")).OrderBy(f=>XzDistance(actor.GlobalPosition,LoadVector(f.Position)));
            foreach(var f in candidates)
            {
                if(_stations.ContainsKey(f.Id)&&_stations[f.Id]!=id)continue;
                var station=FreeStation(f,actor);if(station==null)continue;
                var route=FindRoute(actor,station.Value);if(!route.Found||h.Energy<=route.LengthM*_bootstrapConfig.MoveEnergyPerM+.1)continue;
                if(kind=="repair"&&_ledger.Available(f.Id,"parts")<_bootstrapConfig.RepairParts){h.Reason="维修需要有限结构件";break;}
                Vector3? back=_routes.TryGetValue(id,out var original)?original.Destination:null;
                ReleaseStations(id);TakeStation(f.Id,id);
                var service=new ServiceJob{Id="service-"+(++_serviceSequence),Robot=id,Facility=f.Id,Kind=kind,Station=SavedVector(station.Value),ReturnTo=back.HasValue?SavedVector(back.Value):null};
                _services[id]=service;OrderBase(actor,station.Value);h.Reason=kind=="repair"?"低耐久，真实前往维修位":"按10%或已知返程预算回充";break;
            }
            if(!_services.ContainsKey(id))h.Reason=kind=="repair"?"低耐久；没有可达且有电的维修位":"低电量；没有可达且有电的充电位";
        }
        var power=_baseFacilities.Where(f=>f.Type=="solar"&&f.Built).ToDictionary(f=>f.Id,f=>Daylight?_bootstrapConfig.SolarOutput:0);
        _remainingPower=power;
        foreach(var s in _services.Values.OrderBy(s=>int.Parse(s.Id[8..])).ToArray())
        {
            var actor=Actor(s.Robot);var h=_health[s.Robot];var f=_baseFacilities.Single(f=>f.Id==s.Facility);
            if(s.Blocked)continue;
            s.Waiting+=delta;
            if(s.Returning)
            {
                if(Arrived(actor,LoadVector(s.ReturnTo!))){_services.Remove(s.Robot);h.Reason="已回到原工作站";}
                continue;
            }
            if(!Arrived(actor,LoadVector(s.Station)))continue;
            if(!Powered(f)||f.Source==null){h.Reason="工位无电；未恢复";continue;}
            double rate=s.Kind=="charge"?_bootstrapConfig.ChargePerSecond:_bootstrapConfig.RepairPerSecond;
            if(power[f.Source]<rate){h.Reason="等待有限发电分配";continue;}power[f.Source]-=rate;
            if(s.Kind=="repair")
            {
                if(!s.Paid&&_ledger.Available(s.Facility,"parts")<_bootstrapConfig.RepairParts)
                {
                    s.Blocked=true;actor.ClearOrder();ReleaseStations(s.Robot);h.Reason="维修耗材不足；保留任务，可补料后重试";continue;
                }
                if(!s.Paid){TransferMaterials(s.Id+"-parts",s.Id,s.Facility,"spent",new(){{"parts",_bootstrapConfig.RepairParts}});s.Paid=true;}
                s.Progress=Math.Min(_bootstrapConfig.RepairSeconds,s.Progress+delta);
                if(s.Progress<_bootstrapConfig.RepairSeconds)continue;
                h.Durability=_bootstrapConfig.Capacity;
            }
            else {h.Energy=Math.Min(_bootstrapConfig.Capacity,h.Energy+rate*delta);if(h.Energy<_bootstrapConfig.Capacity)continue;}
            StopBase(actor);ReleaseStations(s.Robot);h.Reason="保障完成；返回原工作站";
            if(s.ReturnTo==null){_services.Remove(s.Robot);continue;}
            s.Returning=true;
            var destination=LoadVector(s.ReturnTo);
            if(!OrderBase(actor,destination))_routes[s.Robot]=new(){Destination=destination,WorldVersion=-1,FacilityRevision=-1};
        }
    }
}
