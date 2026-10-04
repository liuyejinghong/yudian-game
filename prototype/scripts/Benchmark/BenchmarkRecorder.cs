using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Yudian;

public sealed class BenchmarkRecorder : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _summaryPath;
    private readonly List<double> _frameTimes = new();
    private bool _closed;
    public long Frames => _frameTimes.Count;
    public double Elapsed { get; private set; }

    public BenchmarkRecorder(string framesPath, string summaryPath)
    {
        if (framesPath == summaryPath) throw new ArgumentException("frames_csv 与 summary_json 必须不同");
        if (File.Exists(framesPath) || File.Exists(summaryPath))
            throw new IOException("输出文件已存在；每次运行必须使用新路径");
        Directory.CreateDirectory(Path.GetDirectoryName(framesPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
        // 先核实 summary 目录可写，失败时不开始采样。
        string probe = summaryPath + ".probe-" + Guid.NewGuid().ToString("N");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write)) { }
        File.Delete(probe);
        _writer = new StreamWriter(new FileStream(framesPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _summaryPath = summaryPath;
        _writer.WriteLine("frame,time_s,frame_ms,fps");
        _writer.Flush();
    }

    public void Record(double frameMs)
    {
        if (_closed) throw new InvalidOperationException("benchmark 已关闭");
        if (!double.IsFinite(frameMs) || frameMs <= 0) throw new ArgumentException("frame_ms 必须有限且大于零");
        _frameTimes.Add(frameMs);
        Elapsed += frameMs / 1000;
        _writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{Frames},{Elapsed:0.000000},{frameMs:0.000000},{1000 / frameMs:0.000000}"));
        if (Frames % 300 == 0) _writer.Flush();
    }

    public void Finish(Dictionary<string, object> summary, string status)
    {
        if (_closed) return;
        _writer.Flush();
        _writer.Dispose();
        _closed = true;
        summary["status"] = status;
        summary["frames"] = Frames;
        summary["duration_actual_s"] = Elapsed;
        summary["avg_fps"] = Elapsed > 0 ? Frames / Elapsed : 0;
        if (Frames > 0)
        {
            var sorted = _frameTimes.OrderBy(x => x).ToArray();
            double Rank(double p) => sorted[(int)Math.Ceiling(sorted.Length * p) - 1];
            summary["frame_time_ms"] = new { p50 = Rank(.50), p95 = Rank(.95), p99 = Rank(.99), max = sorted[^1], over_33ms = sorted.Count(x => x > 33), method = "nearest_rank_all_frames_including_startup" };
        }
        string temporary = _summaryPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _summaryPath, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Dispose()
    {
        if (!_closed) { _writer.Dispose(); _closed = true; }
    }
}
