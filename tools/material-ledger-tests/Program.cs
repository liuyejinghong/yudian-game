using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Yudian.Resources;
static class Check {
 static int n;
 static void True(bool v,string why){if(!v)throw new Exception(why);n++;}
 static void Reject(Action a){bool caught=false;try{a();}catch(ArgumentException){caught=true;}catch(InvalidOperationException){caught=true;}True(caught,"negative operation accepted");}
 static void Main(string[] args){
  var config=JsonSerializer.Deserialize<BootstrapConfig>(System.IO.File.ReadAllText(args[0]));config.Validate();
  var ledger=new MaterialLedger(new(new[]{new ContainerStock("lander",500,new(config.Initial)),new ContainerStock("cargo",8,new()),new ContainerStock("site",100,new()),new ContainerStock("spent",1000,new())},Array.Empty<MaterialReservation>(),new()));
  var totals=ledger.Totals();var five=new Dictionary<string,int>{{"parts",5}};
  ledger.Reserve("a","lander",five);True(ledger.Count("lander","parts")==48&&ledger.Available("lander","parts")==43,"reservation consumed stock");
  ledger.Reserve("b","lander",new(){{"parts",2}});ledger.Release("b");True(ledger.Reserved("a","lander","parts")==5,"cancel freed another goal");
  ledger.Transfer("take-a","a","lander","cargo",five);ledger.Transfer("take-a","a","lander","cargo",five);True(ledger.Count("cargo","parts")==5&&ledger.Count("lander","parts")==43,"retry duplicated cargo");
  Reject(()=>ledger.Transfer("take-a","a","lander","cargo",new(){{"parts",1}}));
  Reject(()=>ledger.Transfer("too-full","a","lander","cargo",five));
  ledger.Transfer("deliver-a","a","cargo","site",five);ledger.Transfer("consume-a","a","site","spent",five);
  True(ledger.Totals().All(x=>totals[x.Key]==x.Value),"mass changed");
  var copy=new MaterialLedger(JsonSerializer.Deserialize<LedgerSnapshot>(JsonSerializer.Serialize(ledger.Snapshot())));copy.Transfer("consume-a","a","site","spent",five);True(copy.Count("spent","parts")==5,"restore repeated consumption");
  True(MaterialLedger.NetNeed(10,3,4)==3&&MaterialLedger.NetNeed(10,3,0)==7,"foreign transit credited");
  Reject(()=>MaterialLedger.NetNeed(int.MaxValue,3,4));Reject(()=>ledger.Transfer("negative","a","lander","cargo",new(){{"parts",-1}}));
  Reject(()=>new MaterialLedger(ledger.Snapshot() with{Reservations=new[]{new MaterialReservation("x","lander","parts",999)}}));
  Reject(()=>new MaterialLedger(ledger.Snapshot() with{Containers=new[]{new ContainerStock("bad",3,new(){{"parts",4}})}}));
  var external=ledger.Snapshot();external.Containers[0].Items["parts"]=0;True(ledger.Count("lander","parts")==43,"snapshot mutated authority");
  var zero=MaterialLedger.Materials.ToDictionary(m=>m,m=>0);
  var production=new MaterialLedger(new(new[]{new ContainerStock("mine",8,new()),new ContainerStock("batch",8,new()),new ContainerStock("full",1,new(){{"copper",1}})},Array.Empty<MaterialReservation>(),new()));
  var receipt=new MiningReceipt("mined","mine","iron_ore",4,0,1);
  production.Extract(receipt);production.Extract(receipt);
  True(production.Count("mine","iron_ore")==4,"mining retry duplicated ore");
  Reject(()=>production.Extract(receipt with{Quantity=3}));
  Reject(()=>production.Extract(receipt with{Operation="oversized",Quantity=8}));
  production.Transfer("input","goal","mine","batch",new(){{"iron_ore",4}});
  var recipe=config.Recipes.Single(r=>r.Id=="iron");
  production.Convert("iron-batch","batch","test-v1",recipe,2);
  production.Convert("iron-batch","batch","test-v1",recipe,2);
  True(production.Count("batch","iron_ore")==0&&production.Count("batch","iron")==2,"conversion retry duplicated output");
  Reject(()=>production.Convert("iron-batch","batch","test-v1",recipe,1));
  Reject(()=>production.Convert("missing","batch","test-v1",recipe,1));
  Reject(()=>production.Convert("full-output","full","test-v1",config.Recipes.Single(r=>r.Id=="cable"),1));
  Reject(()=>production.Convert("fake-kit","batch","test-v1",new(){Id="kit",Input=new(){{"iron",1}},Output=new(){{"kit",1}},Power=1,WorkSeconds=1},1));
  True(production.Count("full","copper")==1&&production.Count("full","cable")==0,"failed conversion changed input/output");
  // The separate full-output counterexample has one initial copper.
  zero["copper"]=1;production.ValidateBalance(zero,config.Recipes,"test-v1");
  var restored=new MaterialLedger(JsonSerializer.Deserialize<LedgerSnapshot>(JsonSerializer.Serialize(production.Snapshot())));
  restored.Extract(receipt);restored.Convert("iron-batch","batch","test-v1",recipe,2);restored.ValidateBalance(zero,config.Recipes,"test-v1");
  True(restored.Totals()["iron"]==2,"restored recipe reissued output");
  var corrupted=restored.Snapshot();corrupted.Recipes[0].Output["iron"]=3;
  Reject(()=>new MaterialLedger(corrupted).ValidateBalance(zero,config.Recipes,"test-v1"));
  config.Initial["cable"]=0;Reject(()=>config.Validate());
  Console.WriteLine($"MATERIAL_LEDGER_PASS checks={n}");
 }
}
