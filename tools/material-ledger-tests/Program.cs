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
  config.Initial["cable"]=0;Reject(()=>config.Validate());
  Console.WriteLine($"MATERIAL_LEDGER_PASS checks={n}");
 }
}
