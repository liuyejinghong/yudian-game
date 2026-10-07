#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace Yudian.Resources;
public sealed class BuildingDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double Radius { get; init; }
    public required double WorkSeconds { get; init; }
    public required Dictionary<string,int> Cost { get; init; }
}
public sealed class RecipeDefinition
{
    public required string Id { get; init; }
    public required Dictionary<string,int> Input { get; init; }
    public required Dictionary<string,int> Output { get; init; }
    public required double WorkSeconds { get; init; }
    public required double Power { get; init; }
}
public sealed class BootstrapConfig
{
    public required string Version { get; init; }
    public required Dictionary<string,int> Initial { get; init; }
    public required double MoveEnergyPerM { get; init; }
    public required double MoveWearPerM { get; init; }
    public required double WorkEnergyPerSecond { get; init; }
    public required double WorkWearPerSecond { get; init; }
    public required double ChargePerSecond { get; init; }
    public required double RepairPerSecond { get; init; }
    public required int RepairParts { get; init; }
    public required double RepairSeconds { get; init; }
    public required double Capacity { get; init; }
    public required int CargoCapacity { get; init; }
    public required double SolarOutput { get; init; }
    public required double DaySeconds { get; init; }
    public required double DaylightSeconds { get; init; }
    public required double ConnectionRangeM { get; init; }
    public required int ConnectionCables { get; init; }
    public required BuildingDefinition[] Buildings { get; init; }
    public required RecipeDefinition[] Recipes { get; init; }
    public void Validate()
    {
        void Amounts(Dictionary<string,int> values, bool zero = false)
        {
            if (values == null || values.Count == 0 || values.Any(x => !MaterialLedger.Materials.Contains(x.Key) || x.Value < (zero ? 0 : 1) || x.Value > 1000))
                throw new ArgumentException("自举物料配置无效");
        }
        Amounts(Initial,true);
        if (Initial.Count != MaterialLedger.Materials.Length || string.IsNullOrWhiteSpace(Version) || Version.Length > 100)
            throw new ArgumentException("自举配置身份无效");
        double[] rates = [MoveEnergyPerM,MoveWearPerM,WorkEnergyPerSecond,WorkWearPerSecond,ChargePerSecond,RepairPerSecond,RepairSeconds,Capacity,SolarOutput,DaySeconds,DaylightSeconds,ConnectionRangeM];
        if (rates.Any(x => !double.IsFinite(x) || x <= 0 || x > 1000) || DaylightSeconds > DaySeconds || CargoCapacity < 1 || CargoCapacity > 100 || ConnectionCables < 1 || ConnectionCables > 100 || RepairParts < 1 || RepairParts > 100)
            throw new ArgumentException("自举保障数值无效");
        string[] types = ["solar","processor","storage","charger","repair"];
        if (Buildings == null || Buildings.Length != 5 || !Buildings.Select(x => x.Id).Order().SequenceEqual(types.Order()))
            throw new ArgumentException("自举设施集合无效");
        foreach(var b in Buildings)
        {
            Amounts(b.Cost);
            if (string.IsNullOrWhiteSpace(b.Name) || b.Name.Length > 30 || !double.IsFinite(b.Radius) || b.Radius < 1 || b.Radius > 4 || !double.IsFinite(b.WorkSeconds) || b.WorkSeconds <= 0 || b.WorkSeconds > 60)
                throw new ArgumentException("设施配置无效");
        }
        if (Recipes == null || Recipes.Length != 4 || !Recipes.Select(x => x.Id).Order().SequenceEqual(new[]{"iron","copper","parts","cable"}.Order())) throw new ArgumentException("配方定义无效");
        foreach(var r in Recipes)
        { Amounts(r.Input); Amounts(r.Output); if (!double.IsFinite(r.WorkSeconds) || r.WorkSeconds <= 0 || r.WorkSeconds > 60 || !double.IsFinite(r.Power) || r.Power <= 0 || r.Power > 10) throw new ArgumentException("配方投入无效"); }
        // First complete support set plus processor; connections and two repairs are paid from finite startup stock.
        foreach(string material in MaterialLedger.Materials)
        {
            int need = Buildings.Where(x => x.Id != "storage").Sum(x => x.Cost.GetValueOrDefault(material));
            if (material == "cable") need += ConnectionCables * 3;
            if (material == "parts") need += RepairParts * 2;
            if (Initial[material] < need) throw new ArgumentException("首套自举物料不足：" + material);
        }
    }
}
