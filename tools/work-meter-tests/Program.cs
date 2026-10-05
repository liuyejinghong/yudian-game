// level-job-r1 GLM包B：WorkMeter 合同自测。独立 console、零外部包、无测试框架，
// 非 0 退出码表示有失败检查。所有断言值均选二进制精确 double，等值比较为设计意图。
using System;
using System.Globalization;
using Yudian.Construction;

namespace Yudian.Construction.Tests;

/// <summary>最小检查器：独立 console，无测试框架；非 0 退出码表示存在失败检查。</summary>
internal static class Check
{
    public static int Failed;
    public static int Total;

    public static void Ok(string name, Action check)
    {
        Total++;
        try
        {
            check();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            Failed++;
            Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal static class Program
{
    public static int Main()
    {
        int failedTotal = 0;
        // 与既有合同测试一致：全量检查在不变文化与 de-DE（逗号小数点）下各跑一遍。
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE") })
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.WriteLine($"== culture: '{culture.Name}' / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ==");
            failedTotal += WorkMeterChecks.RunAll();
        }

        Console.WriteLine(Check.Total > 0
            ? $"checks run: {Check.Total}, failed: {failedTotal}"
            : "no checks ran");
        Console.WriteLine(failedTotal == 0 ? "ALL CHECKS PASSED" : $"FAILED CHECKS: {failedTotal}");
        return failedTotal == 0 ? 0 : 1;
    }
}

internal static class WorkMeterChecks
{
    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal(double expected, double actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{message} (expected {expected}, got {actual})");
        }
    }

    private static ArgumentException CapturedArgumentException(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("expected ArgumentException was not thrown");
    }

    public static int RunAll()
    {
        int failedBefore = Check.Failed;

        // —— 构造边界：requiredSeconds 有限 (0, 3600] ——
        Check.Ok("ctor_fresh_meter_state", () =>
        {
            var m = new WorkMeter(3.0);
            Equal(0.0, m.ElapsedSeconds, "fresh ElapsedSeconds");
            Equal(0.0, m.Fraction, "fresh Fraction");
            True(!m.IsComplete, "fresh IsComplete");
        });

        Check.Ok("ctor_accepts_smallest_positive", () =>
        {
            var m = new WorkMeter(double.Epsilon);
            m.Advance(double.Epsilon, eligible: true);
            True(m.IsComplete, "Epsilon requirement completes after one positive eligible advance");
            Equal(double.Epsilon, m.ElapsedSeconds, "elapsed at Epsilon cap");
        });

        Check.Ok("ctor_accepts_exact_3600", () =>
        {
            var m = new WorkMeter(3600.0);
            True(!m.IsComplete, "3600 requirement starts incomplete");
        });

        Check.Ok("ctor_rejects_zero", () =>
        {
            CapturedArgumentException(() => new WorkMeter(0.0));
        });

        Check.Ok("ctor_rejects_negative", () =>
        {
            CapturedArgumentException(() => new WorkMeter(-0.5));
        });

        Check.Ok("ctor_rejects_above_3600", () =>
        {
            CapturedArgumentException(() => new WorkMeter(3600.5));
            CapturedArgumentException(() => new WorkMeter(3601.0));
        });

        Check.Ok("ctor_rejects_nan_and_infinities", () =>
        {
            CapturedArgumentException(() => new WorkMeter(double.NaN));
            CapturedArgumentException(() => new WorkMeter(double.PositiveInfinity));
            CapturedArgumentException(() => new WorkMeter(double.NegativeInfinity));
        });

        // —— Advance 边界：delta 有限 [0, 3600] ——
        Check.Ok("advance_accepts_zero_and_3600_boundary", () =>
        {
            var m = new WorkMeter(3600.0);
            m.Advance(0.0, eligible: true);
            Equal(0.0, m.ElapsedSeconds, "zero delta no-op");
            var big = new WorkMeter(3600.0);
            big.Advance(3600.0, eligible: true);
            True(big.IsComplete, "single max delta completes max requirement");
        });

        Check.Ok("advance_rejects_negative_delta", () =>
        {
            var m = new WorkMeter(3.0);
            CapturedArgumentException(() => m.Advance(-0.5, eligible: true));
            CapturedArgumentException(() => m.Advance(-double.Epsilon, eligible: false));
        });

        Check.Ok("advance_rejects_above_3600", () =>
        {
            var m = new WorkMeter(3.0);
            CapturedArgumentException(() => m.Advance(3600.5, eligible: true));
            CapturedArgumentException(() => m.Advance(3601.0, eligible: false));
        });

        Check.Ok("advance_rejects_nan_and_infinities", () =>
        {
            var m = new WorkMeter(3.0);
            CapturedArgumentException(() => m.Advance(double.NaN, eligible: true));
            CapturedArgumentException(() => m.Advance(double.PositiveInfinity, eligible: true));
            CapturedArgumentException(() => m.Advance(double.NegativeInfinity, eligible: false));
        });

        // —— 累计 ——
        Check.Ok("accumulate_eligible_advances", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(1.0, eligible: true);
            Equal(1.0, m.ElapsedSeconds, "after first");
            m.Advance(1.5, eligible: true);
            Equal(2.5, m.ElapsedSeconds, "after second");
            Equal(2.5 / 3.0, m.Fraction, "fraction matches elapsed over required");
            True(m.Fraction >= 0.0 && m.Fraction <= 1.0, "fraction within [0,1]");
            True(!m.IsComplete, "not complete below requirement");
        });

        // —— 中断清零：未完成且 eligible=false ——
        Check.Ok("ineligible_clears_partial_progress", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(1.0, eligible: true);
            m.Advance(1.5, eligible: true);
            m.Advance(10.0, eligible: false);
            Equal(0.0, m.ElapsedSeconds, "reset to zero");
            Equal(0.0, m.Fraction, "fraction reset to zero");
            True(!m.IsComplete, "reset meter is not complete");
            m.Advance(0.25, eligible: true);
            Equal(0.25, m.ElapsedSeconds, "re-accumulate from zero");
        });

        // —— 封顶：累计不超过 required，Fraction 恒 [0,1] ——
        Check.Ok("cap_at_required_on_overshoot", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(2.0, eligible: true);
            m.Advance(5.0, eligible: true);
            Equal(3.0, m.ElapsedSeconds, "capped exactly at required");
            Equal(1.0, m.Fraction, "fraction exactly 1");
            True(m.IsComplete, "complete at cap");
        });

        // —— 完成幂等：完成后合法 Advance（含 eligible=false）保持完成 ——
        Check.Ok("complete_survives_eligible_advance", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(3.0, eligible: true);
            m.Advance(1.0, eligible: true);
            Equal(3.0, m.ElapsedSeconds, "elapsed stays at required");
            True(m.IsComplete, "still complete");
        });

        Check.Ok("complete_survives_ineligible_advance", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(3.0, eligible: true);
            m.Advance(1.0, eligible: false);
            Equal(3.0, m.ElapsedSeconds, "completion is not cleared by ineligible advance");
            Equal(1.0, m.Fraction, "fraction stays 1");
            True(m.IsComplete, "still complete");
        });

        // —— 非法无副作用：抛出后状态不变，可继续正常推进 ——
        Check.Ok("illegal_delta_leaves_state_unchanged", () =>
        {
            var m = new WorkMeter(3.0);
            m.Advance(1.0, eligible: true);
            m.Advance(1.5, eligible: true);
            CapturedArgumentException(() => m.Advance(double.NaN, eligible: true));
            CapturedArgumentException(() => m.Advance(-1.0, eligible: false));
            Equal(2.5, m.ElapsedSeconds, "elapsed unchanged after throws");
            m.Advance(0.5, eligible: true);
            Equal(3.0, m.ElapsedSeconds, "legal advance continues from unchanged state");
            True(m.IsComplete, "completed after recovery");
        });

        // —— 新任务新 meter：实例间零共享 ——
        Check.Ok("independent_meters_no_shared_state", () =>
        {
            var a = new WorkMeter(3.0);
            var b = new WorkMeter(3.0);
            a.Advance(2.0, eligible: true);
            Equal(0.0, b.ElapsedSeconds, "b untouched by a");
            b.Advance(1.0, eligible: false);
            Equal(2.0, a.ElapsedSeconds, "a untouched by b");
            b.Advance(3.0, eligible: true);
            True(b.IsComplete && !a.IsComplete, "b completes independently of a");
        });

        return Check.Failed - failedBefore;
    }
}
