#nullable enable
using System.Collections.Generic;
using Godot;
using Yudian.Terrain;
namespace Yudian;
public partial class Main
{
    private readonly Dictionary<GroundPatrol,(Node3D Visual,Vector3 Previous,double Time,string State)> _haulerVisuals=new();
    private void AttachHaulerVisual(GroundPatrol actor)
    {
        var visual=GD.Load<PackedScene>("res://assets/units/tuoyun-r1/preview.tscn").Instantiate<Node3D>();
        actor.AddChild(visual);_haulerVisuals.Add(actor,(visual,actor.GlobalPosition,0,"idle"));
    }
    private string BaseVisualState(GroundPatrol actor,bool moving)
    {
        string id=actor.Name.ToString();
        if(!Operational(actor))return "disabled";
        if(_services.TryGetValue(id,out var service)&&Arrived(actor,LoadVector(service.Station)))return service.Kind=="repair"?"maintenance":"charge";
        if(DevelopmentEnabled&&Production is {Kind:"mine",Stage:"Working"} task&&task.Robot==id&&Arrived(actor,LoadVector(task.Station!))&&!Servicing(actor))return "work";
        if(_buildJob is {} build&&build.Builder==id&&build.Stage is "Building" or "Levelling"&&Arrived(actor,LoadVector(build.Station))&&!Servicing(actor))return "work";
        if(_levelJob is {} level&&level.Worker==actor&&level.Stage==LevelStage.Working&&WorkerAtStation(level)&&!Servicing(actor))return "work";
        return moving?"move":"idle";
    }
    private void ApplyHauler(GroundPatrol actor,string state,double time)
    {
        var h=_health[actor.Name.ToString()];string reason=state=="disabled"?h.Energy<=0&&h.Durability<=0?"both":h.Energy<=0?"no_power":"mechanical":"none";
        var preset=new Godot.Collections.Dictionary{{"state",state},{"phase","completed"},{"phase_t",0.0},{"cargo",_ledger.Load(CargoContainer(actor.Name.ToString()))>0?"loaded":"empty"},{"reason",reason},{"time_s",time}};
        string error=_haulerVisuals[actor].Visual.Call("apply_preview",preset).AsString();
        if(DevelopmentEnabled&&_haulerVisuals[actor].Visual.GetNodeOrNull<Node3D>("Model/tuoyun-r1/Model/CargoBox") is {} box)box.Visible=false;
        if(error.Length>0)throw new System.InvalidOperationException("驮运状态适配失败："+error);
    }
    private void TickHaulerVisuals(double delta,bool frozen)
    {
        foreach(var actor in _groundRobots)
        {
            if(!_haulerVisuals.TryGetValue(actor,out var item))continue;
            var moved=actor.GlobalPosition-item.Previous;moved.Y=0;string state=BaseVisualState(actor,moved.Length()>.0001f);
            double time=state==item.State?item.Time:0;if(!frozen)time+=state=="move"?moved.Length()/2.2:delta;
            if(!frozen&&moved.Length()>.0001f)actor.Rotation=new(0,Mathf.Atan2(-moved.X,-moved.Z),0);
            ApplyHauler(actor,state,time);_haulerVisuals[actor]=(item.Visual,actor.GlobalPosition,time,state);
        }
    }
    private void RestoreHaulerVisuals()
    {
        foreach(var actor in _groundRobots)
        {
            if(!_haulerVisuals.TryGetValue(actor,out var item))continue;
            string state=BaseVisualState(actor,actor.HasOrder&&!actor.OrderReached);
            _haulerVisuals[actor]=(item.Visual,actor.GlobalPosition,0,state);ApplyHauler(actor,state,0);
        }
    }
}
