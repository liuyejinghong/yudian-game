#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Yudian.Terrain;
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
    private int _bootstrapZeroFrame;
    private Vector3 _bootstrapZeroPower, _bootstrapZeroDurability;
    private double _bootstrapTestSavedTime;
    private bool _bootstrapLevelLimitChecked, _bootstrapReturnCancel;
    private Node3D? _bootstrapTestBlocker;
    private bool _bootstrapBlockedSeen;
    private float _bootstrapMoveStart;
    private double _bootstrapMoveEnergy, _bootstrapMoveDurability, _bootstrapLevelWork;
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
    private void TestDamagedFileLoad()
    {
        byte[] original=File.ReadAllBytes(PlayerSavePath);var ledger=_ledger;var terrain=_liveTerrain!.Current;double time=_playerTime;
        try
        {
            var json=JsonNode.Parse(original)!.AsObject();json["Bootstrap"]!["Facilities"]![1]!["Built"]=true;
            File.WriteAllText(PlayerSavePath,json.ToJsonString());bool rejected=false;
            try{LoadPlayer();}catch(InvalidDataException){rejected=true;}
            GroundRequire(rejected&&_loadPending==null&&ReferenceEquals(_ledger,ledger)&&ReferenceEquals(_liveTerrain.Current,terrain)&&_playerTime==time,"damaged disk save refuses swap and preserves current world");
        }
        finally{File.WriteAllBytes(PlayerSavePath,original);}
    }
    private void TestServiceBlocker(GroundPatrol actor)
    {
        _bootstrapTestBlocker=new Node3D{Position=actor.GlobalPosition};AddChild(_bootstrapTestBlocker);
        foreach(var offset in new[]{new Vector3(2,1.5f,0),new Vector3(-2,1.5f,0),new Vector3(0,1.5f,2),new Vector3(0,1.5f,-2)})
        {
            var wall=new StaticBody3D{Position=offset,CollisionLayer=2,CollisionMask=0};_bootstrapTestBlocker.AddChild(wall);
            wall.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=offset.X!=0?new(.5f,3,5):new(5,3,.5f)}});
        }
    }
    private bool TestCompleted=>_buildJob?.Stage=="Completed";
    private void BootstrapTestStep()
    {
        if(!_groundReady||!ReadPlayerState().Ready||!_groundRobots.All(a=>a.IsOnFloor()))return;
        if(_playerTime>2400)throw new InvalidOperationException($"bootstrap timeout step={_bootstrapTestStep} stage={_buildJob?.Stage} reason={_buildJob?.Reason} health="+string.Join(";",_health.Select(x=>$"{x.Key}:{x.Value.Energy:0.0}/{x.Value.Durability:0.0}:{x.Value.Reason}")));
        if(_groundFrame%500==0)GD.Print($"BOOTSTRAP_TEST_PROGRESS step={_bootstrapTestStep} stage={_buildJob?.Stage} time={_playerTime:0.0} cargo={(_buildJob==null?0:_ledger.Load(CargoContainer(_buildJob.Hauler)))} reason={_buildJob?.Reason}");
        if(_bootstrapTestStep>=23&&_levelJob?.Stage==LevelStage.Failed)throw new InvalidOperationException("level return failed: "+_levelJob.Message);
        if(_buildJob?.Stage=="Blocked")throw new InvalidOperationException("bootstrap blocked: "+_buildJob.Reason);
        switch(_bootstrapTestStep)
        {
            case 0:
                Engine.TimeScale=8;_bootstrapTestStart=_playerTime;
                GroundRequire(_baseFacilities.Count==1&&_baseFacilities[0].Type=="lander"&&_groundRobots.Count==12,"formal new game only lander/finite kits");
                GroundRequire(!PreviewBuild("solar",new(7,0,-22)).Legal,"facility overlap rejects without mutation");
                if(BootstrapPhase.StartsWith("resume")){QueuePlayerAction("load");_bootstrapTestStep=BootstrapPhase switch{"resume-service"=>101,"resume-active"=>103,"resume-charge"=>105,_=>100};break;}
                TestBuild("solar",new(-3,0,-14));_bootstrapTestStep=1;break;
            case 1:
                if(_buildJob?.Stage=="Levelling"&&!_bootstrapLevelLimitChecked){TestBadSave(j=>j["Bootstrap"]!["Build"]!["Work"]=4);_bootstrapLevelLimitChecked=true;}
                if(_buildJob?.Stage!="Delivering"||_ledger.Load(CargoContainer(_buildJob.Hauler))==0)break;
                if(BootstrapPhase=="prepare-active"){QueuePlayerAction("pause");_bootstrapTestStep=102;break;}
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
                SavePlayer();GroundRequire(File.Exists(PlayerSavePath),"cargo save created");TestDamagedFileLoad();
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
                { _bootstrapTestRepairSeen=true;if(repair.Paid&&!_bootstrapTestCycleReady){TestBadSave(json=>
                    {
                        string site=json["Bootstrap"]!["Build"]!["Facility"]!.GetValue<string>();
                        var containers=json["Bootstrap"]!["Ledger"]!["Containers"]!.AsArray();
                        var stock=containers.Single(c=>c!["Id"]!.GetValue<string>()==site)!["Items"]!;
                        var bank=containers.Single(c=>c!["Id"]!.GetValue<string>()=="lander")!["Items"]!;
                        int amount=stock["iron"]!.GetValue<int>();stock["iron"]=0;bank["iron"]=bank["iron"]!.GetValue<int>()+amount;
                    });
                    TestBadSave(json=>json["Bootstrap"]!["Build"]!["Station"]=new JsonArray(0,0,0));
                    SavePlayer();ValidatePlayerSave(CapturePlayer());_bootstrapTestCycleReady=true;
                    if(BootstrapPhase=="prepare-service"){GD.Print("BOOTSTRAP_TEST PREPARED_SERVICE paid="+repair.Paid+" progress="+repair.Progress);GetTree().Quit();return;}} }
                if(!_bootstrapTestRepairSeen||!_services.TryGetValue(_bootstrapTestBuilder,out var repaired)||!repaired.Returning||_health[_bootstrapTestBuilder].Durability<90)break;
                GroundRequire(_health[_bootstrapTestBuilder].Energy<=25,"repair does not recharge");_health[_bootstrapTestBuilder].Energy=8;_bootstrapTestStep=15;break;
            case 15:
                if(_services.TryGetValue(_bootstrapTestBuilder,out var charge)&&charge.Kind=="charge")
                {
                    _bootstrapTestChargeSeen=true;
                    if(BootstrapPhase=="prepare-charge"&&Arrived(Actor(charge.Robot),LoadVector(charge.Station))&&_health[charge.Robot].Energy>8&&!charge.Returning){QueuePlayerAction("pause");_bootstrapTestStep=104;break;}
                }
                if(!_bootstrapTestChargeSeen||_services.ContainsKey(_bootstrapTestBuilder)||_health[_bootstrapTestBuilder].Energy<90||!TestCompleted)break;
                GroundRequire(_health[_bootstrapTestBuilder].Durability<100,"charge does not erase wear");_bootstrapTestCycles++;
                if(_bootstrapTestCycles==1){TestBuild("storage",new(-16,0,2));_bootstrapTestStep=13;break;}
                _bootstrapTestStep=16;break;
            case 16:
                GroundRequire(_ledger.Totals().All(x=>_bootstrapConfig.Initial[x.Key]==x.Value),"final mass conservation including spent");
                GroundRequire(_ledger.Snapshot().Reservations.Length==0,"no orphaned build reservations");
                var noPower=Actor("Robot_Wangshan_1");var noDurability=Actor("Robot_Wangshan_2");
                _bootstrapZeroPower=noPower.GlobalPosition;_bootstrapZeroDurability=noDurability.GlobalPosition;
                _health[noPower.Name].Energy=0;_health[noDurability.Name].Durability=0;
                GroundRequire(OrderBase(noPower,noPower.GlobalPosition+new Vector3(0,0,3))&&OrderBase(noDurability,noDurability.GlobalPosition+new Vector3(0,0,3)),"zero-state test has actual pending routes");
                _bootstrapZeroFrame=_groundFrame;_bootstrapTestStep=17;break;
            case 17:
                if(_groundFrame-_bootstrapZeroFrame<10)break;
                GroundRequire(XzDistance(Actor("Robot_Wangshan_1").GlobalPosition,_bootstrapZeroPower)<.0001&&XzDistance(Actor("Robot_Wangshan_2").GlobalPosition,_bootstrapZeroDurability)<.0001,"zero energy/durability stop pending autonomous routes");
                if(!_userPaused){QueuePlayerAction("pause");break;}var before=CapturePlayer();SavePlayer();ValidatePlayerSave(before);QueuePlayerAction("load");_bootstrapTestStep=18;break;
            case 18:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_baseFacilities.Count(f=>f.Built)==7&&_bootstrapTestCycles==2,"final saved base restores");
                var moving=Actor("Robot_Wangshan_3");_health[moving.Name].Energy=100;_health[moving.Name].Durability=100;
                _bootstrapMoveStart=moving.TravelledM;GroundRequire(OrderBase(moving,moving.GlobalPosition+new Vector3(0,0,3)),"save accounting uses actual movement");
                QueuePlayerAction("pause");_bootstrapTestStep=19;break;
            case 19:
                if(Actor("Robot_Wangshan_3").TravelledM-_bootstrapMoveStart<.3)break;
                QueuePlayerAction("pause");_bootstrapTestStep=20;break;
            case 20:
                if(!_userPaused)break;
                var mover=Actor("Robot_Wangshan_3");var mh=_health[mover.Name];double moved=mover.TravelledM-_bootstrapMoveStart;
                GroundRequire(Math.Abs(mh.Energy-(100-moved*_bootstrapConfig.MoveEnergyPerM))<.0001&&Math.Abs(mh.Durability-(100-moved*_bootstrapConfig.MoveWearPerM))<.0001,"pause settles final actual frame before commands");
                _bootstrapMoveEnergy=mh.Energy;_bootstrapMoveDurability=mh.Durability;SavePlayer();SavePlayer();QueuePlayerAction("load");_bootstrapTestStep=21;break;
            case 21:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_health["Robot_Wangshan_3"].Energy==_bootstrapMoveEnergy&&_health["Robot_Wangshan_3"].Durability==_bootstrapMoveDurability,"repeated save/load cannot refund last movement");
                QueuePlayerAction("pause");_bootstrapTestStep=22;break;
            case 22:
                if(_userPaused)break;
                var site=new Vector3(14,0,-8);var levelPreview=PreviewLevel(site);
                GroundRequire(levelPreview.Legal,"level service fixture legal: "+levelPreview.Reason);QueueLevel(site,levelPreview.Version);_bootstrapTestStep=23;break;
            case 23:
                if(_levelJob?.Stage!=LevelStage.Working||_levelJob.Work.ElapsedSeconds<.2)break;
                _bootstrapTestBuilder=_levelJob.Worker!.Name;
                TestBadSave(j=>j["Bootstrap"]!["Destinations"]!.AsObject().Remove(_bootstrapTestBuilder));
                _bootstrapLevelWork=_levelJob.Work.ElapsedSeconds;_health[_bootstrapTestBuilder].Energy=8;_bootstrapTestStep=24;break;
            case 24:
                if(!_bootstrapBlockedSeen&&_services.TryGetValue(_bootstrapTestBuilder,out var blockedCharge)&&blockedCharge.Kind=="charge"&&!blockedCharge.Returning)
                {
                    if(_bootstrapTestBlocker==null)TestServiceBlocker(Actor(blockedCharge.Robot));
                    if(!blockedCharge.Blocked)break;
                    GroundRequire(!Actor(blockedCharge.Robot).HasOrder&&!_stations.Any(x=>x.Value==blockedCharge.Robot)&&_levelJob!.Work.ElapsedSeconds==_bootstrapLevelWork,"actual physical blocker times out with progress retained and station released");
                    SavePlayer();ValidatePlayerSave(CapturePlayer());RemoveChild(_bootstrapTestBlocker!);_bootstrapTestBlocker!.QueueFree();_bootstrapBlockedSeen=true;
                    QueuePlayerAction("load");_bootstrapTestStep=106;break;
                }
                if(!_services.TryGetValue(_bootstrapTestBuilder,out var returning)||!returning.Returning)break;
                if(_bootstrapReturnCancel){QueuePlayerAction("cancel");_bootstrapTestStep=28;break;}
                GroundRequire(_levelJob!.Work.ElapsedSeconds==_bootstrapLevelWork&&_levelJob.Stage==LevelStage.Working,"level progress preserved through service and return journey");
                QueuePlayerAction("pause");_bootstrapTestStep=25;break;
            case 25:
                if(!_userPaused)break;
                TestBadSave(j=>j["Bootstrap"]!["Services"]![0]!["ReturnTo"]=new JsonArray(30,0,30));
                SavePlayer();ValidatePlayerSave(CapturePlayer());QueuePlayerAction("load");_bootstrapTestStep=26;break;
            case 26:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_services[_bootstrapTestBuilder].Returning&&_levelJob!.Work.ElapsedSeconds==_bootstrapLevelWork,"service return journey survives load without resetting work");
                QueuePlayerAction("pause");_bootstrapTestStep=27;break;
            case 27:
                if(_levelJob?.Stage!=LevelStage.Completed)break;
                _bootstrapReturnCancel=true;
                var cancelSite=new Vector3(14,0,-4);var cancelPreview=PreviewLevel(cancelSite);
                GroundRequire(cancelPreview.Legal,"cancel return fixture legal: "+cancelPreview.Reason);QueueLevel(cancelSite,cancelPreview.Version);_bootstrapTestStep=23;break;
            case 28:
                if(_levelJob?.Stage!=LevelStage.Cancelled)break;
                GroundRequire(!_services.ContainsKey(_bootstrapTestBuilder)&&!Actor(_bootstrapTestBuilder).HasOrder&&!_groundFault,"cancel during service return stops only original journey");
                SavePlayer();ValidatePlayerSave(CapturePlayer());
                GD.Print($"BOOTSTRAP_TEST PASS cycles={_bootstrapTestCycles} built={_baseFacilities.Count(f=>f.Built)} levelReturn=true time={_playerTime:0.0} elapsed={_playerTime-_bootstrapTestStart:0.0} source={AssemblyHash()}");GetTree().Quit();break;
            case 106:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_services[_bootstrapTestBuilder].Blocked&&!Actor(_bootstrapTestBuilder).HasOrder&&_levelJob!.Work.ElapsedSeconds==_bootstrapLevelWork,"blocked service load cannot autonomously restore old level order");
                QueuePlayerAction("retry");_bootstrapTestStep=24;break;
            case 102:
                if(!_userPaused)break;SavePlayer();ValidatePlayerSave(CapturePlayer());GD.Print("BOOTSTRAP_TEST PREPARED_ACTIVE");GetTree().Quit();break;
            case 103:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_buildJob!.Stage=="Delivering"&&_ledger.Load(CargoContainer(_buildJob.Hauler))>0,"active cargo restores exact delivery phase");
                GD.Print("BOOTSTRAP_TEST CROSS_PROCESS_ACTIVE_RESTORED");QueuePlayerAction("pause");_bootstrapTestStep=1;break;
            case 104:
                if(!_userPaused)break;SavePlayer();ValidatePlayerSave(CapturePlayer());GD.Print("BOOTSTRAP_TEST PREPARED_CHARGE");GetTree().Quit();break;
            case 105:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                _bootstrapTestBuilder=_buildJob!.Builder;
                GroundRequire(_userPaused&&_services[_bootstrapTestBuilder].Kind=="charge"&&!_services[_bootstrapTestBuilder].Returning&&_health[_bootstrapTestBuilder].Energy>8&&_health[_bootstrapTestBuilder].Energy<100,"charging saves actual partial energy without refill");
                GD.Print("BOOTSTRAP_TEST CROSS_PROCESS_CHARGE_RESTORED");_bootstrapTestChargeSeen=true;QueuePlayerAction("pause");_bootstrapTestStep=15;break;
            case 101:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                _bootstrapTestBuilder=_buildJob!.Builder;
                GroundRequire(_services[_bootstrapTestBuilder].Paid&&_services[_bootstrapTestBuilder].Kind=="repair"&&_services[_bootstrapTestBuilder].Progress>0,"cross-process paid repair restores exact progress");
                GD.Print("BOOTSTRAP_TEST CROSS_PROCESS_SERVICE_RESTORED");_bootstrapTestRepairSeen=true;_bootstrapTestCycleReady=true;_bootstrapTestStep=14;break;
            case 100:
                if(_loadPending!=null||!_playerNotice.StartsWith("读取完成"))break;
                GroundRequire(_userPaused&&_buildJob?.Stage=="Cancelled"&&_ledger.Load(CargoContainer(_buildJob.Hauler))>0,"cross-process cargo restore");
                GD.Print("BOOTSTRAP_TEST CROSS_PROCESS_RESTORED");QueuePlayerAction("pause");_bootstrapTestStep=5;break;
        }
    }
}
