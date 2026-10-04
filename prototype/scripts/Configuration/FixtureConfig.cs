using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yudian;

// 输入在创建场景前完整校验；不会返回半份配置。
// ParseValidated：损坏 JSON、缺字段、显式 null、重复/未知键、类型/越界错误一律 ArgumentException
// 且消息含字段路径；不存在部分成功。
public sealed class FixtureConfig
{
    [JsonPropertyName("name")] public string Name { get; set; } = "s_small";
    [JsonPropertyName("seed")] public int Seed { get; set; } = 20261004;
    [JsonPropertyName("scale")] public ScaleConfig Scale { get; set; } = new();
    [JsonPropertyName("terrain")] public TerrainConfig Terrain { get; set; } = new();
    [JsonPropertyName("camera_path")] public CameraPathConfig CameraPath { get; set; } = new();
    [JsonPropertyName("quality")] public QualityConfig Quality { get; set; } = new();
    [JsonPropertyName("target_resolution")] public ResConfig TargetResolution { get; set; } = new();
    [JsonPropertyName("benchmark")] public BenchConfig Benchmark { get; set; } = new();

    public static FixtureConfig ParseValidated(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("fixture JSON 为空");
        JsonDocument doc;
        try
        {
            // 键的大小写规则由 Fields 处理。
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"fixture JSON 损坏: {e.Message}", e);
        }

        using (doc)
            return ReadRoot(doc.RootElement);
    }

    private static readonly string[] RootFields =
    {
        "name", "seed", "scale", "terrain", "camera_path", "quality", "target_resolution", "benchmark",
    };
    private static readonly string[] ScaleFields = { "robots_total", "robots_per_type", "facilities", "ring_radius" };
    private static readonly string[] TerrainFields = { "size", "segments", "mound", "mineral_pit" };
    private static readonly string[] FeatureFields = { "position", "radius", "amount" };
    private static readonly string[] CameraFields = { "center", "radius", "height", "period_seconds" };
    private static readonly string[] QualityFields = { "msaa_3d", "fxaa", "scaling_3d_scale", "shadows" };
    private static readonly string[] ResFields = { "width", "height" };
    private static readonly string[] BenchFields = { "duration_seconds", "frames_csv", "summary_json" };

    private static FixtureConfig ReadRoot(JsonElement root)
    {
        var f = Fields(root, "", RootFields);
        return new FixtureConfig
        {
            Name = NonEmpty(f["name"], "name"),
            Seed = RangeInt(f["seed"], "seed", 0, int.MaxValue),
            Scale = ReadScale(f["scale"], "scale"),
            Terrain = ReadTerrain(f["terrain"], "terrain"),
            CameraPath = ReadCamera(f["camera_path"], "camera_path"),
            Quality = ReadQuality(f["quality"], "quality"),
            TargetResolution = ReadRes(f["target_resolution"], "target_resolution"),
            Benchmark = ReadBench(f["benchmark"], "benchmark"),
        };
    }

    private static ScaleConfig ReadScale(JsonElement e, string path)
    {
        var f = Fields(e, path, ScaleFields);
        var pTotal = Child(path, "robots_total");
        var pPer = Child(path, "robots_per_type");
        var pFac = Child(path, "facilities");
        var per = RangeInt(f["robots_per_type"], pPer, 0, 500);
        var total = ReadInt(f["robots_total"], pTotal);
        if (total != per * 3)
            throw new ArgumentException($"字段 {pTotal} 必须等于 {pPer}*3（三类机器人）");
        var fac = RangeInt(f["facilities"], pFac, 0, 256);
        if (total > 0 && fac < 4)
            throw new ArgumentException($"字段 {pFac} 在有机器人时至少为 4");
        return new ScaleConfig
        {
            RobotsTotal = total,
            RobotsPerType = per,
            Facilities = fac,
            RingRadius = RangeFloat(f["ring_radius"], Child(path, "ring_radius"), 0f, 10000f, openLo: true),
        };
    }

    private static TerrainConfig ReadTerrain(JsonElement e, string path)
    {
        var f = Fields(e, path, TerrainFields);
        return new TerrainConfig
        {
            Size = RangeFloat(f["size"], Child(path, "size"), 0f, 10000f, openLo: true),
            Segments = RangeInt(f["segments"], Child(path, "segments"), 1, 512),
            Mound = ReadFeature(f["mound"], Child(path, "mound")),
            MineralPit = ReadFeature(f["mineral_pit"], Child(path, "mineral_pit")),
        };
    }

    // Amount > 0 = 土坡高度，Amount < 0 = 凹陷深度（与 Main 相同语义）。
    private static FeatureConfig ReadFeature(JsonElement e, string path)
    {
        var f = Fields(e, path, FeatureFields);
        return new FeatureConfig
        {
            Position = ReadVector(f["position"], Child(path, "position"), 2),
            Radius = RangeFloat(f["radius"], Child(path, "radius"), 0f, 10000f, openLo: true),
            Amount = RangeFloat(f["amount"], Child(path, "amount"), -10000f, 10000f, openLo: false),
        };
    }

    private static CameraPathConfig ReadCamera(JsonElement e, string path)
    {
        var f = Fields(e, path, CameraFields);
        return new CameraPathConfig
        {
            Center = ReadVector(f["center"], Child(path, "center"), 3),
            Radius = RangeFloat(f["radius"], Child(path, "radius"), 0f, 10000f, openLo: true),
            Height = RangeFloat(f["height"], Child(path, "height"), 0f, 10000f, openLo: true),
            PeriodSeconds = RangeFloat(f["period_seconds"], Child(path, "period_seconds"), 0f, 10000f, openLo: true),
        };
    }

    private static QualityConfig ReadQuality(JsonElement e, string path)
    {
        var f = Fields(e, path, QualityFields);
        var msaa = ReadInt(f["msaa_3d"], Child(path, "msaa_3d"));
        if (msaa is not (0 or 2 or 4 or 8))
            throw new ArgumentException($"字段 {Child(path, "msaa_3d")} 只允许 0/2/4/8");
        return new QualityConfig
        {
            Msaa3d = msaa,
            Fxaa = ReadBool(f["fxaa"], Child(path, "fxaa")),
            Scaling3dScale = RangeFloat(f["scaling_3d_scale"], Child(path, "scaling_3d_scale"), 0f, 2f, openLo: true),
            Shadows = ReadBool(f["shadows"], Child(path, "shadows")),
        };
    }

    private static ResConfig ReadRes(JsonElement e, string path)
    {
        var f = Fields(e, path, ResFields);
        return new ResConfig
        {
            Width = RangeInt(f["width"], Child(path, "width"), 1, 8192),
            Height = RangeInt(f["height"], Child(path, "height"), 1, 8192),
        };
    }

    private static BenchConfig ReadBench(JsonElement e, string path)
    {
        var f = Fields(e, path, BenchFields);
        return new BenchConfig
        {
            DurationSeconds = RangeFloat(f["duration_seconds"], Child(path, "duration_seconds"), 0f, 3600f, openLo: true),
            FramesCsv = NonEmpty(f["frames_csv"], Child(path, "frames_csv")),
            SummaryJson = NonEmpty(f["summary_json"], Child(path, "summary_json")),
        };
    }

    // ---- 通用读取：键校验（未知/重复/遗漏，大小写不敏感） ----

    private static Dictionary<string, JsonElement> Fields(JsonElement obj, string path, string[] known)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(path.Length == 0 ? "fixture JSON 根必须是对象" : $"字段 {path} 必须是对象");
        var seen = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject())
        {
            var match = known.FirstOrDefault(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new ArgumentException($"未知字段 {Child(path, p.Name)}");
            if (seen.ContainsKey(match))
                throw new ArgumentException($"重复字段 {Child(path, p.Name)}");
            seen[match] = p.Value;
        }
        foreach (var k in known)
            if (!seen.ContainsKey(k))
                throw new ArgumentException($"缺少字段 {Child(path, k)}");
        return seen;
    }

    private static string Child(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static string NonEmpty(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Null)
            throw new ArgumentException($"字段 {path} 为显式 null");
        if (e.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"字段 {path} 必须是字符串");
        var s = e.GetString() ?? "";
        if (s.Length == 0)
            throw new ArgumentException($"字段 {path} 不能为空");
        return s;
    }

    private static int ReadInt(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Null)
            throw new ArgumentException($"字段 {path} 为显式 null");
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out var v))
            throw new ArgumentException($"字段 {path} 必须是整数");
        return v;
    }

    private static float ReadFloat(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Null)
            throw new ArgumentException($"字段 {path} 为显式 null");
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetSingle(out var v) || !float.IsFinite(v))
            throw new ArgumentException($"字段 {path} 必须是有限数值");
        return v;
    }

    private static bool ReadBool(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Null)
            throw new ArgumentException($"字段 {path} 为显式 null");
        if (e.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException($"字段 {path} 必须是布尔值");
        return e.ValueKind == JsonValueKind.True;
    }

    private static float[] ReadVector(JsonElement e, string path, int length)
    {
        if (e.ValueKind == JsonValueKind.Null)
            throw new ArgumentException($"字段 {path} 为显式 null");
        if (e.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"字段 {path} 必须是含 {length} 个元素的数组");
        var items = new List<float>();
        var i = 0;
        foreach (var item in e.EnumerateArray())
        {
            var v = ReadFloat(item, $"{path}[{i}]");
            if (v is < -10000f or > 10000f)
                throw new ArgumentException($"字段 {path}[{i}] 绝对值必须 <= 10000");
            items.Add(v);
            i++;
        }
        if (items.Count != length)
            throw new ArgumentException($"字段 {path} 必须恰好 {length} 个元素（当前 {items.Count}）");
        return items.ToArray();
    }

    private static int RangeInt(JsonElement e, string path, int lo, int hi)
    {
        var v = ReadInt(e, path);
        if (v < lo || v > hi)
            throw new ArgumentException($"字段 {path} 超出允许范围 [{lo}, {hi}]");
        return v;
    }

    private static float RangeFloat(JsonElement e, string path, float lo, float hi, bool openLo)
    {
        var v = ReadFloat(e, path);
        if (v < lo || v > hi || (openLo && v == lo))
            throw new ArgumentException($"字段 {path} 超出允许范围 {(openLo ? $"({lo}, {hi}]" : $"[{lo}, {hi}]")}");
        return v;
    }
}

public sealed class ScaleConfig
{
    [JsonPropertyName("robots_total")] public int RobotsTotal { get; set; } = 12;
    [JsonPropertyName("robots_per_type")] public int RobotsPerType { get; set; } = 4;
    [JsonPropertyName("facilities")] public int Facilities { get; set; } = 6;
    [JsonPropertyName("ring_radius")] public float RingRadius { get; set; } = 14f;
}

public sealed class TerrainConfig
{
    [JsonPropertyName("size")] public float Size { get; set; } = 60f;
    [JsonPropertyName("segments")] public int Segments { get; set; } = 64;
    [JsonPropertyName("mound")] public FeatureConfig Mound { get; set; } = new();
    [JsonPropertyName("mineral_pit")] public FeatureConfig MineralPit { get; set; } = new();
}

public sealed class FeatureConfig
{
    [JsonPropertyName("position")] public float[] Position { get; set; } = [12f, -10f];
    [JsonPropertyName("radius")] public float Radius { get; set; } = 6f;
    [JsonPropertyName("amount")] public float Amount { get; set; } = 1.8f;
}

public sealed class CameraPathConfig
{
    [JsonPropertyName("center")] public float[] Center { get; set; } = [0f, 0f, 0f];
    [JsonPropertyName("radius")] public float Radius { get; set; } = 24f;
    [JsonPropertyName("height")] public float Height { get; set; } = 13f;
    [JsonPropertyName("period_seconds")] public float PeriodSeconds { get; set; } = 40f;
}

public sealed class QualityConfig
{
    [JsonPropertyName("msaa_3d")] public int Msaa3d { get; set; } = 4;
    [JsonPropertyName("fxaa")] public bool Fxaa { get; set; } = false;
    [JsonPropertyName("scaling_3d_scale")] public float Scaling3dScale { get; set; } = 1f;
    [JsonPropertyName("shadows")] public bool Shadows { get; set; } = true;
}

public sealed class ResConfig
{
    [JsonPropertyName("width")] public int Width { get; set; } = 1920;
    [JsonPropertyName("height")] public int Height { get; set; } = 1200;
}

public sealed class BenchConfig
{
    [JsonPropertyName("duration_seconds")] public float DurationSeconds { get; set; } = 45f;
    [JsonPropertyName("frames_csv")] public string FramesCsv { get; set; } = "benchmarks/s_small_frames.csv";
    [JsonPropertyName("summary_json")] public string SummaryJson { get; set; } = "benchmarks/s_small_summary.json";
}
