#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yudian.Resources;
using Yudian.Terrain;
namespace Yudian;
public partial class Main
{
    private sealed class DevelopmentSave
    {
        public required MineSite[] Mines { get; init; }
        public required ProductionTask[] Tasks { get; init; }
        public required DevelopmentGoal? Goal { get; init; }
        public required int GoalSequence { get; init; }
        public required int ProductionSequence { get; init; }
    }
    private DevelopmentSave CaptureDevelopment()=>new(){Mines=_mines.ToArray(),Tasks=_productionTasks.ToArray(),Goal=_development,GoalSequence=_developmentSequence,ProductionSequence=_productionSequence};
    private static bool DevelopmentContainer(DevelopmentSave d,string id)=>d.Mines.Any(m=>m.Id==id)||d.Tasks.Any(t=>t.Kind=="recipe"&&t.Destination==id);
    private static bool ValidProductionStation(DevelopmentSave d,string station,string actor)
        => d.Goal?.Active==true && d.Tasks.Any(t=>t.Goal==d.Goal.Id&&t.Kind=="haul"&&t.Robot==actor&&
            (t.Source==station&&t.Stage is "Fetching" or "Pickup"||t.Destination==station&&t.Stage is "CargoWaiting" or "Delivering"));
    private void ApplyDevelopment(DevelopmentSave? saved)
    {
        _mines.Clear();_productionTasks.Clear();_development=null;_developmentSequence=0;_productionSequence=0;
        if(!DevelopmentEnabled)return;
        if(saved==null){InitializeDevelopment();return;}
        _mines.AddRange(saved.Mines);_productionTasks.AddRange(saved.Tasks);_development=saved.Goal;_developmentSequence=saved.GoalSequence;_productionSequence=saved.ProductionSequence;
        RebuildResourceViews();
    }
    private void ValidateDevelopment(PlayerSave saved,TerrainSnapshot terrain,BootstrapConfig config,MaterialLedger ledger)
    {
        var b=saved.Bootstrap!;var d=b.Development;
        if(saved.Schema==2)
        {
            if(d!=null||config.Version!="d11-bootstrap-1"||(b.Ledger.Mining?.Length??0)!=0||(b.Ledger.Recipes?.Length??0)!=0)throw new InvalidDataException("旧档不能含新生产事实");
            return;
        }
        if(d==null||d.Mines==null||d.Tasks==null||d.Mines.Length!=2||d.Tasks.Length>1000||d.GoalSequence<0||d.GoalSequence>10000||d.ProductionSequence<0||d.ProductionSequence>1000||d.Tasks.Length!=d.ProductionSequence)throw new InvalidDataException("生产集合或序号无效");
        var mining=b.Ledger.Mining??[];var recipes=b.Ledger.Recipes??[];
        string[] mineIds=["mine-iron","mine-copper"];
        if(!d.Mines.Select(m=>m?.Id).Order().SequenceEqual(mineIds.Order()))throw new InvalidDataException("矿点集合无效");
        foreach(var m in d.Mines)
        {
            var expected=m.Id=="mine-iron"?new float[]{-14,0,10}:new float[]{1,0,17};
            if(!LoadVector(m.Position).Equals(LoadVector(expected))||m.Material!=(m.Id=="mine-iron"?"iron_ore":"copper_ore")||m.Remaining<0||m.Remaining>96||!b.Ledger.Containers.Any(c=>c.Id==m.Id&&c.Capacity==100)||96-m.Remaining!=mining.Where(r=>r.Site==m.Id&&r.Material==m.Material).Sum(r=>r.Quantity))throw new InvalidDataException("矿点数量、位置或采矿账不一致");
            if(m.Remaining<96)
            {
                double depth=(96-m.Remaining)/96.0*1.2;
                for(int i=0;i<terrain.HeightsM.Count;i++)
                {
                    double x=terrain.OriginXM+i%terrain.Columns*terrain.SpacingM-m.Position[0],z=terrain.OriginZM+i/terrain.Columns*terrain.SpacingM-m.Position[2];
                    if(x*x+z*z<=4&&Math.Abs(terrain.HeightsM[i]-(_liveInitial.HeightsM[i]-depth))>.00000001)throw new InvalidDataException("矿量与权威地形阶段不一致");
                }
            }
        }
        if(mining.Any(r=>!mineIds.Contains(r.Site)||r.AppliedVersion>terrain.Version)||mining.Select(r=>r.AppliedVersion).Distinct().Count()!=mining.Length)throw new InvalidDataException("采矿版本与地形无效");
        if(d.Goal is {} g)
        {
            if(g.Id!="goal-"+d.GoalSequence||g.Type is not ("restock" or "connection")&&!config.Buildings.Any(x=>x.Id==g.Type)||g.Stage is not ("Supplying" or "Building" or "Completed" or "Cancelled" or "Blocked")||g.Reason==null||g.Reason.Length>1000||!float.IsFinite(g.Yaw)||Math.Abs(g.Yaw)>MathF.PI)throw new InvalidDataException("目标身份、授权或状态无效");
            var p=LoadVector(g.Center);
            if(p.Y!=0||Math.Abs(p.X)>=_cfg.Terrain.Size/2||Math.Abs(p.Z)>=_cfg.Terrain.Size/2)throw new InvalidDataException("目标位置无效");
            if(g.Type=="connection")
            {
                var target=b.Facilities.FirstOrDefault(f=>f.Id==g.Facility&&f.Built&&f.Type is "processor" or "charger" or "repair");
                var source=b.Facilities.FirstOrDefault(f=>f.Id==g.Source&&f.Built&&f.Type=="solar");
                if(target==null||source==null||LoadVector(target.Position)!=p||target.Yaw!=g.Yaw||XzDistance(LoadVector(source.Position),p)>config.ConnectionRangeM||g.Stage!="Completed"&&target.Source!=null||g.Stage=="Completed"&&target.Source!=g.Source||g.Stage=="Building"&&(b.Build==null||b.Build.Facility!=g.Facility||b.Build.Source!=g.Source))throw new InvalidDataException("供电连接目标引用无效");
            }
            else if(g.Facility!=null||g.Source!=null)throw new InvalidDataException("非连接目标携带供电引用");
            if(g.Stage=="Supplying"&&(b.Build!=null||g.Type!="restock"&&g.Type!="connection"&&b.Facilities.Any(f=>f.Built&&f.Type==g.Type&&LoadVector(f.Position)==p)))throw new InvalidDataException("前置生产阶段不能携带已执行建设或完成设施");
            if(g.Type=="restock"&&(b.Facilities.FirstOrDefault(f=>f.Built&&f.Type=="repair") is not {} repair||LoadVector(repair.Position)!=p||g.Yaw!=0))throw new InvalidDataException("补料目标没有真实维修站引用");
            if(g.Stage=="Building"&&(b.Build==null||!b.Build.Active||b.Build.Type!=g.Type||LoadVector(b.Build.Center)!=p)||g.Stage=="Completed"&&g.Type is not ("restock" or "connection")&&!b.Facilities.Any(f=>f.Built&&f.Type==g.Type&&LoadVector(f.Position)==p))throw new InvalidDataException("目标与当前建设结果不一致");
            if(g.Active&&saved.Job!=null&&Enum.TryParse<LevelStage>(saved.Job.Stage,out var stage)&&stage is not (LevelStage.Completed or LevelStage.Cancelled or LevelStage.Failed))throw new InvalidDataException("生产目标与整平同时活动");
        }
        else if(d.GoalSequence!=0||d.Tasks.Length!=0)throw new InvalidDataException("生产缺少目标归属");
        foreach(var c in b.Ledger.Containers.Where(c=>c.Id.StartsWith("batch:")||c.Id.StartsWith("mine-")))
            if(!DevelopmentContainer(d,c.Id)||c.Capacity!=100)throw new InvalidDataException("孤立生产容器");
        var taskIds=new HashSet<string>();
        bool Container(string id)=>b.Ledger.Containers.Any(c=>c.Id==id);
        float[] Origin(string id)
        {
            var mine=d.Mines.FirstOrDefault(m=>m.Id==id);if(mine!=null)return mine.Position;
            var batch=d.Tasks.FirstOrDefault(t=>t.Kind=="recipe"&&t.Destination==id);
            var facility=b.Facilities.FirstOrDefault(f=>f.Id==(batch?.Source??id));
            return facility?.Position??throw new InvalidDataException("没有真实生产端点");
        }
        bool AtDock(string container,string robot,float[] point)
        {
            var p=LoadVector(point);var origin=LoadVector(Origin(container));
            var batch=d.Tasks.FirstOrDefault(t=>t.Kind=="recipe"&&t.Destination==container);
            var facility=b.Facilities.FirstOrDefault(f=>f.Id==(batch?.Source??container));
            double radius=facility==null?2:facility.Type=="lander"?2.3:config.Buildings.Single(x=>x.Id==facility.Type).Radius;
            double offset=facility==null?terrain.SpacingM*2+1.4:1.2;
            double distance=radius+Actor(robot).BodyRadius+offset;
            return ((Math.Abs(Math.Abs(p.X-origin.X)-distance)<.0001&&p.Z==origin.Z)||(Math.Abs(Math.Abs(p.Z-origin.Z)-distance)<.0001&&p.X==origin.X)) && Math.Abs(p.Y-SavedGroundHeight(terrain,p.X,p.Z))<.0001;
        }
        void CheckTransfer(ProductionTask t,string suffix,string from,string to,Dictionary<string,int> amounts,bool expected)
        {
            bool exists=b.Ledger.Operations.TryGetValue(t.Id+suffix,out var signature);
            if(exists!=expected||exists&&signature!=MaterialLedger.Signature(t.Id,from,to,amounts))throw new InvalidDataException("生产实物交接与阶段不一致");
        }
        for(int i=0;i<d.Tasks.Length;i++)
        {
            var t=d.Tasks[i];
            if(t==null||t.Id!="prod-"+(i+1)||!taskIds.Add(t.Id)||!t.Goal.StartsWith("goal-")||!int.TryParse(t.Goal[5..],out int goalId)||goalId<1||goalId>d.GoalSequence||t.Kind is not ("mine" or "recipe" or "haul")||!MaterialLedger.Materials.Contains(t.Material)||t.Quantity<1||t.Quantity>(t.Kind=="recipe"?4:8)||!Container(t.Source)||!Container(t.Destination)||!FiniteRange(t.Work,240)||!FiniteRange(t.Waiting,181)||t.Reason==null||t.Reason.Length>1000||t.Stage is not ("Fetching" or "Pickup" or "CargoWaiting" or "Delivering" or "Travel" or "Working" or "Physics" or "Completed" or "Cancelled" or "Blocked"))throw new InvalidDataException("生产任务身份或字段无效");
            bool live=t.Stage is not ("Completed" or "Cancelled");
            if(live&&(d.Goal?.Active!=true&&d.Goal?.Stage!="Blocked"||t.Goal!=d.Goal.Id))throw new InvalidDataException("生产没有活动目标授权");
            if(t.Stage is "Cancelled" or "Blocked" && (t.ResumeStage==null||t.ResumeStage is "Completed" or "Cancelled" or "Blocked"))throw new InvalidDataException("生产重试阶段无效");
            string phase=t.Stage is "Cancelled" or "Blocked"?t.ResumeStage!:t.Stage;
            if(t.Kind=="mine")
            {
                if(t.Material is not ("iron_ore" or "copper_ore")||t.Source!=t.Destination||!mineIds.Contains(t.Source)||t.Robot==null||!t.Robot.StartsWith("Robot_Zhulei_")||!b.Health.ContainsKey(t.Robot)||t.Station==null||!AtDock(t.Source,t.Robot,t.Station)||t.Work>6||phase is not ("Travel" or "Working" or "Physics" or "Completed"))throw new InvalidDataException("采矿任务阶段或工位无效");
                var receipt=mining.SingleOrDefault(r=>r.Operation==t.Id+"-mine");
                if(t.Paid!=(receipt!=null)||receipt!=null&&(receipt.Site!=t.Source||receipt.Material!=t.Material||receipt.Quantity!=t.Quantity)||t.Paid&&t.Work!=6||phase is "Physics" or "Completed"&&!t.Paid)throw new InvalidDataException("采矿任务与结算不一致");
                if(t.Patch!=null)
                {
                    var patch=TerrainDataCodec.ParsePatch(t.Patch);
                    if(patch.PatchId!=t.Id||t.Paid&&(receipt!.BaseVersion!=patch.Base.Version||receipt.AppliedVersion!=patch.Base.Version+1))throw new InvalidDataException("采矿候选与回执不一致");
                }
            }
            else if(t.Kind=="recipe")
            {
                var recipe=config.Recipes.SingleOrDefault(r=>r.Id==t.Material)??throw new InvalidDataException("未知加工配方");
                if(t.Destination!="batch:"+t.Id||t.Robot!=null||t.Station!=null||!b.Facilities.Any(f=>f.Id==t.Source&&f.Built&&f.Type=="processor")||!t.Paid||t.Work>recipe.WorkSeconds*t.Quantity||phase is not ("Working" or "Completed"))throw new InvalidDataException("加工任务阶段无效");
                var inputs=recipe.Input.ToDictionary(x=>x.Key,x=>x.Value*t.Quantity);
                CheckTransfer(t,"-input",t.Source,t.Destination,inputs,true);
                var receipt=recipes.SingleOrDefault(r=>r.Operation==t.Id+"-recipe");
                bool complete=phase=="Completed";
                if((receipt!=null)!=complete||receipt!=null&&(receipt.Container!=t.Destination||receipt.Recipe!=t.Material||receipt.Batches!=t.Quantity)||complete&&t.Work!=recipe.WorkSeconds*t.Quantity||!complete&&t.Stage!="Cancelled"&&inputs.Any(x=>ledger.Count(t.Destination,x.Key)!=x.Value))throw new InvalidDataException("加工投入或配方结算与阶段不一致");
            }
            else
            {
                if(t.Source==t.Destination||t.Robot==null||!t.Robot.StartsWith("Robot_Tuoyun_")||!b.Health.ContainsKey(t.Robot)||t.Work!=0||phase is not ("Fetching" or "Pickup" or "CargoWaiting" or "Delivering" or "Completed"))throw new InvalidDataException("运输任务阶段无效");
                string cargo=CargoContainer(t.Robot);var amount=new Dictionary<string,int>{{t.Material,t.Quantity}};
                bool source=b.Facilities.Any(f=>f.Id==t.Source&&(!f.Built||f.Type is "lander" or "storage" or "processor"))||mineIds.Contains(t.Source)||d.Tasks.Any(batch=>batch.Kind=="recipe"&&batch.Destination==t.Source&&batch.Stage is "Completed" or "Cancelled")||t.Source==cargo&&t.Paid;
                bool destination=b.Facilities.Any(f=>f.Id==t.Destination&&f.Built&&f.Type is "lander" or "storage" or "processor" or "repair");
                if(!source||!destination)throw new InvalidDataException("运输必须绑定允许的真实物料端点");
                CheckTransfer(t,"-take",t.Source,cargo,amount,t.Paid&&t.Source!=cargo);
                CheckTransfer(t,"-unload",cargo,t.Destination,amount,phase=="Completed");
                if(t.Paid!=(phase is "CargoWaiting" or "Delivering" or "Completed")||live&&t.Paid&&ledger.Count(cargo,t.Material)<t.Quantity)throw new InvalidDataException("运输载荷与阶段不一致");
                if(live&&phase is "Pickup" or "Delivering")
                {
                    string port=phase=="Pickup"?t.Source:t.Destination;
                    if(t.Station==null||!AtDock(port,t.Robot,t.Station))throw new InvalidDataException("取卸货站不属于真实端点");
                }
            }
            if(live&&t.Robot!=null&&t.Stage is "Pickup" or "Delivering" or "Working")
            {
                var service=b.Services.FirstOrDefault(s=>s.Robot==t.Robot);
                var destination=service?.ReturnTo??b.Destinations.GetValueOrDefault(t.Robot);
                if(destination==null||t.Station==null||LoadVector(destination)!=LoadVector(t.Station))throw new InvalidDataException("生产路线与工位不一致");
            }
        }
        if(d.Tasks.Count(t=>t.Stage is not ("Completed" or "Cancelled"))>1||mining.Any(r=>!taskIds.Contains(r.Operation.Replace("-mine","")))||recipes.Any(r=>!taskIds.Contains(r.Operation.Replace("-recipe",""))))throw new InvalidDataException("重复活动生产或孤立结算");
        var ownedOperations=d.Tasks.SelectMany(t=>t.Kind=="haul"?new[]{t.Id+"-take",t.Id+"-unload"}:t.Kind=="recipe"?new[]{t.Id+"-input"}:Array.Empty<string>()).ToHashSet();
        if(b.Ledger.Operations.Keys.Any(k=>k.StartsWith("prod-")&&!ownedOperations.Contains(k)))throw new InvalidDataException("未知生产交易");
    }
}
