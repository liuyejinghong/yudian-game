using System;
using System.Globalization;

namespace Navigation.Tests;

internal static class Program
{
    public static int Main()
    {
        int failedTotal = 0;
        // 与 terrain-data-tests 同一口径：不变文化与 de-DE（逗号小数点）下各跑一遍，
        // 证明导航结果与区域文化无关。
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE") })
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.WriteLine($"== culture: '{culture.Name}' / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ==");
            failedTotal += NavigationContractTests.RunAll();
        }

        Console.WriteLine(failedTotal == 0 ? "ALL CHECKS PASSED" : $"FAILED CHECKS: {failedTotal}");
        return failedTotal == 0 ? 0 : 1;
    }
}
