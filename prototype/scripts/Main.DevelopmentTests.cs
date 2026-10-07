#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
namespace Yudian;
public partial class Main
{
    private int _developmentTestStep;
    private string _d12ServiceRobot="Robot_Zhulei_2";
    private double _developmentTestTime;
    private string DevelopmentPhase=>System.Environment.GetEnvironmentVariable("YUDIAN_DEVELOPMENT_TEST_PHASE")??"full";
    private bool DevelopmentComplete=>_development?.Stage=="Completed";
    private void D12Build(string type,Vector3 position)
    {
        var preview=PreviewBuild(type,position);GroundRequire(preview.Legal,"D12 preview "+type+": "+preview.Reason);QueueBuild(type,position,0,preview.Version);
    }
    private void DevelopmentTestStep()
    {
        if(!_groundReady||(!ReadPlayerState().Ready&&_developmentTestStep!=14)||!_groundRobots.All(a=>a.IsOnFloor()))return;
        if(_playerTime>7000)throw new InvalidOperationException($"D12 timeout step={_developmentTestStep} goal={_development?.Stage} prod={Production?.Id}/{Production?.Stage}/{Production?.Reason} build={_buildJob?.Stage}/{_buildJob?.Reason} health="+string.Join(";",_health.Select(x=>$"{x.Key}:{x.Value.Energy:0.0}/{x.Value.Durability:0.0}:{x.Value.Reason}")));
        if(_groundFrame%600==0)GD.Print($"D12_PROGRESS step={_developmentTestStep} time={_playerTime:0.0} goal={_development?.Stage} prod={Production?.Id}:{Production?.Kind}/{Production?.Material}/{Production?.Stage} reason={Production?.Reason??_development?.Reason} build={_buildJob?.Stage}/{_buildJob?.Reason}");
        if(_development?.Stage=="Blocked"&&_developmentTestStep!=91)throw new InvalidOperationException("D12 goal blocked "+_development.Reason);
        switch(_developmentTestStep)
        {
            case 0:
                Engine.TimeScale=8;
                GroundRequire(_bootstrapConfig.Version=="d12-firstplay-1"&&_ledger.Count("lander","parts")==21&&_mines.All(m=>m.Remaining==96),"finite D12 formal startup");
                GroundRequire(!PreviewBuild("storage",new(9,0,9)).Legal,"startup protection prevents early storage deadlock");
                TestBadSave(json=>
                {
                    var d=json["Bootstrap"]!["Development"]!;d["GoalSequence"]=1;
                    d["Goal"]=JsonSerializer.SerializeToNode(new DevelopmentGoal{Id="goal-1",Type="restock",Center=[7,0,-22]},SaveOptions);
                });
                if(DevelopmentPhase=="processor-first"){D12Build("processor",new(-14,0,-14));_developmentTestStep=200;break;}
                if(DevelopmentPhase.StartsWith("resume")){QueuePlayerAction("load");_developmentTestStep=90;break;}
                if(DevelopmentPhase=="legacy"){QueuePlayerAction("load");_developmentTestStep=95;break;}
                D12Build("solar",new(-3,0,-14));_developmentTestStep=1;break;
            case 1:if(!DevelopmentComplete)break;D12Build("charger",new(-3,0,-3));_developmentTestStep=2;break;
            case 2:if(!DevelopmentComplete)break;QueuePlayerAction("connect");_developmentTestStep=3;break;
            case 3:if(_buildJob?.Stage!="Completed"||_buildJob.Type!="connection")break;D12Build("repair",new(8,0,-14));_developmentTestStep=4;break;
            case 4:if(!DevelopmentComplete)break;QueuePlayerAction("connect");_developmentTestStep=5;break;
            case 5:if(_buildJob?.Stage!="Completed"||_buildJob.Type!="connection")break;D12Build("processor",new(-14,0,-14));_developmentTestStep=6;break;
            case 6:if(!DevelopmentComplete)break;QueuePlayerAction("connect");_developmentTestStep=7;break;
            case 7:
                if(_buildJob?.Stage!="Completed"||_buildJob.Type!="connection")break;
                GroundRequire(new[]{"iron","copper","parts","cable"}.All(m=>_ledger.Count("lander",m)==0),"true later gap after safe first set");
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==4,"protected repair supplies");
                SavePlayer();ValidatePlayerSave(CapturePlayer());
                D12Build("storage",new(9,0,9));_developmentTestStep=8;break;
            case 8:
                if(Production is not {Kind:"mine",Stage:"Working"} mine||mine.Work<1)break;
                QueuePlayerAction("pause");_developmentTestStep=9;break;
            case 9:
                if(!_userPaused)break;_developmentTestTime=_playerTime;
                ValidatePlayerSave(CapturePlayer());QueuePlayerAction("save");_developmentTestStep=10;break;
            case 10:
                GroundRequire(_playerTime==_developmentTestTime,"paused mining does not tick");QueuePlayerAction("cancel");_developmentTestStep=11;break;
            case 11:
                GroundRequire(_development?.Stage=="Cancelled"&&_mines.All(m=>m.Remaining==96),"cancel uncommitted mine emits no goods");
                ValidatePlayerSave(CapturePlayer());QueuePlayerAction("retry");_developmentTestStep=12;break;
            case 12:
                if(_development?.Active!=true)break;QueuePlayerAction("pause");_developmentTestStep=13;break;
            case 13:
                if(Production?.Kind=="mine"&&Production.Stage=="Working"&&!Production.Paid)
                {
                    _liveTerrain!.AfterMeshBoundForTest=()=>throw new InvalidOperationException("synthetic D12 mining projection failure");_developmentTestStep=14;
                }
                break;
            case 14:
                if(!_groundFault)return;
                GroundRequire(Production?.Paid==true&&_mines.Sum(m=>96-m.Remaining)==8&&(_ledger.Snapshot().Mining?.Length??0)==1,"failed projection committed ore exactly once");
                _liveTerrain!.AfterMeshBoundForTest=null;RecoverGround();_developmentTestStep=15;break;
            case 15:
                if(!ReadPlayerState().Ready)break;
                ValidatePlayerSave(CapturePlayer());
                GroundRequire((_ledger.Snapshot().Mining?.Length??0)==1,"recover projection does not reissue ore");
                if(DevelopmentPhase=="prepare-mine"){QueuePlayerAction("pause");_developmentTestStep=80;break;}
                _developmentTestStep=16;break;
            case 16:
                if(Production is not {Kind:"haul",Stage:"Delivering"} haul)break;
                GroundRequire(_ledger.Load(CargoContainer(haul.Robot!))>0,"real mine cargo");
                TestBadSave(json=>
                {
                    var tasks=json["Bootstrap"]!["Development"]!["Tasks"]!.AsArray();var t=tasks.Last()!;
                    t["Source"]="spent";t["Stage"]="Fetching";t["Paid"]=false;t["Station"]=null;
                    json["Bootstrap"]!["Ledger"]!["Operations"]!.AsObject().Remove(haul.Id+"-take");
                });
                if(DevelopmentPhase=="prepare-cargo"){QueuePlayerAction("pause");_developmentTestStep=80;break;}
                haul.Waiting=180;TickProduction(haul,1.0/60);
                GroundRequire(_development?.Stage=="Blocked"&&ReadPlayerState().Job?.Active==true&&!PreviewBuild("solar",new(13,0,-1)).Legal,"blocked obligation remains cancellable and cannot be abandoned silently");
                QueuePlayerAction("cancel");_developmentTestStep=17;break;
            case 17:
                GroundRequire(_development?.Stage=="Cancelled"&&_ledger.Snapshot().Mining!.Length>0,"cancel keeps mined world and in-transit stock");
                ValidatePlayerSave(CapturePlayer());QueuePlayerAction("retry");_developmentTestStep=18;break;
            case 18:
                if(Production is not {Kind:"recipe",Stage:"Working",Paid:true} recipe||recipe.Work<1)break;
                if(DevelopmentPhase=="prepare-input"){QueuePlayerAction("pause");_developmentTestStep=80;break;}
                // Prove no sunlight or remaining generator budget cannot advance a paid batch.
                double work=recipe.Work;_remainingPower.Clear();TickProduction(recipe,.1);GroundRequire(recipe.Work==work,"no free processing power");
                TestBadSave(json=>
                {
                    var containers=json["Bootstrap"]!["Ledger"]!["Containers"]!.AsArray();
                    var batch=containers.Single(c=>c!["Id"]!.GetValue<string>()==recipe.Destination)!["Items"]!;
                    var mine=containers.Single(c=>c!["Id"]!.GetValue<string>()=="mine-iron")!["Items"]!;
                    batch["iron_ore"]=batch["iron_ore"]!.GetValue<int>()-1;
                    mine["iron_ore"]=(mine["iron_ore"]?.GetValue<int>()??0)+1;
                });
                ValidatePlayerSave(CapturePlayer());_developmentTestStep=19;break;
            case 19:
                if(!_productionTasks.Any(t=>t.Kind=="recipe"&&t.Stage=="Completed"))break;
                ValidatePlayerSave(CapturePlayer());
                if(DevelopmentPhase=="prepare-output"){QueuePlayerAction("pause");_developmentTestStep=80;break;}
                _developmentTestStep=20;break;
            case 20:
                if(!DevelopmentComplete)break;
                GroundRequire(_baseFacilities.Count(f=>f.Built&&f.Type=="storage")==1,"gap target built via actual production");
                GroundRequire(_ledger.Snapshot().Recipes!.Any(r=>r.Recipe=="parts"),"ore to parts recipe chain");
                ValidatePlayerSave(CapturePlayer());D12Build("solar",new(13,0,-1));_developmentTestStep=21;break;
            case 21:
                if(!DevelopmentComplete)break;
                GroundRequire(_ledger.Snapshot().Mining!.Any(r=>r.Material=="copper_ore")&&_ledger.Snapshot().Recipes!.Any(r=>r.Recipe=="cable"),"second goal has distinct copper/cable consequences");
                GroundRequire(_baseFacilities.Count(f=>f.Built&&f.Type=="solar")==2,"second array real generation capacity");
                ValidatePlayerSave(CapturePlayer());
                _health[_d12ServiceRobot].Durability=40;_developmentTestStep=23;break;
            case 23:
                if(_services.ContainsKey(_d12ServiceRobot)||_health[_d12ServiceRobot].Durability<99)break;
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==2,"first paid repair uses protected stock");
                _health[_d12ServiceRobot].Durability=40;_developmentTestStep=24;break;
            case 24:
                if(_services.ContainsKey(_d12ServiceRobot)||_health[_d12ServiceRobot].Durability<99)break;
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==0,"second paid repair exhausts startup stock");
                QueuePlayerAction("restock");_developmentTestStep=25;break;
            case 25:
                if(!DevelopmentComplete||_development!.Type!="restock")break;
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==4,"new production really restocks repairs without overproduction");
                _health[_d12ServiceRobot].Durability=40;_developmentTestStep=26;break;
            case 26:
                if(_services.ContainsKey(_d12ServiceRobot)||_health[_d12ServiceRobot].Durability<99)break;
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==2,"third repair paid from newly produced material");
                ValidatePlayerSave(CapturePlayer());D12Build("charger",new(18,0,9));_developmentTestStep=27;break;
            case 27:
                if(!DevelopmentComplete||_development!.Type!="charger")break;
                QueuePlayerAction("connect");_developmentTestStep=28;break;
            case 28:
                if(_buildJob?.Stage!="Completed"||_buildJob.Type!="connection")break;
                var secondCharger=_baseFacilities.Single(f=>f.Type=="charger"&&f.Position[0]==18);
                GroundRequire(_baseFacilities.Single(f=>f.Id==secondCharger.Source).Position[0]==13,"second array actually powers new support layout");
                _d12ServiceRobot=_buildJob.Builder;_health[_d12ServiceRobot].Energy=8;_developmentTestStep=29;break;
            case 29:
                if(!_services.TryGetValue(_d12ServiceRobot,out var service)||service.Kind!="charge")break;
                GroundRequire(_baseFacilities.Single(f=>f.Id==service.Facility).Position[0]==18,"expanded charger selected via real route");_developmentTestStep=30;break;
            case 30:
                if(_services.ContainsKey(_d12ServiceRobot)||_health[_d12ServiceRobot].Energy<99)break;
                ValidatePlayerSave(CapturePlayer());QueuePlayerAction("pause");_developmentTestStep=22;break;
            case 22:
                if(!_userPaused)break;TestBadSave(j=>j["Bootstrap"]!["Development"]!["Goal"]!["Stage"]="Supplying");SavePlayer();GD.Print("D12_TEST PASS goals=storage,solar mines="+_ledger.Snapshot().Mining!.Length+" recipes="+_ledger.Snapshot().Recipes!.Length);GetTree().Quit();break;
            case 80:
                if(!_userPaused)break;ValidatePlayerSave(CapturePlayer());SavePlayer();GD.Print("D12_TEST PREPARED phase="+DevelopmentPhase);GetTree().Quit();break;
            case 90:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused,"cross process preserves pause");ValidatePlayerSave(CapturePlayer());
                _developmentTestTime=_playerTime;QueuePlayerAction("load");_developmentTestStep=92;break;
            case 92:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_playerTime==_developmentTestTime,"repeat load does not advance or settle");
                ValidatePlayerSave(CapturePlayer());QueuePlayerAction("pause");_developmentTestStep=93;break;
            case 93:
                if(_userPaused||!DevelopmentComplete)break;ValidatePlayerSave(CapturePlayer());GD.Print("D12_TEST RESTORED phase="+DevelopmentPhase);GetTree().Quit();break;
            case 200:
                if(!DevelopmentComplete)break;
                GroundRequire(!PreviewBuild("storage",new(9,0,9)).Legal,"processor first does not release remaining support reserves");
                ValidatePlayerSave(CapturePlayer());GD.Print("D12_TEST STARTUP_GUARD_PASS");GetTree().Quit();break;
            case 95:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_bootstrapConfig.Version=="d11-bootstrap-1"&&_mines.All(m=>m.Remaining==96),"legacy config safely adopted, new mines finite");
                ValidatePlayerSave(CapturePlayer());SavePlayer();GD.Print("D12_TEST LEGACY_MIGRATED");GetTree().Quit();break;
        }
    }
}
