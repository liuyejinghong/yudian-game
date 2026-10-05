using System;
using System.Collections.Generic;
using System.Globalization;

namespace Yudian;

// 只解析参数；资源读取和输出写入由入口处理。
// userArgs = "--" 之后的用户参数，严格校验（未知/重复/缺值即错）；
// engineArgs = 引擎参数，Godot 选项忽略，仅识别 Yudian 选项并拒绝其重复/缺值；同名 user 优先。
// 裸 --benchmark 是 Godot 引擎选项，被引擎消费，不触发游戏基准。
public sealed class BenchmarkOptions
{
    /// <summary>是否进入基准模式：user --benchmark、任一 --yudian-benchmark、--duration 或环境开关。</summary>
    public bool Benchmark { get; set; }

    /// <summary>权威地形新路径：任一 scope 的无值旗标 --live-terrain；与 Benchmark 互斥。</summary>
    public bool LiveTerrain { get; set; }

    /// <summary>--duration 解析结果（InvariantCulture、有限、(0,3600]）；未提供为 null。</summary>
    public double? Duration { get; set; }

    /// <summary>fixture 路径，默认 fixtures/s_small.json。</summary>
    public string FixturePath { get; set; } = "fixtures/s_small.json";

    /// <summary>--frames-csv；未提供为 null（回退 fixture 配置由入口接线负责）。</summary>
    public string FramesCsv { get; set; } = null!;

    /// <summary>--summary-json；未提供为 null。</summary>
    public string SummaryJson { get; set; } = null!;

    private const string BenchmarkFlag = "--benchmark";
    private const string YudianBenchmarkFlag = "--yudian-benchmark";
    private const string LiveTerrainFlag = "--live-terrain";
    private static readonly string[] ValuedOptions = { "--duration", "--fixture", "--frames-csv", "--summary-json" };

    public static BenchmarkOptions Parse(string[] userArgs, string[] engineArgs, bool environmentBenchmark = false)
    {
        var user = Scan(userArgs, userScope: true);
        var engine = Scan(engineArgs, userScope: false);

        string Value(string option) =>
            user.TryGetValue(option, out var uv) ? uv
            : engine.TryGetValue(option, out var ev) ? ev
            : null;

        double? duration = null;
        var rawDuration = Value("--duration");
        if (rawDuration != null)
        {
            // 与线程当前文化无关：固定 InvariantCulture。
            if (!double.TryParse(rawDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                || !double.IsFinite(d) || d <= 0 || d > 3600)
                throw new ArgumentException($"--duration 值无效: {rawDuration}（需 (0,3600] 内的有限数）");
            duration = d;
        }

        var benchmark = user.ContainsKey(BenchmarkFlag)
                        || user.ContainsKey(YudianBenchmarkFlag)
                        || engine.ContainsKey(YudianBenchmarkFlag)
                        || duration != null
                        || environmentBenchmark;
        var liveTerrain = user.ContainsKey(LiveTerrainFlag) || engine.ContainsKey(LiveTerrainFlag);
        if (benchmark && liveTerrain)
            throw new ArgumentException("--live-terrain 与 benchmark 互斥（--benchmark/--yudian-benchmark/--duration/环境开关）");

        return new BenchmarkOptions
        {
            Benchmark = benchmark,
            LiveTerrain = liveTerrain,
            Duration = duration,
            FixturePath = Value("--fixture") ?? "fixtures/s_small.json",
            FramesCsv = Value("--frames-csv"),
            SummaryJson = Value("--summary-json"),
        };
    }

    // 返回 选项 -> 值（旗标值为 ""）。
    private static Dictionary<string, string> Scan(string[] args, bool userScope)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args == null)
            return found;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == BenchmarkFlag)
            {
                if (!userScope)
                    continue; // 裸 --benchmark 属引擎，不触发游戏
                if (found.ContainsKey(arg))
                    throw new ArgumentException($"重复参数 {arg}");
                found[arg] = "";
            }
            else if (arg == YudianBenchmarkFlag)
            {
                if (found.ContainsKey(arg))
                    throw new ArgumentException($"重复参数 {arg}");
                found[arg] = "";
            }
            else if (arg == LiveTerrainFlag)
            {
                if (found.ContainsKey(arg))
                    throw new ArgumentException($"重复参数 {arg}");
                found[arg] = "";
            }
            else if (Array.IndexOf(ValuedOptions, arg) >= 0)
            {
                if (found.ContainsKey(arg))
                    throw new ArgumentException($"重复参数 {arg}");
                if (i + 1 >= args.Length || args[i + 1].Length == 0
                    || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"参数 {arg} 缺少值");
                found[arg] = args[++i];
            }
            else if (userScope)
            {
                throw new ArgumentException($"未知参数 {arg}");
            }
            // engine 范围：其余 Godot 参数静默忽略
        }
        return found;
    }
}
