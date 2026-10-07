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
    }
    private sealed class BuildJob
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required string Facility { get; init; }
        public string? Source { get; init; }
        public required float[] Center { get; init; }
        public float Yaw { get; init; }
        public required Dictionary<string,int> Cost { get; init; }
        public required string Builder { get; init; }
        public required string Hauler { get; init; }
        public required float[] Station { get; init; }
        public float[]? HaulStation { get; set; }
        public required string Patch { get; init; }
        public string Stage { get; set; } = "Preparing";
        public double Work { get; set; }
        public double Waiting { get; set; }
        public int Trip { get; set; }
        public string Reason { get; set; } = "";
        public bool Active => Stage is not ("Completed" or "Cancelled" or "Blocked");
    }
    private BuildingDefinition Definition(string type) => _bootstrapConfig.Buildings.Single(x => x.Id == type);
    private float BaseRadius(BaseFacility f) => f.Type == "lander" ? 2.3f : (float)Definition(f.Type).Radius;
    private string BaseName(string type) => type == "lander" ? "着陆器" : Definition(type).Name;
    private GroundPatrol Actor(string id) => _groundRobots.Single(x => x.Name.ToString() == id);
    private static string CargoContainer(string id) => "cargo:" + id;
    private bool Operational(GroundPatrol a) => !BootstrapEnabled || _health.TryGetValue(a.Name.ToString(),out var h) && h.Energy > 0 && h.Durability > 0;
    private bool Servicing(GroundPatrol a) => BootstrapEnabled && _services.ContainsKey(a.Name.ToString());
    private bool Daylight => _playerTime % _bootstrapConfig.DaySeconds < _bootstrapConfig.DaylightSeconds;
    private bool ServiceConnected(BaseFacility f) => f.Built && f.Source != null && _baseFacilities.Any(x=>x.Id==f.Source && x.Built && x.Type=="solar");
    private bool Powered(BaseFacility f) => f.Built && (f.Type == "solar" ? Daylight : f.Source != null && _baseFacilities.Any(x => x.Id == f.Source && x.Built && x.Type == "solar") && Daylight);

    private void InitializeBootstrap()
    {
        using var file = Godot.FileAccess.Open("res://config/bootstrap-v1.json", Godot.FileAccess.ModeFlags.Read);
        _bootstrapConfig = JsonSerializer.Deserialize<BootstrapConfig>(file.GetAsText(), SaveOptions) ?? throw new InvalidOperationException("缺少自举配置");
        _bootstrapConfig.Validate();
        _ledger = new(new([new("lander",500,new(_bootstrapConfig.Initial)),new("spent",MaterialLedger.Limit,new())],[],new()));
        _baseFacilities.Add(new(){Id="lander",Type="lander",Position=[7,0,-22],Built=true});
        RebuildBaseFacilities();
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
                string path = f.Type switch {"solar"=>"facilities/solar-r1/solar-r1.glb","processor"=>"facilities/processor-r1/processor-r1.glb",_=>"lowfi-batch-r1/models/"+f.Type+".glb"};
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
        string stock=string.Join(" · ",MaterialLedger.Materials.Where(m=>_bootstrapConfig.Initial[m]>0).Select(m=>$"{MaterialName(m)} {_ledger.Count("lander",m)}（可用{_ledger.Available("lander",m)}）"));
        return new(true,stock,$"{(Daylight?"日照":"夜间")} · 发电 {_baseFacilities.Count(f=>f.Type=="solar"&&Powered(f))*_bootstrapConfig.SolarOutput:0.#} /s · 电缆连接 ≤{_bootstrapConfig.ConnectionRangeM:0}m，每段{_bootstrapConfig.ConnectionCables}线缆",
            _bootstrapConfig.Buildings.Select(b=>new BuildBlueprintView(b.Id,b.Name,(float)b.Radius,FormatMaterials(BuildCost(b.Id)))).ToArray(),
            _baseFacilities.Select(f=>new FacilityView(f.Id,f.Type,BaseName(f.Type),LoadVector(f.Position),BaseRadius(f),f.Built,Powered(f),f.Source)).ToArray(),
            _health.Select(x=>new RobotSupportView(x.Key,x.Value.Energy,x.Value.Durability,_bootstrapConfig.Capacity,_ledger.Load(CargoContainer(x.Key)),
                x.Value.Energy<=0&&x.Value.Durability<=0?"零电且机械停机":x.Value.Energy<=0?"零电停机":x.Value.Durability<=0?"机械停机":_services.TryGetValue(x.Key,out var s)?s.Kind=="repair"?"维修保障":"回充保障":"可用",x.Value.Reason)).ToArray(),_baseRevision);
    }
    private static string MaterialName(string m)=>m switch {"iron_ore"=>"铁矿","copper_ore"=>"铜矿","iron"=>"铁料","copper"=>"铜料","parts"=>"结构件","cable"=>"线缆",_=>"组件套件"};
    private static string FormatMaterials(Dictionary<string,int> amounts)=>string.Join("、",amounts.Select(x=>$"{MaterialName(x.Key)}{x.Value}"));
    private TerrainPatch BuildPatch(TerrainSnapshot t, Vector3 center, float radius, string id)
    {
        var heights=t.HeightsM.ToArray();
        double reach=radius+t.SpacingM;
        for(int r=1;r<t.Rows-1;r++)for(int c=1;c<t.Columns-1;c++)
        {double x=t.OriginXM+c*t.SpacingM-center.X,z=t.OriginZM+r*t.SpacingM-center.Z;if(x*x+z*z<=reach*reach)heights[r*t.Columns+c]=0;}
        return GroundPatchFromHeights(t,heights,id);
    }
    private NavObstacle[] RouteObstacles(string actor, bool includeSites=true)=>_baseFacilities.Where(f=>f.Built||includeSites).Select(f=>new NavObstacle(new(LoadVector(f.Position).X,LoadVector(f.Position).Z),BaseRadius(f)))
        .Concat(_groundRobots.Where(a=>a.Name.ToString()!=actor).Select(a=>new NavObstacle(new(a.GlobalPosition.X,a.GlobalPosition.Z),a.BodyRadius))).ToArray();
    private RouteResult FindRoute(GroundPatrol a, Vector3 to)=>BoundedRoute.Find(_liveTerrain!.Current,new(a.GlobalPosition.X,a.GlobalPosition.Z),new(to.X,to.Z),a.BodyRadius,RouteObstacles(a.Name.ToString()));
    private bool OrderBase(GroundPatrol actor,Vector3 destination)
    {
        var route=FindRoute(actor,destination);if(!route.Found){_health[actor.Name.ToString()].Reason=route.Reason;return false;}
        actor.SetRoute(route.Points.Select(p=>new Vector3((float)p.X,(float)SavedGroundHeight(_liveTerrain!.Current,(float)p.X,(float)p.Z),(float)p.Z)).ToArray());
        _routes[actor.Name.ToString()]=new(){Destination=destination,WorldVersion=route.WorldVersion,FacilityRevision=_baseRevision};return true;
    }
    private Vector3? FreeStation(BaseFacility f,GroundPatrol a)
    {
        var p=LoadVector(f.Position);float d=BaseRadius(f)+a.BodyRadius+1.2f;
        foreach(var offset in new[]{new Vector3(d,0,0),new Vector3(-d,0,0),new Vector3(0,0,d),new Vector3(0,0,-d)})
        {var station=p+offset;if(FindRoute(a,station).Found){station.Y=(float)SavedGroundHeight(_liveTerrain!.Current,station.X,station.Z);return station;}}
        return null;
    }
    private bool TakeStation(string facility,string robot)=>!_stations.TryGetValue(facility,out var owner)?_stations.TryAdd(facility,robot):owner==robot;
    private void ReleaseStations(string robot){foreach(var key in _stations.Where(x=>x.Value==robot).Select(x=>x.Key).ToArray())_stations.Remove(key);}
    private static bool Arrived(GroundPatrol actor,Vector3 station)=>actor.IsOnFloor()&&actor.OrderReached&&XzDistance(actor.GlobalPosition,station)<=.25f&&new Vector2(actor.Velocity.X,actor.Velocity.Z).Length()<=.05f;
    public PlayerSitePreview PreviewBuild(string type,Vector3 center,float yaw=0)
    {
        if(!BootstrapEnabled||!ReadPlayerState().Ready)return new(false,"等待世界准备",center,_liveTerrain?.Current.Version??0,0);
        if(!_bootstrapConfig.Buildings.Any(b=>b.Id==type)||!float.IsFinite(yaw)||Math.Abs(yaw)>MathF.PI||!float.IsFinite(center.X)||!float.IsFinite(center.Z))return new(false,"设施或朝向无效",center,_liveTerrain!.Current.Version,0);
        float radius=(float)Definition(type).Radius;center.Y=0;
        string why="";var t=_liveTerrain!.Current;
        if(_buildJob?.Active==true||_levelJob?.Active==true)why="先完成或取消当前工程";
        else if(!_liveTerrain.PermissionGranted||_liveTerrain.CancellationRequested)why="未获建设权限";
        else if(Math.Abs(center.X)+radius+t.SpacingM*3+2>=_cfg.Terrain.Size/2||Math.Abs(center.Z)+radius+t.SpacingM*3+2>=_cfg.Terrain.Size/2)why="设施与作业站须位于场地内部";
        else if(_baseFacilities.Any(f=>XzDistance(LoadVector(f.Position),center)<=BaseRadius(f)+radius+(float)t.SpacingM*2+1))why="与设施或已有工程重叠";
        else if(_groundRobots.Any(a=>XzDistance(a.GlobalPosition,center)<=a.BodyRadius+radius+(float)t.SpacingM*2))why="作业范围内有机器人";
        else if(BuildCost(type).Any(x=>_ledger.Available("lander",x.Key)<x.Value))why="有限启动库存不足："+FormatMaterials(BuildCost(type));
        else if(!_groundRobots.Any(a=>a.Name.ToString().StartsWith("Robot_Zhulei_")&&Operational(a)))why="没有可行动的筑垒";
        return new(why.Length==0,why.Length==0?"可建设 · "+BaseName(type)+" · "+FormatMaterials(BuildCost(type)):why,center,t.Version,0,radius+(float)t.SpacingM);
    }
    public void QueueBuild(string type,Vector3 center,float yaw,long observedVersion)
        =>QueuePlayer(new("build:"+type,center,observedVersion,null,yaw));
    private void StartBaseBuild(string type,Vector3 center,float yaw,string? connectionTarget=null,string? source=null)
    {
        if(_buildSequence==int.MaxValue)throw new InvalidOperationException("工程编号已达上限");
        bool wire=type=="connection";
        var builder=_groundRobots.Where(a=>a.Name.ToString().StartsWith("Robot_Zhulei_")&&Operational(a)&&!Servicing(a)).OrderBy(a=>XzDistance(a.GlobalPosition,center)).FirstOrDefault();
        var hauler=_groundRobots.Where(a=>a.Name.ToString().StartsWith("Robot_Tuoyun_")&&Operational(a)&&!Servicing(a)).OrderBy(a=>_ledger.Load(CargoContainer(a.Name.ToString()))==0?1:0).FirstOrDefault();
        if(builder==null||hauler==null)throw new InvalidOperationException("需要可行动的筑垒和驮运");
        string id="build-"+(++_buildSequence);var cost=BuildCost(type);
        var f=wire?_baseFacilities.Single(f=>f.Id==connectionTarget):new BaseFacility{Id=id,Type=type,Position=SavedVector(new(center.X,0,center.Z)),Yaw=yaw};
        var patch=wire?GroundPatchFromHeights(_liveTerrain!.Current,_liveTerrain.Current.HeightsM.ToArray(),id):BuildPatch(_liveTerrain!.Current,center,BaseRadius(f),id);
        var station=FindBuildStation(builder,center,patch,BaseRadius(f));
        if(station==null)throw new InvalidOperationException("没有可达且不触及整平范围的施工站");
        if(!wire)_ledger.AddContainer(f.Id,100);
        else _ledger.AddContainer(id,100);
        _ledger.Reserve(id,"lander",cost);
        _levelJob=null;
        _buildJob=new(){Id=id,Type=type,Facility=f.Id,Source=source,Center=SavedVector(new(center.X,0,center.Z)),Yaw=yaw,Cost=cost,Builder=builder.Name.ToString(),Hauler=hauler.Name.ToString(),Station=SavedVector(station.Value),Patch=TerrainDataCodec.Serialize(patch)};
        if(!wire){_baseFacilities.Add(f);_baseRevision++;RebuildBaseFacilities();}
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
        if(_buildJob?.Active==true||_levelJob?.Active==true)throw new InvalidOperationException("先完成或取消当前工程");
        foreach(var f in _baseFacilities.Where(f=>f.Built&&f.Type is not ("solar" or "lander" or "storage")&&f.Source==null))
        {
            var source=_baseFacilities.Where(s=>s.Built&&s.Type=="solar"&&XzDistance(LoadVector(s.Position),LoadVector(f.Position))<=_bootstrapConfig.ConnectionRangeM).OrderBy(s=>XzDistance(LoadVector(s.Position),LoadVector(f.Position))).FirstOrDefault();
            if(source==null)continue;
            StartBaseBuild("connection",LoadVector(f.Position),f.Yaw,f.Id,source.Id);return;
        }
        throw new InvalidOperationException("没有位于已建阵列12m内且尚未连接的用电设施");
    }
    private void CancelBaseBuild()
    {
        if(_buildJob?.Active!=true)return;
        var j=_buildJob;j.Stage="Cancelled";j.Reason="已取消；地形、现场物料与在途货物保留";_ledger.Release(j.Id);
        foreach(var id in new[]{j.Builder,j.Hauler})
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
        if(dock==null||!TakeStation(site.Id,j.Hauler))return false;
        j.HaulStation=SavedVector(dock.Value);return OrderBase(hauler,dock.Value);
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
        if(_buildJob is not {} j || j.Stage is not ("Blocked" or "Cancelled") || _levelJob?.Active==true)
            throw new InvalidOperationException("没有可重试的当前工程");
        _ledger.Release(j.Id);
        string buffer=j.Type=="connection"?j.Id:j.Facility;
        var missing=j.Cost.Where(x=>x.Value>_ledger.Count(buffer,x.Key)+_ledger.Count(CargoContainer(j.Hauler),x.Key))
            .ToDictionary(x=>x.Key,x=>x.Value-_ledger.Count(buffer,x.Key)-_ledger.Count(CargoContainer(j.Hauler),x.Key));
        if(missing.Count>0)_ledger.Reserve(j.Id,"lander",missing);
        var patch=TerrainDataCodec.ParsePatch(j.Patch);
        j.Stage=patch.HeightsM.SequenceEqual(_liveTerrain!.Current.HeightsM)||j.Type=="connection"?"Fetching":"Preparing";
        if(j.Stage=="Preparing"&&patch.Base.Version!=_liveTerrain.Current.Version)throw new InvalidOperationException("地形已变化，保留工程；需另选位置");
        j.Waiting=0;_playerNotice="继续原工程；不会重扣已消耗物料";
    }

    private bool TickBuildWork(GroundPatrol actor,BuildJob j,double delta,double duration)
    {
        if(!Arrived(actor,LoadVector(j.Station)))return false;
        double step=Math.Min(Math.Max(0,duration-j.Work),WorkBudget(actor,delta));
        j.Work+=step;SpendWork(actor,step);return j.Work>=duration;
    }
    private void TickBaseBuild(double delta)
    {
        var j=_buildJob;if(j?.Active!=true)return;
        var builder=Actor(j.Builder);var hauler=Actor(j.Hauler);var station=LoadVector(j.Station);var center=LoadVector(j.Center);var cargo=CargoContainer(j.Hauler);string buffer=j.Type=="connection"?j.Id:j.Facility;
        if(Servicing(builder)||Servicing(hauler)){j.Reason="保障中，原工程、载荷与进度保留";return;}
        if(!Operational(builder)||!Operational(hauler)){j.Reason="执行者停机；货物与工程保留，救援尚待D2";return;}
        j.Waiting+=delta;if(j.Waiting>180){j.Stage="Blocked";_ledger.Release(j.Id);j.Reason="180秒未完成当前阶段；事实保留，可重试";StopBase(builder);StopBase(hauler);ReleaseStations(j.Builder);ReleaseStations(j.Hauler);return;}
        string previous=j.Stage;
        switch(j.Stage)
        {
            case "Preparing":
                var patch=TerrainDataCodec.ParsePatch(j.Patch);
                if(patch.HeightsM.SequenceEqual(patch.Base.HeightsM)){j.Stage="Fetching";break;}
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
                var lander=_baseFacilities.Single(f=>f.Id=="lander");var dock=FreeStation(lander,hauler);
                if(dock==null||!TakeStation("lander",j.Hauler))break;
                if(OrderBase(hauler,dock.Value)){j.Stage="Pickup";j.Reason="驮运前往着陆器取货";}break;
            case "Pickup":
                if(!TakeStation("lander",j.Hauler))break;
                if(!_routes.TryGetValue(j.Hauler,out var pickup)||!Arrived(hauler,pickup.Destination))break;
                if(_ledger.Load(cargo)>0)_ledger.Transfer(j.Id+"-return-"+j.Trip,j.Id,cargo,"lander",new(_ledger.Snapshot().Containers.Single(c=>c.Id==cargo).Items.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value)));
                var need=j.Cost.ToDictionary(x=>x.Key,x=>Math.Max(0,x.Value-_ledger.Count(buffer,x.Key)));int remaining=_bootstrapConfig.CargoCapacity;
                var load=new Dictionary<string,int>();foreach(var pair in need){int n=Math.Min(remaining,pair.Value);if(n>0){load.Add(pair.Key,n);remaining-=n;}}
                if(load.Count==0){ReleaseStations(j.Hauler);j.Stage="BuilderTravel";OrderBase(hauler,hauler.GlobalPosition+new Vector3(0,0,3));break;}
                _ledger.Release(j.Id); var outstanding=need.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value);
                _ledger.Reserve(j.Id,"lander",outstanding);
                j.Trip++;
                _ledger.Transfer(j.Id+"-take-"+j.Trip,j.Id,"lander",cargo,load);ReleaseStations(j.Hauler);
                if(OrderDelivery(j,hauler)){j.Stage="Delivering";j.Reason="已真实取货，驮运载货前往工地";}
                else j.Stage="CargoWaiting";
                break;
            case "CargoWaiting":if(OrderDelivery(j,hauler))j.Stage="Delivering";break;
            case "Delivering":
                if(!TakeStation(j.Facility,j.Hauler))break;
                if(j.HaulStation==null||!Arrived(hauler,LoadVector(j.HaulStation)))break;
                var items=_ledger.Snapshot().Containers.Single(c=>c.Id==cargo).Items.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value);
                _ledger.Transfer(j.Id+"-unload-"+j.Trip,j.Id,cargo,buffer,items);j.Trip++;ReleaseStations(j.Hauler);j.Stage="Fetching";j.Reason="物料已在现场交接；继续补足余料";break;
            case "BuilderTravel":
                if(TakeStation(j.Facility,j.Builder)&&OrderBase(builder,station)){j.Stage="Building";j.Work=0;j.Reason="物料齐备，筑垒前往并实际施工";}break;
            case "Building":
                if(!TakeStation(j.Facility,j.Builder))break;
                double seconds=j.Type=="connection"?3:Definition(j.Type).WorkSeconds;
                if(!TickBuildWork(builder,j,delta,seconds))break;
                var f=_baseFacilities.Single(f=>f.Id==j.Facility);
                if(j.Type!="connection"&&_groundRobots.Any(a=>XzDistance(a.GlobalPosition,center)<BaseRadius(f)+a.BodyRadius)){j.Reason="等待落成占地空闲";break;}
                _ledger.Transfer(j.Id+"-consume",j.Id,buffer,"spent",j.Type=="connection"?j.Cost:Definition(j.Type).Cost);
                if(j.Type=="connection"){f.Source=j.Source;f.ConnectionId=j.Id;}else f.Built=true;
                _baseRevision++;RebuildBaseFacilities();StopBase(builder);ReleaseStations(j.Builder);_ledger.Release(j.Id);j.Stage="Completed";j.Reason=j.Type=="connection"?"电缆已铺设；按日照与实际出力供电":"已落成："+BaseName(j.Type);_playerNotice=j.Reason;break;
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
        foreach(var actor in _groundRobots)
        {
            var h=_health[actor.Name.ToString()];double moved=Math.Max(0,actor.TravelledM-h.Travel);h.Travel=actor.TravelledM;
            h.Energy=Math.Max(0,h.Energy-moved*_bootstrapConfig.MoveEnergyPerM);h.Durability=Math.Max(0,h.Durability-moved*_bootstrapConfig.MoveWearPerM);
            }
    }
    private void TickBootstrap(double delta)
    {
        TickBaseRoutes(delta);TickBaseServices(delta);TickBaseBuild(delta);
    }
    private void TickBaseRoutes(double delta)
    {
        foreach(var pair in _routes.ToArray())
        {
            var a=Actor(pair.Key);var r=pair.Value;if(!Operational(a)||a.Paused||Arrived(a,r.Destination))continue;
            r.Seconds+=delta;r.Stalled=a.Blocked?r.Stalled+delta:0;
            if(r.WorldVersion!=_liveTerrain!.Current.Version||r.FacilityRevision!=_baseRevision||r.Stalled>.6)
            {double elapsed=r.Seconds;if(OrderBase(a,r.Destination))_routes[pair.Key].Seconds=elapsed;else{a.ClearOrder();r.Stalled=0;}}
            if(r.Seconds>120){a.ClearOrder();_health[pair.Key].Reason="路线120秒未到达；等待重新派单";}
        }
    }
    private void TickBaseServices(double delta)
    {
        foreach(var actor in _groundRobots)
        {
            string id=actor.Name.ToString();var h=_health[id];
            if(!Operational(actor)||_services.TryGetValue(id,out var pending)&&!pending.Returning||h.Energy>40&&h.Durability>40)continue;
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
        foreach(var s in _services.Values.OrderBy(s=>int.Parse(s.Id[8..])).ToArray())
        {
            var actor=Actor(s.Robot);var h=_health[s.Robot];var f=_baseFacilities.Single(f=>f.Id==s.Facility);
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
                if(!s.Paid){_ledger.Transfer(s.Id+"-parts",s.Id,s.Facility,"spent",new(){{"parts",_bootstrapConfig.RepairParts}});s.Paid=true;}
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
