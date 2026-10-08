#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Yudian.Goals;
using Yudian.Resources;
using Yudian.Terrain;
namespace Yudian;
public partial class Main
{
    private bool DevelopmentEnabled => BootstrapEnabled && System.Environment.GetEnvironmentVariable("YUDIAN_BOOTSTRAP_SELF_TEST") != "1";
    private readonly List<MineSite> _mines = new();
    private readonly List<ProductionTask> _productionTasks = new();
    private DevelopmentGoal? _development;
    private ProductionTask? Production => _productionTasks.LastOrDefault(t=>t.Goal==_development?.Id&&t.Stage is not ("Completed" or "Cancelled"));
    private int _developmentSequence, _productionSequence;
    private Dictionary<string,double> _remainingPower = new();
    private bool _recipeWorked, _resourceViewDirty;
    private sealed class MineSite
    {
        public required string Id { get; init; }
        public required string Material { get; init; }
        public required float[] Position { get; init; }
        public int Remaining { get; set; } = 96;
    }
    private sealed class DevelopmentGoal
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public required float[] Center { get; init; }
        public float Yaw { get; init; }
        public string? Facility { get; init; }
        public string? Source { get; init; }
        public string Supply { get; init; } = "lander";
        public string Stage { get; set; } = "Supplying";
        public string Reason { get; set; } = "";
        public bool Active => Stage is "Supplying" or "Building";
    }
    private sealed class ProductionTask
    {
        public required string Id { get; init; }
        public required string Goal { get; init; }
        public required string Kind { get; init; }
        public required string Material { get; init; }
        public required int Quantity { get; init; }
        public required string Source { get; init; }
        public required string Destination { get; init; }
        public string? Robot { get; init; }
        public float[]? Station { get; set; }
        public string Stage { get; set; } = "Fetching";
        public string? ResumeStage { get; set; }
        public double Work { get; set; }
        public double Waiting { get; set; }
        public bool Paid { get; set; }
        public string? Patch { get; set; }
        public string Reason { get; set; } = "";
    }
    private void InitializeDevelopment()
    {
        _mines.Add(new(){Id="mine-iron",Material="iron_ore",Position=[-14,0,10]});
        _mines.Add(new(){Id="mine-copper",Material="copper_ore",Position=[1,0,17]});
        foreach(var m in _mines)_ledger.AddContainer(m.Id,100);
        RebuildResourceViews();
    }
    private bool HasCurrentWork => _development?.Active==true || _development?.Stage=="Blocked" || _buildJob?.Active==true || _levelJob?.Active==true;
    private IEnumerable<string> Warehouses => _baseFacilities.Where(f=>f.Built&&f.Type is "lander" or "storage").Select(f=>f.Id);
    private IEnumerable<string> ProductionSources => Warehouses.Concat(_baseFacilities.Where(f=>f.Built&&f.Type=="processor"||!f.Built).Select(f=>f.Id)).Concat(_mines.Select(m=>m.Id))
        .Concat(_productionTasks.Where(t=>t.Kind=="recipe"&&(t.Stage=="Completed"||t.Stage=="Cancelled")).Select(t=>t.Destination))
        .Concat(_groundRobots.Select(a=>CargoContainer(a.Name.ToString()))).Distinct();
    private Dictionary<string,int> UsableStock() => MaterialLedger.Materials.ToDictionary(m=>m,m=>ProductionSources.Sum(c=>_ledger.Available(c,m)));
    private Dictionary<string,int> WarehouseStock() => MaterialLedger.Materials.ToDictionary(m=>m,m=>Warehouses.Sum(c=>_ledger.Available(c,m)));
    private Dictionary<string,int> OreRemaining() => _mines.ToDictionary(m=>m.Material,m=>m.Remaining);
    private string StartupGuard(string type)
    {
        if(!DevelopmentEnabled || ProductionPrerequisite.Length==0)return "";
        // Keep one finite support set safe before optional duplicates or storage consume its resources.
        var needed=_bootstrapConfig.Buildings.Where(b=>b.Id!="storage"&&!_baseFacilities.Any(f=>f.Built&&f.Type==b.Id)&&b.Id!=type).ToArray();
        var cost=BuildCost(type);
        foreach(var m in MaterialLedger.Materials)
        {
            int reserve=needed.Sum(b=>b.Cost.GetValueOrDefault(m));
            if(m=="parts" && needed.Any(b=>b.Id=="repair"))reserve+=_bootstrapConfig.RepairParts*2;
            if(m=="cable")reserve+=_bootstrapConfig.ConnectionCables*(3-_baseFacilities.Count(f=>f.Source!=null));
            if(_ledger.Available("lander",m)-cost.GetValueOrDefault(m)<reserve)
                return "保护首套自举物资：先完成太阳能、充电、维修和加工及连接";
        }
        return "";
    }
    private string ProductionPrerequisite => !_baseFacilities.Any(f=>f.Type=="processor"&&f.Built&&ServiceConnected(f))
        ? "缺料前置：请先选址建加工设施，并在太阳能12m内连接电缆"
        : !_baseFacilities.Any(f=>f.Type=="charger"&&ServiceConnected(f))||!_baseFacilities.Any(f=>f.Type=="repair"&&ServiceConnected(f))
        ? "缺料前置：请先建成并连接充电桩和维修站，保护后续作业" : "";
    private Dictionary<string,int> DevelopmentDemand(string type)
    {
        if(type!="restock")return BuildCost(type);
        var repair=_baseFacilities.FirstOrDefault(f=>f.Built&&f.Type=="repair");
        int quantity=repair==null?4:Math.Max(0,4-_ledger.Count(repair.Id,"parts"));
        return quantity==0?new():new(){{"parts",quantity}};
    }
    private string DevelopmentFeasibility(string type)
    {
        var cost=DevelopmentDemand(type);
        var stock=UsableStock();
        if(cost.All(x=>stock[x.Key]>=x.Value))return "";
        if(ProductionPrerequisite.Length>0)return ProductionPrerequisite;
        var plan=ProductionPlan.Create(cost,stock,_bootstrapConfig.Recipes,OreRemaining());
        return plan.Feasible?"":plan.Reason;
    }
    private Dictionary<string,int> PendingRecipeOutput()
    {
        var result=MaterialLedger.Materials.ToDictionary(m=>m,_=>0);
        foreach(var t in _productionTasks.Where(t=>t.Kind=="recipe"&&t.Paid&&t.Stage is not ("Completed" or "Cancelled")))
            foreach(var item in Recipe(t.Material).Output) result[item.Key]+=item.Value*t.Quantity;
        return result;
    }
    private static string ProductionStageName(string stage)=>stage switch
    {
        "Supplying"=>"筹备材料", "Building"=>"建设中", "Completed"=>"已完成", "Cancelled"=>"已取消", "Blocked"=>"需要处理",
        "Fetching"=>"安排取货", "Travel"=>"前往矿点", "Working"=>"作业中", "Physics"=>"确认地表",
        "Pickup"=>"前往取货", "Delivering"=>"载货送达", "CargoWaiting"=>"载货等待", _=>stage
    };
    public DevelopmentReadModel ReadDevelopment()
    {
        if(!DevelopmentEnabled||_ledger==null||!_groundReady)return new(false,"","","","","","","");
        var stock=UsableStock();var pending=PendingRecipeOutput();
        string Gap(Dictionary<string,int> cost)=>FormatMaterials(cost.Where(x=>stock[x.Key]+pending[x.Key]<x.Value)
            .ToDictionary(x=>x.Key,x=>x.Value-stock[x.Key]-pending[x.Key]));
        var goal=_development;var task=Production;
        string need=goal==null?"选择建设位置后预览":goal.Stage is "Completed" or "Building"?"无需新增生产":Gap(DevelopmentDemand(goal.Type));
        string stage=goal?.Stage=="Building"&&_buildJob is {} build?BaseStageText(build.Stage):task==null?ProductionStageName(goal?.Stage??"待选择"):
            (task.Kind=="mine"?"采集 · ":task.Kind=="recipe"?"加工 · ":"运输 · ")+ProductionStageName(task.Stage);
        string amounts(IEnumerable<string> sources)=>FormatMaterials(MaterialLedger.Materials
            .ToDictionary(m=>m,m=>sources.Sum(c=>_ledger.Available(c,m))).Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value));
        var cargo=_groundRobots.Select(a=>CargoContainer(a.Name.ToString())).ToArray();
        string prepared=amounts(ProductionSources.Except(cargo).Except(_mines.Select(m=>m.Id)));
        string transit=amounts(cargo);
        string processing=FormatMaterials(pending.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value));
        var directions=new[]{"storage","solar"}.Select(type=>
        {
            var cost=BuildCost(type);string reason=HasCurrentWork?"先完成或取消当前目标":StartupGuard(type);
            if(reason.Length==0)reason=DevelopmentFeasibility(type);
            return new DevelopmentDirectionView(type,BaseName(type),FormatMaterials(cost),Gap(cost) is {Length:>0} gap?gap:"无需新增生产",
                type=="storage"?"新增100容量，成为真实取货仓库":$"日照发电+{_bootstrapConfig.SolarOutput:0.#}/s，供电连接范围{_bootstrapConfig.ConnectionRangeM:0}m",
                reason.Length==0,reason);
        }).ToArray();
        string supply=goal==null?"":_baseFacilities.FirstOrDefault(f=>f.Id==goal.Supply) is {} f?BaseName(f.Type):"";
        string reasonText=(goal?.Stage=="Building"?_buildJob?.Reason:null)??task?.Reason??goal?.Reason??"先建太阳能、充电、维修、加工，并接通三段电缆";
        if(reasonText.Length==0)reasonText="开始筹备材料，机器人会自动补前置";
        return new(true,"自动筹备",goal==null?"先建基础设施":goal.Type=="restock"?"补维修耗材":BaseName(goal.Type),stage,
            need.Length==0?"无需新增生产":need,reasonText,string.Join("\n",directions.Select(d=>$"{d.Name} · {d.Cost} → {d.Consequence}")),
            string.Join("；",_mines.Select(m=>$"{MaterialName(m.Material)} {m.Remaining}/96 · 现场{_ledger.Load(m.Id)}")),
            supply,prepared,transit,processing,directions);
    }
    private void RetireTerminalJobs()
    {
        if(_buildJob?.Active!=true)_buildJob=null;
        if(_levelJob?.Active!=true)_levelJob=null;
    }
    private void StartDevelopment(string type,Vector3 center,float yaw,string? facility=null,string? source=null)
    {
        if(HasCurrentWork)throw new InvalidOperationException("先完成或取消当前目标");
        string refusal=DevelopmentFeasibility(type);if(refusal.Length>0)throw new InvalidOperationException(refusal);
        string supply=ChooseBuildSupply(DevelopmentDemand(type),center);
        RetireTerminalJobs();
        _development=new(){Id="goal-"+(++_developmentSequence),Type=type,Center=SavedVector(center),Yaw=yaw,Facility=facility,Source=source,Supply=supply};
        _playerNotice="已授权此目标的有限采集、运输、加工和建设；可暂停或取消";
    }
    private void StartRestock()
    {
        if(!_baseFacilities.Any(f=>f.Built&&f.Type=="repair"))throw new InvalidOperationException("请先建设维修站");
        var repair=_baseFacilities.First(f=>f.Built&&f.Type=="repair");
        if(_ledger.Count(repair.Id,"parts")>=4)throw new InvalidOperationException("维修站已有至少两次维修耗材，无需重复补货");
        StartDevelopment("restock",LoadVector(repair.Position),0);
    }
    private Vector3 PhysicalEndpoint(string container)
    {
        var mine=_mines.FirstOrDefault(m=>m.Id==container);if(mine!=null)return LoadVector(mine.Position);
        var batch=_productionTasks.FirstOrDefault(t=>t.Kind=="recipe"&&t.Destination==container);
        var f=_baseFacilities.FirstOrDefault(f=>f.Id==(batch?.Source??container));
        if(f!=null)return LoadVector(f.Position);
        if(container.StartsWith("cargo:"))return Actor(container[6..]).GlobalPosition;
        throw new InvalidOperationException("没有真实物料端点");
    }
    private Vector3? MaterialDock(string container,GroundPatrol actor)
    {
        var batch=_productionTasks.FirstOrDefault(t=>t.Kind=="recipe"&&t.Destination==container);
        var f=_baseFacilities.FirstOrDefault(f=>f.Id==(batch?.Source??container));
        if(f!=null)return FreeStation(f,actor);
        var m=_mines.Single(site=>site.Id==container);
        float d=2+(float)_liveTerrain!.Current.SpacingM*2+actor.BodyRadius+1.4f;
        var p=LoadVector(m.Position);
        foreach(var offset in new[]{new Vector3(d,0,0),new Vector3(-d,0,0),new Vector3(0,0,d),new Vector3(0,0,-d)})
        {var station=p+offset;station.Y=(float)SavedGroundHeight(_liveTerrain.Current,station.X,station.Z);if(FindRoute(actor,station).Found)return station;}
        return null;
    }
    private string? StockSource(string material,string? except=null) => ProductionSources.FirstOrDefault(c=>c!=except&&_ledger.Available(c,material)>0);
    private string ChooseBuildSupply(Dictionary<string,int> demand,Vector3 center)
    {
        var snapshot=_ledger.Snapshot();
        int returns=_groundRobots.Where(a=>a.Name.ToString().StartsWith("Robot_Tuoyun_")).Sum(a=>_ledger.Load(CargoContainer(a.Name.ToString())));
        return Warehouses.OrderBy(c=>c=="lander"?1:0).ThenBy(c=>XzDistance(PhysicalEndpoint(c),center)).FirstOrDefault(c=>
            _ledger.Load(c)+demand.Sum(x=>Math.Max(0,x.Value-_ledger.Available(c,x.Key)))+returns<=snapshot.Containers.Single(x=>x.Id==c).Capacity)
            ??throw new InvalidOperationException("现有仓库没有容纳目标物料及退货的空间");
    }
    private string? OutputStore(int quantity) => Warehouses.FirstOrDefault(c=>_ledger.Load(c)+quantity<=_ledger.Snapshot().Containers.Single(x=>x.Id==c).Capacity);
    private void StartHaul(string source,string destination,string material,int quantity)
    {
        GroundPatrol? actor=source.StartsWith("cargo:")?Actor(source[6..]):_groundRobots.FirstOrDefault(a=>a.Name.ToString().StartsWith("Robot_Tuoyun_")&&Operational(a)&&!Servicing(a)&&_ledger.Load(CargoContainer(a.Name.ToString()))==0);
        if(actor==null||!Operational(actor)||Servicing(actor)){_development!.Reason="等待可行动的空载驮运或货物原持有者";return;}
        int n=Math.Min(quantity,Math.Min(_bootstrapConfig.CargoCapacity,_ledger.Available(source,material)));
        var task=new ProductionTask{Id="prod-"+(++_productionSequence),Goal=_development!.Id,Kind="haul",Material=material,Quantity=n,Source=source,Destination=destination,Robot=actor.Name.ToString()};
        _productionTasks.Add(task);
        if(source==CargoContainer(task.Robot)){task.Paid=true;task.Stage="CargoWaiting";}
    }
    private bool SupplyInputs(Dictionary<string,int> inputs,string destination)
    {
        foreach(var x in inputs)
        {
            int need=x.Value-_ledger.Available(destination,x.Key);if(need<=0)continue;
            string? source=StockSource(x.Key,destination);
            if(source==null)throw new InvalidOperationException("规划库存变化，输入仍缺："+MaterialName(x.Key));
            StartHaul(source,destination,x.Key,need);return false;
        }
        return true;
    }
    private void StartMining(string material,int quantity)
    {
        var m=_mines.Single(m=>m.Material==material);
        var worker=_groundRobots.FirstOrDefault(a=>a.Name.ToString().StartsWith("Robot_Zhulei_")&&Operational(a)&&!Servicing(a)&&MaterialDock(m.Id,a)!=null);
        if(worker==null){_development!.Reason="矿点没有可达、可行动的筑垒";return;}
        var station=MaterialDock(m.Id,worker)!.Value;
        _productionTasks.Add(new(){Id="prod-"+(++_productionSequence),Goal=_development!.Id,Kind="mine",Material=material,Quantity=Math.Min(8,Math.Min(quantity,m.Remaining)),Source=m.Id,Destination=m.Id,Robot=worker.Name.ToString(),Station=SavedVector(station),Stage="Travel"});
    }
    private RecipeDefinition Recipe(string id)=>_bootstrapConfig.Recipes.Single(r=>r.Id==id);
    private void StartRecipe(string material,int requested)
    {
        var f=_baseFacilities.First(f=>f.Type=="processor"&&f.Built&&ServiceConnected(f));
        var recipe=Recipe(material);int batches=Math.Min(4,requested);
        var input=recipe.Input.ToDictionary(x=>x.Key,x=>x.Value*batches);
        if(!SupplyInputs(input,f.Id))return;
        string id="prod-"+(++_productionSequence),batch="batch:"+id;
        _ledger.AddContainer(batch,100);
        var t=new ProductionTask{Id=id,Goal=_development!.Id,Kind="recipe",Material=material,Quantity=batches,Source=f.Id,Destination=batch,Stage="Working"};
        _productionTasks.Add(t);
        TransferMaterials(id+"-input",id,f.Id,batch,input);t.Paid=true;
    }
    private void TickDevelopment(double delta)
    {
        if(!DevelopmentEnabled||_development?.Active!=true)return;
        var goal=_development;
        if(goal.Stage=="Building")
        {
            if(_buildJob?.Stage=="Completed"){goal.Stage="Completed";goal.Reason="发展目标已建成；选择下一方向";}
            else if(_buildJob?.Stage is "Blocked" or "Cancelled"){goal.Stage="Blocked";goal.Reason=_buildJob.Reason;}
            return;
        }
        var task=Production;
        if(task!=null){TickProduction(task,delta);return;}
        var demand=DevelopmentDemand(goal.Type);
        var stock=UsableStock();
        if(demand.All(x=>stock[x.Key]>=x.Value))
        {
            string destination=goal.Type=="restock"?_baseFacilities.First(f=>f.Built&&f.Type=="repair").Id:goal.Supply;
            if(!SupplyInputs(goal.Type=="restock"?new(){{"parts",4}}:demand,destination))return;
            if(goal.Type=="restock"){goal.Stage="Completed";goal.Reason="维修耗材已真实送达；等待保障可重试";return;}
            StartBaseBuild(goal.Type,LoadVector(goal.Center),goal.Yaw,goal.Facility,goal.Source,goal.Supply);goal.Stage="Building";return;
        }
        var plan=ProductionPlan.Create(demand,stock,_bootstrapConfig.Recipes,OreRemaining());
        if(!plan.Feasible){goal.Stage="Blocked";goal.Reason=plan.Reason;return;}
        var first=plan.Steps[0];
        if(first.Kind=="mine")StartMining(first.Material,first.Quantity);else StartRecipe(first.Material,first.Quantity);
    }
    private TerrainPatch MinePatch(MineSite mine,int remaining,string id)
    {
        var t=_liveTerrain!.Current;var heights=t.HeightsM.ToArray();var p=LoadVector(mine.Position);
        double depth=(96-remaining)/96.0*1.2;
        for(int i=0;i<heights.Length;i++)
        {double x=t.OriginXM+i%t.Columns*t.SpacingM-p.X,z=t.OriginZM+i/t.Columns*t.SpacingM-p.Z;if(x*x+z*z<=4)heights[i]=_liveInitial.HeightsM[i]-depth;}
        return GroundPatchFromHeights(t,heights,id);
    }
    private void CommitMine(ProductionTask task)
    {
        var mine=_mines.Single(m=>m.Id==task.Source);
        if(task.Paid)return;
        if(!_liveTerrain!.PermissionGranted||_liveTerrain.CancellationRequested||mine.Remaining<task.Quantity)throw new InvalidOperationException("采矿权限或剩余量变化");
        var patch=MinePatch(mine,mine.Remaining-task.Quantity,task.Id);
        var next=new MaterialLedger(_ledger.Snapshot());
        next.Extract(new(task.Id+"-mine",mine.Id,mine.Material,task.Quantity,patch.Base.Version,patch.Base.Version+1));
        task.Patch=TerrainDataCodec.Serialize(patch);
        // A failed projection may still follow a successful logical commit. Adopt its resource facts exactly once.
        try{SubmitGroundResult(patch);}
        finally
        {
            if(_liveTerrain.Current.Version==patch.Base.Version+1&&_liveTerrain.Current.HeightsM.SequenceEqual(patch.HeightsM))
            {_ledger=next;mine.Remaining-=task.Quantity;task.Paid=true;task.Stage="Physics";_resourceViewDirty=true;}
        }
        task.Reason=task.Paid?"矿量与现场产物已提交，等待物理投影":"等待作业范围腾空，尚未采出";
    }
    private void TickProduction(ProductionTask t,double delta)
    {
        if(t.Stage=="Blocked")return;
        var actor=t.Robot==null?null:Actor(t.Robot);
        if(actor!=null&&(Servicing(actor)||!Operational(actor))){t.Reason="执行者保障或停机；载荷与原进度保留";return;}
        t.Waiting+=delta;
        if(t.Waiting>180){t.ResumeStage=t.Stage;t.Stage="Blocked";t.Reason="阶段180秒未完成；保留事实，可重试";_development!.Stage="Blocked";_development.Reason=t.Reason;if(actor!=null){StopBase(actor);ReleaseStations(t.Robot!);}return;}
        string previous=t.Stage;
        if(t.Kind=="mine")
        {
            if(t.Stage=="Travel"&&OrderBase(actor!,LoadVector(t.Station!)))t.Stage="Working";
            else if(t.Stage=="Working"&&Arrived(actor!,LoadVector(t.Station!)))
            {double step=Math.Min(6-t.Work,WorkBudget(actor!,delta));t.Work+=step;SpendWork(actor!,step);t.Reason="筑垒现场采集；耗电与磨损计入";if(t.Work>=6)CommitMine(t);}
            else if(t.Stage=="Physics"&&_groundVerified==_liveTerrain!.Current.Version){StopBase(actor!);t.Stage="Completed";t.Patch=null;t.Reason="采矿完成；现场物料等待实运";}
        }
        else if(t.Kind=="recipe")
        {
            var recipe=Recipe(t.Material);var f=_baseFacilities.Single(f=>f.Id==t.Source);
            var prospective=new MaterialLedger(_ledger.Snapshot());
            prospective.Convert(t.Id+"-recipe",t.Destination,_bootstrapConfig.Version,recipe,t.Quantity);
            if(!Powered(f)||f.Source==null){t.Reason="加工等待日照/已连接电源；投入保留，未产出";t.Waiting=0;return;}
            if(!_remainingPower.TryGetValue(f.Source,out double power)||power<recipe.Power){t.Reason="等待功率；现有充电/维修优先";t.Waiting=0;return;}
            if(OutputStore(recipe.Output.Values.Sum()*t.Quantity)==null){t.Reason="仓储空间不足；加工不产货，先释放容量";t.Waiting=0;return;}
            _remainingPower[f.Source]-=recipe.Power;_recipeWorked=true;
            t.Work=Math.Min(recipe.WorkSeconds*t.Quantity,t.Work+delta);t.Reason="加工设施实际用电生产："+MaterialName(t.Material);
            if(t.Work>=recipe.WorkSeconds*t.Quantity){_ledger=prospective;t.Stage="Completed";t.Reason="配方已结算；出料留在加工设施等待实运";_resourceViewDirty=true;}
        }
        else
        {
            string cargo=CargoContainer(t.Robot!);
            switch(t.Stage)
            {
                case "Fetching":
                    var pickup=MaterialDock(t.Source,actor!);
                    if(pickup!=null&&TakeStation(t.Source,t.Robot!)&&OrderBase(actor!,pickup.Value)){t.Station=SavedVector(pickup.Value);t.Stage="Pickup";t.Reason="驮运前往真实取货位";}break;
                case "Pickup":
                    if(!TakeStation(t.Source,t.Robot!)||!Arrived(actor!,LoadVector(t.Station!)))break;
                    TransferMaterials(t.Id+"-take",t.Id,t.Source,cargo,new(){{t.Material,t.Quantity}});t.Paid=true;ReleaseStations(t.Robot!);t.Stage="CargoWaiting";break;
                case "CargoWaiting":
                    var delivery=MaterialDock(t.Destination,actor!);
                    if(delivery!=null&&TakeStation(t.Destination,t.Robot!)&&OrderBase(actor!,delivery.Value)){t.Station=SavedVector(delivery.Value);t.Stage="Delivering";t.Reason="真实载货运输："+MaterialName(t.Material)+t.Quantity;}break;
                case "Delivering":
                    if(!TakeStation(t.Destination,t.Robot!)||!Arrived(actor!,LoadVector(t.Station!)))break;
                    try{TransferMaterials(t.Id+"-unload",t.Id,cargo,t.Destination,new(){{t.Material,t.Quantity}});}
                    catch(InvalidOperationException ex){t.Reason=ex.Message;t.Waiting=0;return;}
                    StopBase(actor!);ReleaseStations(t.Robot!);t.Stage="Completed";t.Reason="已到站卸货，物料归属已交接";break;
            }
        }
        if(t.Stage!=previous)t.Waiting=0;
        if(_productionTasks.Count>1000)throw new InvalidOperationException("本档生产任务上限，停止新义务");
    }
    private void CancelDevelopment()
    {
        if(_development?.Active!=true&&_development?.Stage!="Blocked")return;
        if(_buildJob?.Active==true)CancelBaseBuild();
        var t=Production;
        if(t!=null){if(t.Stage!="Blocked")t.ResumeStage=t.Stage;t.Stage="Cancelled";if(t.Robot!=null)ClearBaseWorkOrder(t.Robot);}
        _development.Stage="Cancelled";_development.Reason="目标取消；已开挖、投入、产物与在途货物保留，不产生新义务";_playerNotice=_development.Reason;
    }
    private void RetryDevelopment()
    {
        if(_development?.Stage is not ("Blocked" or "Cancelled"))throw new InvalidOperationException("没有可重试的经营目标");
        if(_buildJob?.Stage is "Blocked" or "Cancelled"){RetryBaseBuild();_development.Stage="Building";return;}
        var task=_productionTasks.LastOrDefault(t=>t.Goal==_development.Id&&t.Stage is "Blocked" or "Cancelled");
        if(task!=null)
        {
            task.Stage=task.ResumeStage??"Fetching";task.Waiting=0;
            if(task.Robot!=null)
            {
                if(task.Kind=="mine")task.Stage=task.Paid?"Physics":"Travel";
                else task.Stage=task.Paid?"CargoWaiting":"Fetching";
            }
        }
        _development.Stage="Supplying";_development.Reason="继续原目标，保留已结算物料";_playerNotice=_development.Reason;
    }
    private void TickProcessorVisuals()
    {
        if(!DevelopmentEnabled)return;
        foreach(var f in _baseFacilities.Where(f=>f.Built&&f.Type=="processor"))
        {
            var node=_facilityNodes[f.Id].GetChild<Node3D>(0);
            var task=Production is {Kind:"recipe"} t&&t.Source==f.Id?t:null;
            bool powered=Powered(f);string state=task!=null&&_recipeWorked?"work":powered?"idle":"disabled";
            var preset=new Godot.Collections.Dictionary{{"state",state},{"phase","completed"},{"phase_t",0.0},{"cargo","empty"},{"reason",powered?"none":"no_power"},{"time_s",task?.Work??0}};
            string error=node.Call("apply_preview",preset).AsString();
            if(error.Length>0)throw new InvalidOperationException("加工状态适配失败："+error);
            var oldCrate=node.GetNodeOrNull<Node3D>("Model/processor-r1/Model/OutputCrate");if(oldCrate!=null)oldCrate.Visible=false;
        }
    }
    private void TransferMaterials(string operation,string goal,string source,string destination,Dictionary<string,int> amounts)
    {
        _ledger.Transfer(operation,goal,source,destination,amounts);
        _resourceViewDirty=true;
    }
    private readonly List<Node3D> _resourceViews=new();
    private CanvasLayer? _cargoGlyphLayer;
    private readonly Dictionary<string,Texture2D> _cargoGlyphTextures=new();
    private readonly List<(GroundPatrol Actor,Control Row)> _cargoGlyphs=new();
    private void TickCargoGlyphs()
    {
        foreach(var (actor,row) in _cargoGlyphs)
        {
            var point=actor.GlobalPosition+Vector3.Up*1.3f;
            row.Visible=!_camera.IsPositionBehind(point)&&_camera.GlobalPosition.DistanceTo(point)>=10;
            row.Position=_camera.UnprojectPosition(point)-new Vector2(row.Size.X/2,0);
        }
    }
    private void RebuildResourceViews()
    {
        if(!DevelopmentEnabled)return;
        foreach(var n in _resourceViews){n.GetParent().RemoveChild(n);n.QueueFree();}_resourceViews.Clear();
        foreach(var (_,row) in _cargoGlyphs){row.GetParent().RemoveChild(row);row.QueueFree();}_cargoGlyphs.Clear();
        foreach(var m in _mines)
        {
            var p=LoadVector(m.Position);p.Y=(float)SavedGroundHeight(_liveTerrain!.Current,p.X,p.Z)+.15f;
            var label=new Label3D{Position=p+Vector3.Up,Text=$"{MaterialName(m.Material)} {m.Remaining}/96",FontSize=40,PixelSize=.012f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled};AddChild(label);_resourceViews.Add(label);
        }
        foreach(var c in _ledger.Snapshot().Containers.Where(c=>c.Id!="spent"&&c.Items.Any(x=>x.Value>0)))
        {
            if(c.Id=="lander"||!c.Id.StartsWith("cargo:")&&!c.Id.StartsWith("batch:")&&!c.Id.StartsWith("mine-")&&!_baseFacilities.Any(f=>f.Id==c.Id&&f.Type=="storage"))continue;
            Node3D parent=c.Id.StartsWith("cargo:")?Actor(c.Id[6..]):this;
            bool cargo=parent!=this;
            var point=cargo?new Vector3(0,.72f,.28f):PhysicalEndpoint(c.Id);
            bool processor=false;
            bool output=false;
            var basis=Basis.Identity;
            if(cargo)
            {
                var actor=(GroundPatrol)parent;
                var supported=c.Items.Where(x=>x.Value>0&&ResourceLoader.Exists("res://assets/resources/d12-r1/icons/"+x.Key+".svg")).ToArray();
                if(supported.Length>0)
                {
                    var socket=_haulerVisuals[actor].Visual.FindChild("Socket_Cargo",true,false) as Node3D??throw new InvalidOperationException("载货挂点缺失");
                    var carrier=GD.Load<PackedScene>("res://assets/units/tuoyun-cargo-carrier-d12-r1/tuoyun-cargo-carrier-d12-r1.glb").Instantiate<Node3D>();socket.AddChild(carrier);_resourceViews.Add(carrier);
                    if(_cargoGlyphLayer==null){_cargoGlyphLayer=new CanvasLayer{Layer=0};AddChild(_cargoGlyphLayer);}
                    var row=new Control{Size=new Vector2(supported.Length*18-2,16),MouseFilter=Control.MouseFilterEnum.Ignore};_cargoGlyphLayer.AddChild(row);_cargoGlyphs.Add((actor,row));
                    for(int i=0;i<supported.Length;i++)
                    {
                        string id=supported[i].Key;
                        if(!_cargoGlyphTextures.TryGetValue(id,out var texture)){texture=GD.Load<Texture2D>("res://assets/resources/d12-r1/icons/"+id+".svg");_cargoGlyphTextures.Add(id,texture);}
                        row.AddChild(new TextureRect{Position=new Vector2(i*18,0),Size=new Vector2(16,16),Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=Control.MouseFilterEnum.Ignore});
                    }
                }
            }
            if(parent==this){var batch=_productionTasks.FirstOrDefault(t=>t.Kind=="recipe"&&t.Destination==c.Id);
                if(batch!=null){var facility=_facilityNodes[batch.Source];output=batch.Stage=="Completed";var socket=facility.FindChild(output?"Socket_Output":"Socket_Input",true,false) as Node3D??throw new InvalidOperationException("加工挂点缺失");point=socket.GlobalPosition;basis=facility.GlobalBasis;processor=true;}
                else if(_baseFacilities.Any(f=>f.Id==c.Id&&f.Type=="storage"))point=_facilityNodes[c.Id].ToGlobal(new Vector3(2.4f,0,1));
                if(!processor)point.Y=(float)SavedGroundHeight(_liveTerrain!.Current,point.X,point.Z);}
            var items=c.Items.Where(x=>x.Value>0&&x.Key!="kit").Take(4).ToArray();
            for(int i=0;i<items.Length;i++)
            {
                bool small=cargo||processor;
                float z=cargo?(i/2==0?-.18f:.09f):processor?(output&&items[i].Key!="iron"?(i/2==0?-.2f:.07f):i/2*.25f):i/2*.55f-.275f;
                var offset=new Vector3(small?(i%2==0?-.19f:.19f):i%2*.65f-.325f,0,z);
                var pile=new Node3D{Transform=new Transform3D(basis.Scaled(Vector3.One*(small ? .65f : 1)),point+basis*offset)};
                if(items[i].Key=="iron"&&cargo)pile.RotateY(Mathf.Pi/2);
                parent.AddChild(pile);_resourceViews.Add(pile);
                string path="res://assets/resources/d12-r1/"+items[i].Key+".glb";
                if(ResourceLoader.Exists(path))pile.AddChild(GD.Load<PackedScene>(path).Instantiate<Node3D>());
                else MeshPart(pile,new BoxMesh{Size=new(.5f,.25f,.4f)},items[i].Key switch {"iron_ore"=>new Color(.35f,.22f,.18f),"copper_ore"=>new Color(.33f,.43f,.31f),"iron"=>new Color(.45f,.5f,.54f),"copper"=>new Color(.65f,.36f,.2f),"parts"=>new Color(.7f,.7f,.72f),_=>new Color(.2f,.25f,.3f)},Vector3.Up*.125f);
            }
            var label=new Label3D{Position=point+Vector3.Up*.6f,Text=FormatMaterials(c.Items.Where(x=>x.Value>0).ToDictionary(x=>x.Key,x=>x.Value)),FontSize=30,PixelSize=.008f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled};parent.AddChild(label);_resourceViews.Add(label);
        }
    }
}
