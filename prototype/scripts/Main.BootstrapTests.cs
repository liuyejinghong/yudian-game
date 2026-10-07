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
    private int _bootstrapTestStep;
    private double _bootstrapTestStart;
    private bool _bootstrapTestRepairSeen;
    private bool _bootstrapTestChargeSeen;
    private bool _bootstrapTestCycleReady;
    private int _bootstrapTestCycles;
    private string _bootstrapTestBuilder="";
    private double _bootstrapTestSavedTime;
    private string BootstrapPhase=>System.Environment.GetEnvironmentVariable("YUDIAN_BOOTSTRAP_TEST_PHASE")??"full";
    private void TestBuild(string type,Vector3 position)
    {
        var preview=PreviewBuild(type,position);GroundRequire(preview.Legal,"bootstrap preview "+type+": "+preview.Reason);
        QueueBuild(type,position,0,preview.Version);
    }
    private void TestBadSave(Action<JsonObject> mutate)
    {
        var json=JsonNode.Parse(JsonSerializer.Serialize(CapturePlayer(),SaveOptions))!.AsObject();mutate(json);
        bool rejected=false;
        try{ValidatePlayerSave(JsonSerializer.Deserialize<PlayerSave>(json.ToJsonString(),SaveOptions)!);}
        catch(InvalidDataException){rejected=true;}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}
        GroundRequire(rejected,"damaged bootstrap facts rejected before swap");
    }
    private bool TestCompleted=>_buildJob?.Stage=="Completed";
    private void BootstrapTestStep()
    {
        if(!_groundReady||!ReadPlayerState().Ready||!_groundRobots.All(a=>a.IsOnFloor()))return;
        if(_playerTime>2400)throw new InvalidOperationException($"bootstrap timeout step={_bootstrapTestStep} stage={_buildJob?.Stage} reason={_buildJob?.Reason} health="+string.Join(";",_health.Select(x=>$"{x.Key}:{x.Value.Energy:0.0}/{x.Value.Durability:0.0}:{x.Value.Reason}")));
        if(_groundFrame%500==0)GD.Print($"BOOTSTRAP_TEST_PROGRESS step={_bootstrapTestStep} stage={_buildJob?.Stage} time={_playerTime:0.0} cargo={(_buildJob==null?0:_ledger.Load(CargoContainer(_buildJob.Hauler)))} reason={_buildJob?.Reason}");
        if(_buildJob?.Stage=="Blocked")throw new InvalidOperationException("bootstrap blocked: "+_buildJob.Reason);
        switch(_bootstrapTestStep)
        {
            case 0:
                Engine.TimeScale=8;_bootstrapTestStart=_playerTime;
                GroundRequire(_baseFacilities.Count==1&&_baseFacilities[0].Type=="lander"&&_groundRobots.Count==12,"formal new game only lander/finite kits");
                GroundRequire(!PreviewBuild("solar",new(7,0,-22)).Legal,"facility overlap rejects without mutation");
                if(BootstrapPhase=="resume"){QueuePlayerAction("load");_bootstrapTestStep=100;break;}
                TestBuild("solar",new(-3,0,-14));_bootstrapTestStep=1;break;
            case 1:
                if(_buildJob?.Stage!="Delivering"||_ledger.Load(CargoContainer(_buildJob.Hauler))==0)break;
                QueuePlayerAction("cancel");_bootstrapTestStep=2;break;
            case 2:
                if(_buildJob?.Stage!="Cancelled")break;
                GroundRequire(_ledger.Load(CargoContainer(_buildJob.Hauler))>0,"cancel in transit retains real cargo");
                QueuePlayerAction("pause");_bootstrapTestStep=3;break;
            case 3:
                if(!_userPaused)break;
                _bootstrapTestSavedTime=_playerTime;
                TestBadSave(j=>j["Bootstrap"]!["Facilities"]![1]!["Built"]=true);
                TestBadSave(j=>j["Bootstrap"]!["Ledger"]!["Containers"]!.AsArray().RemoveAt(1));
                TestBadSave(j=>j["Bootstrap"]!["Ledger"]!["Containers"]![0]!["Id"]="unused");
                TestBadSave(j=>j["Bootstrap"]!["Build"]!["Trip"]=0);
                TestBadSave(j=>j["Bootstrap"]!["Stations"]!["lander"]="Robot_Wangshan_1");
                SavePlayer();GroundRequire(File.Exists(PlayerSavePath),"cargo save created");
                if(BootstrapPhase=="prepare"){GD.Print("BOOTSTRAP_TEST PREPARED cargo="+_ledger.Load(CargoContainer(_buildJob!.Hauler)));GetTree().Quit();return;}
                QueuePlayerAction("load");_bootstrapTestStep=4;break;
            case 4:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_playerTime==_bootstrapTestSavedTime,"load preserves paused time");
                GroundRequire(_ledger.Load(CargoContainer(_buildJob!.Hauler))>0,"load preserves canceled cargo");
                QueuePlayerAction("pause");_bootstrapTestStep=5;break;
            case 5:
                if(_userPaused)break;QueuePlayerAction("retry");_bootstrapTestStep=6;break;
            case 6:
                if(!TestCompleted)break;GroundRequire(_baseFacilities.Count(f=>f.Built&&f.Type=="solar")==1,"solar registered once");
                TestBuild("charger",new(-3,0,-3));_bootstrapTestStep=7;break;
            case 7:if(!TestCompleted)break;QueuePlayerAction("connect");_bootstrapTestStep=8;break;
            case 8:
                if(!TestCompleted||_buildJob!.Type!="connection")break;
                GroundRequire(_baseFacilities.Single(f=>f.Type=="charger").Source!=null,"paid physical cable connection");TestBuild("repair",new(8,0,-14));_bootstrapTestStep=9;break;
            case 9:if(!TestCompleted)break;QueuePlayerAction("connect");_bootstrapTestStep=10;break;
            case 10:
                if(!TestCompleted||_buildJob!.Type!="connection")break;
                GroundRequire(_ledger.Count(_baseFacilities.Single(f=>f.Type=="repair").Id,"parts")==4,"repair supplies physically delivered");
                TestBuild("processor",new(-14,0,-14));_bootstrapTestStep=11;break;
            case 11:if(!TestCompleted)break;QueuePlayerAction("connect");_bootstrapTestStep=12;break;
            case 12:
                if(!TestCompleted||_buildJob!.Type!="connection")break;
                GroundRequire(_baseFacilities.Count(f=>f.Built)==5,"first support set and processor constructed from finite kit");
                SavePlayer();ValidatePlayerSave(CapturePlayer());TestBuild("storage",new(9,0,9));_bootstrapTestStep=13;break;
            case 13:
                if(_buildJob?.Stage!="Building"||_buildJob.Work<1||!Arrived(Actor(_buildJob.Builder),LoadVector(_buildJob.Station)))break;
                _bootstrapTestBuilder=_buildJob.Builder;var health=_health[_bootstrapTestBuilder];health.Energy=25;health.Durability=40;
                _bootstrapTestRepairSeen=false;_bootstrapTestChargeSeen=false;_bootstrapTestCycleReady=false;_bootstrapTestStep=14;break;
            case 14:
                if(_services.TryGetValue(_bootstrapTestBuilder,out var repair)&&repair.Kind=="repair")
                { _bootstrapTestRepairSeen=true;if(repair.Paid&&!_bootstrapTestCycleReady){SavePlayer();ValidatePlayerSave(CapturePlayer());_bootstrapTestCycleReady=true;} }
                if(!_bootstrapTestRepairSeen||_services.ContainsKey(_bootstrapTestBuilder)||_health[_bootstrapTestBuilder].Durability<90)break;
                GroundRequire(_health[_bootstrapTestBuilder].Energy<=25,"repair does not recharge");_health[_bootstrapTestBuilder].Energy=8;_bootstrapTestStep=15;break;
            case 15:
                if(_services.TryGetValue(_bootstrapTestBuilder,out var charge)&&charge.Kind=="charge")_bootstrapTestChargeSeen=true;
                if(!_bootstrapTestChargeSeen||_services.ContainsKey(_bootstrapTestBuilder)||_health[_bootstrapTestBuilder].Energy<90||!TestCompleted)break;
                GroundRequire(_health[_bootstrapTestBuilder].Durability<100,"charge does not erase wear");_bootstrapTestCycles++;
                if(_bootstrapTestCycles==1){TestBuild("storage",new(-16,0,2));_bootstrapTestStep=13;break;}
                _bootstrapTestStep=16;break;
            case 16:
                GroundRequire(_ledger.Totals().All(x=>_bootstrapConfig.Initial[x.Key]==x.Value),"final mass conservation including spent");
                GroundRequire(_ledger.Snapshot().Reservations.Length==0,"no orphaned build reservations");
                _bootstrapTestSavedTime=_playerTime;QueuePlayerAction("pause");_bootstrapTestStep=17;break;
            case 17:
                if(!_userPaused)break;var before=CapturePlayer();SavePlayer();ValidatePlayerSave(before);QueuePlayerAction("load");_bootstrapTestStep=18;break;
            case 18:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_baseFacilities.Count(f=>f.Built)==7&&_bootstrapTestCycles==2,"final saved base restores");
                GD.Print($"BOOTSTRAP_TEST PASS cycles={_bootstrapTestCycles} built={_baseFacilities.Count(f=>f.Built)} time={_playerTime:0.0} elapsed={_playerTime-_bootstrapTestStart:0.0} source={AssemblyHash()}");GetTree().Quit();break;
            case 100:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_buildJob?.Stage=="Cancelled"&&_ledger.Load(CargoContainer(_buildJob.Hauler))>0,"cross-process cargo restore");
                GD.Print("BOOTSTRAP_TEST CROSS_PROCESS_RESTORED");QueuePlayerAction("pause");_bootstrapTestStep=5;break;
        }
    }
}
