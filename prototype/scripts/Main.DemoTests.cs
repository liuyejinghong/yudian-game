#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Yudian;
public partial class Main
{
    private int _demoStep, _demoFrame;
    private double _demoTime;
    private byte[] _demoBytes=[];
    private string _demoPath="";
    private void DemoCheck(bool passed,string name)
    { GroundRequire(passed,name); GD.Print("DEMO_CHECK PASS "+name); }
    private void DemoTestStep()
    {
        if(OperatingSystem.IsWindows())throw new PlatformNotSupportedException("demo permission check requires Unix");
        GroundRequire(_groundFrame<900,"demo test timeout");
        if(!_groundReady||!_groundRobots.All(a=>a.IsOnFloor())||_groundFrame<90)return;
        string phase=System.Environment.GetEnvironmentVariable("YUDIAN_DEMO_TEST_PHASE")??"prepare";
        if(_demoStep==21)
        {
            if(_groundFrame-_demoFrame<3)return;
            DemoCheck(_resourceViews.All(GodotObject.IsInstanceValid),"resource views remain valid after deferred frees");
            GD.Print("DEMO_TEST PASS phase="+phase);GetTree().Quit();return;
        }
        if(_demoStep==0)
        {
            DemoCheck(_entryOpen&&_playerTime==0&&_groundRobots.All(a=>a.IsPhysicsProcessing()),"entry freezes simulation but allows grounding");
            _demoPath=PlayerSavePath;_demoBytes=File.ReadAllBytes(_demoPath);
            if(phase=="prepare"){QueuePlayerAction("savequit");_demoStep=1;}
            else {if(phase=="rollback")_playerTestInjectLoadFault=true;QueuePlayerAction("load");_demoStep=20;}
            return;
        }
        if(_demoStep==20)
        {
            if(_loadPending!=null)return;
            if(phase=="rollback")
            {
                DemoCheck(_loadRecovered&&_entryOpen&&_playerTime==0,"failed physical load keeps entry and original world");
                DemoCheck(File.ReadAllBytes(_demoPath).SequenceEqual(_demoBytes),"failed load keeps original slot");
            }
            else
            {
                using var json=JsonDocument.Parse(_demoBytes);
                DemoCheck(!_entryOpen&&_userPaused&&_playerTime==json.RootElement.GetProperty("Time").GetDouble(),"continue leaves entry only after successful load and preserves pause");
                var before=JsonSerializer.Serialize(CaptureBootstrap(),SaveOptions);var view=ReadDevelopment();
                RebuildResourceViews();
                DemoCheck(_cargoGlyphs.Count==_ledger.Snapshot().Containers.Count(c=>c.Id.StartsWith("cargo:")&&c.Items.Any(x=>x.Value>0&&ResourceLoader.Exists("res://assets/resources/d12-r1/icons/"+x.Key+".svg"))),"glyphs and carriers follow actual nonempty supported cargo");
                DemoCheck(_resourceViews.Where(n=>n.GetParent()?.Name=="Socket_Cargo").Count()==_cargoGlyphs.Count,"carrier uses actual socket and empty cargo hides attachment");
                DemoCheck(_resourceViews.All(n=>!_facilityNodes.Values.Any(f=>f.IsAncestorOf(n))),"batch resources stay outside replaceable facility roots");
                RebuildBaseFacilities();RebuildResourceViews();
                DemoCheck(_resourceViews.All(GodotObject.IsInstanceValid),"resource views survive rebuilding facility roots");
                if(phase=="recipe")DemoCheck(view.InProcess.Contains("铁料4")&&!view.Need.Contains("铁料4"),"paid recipe output counted separately from new production gap");
                if(phase=="ore")DemoCheck(view.Transit.Contains("铁矿8"),"raw ore cargo remains visible in transit facts");
                if(phase=="output")DemoCheck(_ledger.Snapshot().Containers.Any(c=>c.Id.StartsWith("batch:")&&c.Items.TryGetValue("iron",out int quantity)&&quantity==4),"real output fixture exercises nonempty batch views");
                if(phase=="cargo")DemoCheck(view.Transit.Contains("结构件4")&&view.Need=="无需新增生产","actual cargo closes production gap without becoming warehouse stock");
                if(phase=="completed")DemoCheck(view.Stage=="已完成"&&view.Need=="无需新增生产"&&view.Directions!.All(d=>d.Cost==FormatMaterials(BuildCost(d.Id))),"completed goal and configured direction costs are truthful");
                DemoCheck(before==JsonSerializer.Serialize(CaptureBootstrap(),SaveOptions),"read model does not mutate authoritative facts");
            }
            _demoFrame=_groundFrame;_demoStep=21;return;
        }
        switch(_demoStep)
        {
            case 1:
                DemoCheck(_entryOpen&&_playerTime==0&&File.ReadAllBytes(_demoPath).SequenceEqual(_demoBytes),"entry savequit cannot overwrite old slot or quit");
                QueuePlayerAction("load");_demoStep=2;break;
            case 2:
                DemoCheck(_entryOpen&&_loadPending==null&&_playerNotice.StartsWith("读取失败"),"bad slot keeps entry with visible failure");
                QueuePlayerAction("newgame");QueuePlayerAction("newgame");_demoStep=3;break;
            case 3:
                DemoCheck(!_entryOpen&&File.ReadAllBytes(_demoPath).SequenceEqual(_demoBytes),"new game chooses existing initial world without replacing old slot");
                _demoFrame=_groundFrame;_demoStep=4;break;
            case 4:
                if(_groundFrame-_demoFrame<30)break;
                DemoCheck(_playerTime>0,"new game starts ordinary simulation");QueuePlayerAction("pause");_demoStep=5;break;
            case 5:
                DemoCheck(_userPaused&&SavePlayer(),"grounded new game can save");_demoTime=_playerTime;_demoBytes=File.ReadAllBytes(_demoPath);
                string directory=_demoPath+".readonly";Directory.CreateDirectory(directory);
                string denied=Path.Combine(directory,"valid.json");File.WriteAllBytes(denied,_demoBytes);
                File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserExecute);
                System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE",denied);QueuePlayerAction("savequit");_demoStep=6;break;
            case 6:
                string rejectedPath=PlayerSavePath;
                try
                {
                    DemoCheck(_userPaused&&_playerTime==_demoTime&&_playerNotice.StartsWith("保存失败")&&File.ReadAllBytes(rejectedPath).SequenceEqual(_demoBytes),"write refusal keeps world and old slot and does not quit");
                }
                finally
                {
                    File.SetUnixFileMode(Path.GetDirectoryName(rejectedPath)!,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
                    System.Environment.SetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE",_demoPath);
                }
                GD.Print("DEMO_TEST SAVEQUIT_REQUEST");QueuePlayerAction("savequit");_demoStep=7;break;
        }
    }
}
