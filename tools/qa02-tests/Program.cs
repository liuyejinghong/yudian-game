// QA-02 输入合同测试：无 NuGet 依赖的 net10 console。
// 运行（票内命令，fixture 路径为实际工作树绝对路径）：
//   "$YUDIAN_DOTNET" run --project tools/qa02-tests/Qa02.Tests.csproj -- "$PWD/prototype/fixtures/s_small.json"
// 全部通过退出 0，任一失败退出非 0。输出中路径仅相对形式。
using System;
using System.Globalization;
using System.IO;
using Yudian;

namespace Qa02;

internal static class Program
{
    private static int _total;
    private static int _failed;

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("用法: dotnet run --project tools/qa02-tests/Qa02.Tests.csproj -- <fixture.json 路径>");
            return 2;
        }
        var fixturePath = Path.GetFullPath(args[0]);
        if (!File.Exists(fixturePath))
        {
            Console.Error.WriteLine($"fixture 不存在: {args[0]}");
            return 2;
        }
        var fixtureRel = Path.GetRelativePath(Environment.CurrentDirectory, fixturePath);

        FixtureTests(fixturePath, fixtureRel);
        CliTests();

        Console.WriteLine($"QA-02 合同测试: 共 {_total} 项, 失败 {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    // ---- 断言助手 ----

    private static void Check(bool ok, string name, string detail = "")
    {
        _total++;
        if (ok)
            Console.WriteLine($"PASS {name}");
        else
        {
            _failed++;
            Console.WriteLine($"FAIL {name}{(detail.Length > 0 ? $" — {detail}" : "")}");
        }
    }

    // 期望不抛异常
    private static void Ok(string name, Action act)
    {
        try { act(); Check(true, name); }
        catch (Exception e) { Check(false, name, $"不应抛异常，但抛了 {e.GetType().Name}: {e.Message}"); }
    }

    // 期望 ArgumentException 且消息含 expected（断言错误包含字段/选项名）
    private static void Err(string name, string expected, Action act)
    {
        _total++;
        try
        {
            act();
            _failed++;
            Console.WriteLine($"FAIL {name} — 未抛出异常");
        }
        catch (ArgumentException e)
        {
            var hit = e.Message.Contains(expected, StringComparison.Ordinal);
            if (!hit) _failed++;
            Console.WriteLine(hit ? $"PASS {name}" : $"FAIL {name} — 消息『{e.Message}』不含『{expected}』");
        }
        catch (Exception e)
        {
            _failed++;
            Console.WriteLine($"FAIL {name} — 异常类型 {e.GetType().Name} 不是 ArgumentException: {e.Message}");
        }
    }

    // ---- fixture 样例构造：各成员独立替换/删除 ----

    private const string MName = "\"name\": \"s_small\"";
    private const string MSeed = "\"seed\": 20261004";

    private static string MScaleOf(string total = "12", string per = "4", string fac = "6", string ring = "14.0") =>
        $"\"scale\": {{ \"robots_total\": {total}, \"robots_per_type\": {per}, \"facilities\": {fac}, \"ring_radius\": {ring} }}";

    private static string Mound(string pos = "[12.0, -10.0]", string radius = "6.0", string amount = "1.8") =>
        $"\"mound\": {{ \"position\": {pos}, \"radius\": {radius}, \"amount\": {amount} }}";

    private static string Pit(string pos = "[-14.0, 10.0]", string radius = "4.5", string amount = "-1.4") =>
        $"\"mineral_pit\": {{ \"position\": {pos}, \"radius\": {radius}, \"amount\": {amount} }}";

    private static string MTerrainOf(string size = "60.0", string segments = "64", string mound = null, string pit = null) =>
        $"\"terrain\": {{ \"size\": {size}, \"segments\": {segments}, {mound ?? Mound()}, {pit ?? Pit()} }}";

    private static string MCameraOf(string center = "[0.0, 0.0, 0.0]", string radius = "24.0", string height = "13.0", string period = "40.0") =>
        $"\"camera_path\": {{ \"center\": {center}, \"radius\": {radius}, \"height\": {height}, \"period_seconds\": {period} }}";

    private static string MQualityOf(string msaa = "4", string fxaa = "false", string scale = "1.0", string shadows = "true") =>
        $"\"quality\": {{ \"msaa_3d\": {msaa}, \"fxaa\": {fxaa}, \"scaling_3d_scale\": {scale}, \"shadows\": {shadows} }}";

    private static string MResOf(string width = "1920", string height = "1200") =>
        $"\"target_resolution\": {{ \"width\": {width}, \"height\": {height} }}";

    private static string MBenchOf(string dur = "45.0", string csv = "\"benchmarks/frames.csv\"", string sum = "\"benchmarks/summary.json\"") =>
        $"\"benchmark\": {{ \"duration_seconds\": {dur}, \"frames_csv\": {csv}, \"summary_json\": {sum} }}";

    private static string Fx(params string[] members) => "{\n  " + string.Join(",\n  ", members) + "\n}";

    // 允许尾逗号的变体（保持 AllowTrailingCommas）
    private static string FxTrailing(params string[] members) => "{\n  " + string.Join(",\n  ", members) + ",\n}";

    private static FixtureConfig Parse(string json) => FixtureConfig.ParseValidated(json);

    private static void FixtureTests(string fixturePath, string fixtureRel)
    {
        Console.WriteLine($"== fixture 合同 ({fixtureRel}) ==");

        // 正常 S 档：真实 fixture 文件
        FixtureConfig s = null;
        Ok("正常S档: 解析真实 s_small", () => { s = Parse(File.ReadAllText(fixturePath)); });
        Check(s != null && s.Name == "s_small" && s.Seed == 20261004, "正常S档: name/seed");
        Check(s != null && s.Scale.RobotsTotal == 12 && s.Scale.RobotsPerType == 4 && s.Scale.Facilities == 6
              && MathF.Abs(s.Scale.RingRadius - 14f) < 1e-5, "正常S档: 规模字段");
        Check(s != null && s.Terrain.Mound.Position.Length == 2 && MathF.Abs(s.Terrain.Mound.Position[0] - 12f) < 1e-5
              && MathF.Abs(s.Terrain.MineralPit.Amount + 1.4f) < 1e-5, "正常S档: 地形特征");
        Check(s != null && s.CameraPath.Center.Length == 3 && s.Quality.Msaa3d == 4
              && s.TargetResolution.Width == 1920 && MathF.Abs(s.Benchmark.DurationSeconds - 45f) < 1e-4
              && s.Benchmark.FramesCsv.Length > 0 && s.Benchmark.SummaryJson.Length > 0, "正常S档: 相机/质量/分辨率/输出");

        // 保持 Main 的 JSON 读取行为
        Ok("尾逗号允许", () => Parse(FxTrailing(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Ok("键大小写不敏感", () =>
        {
            var c = Parse(Fx("\"Name\": \"s_small\"", "\"SEED\": 5", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf()));
            if (c.Name != "s_small" || c.Seed != 5) throw new InvalidOperationException("大小写绑定失败");
        });

        // 规模合同
        Ok("零机器人: 0机0设施合法", () => Parse(Fx(MName, MSeed, MScaleOf("0", "0", "0"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Ok("边界值全合法: 500/类·256设施·各上限", () => Parse(Fx(MName, MSeed, MScaleOf("1500", "500", "256", "10000"),
            MTerrainOf("10000", "512", Mound("[10000, -10000]", "10000", "-10000")),
            MCameraOf("[10000, 0, -10000]", "10000", "10000", "10000"),
            MQualityOf("8", "true", "2", "false"), MResOf("8192", "8192"), MBenchOf("3600"))));
        Err("总量不一致: 13≠4*3", "scale.robots_total", () => Parse(Fx(MName, MSeed, MScaleOf("13", "4", "6"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("设施不足: 12机3设施", "scale.facilities", () => Parse(Fx(MName, MSeed, MScaleOf("12", "4", "3"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("每类超500", "scale.robots_per_type", () => Parse(Fx(MName, MSeed, MScaleOf("1503", "501", "6"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("每类为负", "scale.robots_per_type", () => Parse(Fx(MName, MSeed, MScaleOf("-3", "-1", "6"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("设施超256", "scale.facilities", () => Parse(Fx(MName, MSeed, MScaleOf("12", "4", "257"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("seed为负", "seed", () => Parse(Fx(MName, "\"seed\": -1", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("name为空", "name", () => Parse(Fx("\"name\": \"\"", MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));

        // 探针数值边界
        Err("ring_radius为0", "scale.ring_radius", () => Parse(Fx(MName, MSeed, MScaleOf("12", "4", "6", "0"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("ring_radius为负", "scale.ring_radius", () => Parse(Fx(MName, MSeed, MScaleOf("12", "4", "6", "-1"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("ring_radius超上限", "scale.ring_radius", () => Parse(Fx(MName, MSeed, MScaleOf("12", "4", "6", "10000.5"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("size为0", "terrain.size", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf("0"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("size超上限", "terrain.size", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf("10000.5"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("segments为0", "terrain.segments", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf("60.0", "0"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("segments超512", "terrain.segments", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf("60.0", "513"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("特征半径为0", "terrain.mound.radius", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(radius: "0")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("特征半径为负", "terrain.mineral_pit.radius", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(pit: Pit(radius: "-1")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("amount低于下限", "terrain.mound.amount", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(amount: "-10000.5")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("amount高于上限", "terrain.mineral_pit.amount", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(pit: Pit(amount: "10000.5")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));

        // 数组长度与元素
        Err("position元素数≠2(3个)", "terrain.mound.position", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(pos: "[1.0, 2.0, 3.0]")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("position元素数≠2(1个)", "terrain.mound.position", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(pos: "[1.0]")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("center元素数≠3(2个)", "camera_path.center", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(center: "[0.0, 0.0]"), MQualityOf(), MResOf(), MBenchOf())));
        Err("center元素数≠3(4个)", "camera_path.center", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(center: "[0.0, 0.0, 0.0, 0.0]"), MQualityOf(), MResOf(), MBenchOf())));
        Err("position元素超界", "terrain.mound.position[0]", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(pos: "[10000.5, 0.0]")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("center元素非有限(1e400溢出)", "camera_path.center[1]", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(center: "[0.0, 1e400, 0.0]"), MQualityOf(), MResOf(), MBenchOf())));
        Err("相机半径为0", "camera_path.radius", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(radius: "0"), MQualityOf(), MResOf(), MBenchOf())));
        Err("相机高度为0", "camera_path.height", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(height: "0"), MQualityOf(), MResOf(), MBenchOf())));
        Err("相机周期为0", "camera_path.period_seconds", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(period: "0"), MQualityOf(), MResOf(), MBenchOf())));

        // 显式 null
        Err("seed显式null", "seed", () => Parse(Fx(MName, "\"seed\": null", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("scale显式null", "scale", () => Parse(Fx(MName, MSeed, "\"scale\": null", MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("mound显式null", "terrain.mound", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: "\"mound\": null"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("position元素null", "terrain.mound.position[0]", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(pos: "[null, 1.0]")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("fxaa显式null", "quality.fxaa", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(fxaa: "null"), MResOf(), MBenchOf())));
        Err("frames_csv显式null", "benchmark.frames_csv", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("45.0", "null"))));

        // 遗漏字段
        Err("遗漏quality", "quality", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MResOf(), MBenchOf())));
        Err("遗漏seed", "seed", () => Parse(Fx(MName, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("遗漏mound", "terrain.mound", () => Parse(Fx(MName, MSeed, MScaleOf(),
            "\"terrain\": { \"size\": 60.0, \"segments\": 64, \"mineral_pit\": { \"position\": [-14.0, 10.0], \"radius\": 4.5, \"amount\": -1.4 } }",
            MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("遗漏summary_json", "benchmark.summary_json", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(),
            "\"benchmark\": { \"duration_seconds\": 45.0, \"frames_csv\": \"benchmarks/frames.csv\" }")));
        Err("遗漏target_resolution", "target_resolution", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MBenchOf())));

        // 重复键与未知键
        Err("重复键seed", "重复字段 seed", () => Parse(Fx(MName, "\"seed\": 20261004", "\"seed\": 7", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("嵌套重复键radius", "terrain.mound.radius", () => Parse(Fx(MName, MSeed, MScaleOf(),
            MTerrainOf(mound: "\"mound\": { \"position\": [12.0, -10.0], \"radius\": 6.0, \"radius\": 7.0, \"amount\": 1.8 }"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("大小写变体重复键", "Seed", () => Parse(Fx(MName, MSeed, "\"Seed\": 7", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("未知键顶层", "未知字段 robot_count", () => Parse(Fx(MName, MSeed, "\"robot_count\": 3", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("未知键嵌套", "scale.bogus", () => Parse(Fx(MName, MSeed,
            "\"scale\": { \"robots_total\": 12, \"robots_per_type\": 4, \"facilities\": 6, \"ring_radius\": 14.0, \"bogus\": 1 }",
            MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));

        // 质量与分辨率
        Err("非法MSAA(1)", "quality.msaa_3d", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(msaa: "1"), MResOf(), MBenchOf())));
        Err("非法MSAA(16)", "quality.msaa_3d", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(msaa: "16"), MResOf(), MBenchOf())));
        Ok("MSAA 0 合法", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(msaa: "0", scale: "0.5"), MResOf(), MBenchOf())));
        Err("scaling_3d_scale为0", "quality.scaling_3d_scale", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(scale: "0"), MResOf(), MBenchOf())));
        Err("scaling_3d_scale超2", "quality.scaling_3d_scale", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(scale: "2.01"), MResOf(), MBenchOf())));
        Err("width为0", "target_resolution.width", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf("0"), MBenchOf())));
        Err("height超8192", "target_resolution.height", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf("1920", "8193"), MBenchOf())));

        // 时长与输出字段
        Err("duration_seconds为0", "benchmark.duration_seconds", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("0"))));
        Err("duration_seconds超3600", "benchmark.duration_seconds", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("3600.5"))));
        Err("duration_seconds溢出(1e300)", "benchmark.duration_seconds", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("1e300"))));
        Err("frames_csv为空", "benchmark.frames_csv", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("45.0", "\"\""))));
        Err("summary_json为空", "benchmark.summary_json", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf("45.0", "\"a.csv\"", "\"\""))));

        // 类型错误与损坏 JSON
        Err("seed为布尔", "seed", () => Parse(Fx(MName, "\"seed\": true", MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("fxaa为数字", "quality.fxaa", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(fxaa: "1"), MResOf(), MBenchOf())));
        Err("width为字符串", "target_resolution.width", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf("\"1920\""), MBenchOf())));
        Err("name为数字", "name", () => Parse(Fx("\"name\": 5", MSeed, MScaleOf(), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("segments为小数", "terrain.segments", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf("60.0", "64.5"), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("position为字符串", "terrain.mound.position", () => Parse(Fx(MName, MSeed, MScaleOf(), MTerrainOf(mound: Mound(pos: "\"abc\"")), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())));
        Err("损坏JSON", "损坏", () => Parse("{oops"));
        Err("根不是对象", "根", () => Parse("[]"));
    }

    // ---- BenchmarkOptions CLI 合同 ----

    private static BenchmarkOptions P(params string[] user) => BenchmarkOptions.Parse(user, new string[0]);
    private static BenchmarkOptions PE(string[] user, string[] engine) => BenchmarkOptions.Parse(user, engine);

    private static void CliTests()
    {
        Console.WriteLine("== CLI 合同 (BenchmarkOptions) ==");

        var d = P();
        Check(!d.Benchmark && d.Duration == null && d.FixturePath == "fixtures/s_small.json"
              && d.FramesCsv == null && d.SummaryJson == null, "默认: 不触发/默认fixture/输出为null",
              $"实际 Benchmark={d.Benchmark} Duration={d.Duration} FixturePath={d.FixturePath}");

        Check(P("--benchmark").Benchmark, "user --benchmark 触发");
        Check(P("--yudian-benchmark").Benchmark, "user --yudian-benchmark 触发");
        Check(PE(new string[0], new[] { "--yudian-benchmark" }).Benchmark, "engine --yudian-benchmark 触发");
        Check(BenchmarkOptions.Parse(new string[0], new string[0], true).Benchmark, "环境变量env=true 触发");
        Check(!PE(new string[0], new[] { "--benchmark" }).Benchmark, "引擎裸 --benchmark 不触发且不报错");
        Check(PE(new[] { "--benchmark" }, new[] { "--benchmark" }).Benchmark, "user --benchmark + 引擎 --benchmark 触发");

        var du = P("--duration", "30");
        Check(du.Benchmark && du.Duration == 30.0, "--duration 触发且解析");
        var de = PE(new string[0], new[] { "--duration", "5" });
        Check(de.Benchmark && de.Duration == 5.0, "engine --duration 触发且解析");

        Check(PE(new[] { "--fixture", "u.json" }, new[] { "--fixture", "e.json" }).FixturePath == "u.json", "user --fixture 优先");
        Check(PE(new string[0], new[] { "--fixture", "e.json" }).FixturePath == "e.json", "engine --fixture 兜底");
        Check(PE(new[] { "--duration", "30" }, new[] { "--duration", "60" }).Duration == 30.0, "user --duration 优先");
        Check(PE(new[] { "--frames-csv", "u.csv" }, new[] { "--frames-csv", "e.csv", "--summary-json", "e.json" }).FramesCsv == "u.csv"
              && PE(new[] { "--frames-csv", "u.csv" }, new[] { "--frames-csv", "e.csv", "--summary-json", "e.json" }).SummaryJson == "e.json",
              "输出路径 user 覆盖 / engine 兜底");

        var noise = PE(new string[0], new[] { "--resolution", "1280x720", "--fullscreen", "--quit-after", "5", "-v" });
        Check(!noise.Benchmark && noise.FixturePath == "fixtures/s_small.json", "引擎 Godot 参数忽略");

        Err("user选项缺值(末尾)", "--fixture", () => P("--fixture"));
        Err("user选项缺值(下一项是选项)", "--duration", () => P("--duration", "--fixture", "x"));
        Err("engine选项缺值", "--summary-json", () => PE(new string[0], new[] { "--summary-json" }));
        Err("user选项空值", "--fixture", () => P("--fixture", ""));
        Err("user重复--fixture", "--fixture", () => P("--fixture", "a", "--fixture", "b"));
        Err("user重复--benchmark", "--benchmark", () => P("--benchmark", "--benchmark"));
        Err("user重复--yudian-benchmark", "--yudian-benchmark", () => P("--yudian-benchmark", "--yudian-benchmark"));
        Err("engine重复--duration", "--duration", () => PE(new string[0], new[] { "--duration", "1", "--duration", "2" }));
        Err("engine重复--yudian-benchmark", "--yudian-benchmark", () => PE(new string[0], new[] { "--yudian-benchmark", "--yudian-benchmark" }));
        Err("user未知选项", "--wat", () => P("--wat"));
        Err("user位置参数", "file.json", () => P("file.json"));
        Err("user -- 分隔符视为未知", "--", () => P("--"));

        Err("--duration NaN", "--duration", () => P("--duration", "NaN"));
        Err("--duration Infinity", "--duration", () => P("--duration", "Infinity"));
        Err("--duration 非数字", "--duration", () => P("--duration", "abc"));
        Err("--duration 为0", "--duration", () => P("--duration", "0"));
        Err("--duration 超3600", "--duration", () => P("--duration", "3600.5"));
        Err("--duration 为负", "--duration", () => P("--duration", "-1"));
        Check(P("--duration", "3600").Duration == 3600.0, "--duration 上界3600合法");
        Check(P("--duration", "0.5").Duration == 0.5, "--duration 小值合法");

        // 文化无关解析：当前文化用小数逗号，"2.5" 必须仍按 InvariantCulture 解析
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("de-DE");
            Check(P("--duration", "2.5").Duration == 2.5, "CLI duration 文化无关(de-DE下2.5)");
            Check(MathF.Abs(Parse(Fx(MName, MSeed, MScaleOf(ring: "14.5"), MTerrainOf(), MCameraOf(), MQualityOf(), MResOf(), MBenchOf())).Scale.RingRadius - 14.5f) < 1e-5,
                  "fixture 数值文化无关(de-DE下14.5)");
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }

        // 组合场景
        var full = PE(new[] { "--benchmark", "--duration", "12.5", "--fixture", "m.json", "--frames-csv", "f.csv", "--summary-json", "s.json" },
                      new[] { "--yudian-benchmark" });
        Check(full.Benchmark && full.Duration == 12.5 && full.FixturePath == "m.json"
              && full.FramesCsv == "f.csv" && full.SummaryJson == "s.json", "组合: 全选项生效");
    }
}
