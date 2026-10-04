using System.Collections;
using System.Globalization;
using Yudian.Terrain;

static class Probe
{
    static int count, failed;
    static void Check(string name, Action test) { try { test(); count++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.GetType().Name + " " + error.Message); } }
    static void Assert(bool value) { if (!value) throw new Exception("assertion failed"); }
    static void Reject(Action action, string path) { try { action(); } catch (ArgumentException e) { Assert(e.Message.Contains(path, StringComparison.OrdinalIgnoreCase)); return; } throw new Exception("expected ArgumentException for " + path); }
    static void RejectDecode(Action action, string path) { try { action(); } catch (ArgumentException error) { Assert(error.Message.Contains(path, StringComparison.OrdinalIgnoreCase) || error.Message.Contains("json", StringComparison.OrdinalIgnoreCase)); return; } throw new Exception("expected decoding ArgumentException"); }
    static void Same(TerrainSnapshot a, TerrainSnapshot b) { Assert(a.SchemaVersion == b.SchemaVersion && a.RegionId == b.RegionId && a.Version == b.Version && a.OriginXM == b.OriginXM && a.OriginZM == b.OriginZM && a.SpacingM == b.SpacingM && a.Rows == b.Rows && a.Columns == b.Columns && a.HeightsM.SequenceEqual(b.HeightsM)); }
    static void Immutable(IReadOnlyList<double> values) { var before = values.ToArray(); Assert(values is not double[]); if (values is ICollection collection) { Assert(collection.SyncRoot is not double[]); Assert(collection.SyncRoot is not IList<double>); } if (values is IList<double> list) { Assert(list.IsReadOnly); try { list[0] = 999; } catch (NotSupportedException) { } } Assert(values.SequenceEqual(before)); }
    const string Small = "{\"schema_version\":1,\"region_id\":\"sample-a\",\"version\":7,\"origin_x_m\":-1.5,\"origin_z_m\":2,\"spacing_m\":0.5,\"rows\":2,\"columns\":3,\"heights_m\":[1,2,4,8,16,32]}";
    const string Base = "{\"schema_version\":1,\"region_id\":\"sample-b\",\"version\":9,\"origin_x_m\":-2,\"origin_z_m\":3,\"spacing_m\":1,\"rows\":3,\"columns\":4,\"heights_m\":[10,11,12,13,20,21,22,23,30,31,32,33]}";
    static string Patch(string values = "10,11,12,13,20,0,-1,23,30,31,32,33") => "{\"schema_version\":1,\"patch_id\":\"patch-b\",\"base\":" + Base + ",\"heights_m\":[" + values + "]}";
    static void Main()
    {
        Check("non-symmetric golden orientation", () => { var s = TerrainDataCodec.ParseSnapshot(Small); Assert(s.Rows == 2 && s.Columns == 3 && s.GetHeight(0, 2) == 4 && s.GetHeight(1, 0) == 8); Assert(s.OriginXM + 2 * s.SpacingM == -0.5 && s.OriginZM + s.SpacingM == 2.5); var t = TerrainDataCodec.ParseSnapshot(TerrainDataCodec.Serialize(s)); Same(t, s); });
        Check("candidate is read-only, matching current, interior valid", () => { var p = TerrainDataCodec.ParsePatch(Patch()); var c = TerrainDataCodec.ParseSnapshot(Base); var before = TerrainDataCodec.Serialize(c); TerrainDataCodec.ValidateAgainst(p, c); Assert(p.HeightsM[5] == 0 && p.HeightsM[6] == -1); Assert(TerrainDataCodec.Serialize(c) == before); var t = TerrainDataCodec.ParsePatch(TerrainDataCodec.Serialize(p)); Assert(t.SchemaVersion == p.SchemaVersion && t.PatchId == p.PatchId && t.HeightsM.SequenceEqual(p.HeightsM)); Same(t.Base, p.Base); });
        Check("immutable heights cannot be modified", () => { var s = TerrainDataCodec.ParseSnapshot(Small); var p = TerrainDataCodec.ParsePatch(Patch()); Immutable(s.HeightsM); Immutable(p.HeightsM); Immutable(p.Base.HeightsM); });
        foreach (var index in new[] { 1, 9, 4, 7 }) Check("perimeter index " + index, () => { var values = new double[] { 10, 11, 12, 13, 20, 0, -1, 23, 30, 31, 32, 33 }; values[index] = 999; Reject(() => TerrainDataCodec.ParsePatch(Patch(string.Join(',', values))), "heights_m"); });
        Check("same version but different base contents", () => { var p = TerrainDataCodec.ParsePatch(Patch()); var c = TerrainDataCodec.ParseSnapshot(Base.Replace("20,21,22,23", "20,7,22,23")); var before = TerrainDataCodec.Serialize(c); Reject(() => TerrainDataCodec.ValidateAgainst(p, c), "heights_m"); Assert(before == TerrainDataCodec.Serialize(c)); });
        foreach (var edit in new[] { ("\"version\":9", "\"version\":10", "version"), ("sample-b", "another", "region_id"), ("\"origin_x_m\":-2", "\"origin_x_m\":-3", "origin_x_m"), ("\"spacing_m\":1", "\"spacing_m\":2", "spacing_m") }) Check("mismatch " + edit.Item3, () => Reject(() => TerrainDataCodec.ValidateAgainst(TerrainDataCodec.ParsePatch(Patch()), TerrainDataCodec.ParseSnapshot(Base.Replace(edit.Item1, edit.Item2))), edit.Item3));
        foreach (var edit in new[] { ("\"version\":7", "\"version\":7.0", "version"), ("\"version\":7", "\"version\":7e0", "version"), ("\"version\":7", "\"version\":9007199254740992", "version"), ("\"rows\":2", "\"rows\":258", "rows"), ("\"spacing_m\":0.5", "\"spacing_m\":1e999", "spacing_m"), ("\"spacing_m\":0.5", "\"spacing_m\":0", "spacing_m"), ("sample-a", "sample-a\\n", "region_id"), ("\"columns\":3", "\"Columns\":3", "Columns"), ("\"heights_m\":[1,2,4,8,16,32]", "\"heights_m\":[1,2,4,8,16]", "heights_m") }) Check("reject " + edit.Item2, () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace(edit.Item1, edit.Item2)), edit.Item3));
        foreach (var invalid in new[] { @"a\uD800", @"a\uDC00" })
        {
            Check("snapshot invalid Unicode ID " + invalid, () => RejectDecode(() => TerrainDataCodec.ParseSnapshot(Small.Replace("sample-a", invalid)), "region_id"));
            Check("snapshot invalid Unicode field name " + invalid, () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace("region_id", invalid)), "json"));
            Check("patch invalid Unicode ID " + invalid, () => RejectDecode(() => TerrainDataCodec.ParsePatch(Patch().Replace("patch-b", invalid)), "patch_id"));
            Check("base invalid Unicode ID " + invalid, () => RejectDecode(() => TerrainDataCodec.ParsePatch(Patch().Replace("sample-b", invalid)), "region_id"));
            Check("base invalid Unicode field name " + invalid, () => Reject(() => TerrainDataCodec.ParsePatch(Patch().Replace("region_id", invalid)), "json"));
        }
        Check("duplicate field", () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace("\"version\":7", "\"version\":7,\"version\":7")), "version"));
        Check("unknown field", () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace("\"version\":7", "\"unused\":0,\"version\":7")), "unused"));
        Check("missing field", () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace("\"region_id\":\"sample-a\",", "")), "region_id"));
        Check("explicit null", () => Reject(() => TerrainDataCodec.ParseSnapshot(Small.Replace("\"region_id\":\"sample-a\"", "\"region_id\":null")), "region_id"));
        Check("too long input", () => Reject(() => TerrainDataCodec.ParseSnapshot(new string(' ', 4194305) + Small), "json"));
        Check("index errors", () => { var s = TerrainDataCodec.ParseSnapshot(Small); Reject(() => s.GetHeight(-1, 0), "row"); Reject(() => s.GetHeight(0, 3), "column"); });
        foreach (var culture in new[] { "fr-FR", "en-US" }) Check("culture " + culture, () => { var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); var s = TerrainDataCodec.ParseSnapshot(Small); var j = TerrainDataCodec.Serialize(s); Assert(TerrainDataCodec.ParseSnapshot(j).SpacingM == 0.5); } finally { CultureInfo.CurrentCulture = old; } });
        Console.WriteLine($"Independent acceptance: {count} checks passed; {failed} failed."); Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
