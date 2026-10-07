#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Yudian.Resources;
using Yudian.Navigation;
using Yudian.Terrain;
namespace Yudian;
public partial class Main
{
    private sealed class BootstrapSave
    {
        public required string ConfigHash { get; init; }
        public required long Revision { get; init; }
        public required int BuildSequence { get; init; }
        public required int ServiceSequence { get; init; }
        public required LedgerSnapshot Ledger { get; init; }
        public required BaseFacility[] Facilities { get; init; }
        public required Dictionary<string, RobotHealth> Health { get; init; }
        public required BuildJob? Build { get; init; }
        public required ServiceJob[] Services { get; init; }
        public required Dictionary<string,string> Stations { get; init; }
        public required Dictionary<string,float[]> Destinations { get; init; }
    }
    private string BootstrapHash
    {
        get{using var file=Godot.FileAccess.Open("res://config/bootstrap-v1.json",Godot.FileAccess.ModeFlags.Read);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(file.GetAsText())));}
    }
    private BootstrapSave CaptureBootstrap()
    {
        var snapshot=new BootstrapSave{ConfigHash=BootstrapHash,Revision=_baseRevision,BuildSequence=_buildSequence,ServiceSequence=_serviceSequence,
            Ledger=_ledger.Snapshot(),Facilities=_baseFacilities.ToArray(),Health=new(_health),Build=_buildJob,Services=_services.Values.ToArray(),Stations=new(_stations),Destinations=_routes.ToDictionary(x=>x.Key,x=>SavedVector(x.Value.Destination))};
        return JsonSerializer.Deserialize<BootstrapSave>(JsonSerializer.Serialize(snapshot,SaveOptions),SaveOptions)!;
    }
    private static bool FiniteRange(double x,double max)=>double.IsFinite(x)&&x>=0&&x<=max;
    private void ValidateBootstrap(PlayerSave saved,TerrainSnapshot terrain)
    {
        var s=saved.Bootstrap??throw new InvalidDataException("缺少自举事实");
        if(s.ConfigHash!=BootstrapHash||s.Revision<0||s.Revision>1_000_000||s.BuildSequence<0||s.BuildSequence>10000||s.ServiceSequence<0||s.ServiceSequence>100000||s.Facilities==null||s.Facilities.Length==0||s.Facilities.Length>500||s.Health==null||s.Services==null||s.Stations==null||s.Destinations==null)
            throw new InvalidDataException("自举配置、版本或集合无效");
        var ledger=new MaterialLedger(s.Ledger);
        if(!s.Ledger.Containers.Any(c=>c.Id=="lander"&&c.Capacity==500)||!s.Ledger.Containers.Any(c=>c.Id=="spent"&&c.Capacity==MaterialLedger.Limit))throw new InvalidDataException("缺少固定库存或消耗容器");
        foreach(var c in s.Ledger.Containers)
        {
            if(c.Id is "lander" or "spent" || saved.Robots.Any(a=>c.Id==CargoContainer(a.Id)))continue;
            if(!c.Id.StartsWith("build-")||!int.TryParse(c.Id[6..],out int n)||n<1||n>s.BuildSequence||c.Capacity!=100)throw new InvalidDataException("物料容器没有合法归属");
        }
        var spent=MaterialLedger.Materials.ToDictionary(m=>m,m=>0);
        void CheckConsumed(string id,string container,Dictionary<string,int> cost)
        {
            if(!s.Ledger.Operations.TryGetValue(id+"-consume",out var signature)||signature!=MaterialLedger.Signature(id,container,"spent",cost))throw new InvalidDataException("设施缺少一致的材料消耗记录");
            foreach(var pair in cost)spent[pair.Key]+=pair.Value;
        }
        if(!ledger.Totals().All(x=>_bootstrapConfig.Initial[x.Key]==x.Value))throw new InvalidDataException("物料不守恒");
        if(s.Facilities.Select(x=>x?.Id).Distinct().Count()!=s.Facilities.Length||s.Facilities.Count(x=>x.Id=="lander"&&x.Type=="lander"&&x.Built)!=1)throw new InvalidDataException("设施身份无效");
        foreach(var f in s.Facilities)
        {
            if(f==null||string.IsNullOrWhiteSpace(f.Id)||f.Id.Length>100||f.Type!="lander"&&!_bootstrapConfig.Buildings.Any(b=>b.Id==f.Type)||!float.IsFinite(f.Yaw)||Math.Abs(f.Yaw)>MathF.PI)throw new InvalidDataException("设施类型或朝向无效");
            var p=LoadVector(f.Position);float radius=BaseRadius(f);
            if(p.Y!=0||Math.Abs(p.X)+radius>=_cfg.Terrain.Size/2||Math.Abs(p.Z)+radius>=_cfg.Terrain.Size/2||f.Id=="lander"&&(p.X!=7||p.Z!=-22||f.Source!=null))throw new InvalidDataException("设施位置无效");
            if(f.Id!="lander"&&(!f.Id.StartsWith("build-")||!int.TryParse(f.Id[6..],out int i)||i<1||i>s.BuildSequence))throw new InvalidDataException("未知设施来源");
            if(s.Facilities.Any(other=>other!=f&&XzDistance(LoadVector(other.Position),p)<=radius+BaseRadius(other)))throw new InvalidDataException("设施相互重叠");
            if(f.Source is {} source&&(!f.Built||f.Type is "solar" or "lander" or "storage"||!s.Facilities.Any(x=>x.Id==source&&x.Built&&x.Type=="solar"&&XzDistance(LoadVector(x.Position),p)<=_bootstrapConfig.ConnectionRangeM)))throw new InvalidDataException("供电来源或线缆距离无效");
            if(f.Id!="lander"&&f.Built)CheckConsumed(f.Id,f.Id,Definition(f.Type).Cost);
            if(f.Source!=null)
            {
                if(f.ConnectionId==null||!f.ConnectionId.StartsWith("build-")||!int.TryParse(f.ConnectionId[6..],out int connection)||connection<1||connection>s.BuildSequence)throw new InvalidDataException("线缆消耗身份缺失");
                CheckConsumed(f.ConnectionId,f.ConnectionId,BuildCost("connection"));
            }
            else if(f.ConnectionId!=null)throw new InvalidDataException("线缆与来源不一致");
            if(f.Id!="lander"&& !s.Ledger.Containers.Any(c=>c.Id==f.Id&&c.Capacity==100))throw new InvalidDataException("现场物料容器缺失");
        }
        if(s.Health.Count!=saved.Robots.Length||saved.Robots.Any(a=>!s.Health.ContainsKey(a.Id)))throw new InvalidDataException("机器人健康集合无效");
        foreach(var h in s.Health)
            if(h.Value==null||!FiniteRange(h.Value.Energy,_bootstrapConfig.Capacity)||!FiniteRange(h.Value.Durability,_bootstrapConfig.Capacity)||!FiniteRange(h.Value.Travel,1e12)||h.Value.Reason==null||h.Value.Reason.Length>1000||!s.Ledger.Containers.Any(c=>c.Id==CargoContainer(h.Key)&&c.Capacity==_bootstrapConfig.CargoCapacity))throw new InvalidDataException("机器人健康或载荷无效");
        foreach(var slot in s.Stations)if(!s.Facilities.Any(f=>f.Id==slot.Key)||!s.Health.ContainsKey(slot.Value))throw new InvalidDataException("未知工位预约");
        if(s.Services.Length>saved.Robots.Length||s.Services.Select(x=>x.Robot).Distinct().Count()!=s.Services.Length||s.Services.Select(x=>x.Id).Distinct().Count()!=s.Services.Length)throw new InvalidDataException("重复服务关系");
        foreach(var service in s.Services)
        {
            if(service==null||!s.Health.ContainsKey(service.Robot)||!service.Id.StartsWith("service-")||!int.TryParse(service.Id[8..],out int id)||id<1||id>s.ServiceSequence||service.Kind is not ("charge" or "repair")||!s.Facilities.Any(f=>f.Id==service.Facility&&f.Built&&f.Type==(service.Kind=="charge"?"charger":"repair"))||(!service.Returning&&(!s.Stations.TryGetValue(service.Facility,out var owner)||owner!=service.Robot))||!FiniteRange(service.Progress,_bootstrapConfig.RepairSeconds)||!FiniteRange(service.Waiting,1e12))throw new InvalidDataException("服务身份或预约不一致");
            var servicePosition=LoadVector(service.Station);var facility=s.Facilities.Single(f=>f.Id==service.Facility);
            var actor=_groundRobots.Single(a=>a.Name.ToString()==service.Robot);var fpos=LoadVector(facility.Position);float reach=BaseRadius(facility)+actor.BodyRadius+1.2f;
            bool station=(Math.Abs(Math.Abs(servicePosition.X-fpos.X)-reach)<.0001 && servicePosition.Z==fpos.Z)||(Math.Abs(Math.Abs(servicePosition.Z-fpos.Z)-reach)<.0001 && servicePosition.X==fpos.X);
            if(!station||Math.Abs(servicePosition.Y-SavedGroundHeight(terrain,servicePosition.X,servicePosition.Z))>.0001||!s.Destinations.TryGetValue(service.Robot,out var destination)||LoadVector(destination)!=(service.Returning&&service.ReturnTo!=null?LoadVector(service.ReturnTo):servicePosition))throw new InvalidDataException("服务站位或路线不一致");
            if(service.ReturnTo!=null)
            {
                var back=LoadVector(service.ReturnTo);
                if(Math.Abs(back.X)>=_cfg.Terrain.Size/2||Math.Abs(back.Z)>=_cfg.Terrain.Size/2)throw new InvalidDataException("保障返程目的地越界");
            }
            if(saved.Job is {} level&&level.Worker==service.Robot&&Enum.TryParse<LevelStage>(level.Stage,out var levelStage)&&levelStage is not (LevelStage.Completed or LevelStage.Cancelled or LevelStage.Failed)&& (service.ReturnTo==null||LoadVector(service.ReturnTo)!=LoadVector(level.Station)))throw new InvalidDataException("整平保障返程与原工作站不一致");
            if(service.Returning&&(service.ReturnTo==null||s.Stations.Any(x=>x.Value==service.Robot)||service.Kind=="repair"&&(!service.Paid||service.Progress!=_bootstrapConfig.RepairSeconds)))throw new InvalidDataException("保障返程状态无效");
            if(service.Kind=="charge"&&(service.Paid||service.Progress!=0)||service.Paid&&!s.Ledger.Operations.ContainsKey(service.Id+"-parts")||!service.Paid&&service.Progress!=0)throw new InvalidDataException("服务扣料与进度不一致");
            if(service.Paid)
            {
                var cost=new Dictionary<string,int>{{"parts",_bootstrapConfig.RepairParts}};
                if(s.Ledger.Operations[service.Id+"-parts"]!=MaterialLedger.Signature(service.Id,service.Facility,"spent",cost))throw new InvalidDataException("维修消耗与归属不一致");
            }
        }
        foreach(var operation in s.Ledger.Operations.Where(x=>x.Key.StartsWith("service-")&&x.Key.EndsWith("-parts")))
        {
            var parts=operation.Value.Split('|');
            if(parts.Length!=4||parts[2]!="spent"||parts[3]!="parts:"+_bootstrapConfig.RepairParts||!s.Facilities.Any(f=>f.Id==parts[1]&&f.Built&&f.Type=="repair"))throw new InvalidDataException("维修结算记录无效");
            spent["parts"]+=_bootstrapConfig.RepairParts;
        }
        if(!spent.All(x=>ledger.Count("spent",x.Key)==x.Value))throw new InvalidDataException("消耗记录与已用材料不一致");
        foreach(var dest in s.Destinations)
        {if(!s.Health.ContainsKey(dest.Key))throw new InvalidDataException("路线身份无效");var p=LoadVector(dest.Value);if(Math.Abs(p.X)>=_cfg.Terrain.Size/2||Math.Abs(p.Z)>=_cfg.Terrain.Size/2)throw new InvalidDataException("路线越界");}
        if(s.Build is {} b)
        {
            string[] stages=["Preparing","LevelTravel","Levelling","LevelPhysics","Fetching","Pickup","CargoWaiting","Delivering","BuilderTravel","Building","Completed","Cancelled","Blocked"];
            if(!stages.Contains(b.Stage)||!b.Id.StartsWith("build-")||!int.TryParse(b.Id[6..],out int id)||id<1||id>s.BuildSequence||!s.Health.ContainsKey(b.Builder)||!b.Builder.StartsWith("Robot_Zhulei_")||!s.Health.ContainsKey(b.Hauler)||!b.Hauler.StartsWith("Robot_Tuoyun_")||b.Builder==b.Hauler||b.Type!="connection"&&!_bootstrapConfig.Buildings.Any(x=>x.Id==b.Type)||!s.Facilities.Any(x=>x.Id==b.Facility)||!FiniteRange(b.Work,b.Type=="connection"?3:Math.Max(3,Definition(b.Type).WorkSeconds))||!FiniteRange(b.Waiting,181)||b.Trip<0||b.Trip>10000||b.Reason==null||b.Reason.Length>1000)
                throw new InvalidDataException("工程身份、阶段或时间无效");
            if(b.Stage=="Levelling"&&b.Work>3)throw new InvalidDataException("整平进度越过阶段上限");
            foreach(var operation in s.Ledger.Operations.Keys.Where(k=>k.StartsWith(b.Id+"-")))
            {
                var pieces=operation[(b.Id.Length+1)..].Split('-');
                if(pieces.Length==2&&pieces[0] is "take" or "unload" or "return"&&(!int.TryParse(pieces[1],out int trip)||trip<0||trip>b.Trip))throw new InvalidDataException("运输序号落后于实际交接");
            }
            var center=LoadVector(b.Center);LoadVector(b.Station);if(b.HaulStation!=null)LoadVector(b.HaulStation);if(b.Stage=="Delivering"&&b.HaulStation==null)throw new InvalidDataException("交付站缺失");var patch=TerrainDataCodec.ParsePatch(b.Patch);
            var expectedCost=BuildCost(b.Type);
            if(b.Cost==null||b.Cost.Count!=expectedCost.Count||!expectedCost.All(x=>b.Cost.GetValueOrDefault(x.Key)==x.Value)||!float.IsFinite(b.Yaw)||Math.Abs(b.Yaw)>MathF.PI)throw new InvalidDataException("工程成本或朝向无效");
            var f=s.Facilities.Single(x=>x.Id==b.Facility);
            string buffer=b.Type=="connection"?b.Id:b.Facility;
            if(!s.Ledger.Containers.Any(c=>c.Id==buffer&&c.Capacity==100))throw new InvalidDataException("当前工程现场容器缺失");
            bool AtPort(BaseFacility facility,string robot,Vector3 position,double offset)
            {
                var origin=LoadVector(facility.Position);double d=BaseRadius(facility)+_groundRobots.Single(a=>a.Name.ToString()==robot).BodyRadius+offset;
                bool cardinal=(Math.Abs(Math.Abs(position.X-origin.X)-d)<.0001&&position.Z==origin.Z)||(Math.Abs(Math.Abs(position.Z-origin.Z)-d)<.0001&&position.X==origin.X);
                return cardinal&&Math.Abs(position.X)<_cfg.Terrain.Size/2&&Math.Abs(position.Z)<_cfg.Terrain.Size/2&&Math.Abs(position.Y-SavedGroundHeight(terrain,position.X,position.Z))<.0001;
            }
            Vector3? TaskDestination(string robot)
            {
                var service=s.Services.FirstOrDefault(x=>x.Robot==robot);
                if(service!=null)return service.ReturnTo==null?null:LoadVector(service.ReturnTo);
                return s.Destinations.TryGetValue(robot,out var destination)?LoadVector(destination):null;
            }
            var builderStation=LoadVector(b.Station);
            // Builder standoff includes the patch's changed cells and an extra 1.4m beyond the body.
            double builderOffset=patch.Base.SpacingM*2+1.4-_groundRobots.Single(a=>a.Name.ToString()==b.Builder).BodyRadius;
            if(!AtPort(f,b.Builder,builderStation,builderOffset)||TouchesFootprint(patch,builderStation.X,builderStation.Z,Actor(b.Builder).BodyRadius))throw new InvalidDataException("施工站不属于当前工程");
            if(b.HaulStation!=null&&!AtPort(f,b.Hauler,LoadVector(b.HaulStation),1.2))throw new InvalidDataException("卸货站不属于当前工程");
            if(b.Active)
            {
                if(b.Stage is "LevelTravel" or "Levelling" or "Building" && TaskDestination(b.Builder)!=builderStation)throw new InvalidDataException("筑垒目的地与施工站不一致");
                if(b.Stage=="Pickup")
                {
                    var dock=TaskDestination(b.Hauler);if(dock==null||!AtPort(s.Facilities.Single(x=>x.Id=="lander"),b.Hauler,dock.Value,1.2))throw new InvalidDataException("取货目的地不属于着陆器");
                }
                if(b.Stage=="Delivering"&&(b.HaulStation==null||TaskDestination(b.Hauler)!=LoadVector(b.HaulStation)))throw new InvalidDataException("驮运目的地与交货站不一致");
                if(b.Stage is "BuilderTravel" or "Building" && b.Cost.Any(x=>ledger.Count(buffer,x.Key)<x.Value))throw new InvalidDataException("施工阶段物料未齐备");
                if(b.Cost.Any(x=>ledger.Count(buffer,x.Key)+ledger.Count(CargoContainer(b.Hauler),x.Key)+ledger.Reserved(b.Id,"lander",x.Key)<x.Value))throw new InvalidDataException("工程物料没有实际归属或预约");
            }
            if(center!=LoadVector(f.Position)||b.Type!="connection"&&(b.Type!=f.Type||b.Yaw!=f.Yaw||b.Id!=f.Id)||b.Type=="connection"&&(!f.Built||b.Source==null||!s.Facilities.Any(x=>x.Id==b.Source&&x.Built&&x.Type=="solar"&&XzDistance(LoadVector(x.Position),center)<=_bootstrapConfig.ConnectionRangeM)))throw new InvalidDataException("工程设施关系无效");
            if(patch.PatchId!=b.Id||patch.Base.RegionId!=terrain.RegionId||patch.Base.Rows!=terrain.Rows||patch.Base.Columns!=terrain.Columns||patch.Base.SpacingM!=terrain.SpacingM||patch.Base.OriginXM!=terrain.OriginXM||patch.Base.OriginZM!=terrain.OriginZM)throw new InvalidDataException("工程地形不一致");
            var expected=b.Type=="connection"?GroundPatchFromHeights(patch.Base,patch.Base.HeightsM.ToArray(),b.Id):BuildPatch(patch.Base,center,BaseRadius(f),b.Id);
            if(!expected.HeightsM.SequenceEqual(patch.HeightsM)||patch.Base.Version>terrain.Version)throw new InvalidDataException("工程地形候选非法");
            bool original=terrain.Version==patch.Base.Version&&terrain.HeightsM.SequenceEqual(patch.Base.HeightsM);
            bool committed=terrain.Version==patch.Base.Version+1&&terrain.HeightsM.SequenceEqual(patch.HeightsM);
            bool noChange=patch.Base.HeightsM.SequenceEqual(patch.HeightsM);
            if(!original&&!committed||b.Stage is "Preparing" or "LevelTravel" or "Levelling"&&!original||b.Stage is "LevelPhysics" or "Fetching" or "Pickup" or "CargoWaiting" or "Delivering" or "BuilderTravel" or "Building" or "Completed"&&!(committed||noChange&&original))throw new InvalidDataException("工程阶段与地形提交不一致");
            if(b.Stage=="Completed"&&(!s.Ledger.Operations.ContainsKey(b.Id+"-consume")||!f.Built||b.Type=="connection"&&f.Source!=b.Source)||b.Stage!="Completed"&&s.Ledger.Operations.ContainsKey(b.Id+"-consume"))throw new InvalidDataException("完成与材料结算不一致");
            if(b.Active&&saved.Job!=null&&Enum.TryParse<LevelStage>(saved.Job.Stage,out var stage)&&stage is not (LevelStage.Completed or LevelStage.Cancelled or LevelStage.Failed))throw new InvalidDataException("两项工程同时活动");
        }
        foreach(var slot in s.Stations)
        {
            if(s.Services.Any(service=>service.Facility==slot.Key&&service.Robot==slot.Value))continue;
            var current=s.Build;
            bool builder=current?.Active==true&&slot.Key==current.Facility&&slot.Value==current.Builder&&current.Stage is "LevelTravel" or "Levelling" or "LevelPhysics" or "BuilderTravel" or "Building";
            bool hauler=current?.Active==true&&slot.Value==current.Hauler&&(slot.Key=="lander"&&current.Stage is "Fetching" or "Pickup"||slot.Key==current.Facility&&current.Stage is "CargoWaiting" or "Delivering");
            if(!builder&&!hauler)throw new InvalidDataException("工位预约没有当前执行者");
        }
        foreach(var reservation in s.Ledger.Reservations)
            if(s.Build==null||reservation.Goal!=s.Build.Id||!s.Build.Active||reservation.Container!="lander")throw new InvalidDataException("预约没有有效所属目标");
    }
    private void ApplyBootstrap(BootstrapSave s)
    {
        _ledger=new(s.Ledger);_baseFacilities.Clear();_baseFacilities.AddRange(s.Facilities);_baseRevision=s.Revision;_buildSequence=s.BuildSequence;_serviceSequence=s.ServiceSequence;_buildJob=s.Build;
        _health.Clear();foreach(var x in s.Health){x.Value.Travel=Actor(x.Key).TravelledM;_health.Add(x.Key,x.Value);}
        _services.Clear();foreach(var x in s.Services)_services.Add(x.Robot,x);
        _stations.Clear();foreach(var x in s.Stations)_stations.Add(x.Key,x.Value);
        _routes.Clear();RebuildBaseFacilities();
    }
    private void RestoreBaseOrders(BootstrapSave s)
    {
        foreach(var d in s.Destinations){var actor=Actor(d.Key);var destination=LoadVector(d.Value);
            if(!Operational(actor)||!OrderBase(actor,destination))
            {
                _routes[d.Key]=new(){Destination=destination,WorldVersion=-1,FacilityRevision=-1};
                _health[d.Key].Reason="读档后停机或路线受阻；目的地与事实保留";
            }}
    }
}
