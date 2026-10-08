#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Yudian.Resources;

namespace Yudian.Goals;

public sealed record ProductionStep(string Kind, string Material, int Quantity);
public sealed record ProductionPlanResult(bool Feasible, string Reason, ProductionStep[] Steps);

// Pure net-gap planning over caller-merged stock+own-transit; quantities are aggregate
// (mine=ore units, recipe=batches) and Main chunks execution and re-plans per transaction.
public static class ProductionPlan
{
    public static ProductionPlanResult Create(Dictionary<string,int> demand, Dictionary<string,int> available, RecipeDefinition[] recipes, Dictionary<string,int> remainingOre)
    {
        Validate(demand, available, recipes, remainingOre);
        var stock = new Dictionary<string,long>(StringComparer.Ordinal);
        foreach (var m in MaterialLedger.Materials) stock[m] = available.GetValueOrDefault(m);
        var producer = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        foreach (var r in recipes)
            foreach (var o in r.Output) producer.Add(o.Key, r);

        var total = new Dictionary<string,long>(StringComparer.Ordinal);
        foreach (var d in demand) total[d.Key] = d.Value;

        // Post-order DFS over demanded chains yields production order; an active node revisited is a cycle.
        var state = new Dictionary<string,int>(StringComparer.Ordinal);
        var order = new List<string>();
        string? cyclic = null;
        void Visit(string m)
        {
            if (cyclic != null) return;
            var s = state.GetValueOrDefault(m);
            if (s == 1) { cyclic = m; return; }
            if (s == 2) return;
            state[m] = 1;
            if (producer.TryGetValue(m, out var r))
                foreach (var i in r.Input.Keys) Visit(i);
            state[m] = 2;
            order.Add(m);
        }
        foreach (var d in demand)
            if (d.Value - available.GetValueOrDefault(d.Key) > 0) Visit(d.Key);
        if (cyclic != null) return new(false, $"配方存在循环依赖：{cyclic}", Array.Empty<ProductionStep>());

        var batches = new Dictionary<string,long>(StringComparer.Ordinal);
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var m = order[i];
            if (!producer.TryGetValue(m, out var r)) continue;
            var unmet = total.GetValueOrDefault(m) - stock[m];
            if (unmet <= 0) continue;
            var per = r.Output[m];
            var b = (unmet + per - 1) / per;
            batches[m] = b;
            foreach (var inp in r.Input)
            {
                var next = total.GetValueOrDefault(inp.Key) + (long)inp.Value * b;
                if (next > MaterialLedger.Limit)
                    return new(false, $"计划需求{inp.Key}超出数量上限{MaterialLedger.Limit}", Array.Empty<ProductionStep>());
                total[inp.Key] = next;
            }
        }

        var steps = new List<ProductionStep>();
        foreach (var m in order)
        {
            var t = total.GetValueOrDefault(m);
            if (!producer.ContainsKey(m))
            {
                var mine = t - Math.Min(t, stock[m]);
                if (mine > 0)
                {
                    var left = remainingOre.GetValueOrDefault(m);
                    if (left < mine)
                        return new(false, remainingOre.ContainsKey(m)
                            ? $"矿点{m}剩余不足：尚需{mine}，仅存{left}"
                            : $"物料{m}无生产来源，缺口{mine}", Array.Empty<ProductionStep>());
                    steps.Add(new("mine", m, (int)mine));
                }
                continue;
            }
            if (batches.GetValueOrDefault(m) > 0) steps.Add(new("recipe", m, (int)batches[m]));
        }
        if (steps.Count > 128) return new(false, "计划步骤超出单次上限128", Array.Empty<ProductionStep>());
        return new(true, "可行", steps.ToArray());
    }

    private static void Validate(Dictionary<string,int>? demand, Dictionary<string,int>? available, RecipeDefinition[]? recipes, Dictionary<string,int>? remainingOre)
    {
        void Amounts(Dictionary<string,int>? values, int min, string why)
        {
            if (values == null || values.Count == 0 && min > 0 ||
                values.Any(x => !MaterialLedger.Materials.Contains(x.Key) || x.Value < min || x.Value > MaterialLedger.Limit))
                throw new ArgumentException(why);
        }
        Amounts(demand, 0, "需求数量无效");
        Amounts(available, 0, "可用数量无效");
        Amounts(remainingOre, 0, "矿量数量无效");
        if (recipes == null || recipes.Length != 4) throw new ArgumentException("配方数量无效");
        foreach (var r in recipes)
        {
            if (r == null) throw new ArgumentException("配方定义无效");
            Amounts(r.Input, 1, "配方投入无效");
            if (r.Output == null || r.Output.Count != 1 || r.Output.ContainsKey("kit") ||
                r.Output.Any(x => !MaterialLedger.Materials.Contains(x.Key) || x.Value < 1 || x.Value > MaterialLedger.Limit))
                throw new ArgumentException("配方产出无效");
        }
        var seen = new HashSet<string>();
        foreach (var r in recipes)
            foreach (var o in r.Output.Keys)
                if (!seen.Add(o)) throw new ArgumentException("配方产出重复");
    }
}
