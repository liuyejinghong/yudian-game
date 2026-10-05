using System;
using System.Globalization;

namespace TerrainStage.Tests;

internal static class Program
{
    public static int Main()
    {
        int failedTotal = 0;
        // 合同要求至少两种 CultureInfo：全量检查在不变文化与 de-DE（逗号小数点）下各跑一遍。
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE") })
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.WriteLine($"== culture: '{culture.Name}' / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ==");
            failedTotal += TerrainStageContractTests.RunAll();
        }

        Console.WriteLine(Check.Total > 0
            ? $"checks run: {Check.Total}, failed: {failedTotal}"
            : "no checks ran");
        Console.WriteLine(failedTotal == 0 ? "ALL CHECKS PASSED" : $"FAILED CHECKS: {failedTotal}");
        return failedTotal == 0 ? 0 : 1;
    }
}
