using System.Text.Json;
using Yudian;
var temp = Path.Combine(Path.GetTempPath(), "yudian-recorder-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var csv = Path.Combine(temp,"frames.csv"); var summary = Path.Combine(temp,"summary.json");
    using (var r = new BenchmarkRecorder(csv, summary))
    {
        foreach (double ms in new double[] { 8, 10, 20, 40 }) r.Record(ms);
        r.Finish(new Dictionary<string,object>(), "completed");
        r.Finish(new Dictionary<string,object>(), "interrupted");
    }
    using var doc = JsonDocument.Parse(File.ReadAllText(summary));
    var root = doc.RootElement;
    if (root.GetProperty("status").GetString() != "completed" || root.GetProperty("frames").GetInt32() != 4 ||
        root.GetProperty("frame_time_ms").GetProperty("p50").GetDouble() != 10 ||
        root.GetProperty("frame_time_ms").GetProperty("p95").GetDouble() != 40 ||
        root.GetProperty("frame_time_ms").GetProperty("over_33ms").GetInt32() != 1) throw new Exception("nearest-rank/all-frames mismatch");
    bool rejected=false;
    try { using var r = new BenchmarkRecorder(csv, summary); } catch(IOException) { rejected=true; }
    if (!rejected) throw new Exception("existing output overwritten");
    rejected=false;
    try { using var r = new BenchmarkRecorder(Path.Combine(temp,"same"),Path.Combine(temp,"same")); } catch(ArgumentException) { rejected=true; }
    if (!rejected) throw new Exception("identical paths accepted");
    using (var r = new BenchmarkRecorder(Path.Combine(temp,"interrupted.csv"),Path.Combine(temp,"interrupted.json")))
    {
        rejected=false;
        try { r.Record(double.NaN); } catch(ArgumentException) { rejected=true; }
        if (!rejected) throw new Exception("invalid frame accepted");
        r.Record(5); r.Finish(new Dictionary<string,object>(),"interrupted");
    }
    using var interrupted = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp,"interrupted.json")));
    if(interrupted.RootElement.GetProperty("status").GetString() != "interrupted") throw new Exception("interruption lost");
    // 所有句柄已经关闭，文件可以重新独占打开。
    using (new FileStream(csv,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
    Console.WriteLine("BENCHMARK_RECORDER_OK: known ranks, interrupted state, invalid sample, existing/same paths rejected, handles closed");
}
finally { Directory.Delete(temp,true); }
