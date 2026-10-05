using System.Collections;
using System.Globalization;
using System.Text.Json;
using Yudian.Terrain;

int passed = 0, failed = 0;
void Check(string label, Action check)
{
    try { check(); passed++; Console.WriteLine($"PASS {label}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {label}: {ex.GetType().Name}: {ex.Message}"); }
}
void Require(bool value, string message = "assertion failed")
{
    if (!value) throw new Exception(message);
}
void Close(double actual, double expected) => Require(Math.Abs(actual - expected) <= 1e-9, $"{actual:R} != {expected:R}");
void Reject(Action action, string path)
{
    try { action(); } catch (ArgumentException ex) { Require(ex.Message.Contains(path, StringComparison.OrdinalIgnoreCase), ex.Message); return; }
    throw new Exception("expected ArgumentException");
}
TerrainSnapshot Snapshot(int rows, int columns, double[] heights, string id = "sample", long version = 9, double x = 0, double z = 0, double spacing = 1)
    => TerrainDataCodec.ParseSnapshot(JsonSerializer.Serialize(new { schema_version = 1, region_id = id, version, origin_x_m = x, origin_z_m = z, spacing_m = spacing, rows, columns, heights_m = heights }));
TerrainPatch Patch(TerrainSnapshot snapshot, double[] heights)
    => TerrainDataCodec.ParsePatch("{\"schema_version\":1,\"patch_id\":\"candidate\",\"base\":" + TerrainDataCodec.Serialize(snapshot) + ",\"heights_m\":" + JsonSerializer.Serialize(heights) + "}");
void Point(TerrainVertex v, double x, double y, double z) { Close(v.X, x); Close(v.Y, y); Close(v.Z, z); }
void Geometry(TerrainMesh mesh)
{
    Require(mesh.Vertices.Count == mesh.GridRows * mesh.GridColumns);
    Require(mesh.Indices.Count == 6 * (mesh.GridRows - 1) * (mesh.GridColumns - 1));
    Require(mesh.Vertices.All(v => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z)));
    int n = 0;
    for (int r = 0; r < mesh.GridRows - 1; r++)
        for (int c = 0; c < mesh.GridColumns - 1; c++)
        {
            int a = r * mesh.GridColumns + c;
            int[] expected = { a, a + mesh.GridColumns + 1, a + 1, a, a + mesh.GridColumns, a + mesh.GridColumns + 1 };
            foreach (var index in expected) Require(mesh.Indices[n++] == index, "triangle order");
        }
    for (int i = 0; i < mesh.Indices.Count; i += 3)
    {
        var a = mesh.Vertices[mesh.Indices[i]]; var b = mesh.Vertices[mesh.Indices[i + 1]]; var c = mesh.Vertices[mesh.Indices[i + 2]];
        Require((b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z) > 0, "winding/degenerate triangle");
    }
}
// Sample the emitted triangles, independently of either builder's interpolation code.
double Surface(TerrainMesh mesh, double x, double z)
{
    for (int i = 0; i < mesh.Indices.Count; i += 3)
    {
        var a = mesh.Vertices[mesh.Indices[i]]; var b = mesh.Vertices[mesh.Indices[i + 1]]; var c = mesh.Vertices[mesh.Indices[i + 2]];
        double determinant = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
        double u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / determinant;
        double v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / determinant;
        double w = 1 - u - v;
        if (u >= -1e-10 && v >= -1e-10 && w >= -1e-10) return u * a.Y + v * b.Y + w * c.Y;
    }
    throw new Exception("point outside emitted triangles");
}
var originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (var culture in new[] { "en-US", "fr-FR" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        var gold = Snapshot(2, 3, new double[] { 1, 2, 4, 8, 16, 32 }, "sample-a", 7, -1.5, 2, .5);
        var basis = Snapshot(3, 4, new double[] { 10, 11, 12, 13, 20, 21, 22, 23, 30, 31, 32, 33 }, "sample-b", 9, -2, 3);
        var patch = Patch(basis, new double[] { 10, 11, 12, 13, 20, 0, -1, 23, 30, 31, 32, 33 });
        foreach (var candidate in new[] {
            (Name: "stage", Factor: 1, Build: (Func<TerrainSnapshot,TerrainMesh>)StageMeshBuilder.Build, BuildPatch: (Func<TerrainPatch,TerrainSnapshot,TerrainMesh>)StageMeshBuilder.Build),
            (Name: "heightfield", Factor: 2, Build: (Func<TerrainSnapshot,TerrainMesh>)HeightfieldMeshBuilder.Build, BuildPatch: (Func<TerrainPatch,TerrainSnapshot,TerrainMesh>)HeightfieldMeshBuilder.Build) })
        {
            string label = culture + "/" + candidate.Name + "/";
            var mesh = candidate.Build(gold);
            Check(label + "non-square layout/count/full winding", () => { Require(mesh.GridRows == candidate.Factor + 1 && mesh.GridColumns == 2 * candidate.Factor + 1); Geometry(mesh); });
            Check(label + "original nodes world coordinates", () => {
                for (int r = 0; r < gold.Rows; r++) for (int c = 0; c < gold.Columns; c++)
                    Point(mesh.Vertices[r * candidate.Factor * mesh.GridColumns + c * candidate.Factor], gold.OriginXM + c * gold.SpacingM, gold.GetHeight(r, c), gold.OriginZM + r * gold.SpacingM);
            });
            Check(label + "candidate changes and base remains", () => {
                var before = TerrainDataCodec.Serialize(basis); var beforePatch = TerrainDataCodec.Serialize(patch);
                var changed = candidate.BuildPatch(patch, basis); Geometry(changed);
                Point(changed.Vertices[candidate.Factor * changed.GridColumns + candidate.Factor], -1, 0, 4);
                Point(changed.Vertices[candidate.Factor * changed.GridColumns + 2 * candidate.Factor], 0, -1, 4);
                Require(before == TerrainDataCodec.Serialize(basis) && beforePatch == TerrainDataCodec.Serialize(patch));
            });
            Check(label + "noop patch same mesh", () => { var noop = candidate.BuildPatch(Patch(gold, gold.HeightsM.ToArray()), gold); Require(noop.Vertices.SequenceEqual(mesh.Vertices) && noop.Indices.SequenceEqual(mesh.Indices)); });
            Check(label + "repeat determinism", () => { var repeat = candidate.Build(gold); Require(repeat.Vertices.SequenceEqual(mesh.Vertices) && repeat.Indices.SequenceEqual(mesh.Indices)); });
            Check(label + "no mutable collection/SyncRoot", () => { Require(mesh.Vertices is not ICollection && mesh.Vertices is not IList<TerrainVertex> && mesh.Vertices is not TerrainVertex[]); Require(mesh.Indices is not ICollection && mesh.Indices is not IList<int> && mesh.Indices is not int[]); });
            Check(label + "output index guards", () => { Reject(() => { _ = mesh.Vertices[-1]; }, "index"); Reject(() => { _ = mesh.Indices[mesh.Indices.Count]; }, "index"); });
            Check(label + "null snapshot", () => Reject(() => candidate.Build(null), "snapshot"));
            Check(label + "null patch", () => Reject(() => candidate.BuildPatch(null, basis), "patch"));
            Check(label + "null current", () => Reject(() => candidate.BuildPatch(patch, null), "current"));
            Check(label + "stale current", () => Reject(() => candidate.BuildPatch(patch, Snapshot(3, 4, basis.HeightsM.ToArray(), "sample-b", 10, -2, 3)), "version"));
            Check(label + "wrong region", () => Reject(() => candidate.BuildPatch(patch, Snapshot(3, 4, basis.HeightsM.ToArray(), "other", 9, -2, 3)), "region_id"));
            Check(label + "same version different contents", () => { var h = basis.HeightsM.ToArray(); h[5]++; Reject(() => candidate.BuildPatch(patch, Snapshot(3, 4, h, "sample-b", 9, -2, 3)), "heights_m"); });
            Check(label + "maximum grid finite/order", () => {
                var h = Enumerable.Range(0, 257 * 257).Select(i => (i % 257) * .01 + (i / 257) * .001).ToArray();
                var maximum = candidate.Build(Snapshot(257, 257, h, x: 10000, z: -10000, spacing: 100));
                Require(maximum.GridRows == 256 * candidate.Factor + 1 && maximum.GridColumns == maximum.GridRows); Geometry(maximum);
                Point(maximum.Vertices[^1], 35600, 2.816, 15600);
            });
            Check(label + "2xN minimum band", () => Geometry(candidate.Build(Snapshot(2, 4, new double[] { -1000, -2, 3, 1000, -1000, -2, 3, 1000 }, spacing: .01))));
            Check(label + "Nx2 minimum band", () => Geometry(candidate.Build(Snapshot(4, 2, new double[] { -1000, 1000, -2, 3, 4, 5, 6, 7 }, spacing: .01))));
            var left = candidate.Build(Snapshot(3, 3, new double[] { 1, 2, 3, 4, 6, 5, 7, 8, 9 }, "left"));
            var right = candidate.Build(Snapshot(3, 3, new double[] { 3, 12, 13, 5, 14, 15, 9, 16, 17 }, "right", x: 2));
            Check(label + "actual triangle nonzero adjacent edge", () => {
                foreach (double z in new[] { 0, .25, .5, .75, 1, 1.25, 1.5, 1.75, 2 }) {
                    double expected = z <= 1 ? 3 + 2 * z : 5 + 4 * (z - 1);
                    Close(Surface(left, 2, z), expected); Close(Surface(right, 2, z), expected);
                }
            });
        }
        var twist = Snapshot(2, 2, new double[] { 0, 0, 0, 4 });
        Check(culture + "/stage exact 2x2 index golden", () => Require(StageMeshBuilder.Build(twist).Indices.SequenceEqual(new[] { 0, 3, 1, 0, 2, 3 })));
        Check(culture + "/twisted cell distinct diagonal height", () => { Close(Surface(StageMeshBuilder.Build(twist), .5, .5), 2); Close(Surface(HeightfieldMeshBuilder.Build(twist), .5, .5), 1); });
        Check(culture + "/fine complete non-square vertex golden", () => {
            var fine = HeightfieldMeshBuilder.Build(gold);
            double[] expected = { 1, 1.5, 2, 3, 4, 4.5, 6.75, 9, 13.5, 18, 8, 12, 16, 24, 32 };
            for (int i = 0; i < expected.Length; i++) Point(fine.Vertices[i], -1.5 + (i % 5) * .25, expected[i], 2 + (i / 5) * .25);
        });
        Check(culture + "/fine all candidate vertices use new heights", () => {
            var fine = HeightfieldMeshBuilder.Build(patch, basis);
            double[] expected = {
                10, 10.5, 11, 11.5, 12, 12.5, 13,
                15, 10.25, 5.5, 5.5, 5.5, 11.75, 18,
                20, 10, 0, -.5, -1, 11, 23,
                25, 20.25, 15.5, 15.5, 15.5, 21.75, 28,
                30, 30.5, 31, 31.5, 32, 32.5, 33 };
            for (int i = 0; i < expected.Length; i++) Point(fine.Vertices[i], -2 + (i % 7) * .5, expected[i], 3 + (i / 7) * .5);
        });
        Check(culture + "/fine triangle is not exact bilinear surface", () => { double actual = Surface(HeightfieldMeshBuilder.Build(twist), .75, .25); Close(actual, 1); Require(Math.Abs(actual - .75) > .1); });
        Check(culture + "/cross candidate adjacent edge position-height", () => {
            var a = StageMeshBuilder.Build(Snapshot(3, 3, new double[] { 1, 2, 3, 4, 6, 5, 7, 8, 9 }));
            var b = HeightfieldMeshBuilder.Build(Snapshot(3, 3, new double[] { 3, 12, 13, 5, 14, 15, 9, 16, 17 }, x: 2));
            foreach (double z in new[] { .25, .75, 1.25, 1.75 }) Close(Surface(a, 2, z), Surface(b, 2, z));
        });
    }
    Check("shared mesh owns buffers", () => {
        var vertices = new[] { new TerrainVertex(0, 0, 0), new TerrainVertex(1, 0, 0), new TerrainVertex(0, 0, 1), new TerrainVertex(1, 0, 1) };
        var indices = new[] { 0, 3, 1, 0, 2, 3 }; var mesh = new TerrainMesh(2, 2, vertices, indices);
        vertices[0] = new TerrainVertex(0, double.NaN, 0); indices[0] = 99;
        Point(mesh.Vertices[0], 0, 0, 0); Require(mesh.Indices[0] == 0);
    });
    var validVertices = new TerrainVertex[4]; var validIndices = new int[6];
    Check("shared mesh invalid rows", () => Reject(() => new TerrainMesh(1, 2, validVertices, validIndices), "gridRows"));
    Check("shared mesh invalid columns", () => Reject(() => new TerrainMesh(2, 514, validVertices, validIndices), "gridColumns"));
    Check("shared mesh missing vertices", () => Reject(() => new TerrainMesh(2, 2, null, validIndices), "vertices"));
    Check("shared mesh missing indices", () => Reject(() => new TerrainMesh(2, 2, validVertices, null), "indices"));
    Check("shared mesh wrong vertices length", () => Reject(() => new TerrainMesh(2, 2, new TerrainVertex[3], validIndices), "vertices"));
    Check("shared mesh wrong indices length", () => Reject(() => new TerrainMesh(2, 2, validVertices, new int[3]), "indices"));
    Check("shared mesh nonfinite coordinates", () => Reject(() => new TerrainMesh(2, 2, new[] { new TerrainVertex(double.PositiveInfinity, 0, 0), default, default, default }, validIndices), "vertices"));
    Check("shared mesh index outside range", () => Reject(() => new TerrainMesh(2, 2, validVertices, new[] { 0, 1, 4, 0, 2, 3 }), "indices"));
}
finally { CultureInfo.CurrentCulture = originalCulture; }
Console.WriteLine($"RESULT {passed} PASS / {failed} FAIL");
Environment.ExitCode = failed == 0 ? 0 : 1;
