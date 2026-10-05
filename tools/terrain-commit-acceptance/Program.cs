using System.Collections;
using System.Text.Json;
using Yudian.Terrain;

var cases = new List<(string Name, Action Run)>();
void Case(string name, Action run) => cases.Add((name, run));
void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
void Status(TerrainCommitResult r, TerrainCommitStatus status, TerrainSnapshot snapshot, long? applied = null)
{
    Check(r.Status == status, $"expected {status}, got {r.Status}");
    Check(ReferenceEquals(r.Snapshot, snapshot), "receipt is not end-of-call authority snapshot");
    Check(r.AppliedVersion == applied, "wrong original applied version");
}
void Bad(Action action, string param)
{
    try { action(); } catch (ArgumentException e) { Check(e.ParamName == param, "wrong argument name"); return; }
    throw new Exception("invalid argument accepted");
}
TerrainSnapshot Snapshot(long version = 9, double[] heights = null, string region = "sample-b",
    double x = -2, double z = 3, double spacing = 1)
    => TerrainDataCodec.ParseSnapshot(JsonSerializer.Serialize(new {
        schema_version = 1, region_id = region, version, origin_x_m = x, origin_z_m = z,
        spacing_m = spacing, rows = 3, columns = 4,
        heights_m = heights ?? new double[] {10,11,12,13,20,21,22,23,30,31,32,33}
    }));
TerrainPatch Patch(TerrainSnapshot b, double h1 = 0, double h2 = -1, string id = "patch-a")
{
    var hs = b.HeightsM.ToArray(); hs[5] = h1; hs[6] = h2;
    return TerrainDataCodec.ParsePatch("{\"schema_version\":1,\"patch_id\":" + JsonSerializer.Serialize(id)
        + ",\"base\":" + TerrainDataCodec.Serialize(b) + ",\"heights_m\":" + JsonSerializer.Serialize(hs) + "}");
}

Case("golden commit and immutable source", () => {
    var b = Snapshot(); var p = Patch(b); var before = TerrainDataCodec.Serialize(p);
    var state = new TerrainRegionState(b); var r = state.Commit("req-a", p, true, false);
    Status(r, TerrainCommitStatus.Committed, state.Current, 10);
    Check(r.RequestId == "req-a" && r.PatchId == "patch-a", "wrong receipt identity");
    Check(state.Current.Version == 10 && !ReferenceEquals(b, state.Current), "no new version snapshot");
    Check(state.Current.HeightsM.SequenceEqual(new double[]{10,11,12,13,20,0,-1,23,30,31,32,33}), "wrong row-major candidate or perimeter");
    Check(state.Current.RegionId == "sample-b" && state.Current.Rows == 3 && state.Current.Columns == 4
        && state.Current.OriginXM == -2 && state.Current.OriginZM == 3 && state.Current.SpacingM == 1, "layout changed");
    Check(b.Version == 9 && b.GetHeight(1,1) == 21 && b.GetHeight(1,2) == 22, "old authority mutated");
    Check(TerrainDataCodec.Serialize(p) == before, "patch changed");
    TerrainDataCodec.ValidateAgainst(Patch(state.Current, -2, -3), state.Current);
});
Case("two offline ready candidates rechecked at authority", () => {
    var b = Snapshot(); var p = Patch(b); var q = Patch(b, 1, 2, "patch-b");
    Check(TerrainCommitPolicy.Evaluate(q,b,true,false) == TerrainCommitStatus.Ready, "candidate not ready offline");
    var s = new TerrainRegionState(b); s.Commit("a",p,true,false); var first = s.Current;
    Status(s.Commit("b",q,true,false),TerrainCommitStatus.StaleBase,first);
});
Case("new base advances and old receipts remain frozen", () => {
    var s = new TerrainRegionState(Snapshot()); var p = Patch(s.Current); var r1 = s.Commit("a",p,true,false);
    var r2 = s.Commit("b",Patch(s.Current,-2,-3),true,false);
    Status(r2,TerrainCommitStatus.Committed,s.Current,11);
    Check(r1.Snapshot.Version == 10 && r1.Snapshot.GetHeight(1,1) == 0, "previous receipt mutated");
    Status(s.Commit("a",p,false,true),TerrainCommitStatus.AlreadyCommitted,s.Current,10);
});
Case("semantic replay accepts separately parsed patch", () => {
    var s = new TerrainRegionState(Snapshot()); var p = Patch(s.Current); s.Commit("a",p,true,false);
    Status(s.Commit("a",TerrainDataCodec.ParsePatch(TerrainDataCodec.Serialize(p)),true,false),
        TerrainCommitStatus.AlreadyCommitted,s.Current,10);
});
Case("replay uses numeric signed-zero equality", () => {
    var b = Snapshot(); var s = new TerrainRegionState(b); s.Commit("a",Patch(b,0,-1),true,false);
    Status(s.Commit("a",Patch(b,-0.0,-1),false,true),TerrainCommitStatus.AlreadyCommitted,s.Current,10);
});
Case("same request changed candidate rejected", () => {
    var b=Snapshot();var s=new TerrainRegionState(b);s.Commit("a",Patch(b),true,false);
    Status(s.Commit("a",Patch(b,2,-1),true,false),TerrainCommitStatus.RequestConflict,s.Current);
});
Case("same request changed patch id rejected even cancelled", () => {
    var b=Snapshot();var s=new TerrainRegionState(b);s.Commit("a",Patch(b),true,false);
    Status(s.Commit("a",Patch(b,0,-1,"patch-b"),false,true),TerrainCommitStatus.RequestConflict,s.Current);
});
foreach (var variation in new (string Name, Func<TerrainSnapshot> Make)[] {
    ("version",()=>Snapshot(8)), ("region",()=>Snapshot(region:"other")),
    ("origin-x",()=>Snapshot(x:-1)), ("origin-z",()=>Snapshot(z:4)),
    ("spacing",()=>Snapshot(spacing:2)),
    ("same-version heights",()=>Snapshot(heights:new double[]{10,11,12,13,20,25,22,23,30,31,32,33}))
}) {
    Case("request conflict includes full base " + variation.Name, () => {
        var s = new TerrainRegionState(Snapshot()); s.Commit("a",Patch(s.Current),true,false);
        Status(s.Commit("a",Patch(variation.Make()),true,false),TerrainCommitStatus.RequestConflict,s.Current);
    });
    Case("fresh request rejects stale " + variation.Name, () => {
        var s = new TerrainRegionState(Snapshot());
        Status(s.Commit("a",Patch(variation.Make()),true,false),TerrainCommitStatus.StaleBase,s.Current);
        Status(s.Commit("a",Patch(s.Current),true,false),TerrainCommitStatus.Committed,s.Current,10);
    });
}
Case("permission rejection does not consume request", () => {
    var s=new TerrainRegionState(Snapshot());var p=Patch(s.Current);
    Status(s.Commit("a",p,false,false),TerrainCommitStatus.PermissionDenied,s.Current);
    Status(s.Commit("a",p,true,false),TerrainCommitStatus.Committed,s.Current,10);
});
Case("cancellation precedes permission and permits later retry", () => {
    var s=new TerrainRegionState(Snapshot());var p=Patch(s.Current);
    Status(s.Commit("a",p,false,true),TerrainCommitStatus.Cancelled,s.Current);
    Status(s.Commit("a",p,true,false),TerrainCommitStatus.Committed,s.Current,10);
});
Case("late cancellation preserves previous committed result", () => {
    var s=new TerrainRegionState(Snapshot());s.Commit("a",Patch(s.Current),true,false);var first=s.Current;
    Status(s.Commit("b",Patch(first,-2,-3),true,true),TerrainCommitStatus.Cancelled,first);
    Check(s.Current.GetHeight(1,1)==0 && s.Current.Version==10,"cancellation reverted first commit");
});
Case("no change consumes neither version nor id", () => {
    var b=Snapshot();var s=new TerrainRegionState(b);
    Status(s.Commit("a",Patch(b,21,22),true,false),TerrainCommitStatus.NoChange,b);
    Status(s.Commit("a",Patch(b),true,false),TerrainCommitStatus.Committed,s.Current,10);
});
Case("version ceiling distinguishes unchanged and changed", () => {
    var b=Snapshot(9007199254740991);var s=new TerrainRegionState(b);
    Status(s.Commit("a",Patch(b,21,22),true,false),TerrainCommitStatus.NoChange,b);
    Status(s.Commit("a",Patch(b),true,false),TerrainCommitStatus.VersionLimit,b);
});
Case("last supported increment and later replay", () => {
    var b=Snapshot(9007199254740990);var s=new TerrainRegionState(b);var p=Patch(b);
    Status(s.Commit("a",p,true,false),TerrainCommitStatus.Committed,s.Current,9007199254740991);
    Status(s.Commit("b",Patch(s.Current,-2,-3),true,false),TerrainCommitStatus.VersionLimit,s.Current);
    Status(s.Commit("a",p,false,true),TerrainCommitStatus.AlreadyCommitted,s.Current,9007199254740991);
});
Case("authority and receipt immutable public surfaces", () => {
    var s=new TerrainRegionState(Snapshot());var r=s.Commit("a",Patch(s.Current),true,false);
    foreach(var type in new[]{typeof(TerrainCommitResult),typeof(TerrainRegionState)})
        Check(type.GetProperties().All(p=>p.SetMethod is null || !p.SetMethod.IsPublic),"public setter");
    Check(!(s.Current.HeightsM is double[]) && !(s.Current.HeightsM is IList),"mutable heights exposed");
    var copy=s.Current.HeightsM.ToArray();copy[5]=99;
    Check(r.Snapshot.GetHeight(1,1)==0,"consumer copy mutated authority");
});
Case("null argument names and validation before replay", () => {
    Bad(()=>new TerrainRegionState(null),"initial");var s=new TerrainRegionState(Snapshot());
    var p=Patch(s.Current);s.Commit("a",p,true,false);
    Bad(()=>s.Commit("a",null,false,true),"patch");
    Bad(()=>s.Commit(null,null,false,true),"requestId");
    Status(s.Commit("a",p,true,false),TerrainCommitStatus.AlreadyCommitted,s.Current,10);
});
Case("ASCII request boundary and invalid ids", () => {
    var s=new TerrainRegionState(Snapshot());var p=Patch(s.Current);
    foreach(var id in new[]{"","A","1a","a ","a\n","é","a中",new string('a',65)})
        Bad(()=>s.Commit(id,p,true,false),"requestId");
    string valid="a_9-"+new string('x',60);
    Status(s.Commit(valid,p,true,false),TerrainCommitStatus.Committed,s.Current,10);
});

int failed=0;
foreach(var item in cases) {
    try { item.Run(); Console.WriteLine("PASS " + item.Name); }
    catch(Exception e) { failed++; Console.WriteLine("FAIL " + item.Name + ": " + e.Message); }
}
Console.WriteLine($"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; cases={cases.Count}; failed={failed}");
return failed==0 ? 0 : 1;
