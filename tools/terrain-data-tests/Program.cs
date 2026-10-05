using System;
using System.Globalization;

namespace TerrainData.Tests;

internal static class Program
{
    public static int Main()
    {
        int failedTotal = 0;
        // 合同要求至少两个 CultureInfo 环境：全量检查在不变文化与 de-DE（逗号小数点）下各跑一遍。
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE") })
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.WriteLine($"== culture: '{culture.Name}' / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ==");
            failedTotal += TerrainDataContractTests.RunAll();
        }

        Console.WriteLine(failedTotal == 0 ? "ALL CHECKS PASSED" : $"FAILED CHECKS: {failedTotal}");
        return failedTotal == 0 ? 0 : 1;
    }
}
