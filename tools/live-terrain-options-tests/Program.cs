// LIVE-TERRAIN-OPTIONS 包A独立检查：无 NuGet 依赖的 net10 console，仅编译 BenchmarkOptions.cs。
// 运行（票内命令，工作树根目录）：
//   DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" run --project tools/live-terrain-options-tests/LiveTerrainOptions.Tests.csproj
// 全部通过退出 0，任一失败退出非 0。
using System;
using Yudian;

namespace LiveTerrainOptionsTests;

internal static class Program
{
    private static int _total;
    private static int _failed;

    private static int Main()
    {
        FlagTests();
        MutualExclusionTests();
        LegacyTests();

        Console.WriteLine($"live-terrain 选项检查: 共 {_total} 项, 失败 {_failed}");
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

    // 期望解析成功并核对 LiveTerrain/Benchmark/FixturePath
    private static void ParseOk(string name, string[] user, string[] engine, bool env = false,
        string liveExpected = "false", string benchExpected = "false", string fixtureExpected = null)
    {
        _total++;
        try
        {
            var o = BenchmarkOptions.Parse(user, engine, env);
            var live = o.LiveTerrain ? "true" : "false";
            var bench = o.Benchmark ? "true" : "false";
            var bad = live != liveExpected || bench != benchExpected
                   || (fixtureExpected != null && o.FixturePath != fixtureExpected);
            if (bad)
            {
                _failed++;
                Console.WriteLine($"FAIL {name} — LiveTerrain={live} Benchmark={bench} Fixture={o.FixturePath}");
            }
            else
                Console.WriteLine($"PASS {name}");
        }
        catch (Exception e)
        {
            _failed++;
            Console.WriteLine($"FAIL {name} — 不应抛异常，但抛了 {e.GetType().Name}: {e.Message}");
        }
    }

    // 期望 ArgumentException 且消息同时含 live-terrain 与 benchmark
    private static void Conflict(string name, string[] user, string[] engine, bool env = false)
    {
        _total++;
        try
        {
            BenchmarkOptions.Parse(user, engine, env);
            _failed++;
            Console.WriteLine($"FAIL {name} — 未抛出异常");
        }
        catch (ArgumentException e)
        {
            var hit = e.Message.Contains("live-terrain", StringComparison.Ordinal)
                   && e.Message.Contains("benchmark", StringComparison.Ordinal);
            if (!hit) _failed++;
            Console.WriteLine(hit ? $"PASS {name}" : $"FAIL {name} — 消息『{e.Message}』需同时含 live-terrain 与 benchmark");
        }
        catch (Exception e)
        {
            _failed++;
            Console.WriteLine($"FAIL {name} — 异常类型 {e.GetType().Name} 不是 ArgumentException: {e.Message}");
        }
    }

    // 期望 ArgumentException 且消息含 expected（重复/未知等旧错误）
    private static void Err(string name, string expected, string[] user, string[] engine, bool env = false)
    {
        _total++;
        try
        {
            BenchmarkOptions.Parse(user, engine, env);
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

    // ---- 用例 ----

    private static void FlagTests()
    {
        var none = Array.Empty<string>();
        ParseOk("user --live-terrain 启用", new[] { "--live-terrain" }, none, liveExpected: "true");
        ParseOk("engine --live-terrain 启用", none, new[] { "--live-terrain" }, liveExpected: "true");
        ParseOk("user+engine 同旗标跨 scope 不算重复", new[] { "--live-terrain" }, new[] { "--live-terrain" }, liveExpected: "true");
        Err("user 重复 --live-terrain", "重复参数 --live-terrain", new[] { "--live-terrain", "--live-terrain" }, none);
        Err("engine 重复 --live-terrain", "重复参数 --live-terrain", none, new[] { "--live-terrain", "--live-terrain" });
        Err("live-terrain 下 user 未知仍拒绝", "未知参数", new[] { "--live-terrain", "--bogus" }, none);
        ParseOk("live-terrain 下 engine Godot 参数仍忽略", none,
            new[] { "--live-terrain", "--verbose", "--path", "x" }, liveExpected: "true");
        // 裸 --benchmark 是 Godot 引擎选项，不触发游戏基准：可与 live-terrain 共存。
        ParseOk("engine 裸 --benchmark 不触发，与 live-terrain 共存",
            new[] { "--live-terrain" }, new[] { "--benchmark" }, liveExpected: "true", benchExpected: "false");
    }

    private static void MutualExclusionTests()
    {
        var none = Array.Empty<string>();
        Conflict("互斥: user --benchmark", new[] { "--live-terrain", "--benchmark" }, none);
        Conflict("互斥: user --yudian-benchmark", new[] { "--live-terrain", "--yudian-benchmark" }, none);
        Conflict("互斥: engine --yudian-benchmark", new[] { "--live-terrain" }, new[] { "--yudian-benchmark" });
        Conflict("互斥: engine --live-terrain + user --benchmark", new[] { "--benchmark" }, new[] { "--live-terrain" });
        Conflict("互斥: user --duration", new[] { "--live-terrain", "--duration", "5" }, none);
        Conflict("互斥: engine --duration", new[] { "--live-terrain" }, new[] { "--duration", "5" });
        Conflict("互斥: 环境开关", new[] { "--live-terrain" }, none, env: true);
    }

    private static void LegacyTests()
    {
        var none = Array.Empty<string>();
        ParseOk("旧默认: 无参数", none, none, fixtureExpected: "fixtures/s_small.json");
        ParseOk("旧默认: 环境开关仍触发 benchmark", none, none, env: true, benchExpected: "true");
        ParseOk("user --duration 仍触发 benchmark", new[] { "--duration", "5" }, none, benchExpected: "true");
        ParseOk("旧 fixture 优先级: user>engine", new[] { "--fixture", "a.json" }, new[] { "--fixture", "b.json" },
            fixtureExpected: "a.json");
        ParseOk("live-terrain 下 user fixture 优先", new[] { "--live-terrain", "--fixture", "a.json" },
            new[] { "--fixture", "b.json" }, liveExpected: "true", fixtureExpected: "a.json");
        ParseOk("live-terrain 下 engine fixture 兜底", new[] { "--live-terrain" }, new[] { "--fixture", "b.json" },
            liveExpected: "true", fixtureExpected: "b.json");
    }
}
