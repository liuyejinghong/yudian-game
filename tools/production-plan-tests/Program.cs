using System;
using System.Collections.Generic;
using System.Linq;
using Yudian.Goals;
using Yudian.Resources;
static class Check {
 static int n;
 static void True(bool v,string why){if(!v)throw new Exception(why);n++;}
 static void Throws(Action a){bool c=false;try{a();}catch(ArgumentException){c=true;}True(c,"expected ArgumentException");}
 static RecipeDefinition R(string id,Dictionary<string,int> input,Dictionary<string,int> output)=>new(){Id=id,Input=input,Output=output,WorkSeconds=4,Power=1};
 static Dictionary<string,int> D()=>new();
 static Dictionary<string,int> D(string k1,int v1)=>new(){{k1,v1}};
 static Dictionary<string,int> D(string k1,int v1,string k2,int v2)=>new(){{k1,v1},{k2,v2}};
 static Dictionary<string,int> D(string k1,int v1,string k2,int v2,string k3,int v3)=>new(){{k1,v1},{k2,v2},{k3,v3}};
 static Dictionary<string,int> D(string k1,int v1,string k2,int v2,string k3,int v3,string k4,int v4)=>new(){{k1,v1},{k2,v2},{k3,v3},{k4,v4}};
 static void Feasible(ProductionPlanResult r,params ProductionStep[] want){
  True(r.Feasible,"expected feasible, got: "+r.Reason);
  True(r.Steps.Length==want.Length,$"step count {r.Steps.Length}!={want.Length}");
  for(var i=0;i<want.Length;i++)True(r.Steps[i].Kind==want[i].Kind&&r.Steps[i].Material==want[i].Material&&r.Steps[i].Quantity==want[i].Quantity,$"step[{i}] {r.Steps[i].Kind}/{r.Steps[i].Material}/{r.Steps[i].Quantity}!={want[i].Kind}/{want[i].Material}/{want[i].Quantity}");
  n++;
 }
 static void Infeasible(ProductionPlanResult r,string contains){True(!r.Feasible&&r.Steps!=null&&r.Steps.Length==0&&r.Reason.Contains(contains),$"expected infeasible({contains}), got {r.Feasible} '{r.Reason}' steps={r.Steps?.Length}");n++;}
 static void Main(){
  True(MaterialLedger.Limit==100_000&&MaterialLedger.Materials.Contains("kit")&&typeof(BootstrapConfig).Name=="BootstrapConfig","linked Resources/Core sources");
  var recipes=new[]{R("iron",D("iron_ore",2),D("iron",1)),R("copper",D("copper_ore",2),D("copper",1)),R("parts",D("iron",2),D("parts",1)),R("cable",D("copper",1),D("cable",2))};
  True(recipes.SelectMany(r=>r.Input.Keys.Concat(r.Output.Keys)).All(m=>MaterialLedger.Materials.Contains(m)),"frozen recipes inside ledger materials");
  var ore96=new Dictionary<string,int>{{"iron_ore",96},{"copper_ore",96}};
  // Net gap only: covered demand and empty demand produce no steps.
  Feasible(ProductionPlan.Create(D(),D(),recipes,ore96));
  Feasible(ProductionPlan.Create(D("parts",4),D("parts",4),recipes,ore96));
  // Partially covered demand still plans the remainder: parts 4 over stock 2 -> 2 batches -> 4 iron -> 8 ore.
  Feasible(ProductionPlan.Create(D("parts",4),D("parts",2),recipes,ore96),
   new ProductionStep("mine","iron_ore",8),new ProductionStep("recipe","iron",4),new ProductionStep("recipe","parts",2));
  // Ceiling: cable 3 needs 2 batches -> 4 cable, copper stock 2 covers input exactly, surplus cable uncounted.
  Feasible(ProductionPlan.Create(D("cable",3),D("copper",2),recipes,ore96),new ProductionStep("recipe","cable",2));
  // Sibling demands share one pool: iron demand 3 + parts 2 over stock iron 1 -> 6 iron batches, 12 ore.
  Feasible(ProductionPlan.Create(D("iron",3,"parts",2),D("iron",1),recipes,ore96),
   new ProductionStep("mine","iron_ore",12),new ProductionStep("recipe","iron",6),new ProductionStep("recipe","parts",2));
  // Caller-merged in-transit ore reduces mining; topological order mine->iron->parts holds.
  Feasible(ProductionPlan.Create(D("parts",2),D("iron_ore",4),recipes,ore96),
   new ProductionStep("mine","iron_ore",4),new ProductionStep("recipe","iron",4),new ProductionStep("recipe","parts",2));
  // Contract d12 storage direction: iron4+parts4+kit1 over lander{kit:3} = 24 ore -> 12 iron -> 4 parts + 4 building iron; kit untouched.
  Feasible(ProductionPlan.Create(D("iron",4,"parts",4,"kit",1),D("kit",3),recipes,D("iron_ore",96)),
   new ProductionStep("mine","iron_ore",24),new ProductionStep("recipe","iron",12),new ProductionStep("recipe","parts",4));
  // Contract d12 solar direction adds 2 copper ore -> 1 copper -> 2 cable, ordered before its consumer.
  var solar=ProductionPlan.Create(D("iron",4,"parts",4,"cable",2,"kit",1),D("kit",3),recipes,ore96);
  Feasible(solar,new ProductionStep("mine","iron_ore",24),new ProductionStep("recipe","iron",12),new ProductionStep("recipe","parts",4),
   new ProductionStep("mine","copper_ore",2),new ProductionStep("recipe","copper",1),new ProductionStep("recipe","cable",1));
  True(Array.IndexOf(solar.Steps,new ProductionStep("recipe","copper",1))<Array.IndexOf(solar.Steps,new ProductionStep("recipe","cable",1)),"copper before cable");
  // kit has no recipe: shortfall refused up front, no partial steps.
  Infeasible(ProductionPlan.Create(D("kit",1),D(),recipes,ore96),"kit");
  Infeasible(ProductionPlan.Create(D("kit",3),D("kit",1),recipes,ore96),"kit");
  // Finite ore pre-verification: missing key or insufficient remainder both refused with nothing emitted.
  Infeasible(ProductionPlan.Create(D("iron",1),D(),recipes,D()),"iron_ore");
  Infeasible(ProductionPlan.Create(D("parts",10),D(),recipes,D("iron_ore",5)),"iron_ore");
  Infeasible(ProductionPlan.Create(D("iron",50,"parts",50),D(),recipes,D("iron_ore",99)),"iron_ore");
  // Cycle in the demanded chain is infeasible, not an exception.
  var cyclic=new[]{R("iron",D("parts",2),D("iron",1)),R("copper",D("copper_ore",2),D("copper",1)),R("parts",D("iron",2),D("parts",1)),R("cable",D("copper",1),D("cable",2))};
  Infeasible(ProductionPlan.Create(D("parts",1),D(),cyclic,ore96),"循环");
  True(ProductionPlan.Create(D("cable",1),D(),cyclic,ore96).Feasible,"undemanded cycle ignored");
  // Domain boundary is still feasible at the cap.
  Feasible(ProductionPlan.Create(D("cable",100_000),D(),recipes,D("copper_ore",100_000)),
   new ProductionStep("mine","copper_ore",100_000),new ProductionStep("recipe","copper",50_000),new ProductionStep("recipe","cable",50_000));
  // Cascaded need beyond ledger limit is refused whole.
  Infeasible(ProductionPlan.Create(D("parts",100_000),D(),recipes,ore96),"上限");
  // Illegal input throws, never plans.
  Throws(()=>ProductionPlan.Create(null!,D(),recipes,ore96));
  Throws(()=>ProductionPlan.Create(D("unobtainium",1),D(),recipes,ore96));
  Throws(()=>ProductionPlan.Create(D("iron",-1),D(),recipes,ore96));
  Throws(()=>ProductionPlan.Create(D("iron",100_001),D(),recipes,ore96));
  Throws(()=>ProductionPlan.Create(D(),null!,recipes,ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),new[]{recipes[0],recipes[1],recipes[2]},ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),recipes.Append(R("kitx",D("iron",1),D("kit",1))).ToArray(),ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),recipes.Append(R("dup",D("iron",1),D("iron",1))).ToArray(),ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),recipes.Append(R("multi",D("iron",1),new Dictionary<string,int>{{"iron",1},{"copper",1}})).ToArray(),ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),new[]{R("x",D("iron",0),D("copper",1)),recipes[1],recipes[2],recipes[3]},ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),null!,ore96));
  Throws(()=>ProductionPlan.Create(D(),D(),recipes,D("rock",1)));
  Throws(()=>ProductionPlan.Create(D(),D(),recipes,D("iron_ore",-5)));
  Throws(()=>ProductionPlan.Create(D(),D(),new[]{new RecipeDefinition{Id="nullin",Input=null!,Output=D("iron",1),WorkSeconds=4,Power=1},recipes[1],recipes[2],recipes[3]},ore96));
  Console.WriteLine($"PRODUCTION_PLAN_PASS checks={n}");
 }
}
