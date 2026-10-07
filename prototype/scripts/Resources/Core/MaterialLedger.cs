#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Yudian.Resources;

public sealed record ContainerStock(string Id, int Capacity, Dictionary<string, int> Items);
public sealed record MaterialReservation(string Goal, string Container, string Material, int Quantity);
public sealed record LedgerSnapshot(ContainerStock[] Containers, MaterialReservation[] Reservations, Dictionary<string, string> Operations);

// One simulation-thread writer. Cargo and spent materials remain physical custody locations.
public sealed class MaterialLedger
{
    public static readonly string[] Materials = ["iron_ore", "copper_ore", "iron", "copper", "parts", "cable", "kit"];
    public const int Limit = 100_000;
    private readonly Dictionary<string, ContainerStock> _containers;
    private readonly List<MaterialReservation> _reservations;
    private readonly Dictionary<string, string> _operations;

    public MaterialLedger(LedgerSnapshot snapshot)
    {
        if (snapshot?.Containers == null || snapshot.Reservations == null || snapshot.Operations == null ||
            snapshot.Containers.Length > 1000 || snapshot.Reservations.Length > 1000 || snapshot.Operations.Count > 10_000)
            throw new ArgumentException("物料快照大小或字段无效");
        _containers = new(StringComparer.Ordinal);
        foreach (var c in snapshot.Containers)
        {
            if (c == null || !ValidId(c.Id) || c.Capacity < 0 || c.Capacity > Limit || c.Items == null ||
                c.Items.Any(x => !Materials.Contains(x.Key) || x.Value < 0 || x.Value > Limit) ||
                c.Items.Values.Sum(x => (long)x) > c.Capacity || !_containers.TryAdd(c.Id, new(c.Id, c.Capacity, new(c.Items))))
                throw new ArgumentException("物料容器无效");
        }
        _reservations = new(snapshot.Reservations);
        foreach (var r in _reservations)
            if (r == null || !ValidId(r.Goal) || !_containers.ContainsKey(r.Container) || !Materials.Contains(r.Material) || r.Quantity <= 0 || r.Quantity > Limit)
                throw new ArgumentException("物料预约无效");
        if (_reservations.Select(x => (x.Goal, x.Container, x.Material)).Distinct().Count() != _reservations.Count ||
            _reservations.GroupBy(x => (x.Container, x.Material)).Any(g => g.Sum(x => (long)x.Quantity) > Count(g.Key.Container, g.Key.Material)))
            throw new ArgumentException("物料预约超过现货");
        if (snapshot.Operations.Any(x => !ValidId(x.Key) || x.Value == null || x.Value.Length > 2000))
            throw new ArgumentException("物料操作记录无效");
        _operations = new(snapshot.Operations, StringComparer.Ordinal);
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 100 && !id.Contains('|');
    private ContainerStock Container(string id) => _containers.TryGetValue(id, out var c) ? c : throw new ArgumentException("未知物料容器");
    public int Count(string container, string material) => Container(container).Items.GetValueOrDefault(material);
    public int Load(string container) => Container(container).Items.Values.Sum();
    public int Available(string container, string material) => Count(container, material) - _reservations.Where(x => x.Container == container && x.Material == material).Sum(x => x.Quantity);
    public int Reserved(string goal, string container, string material) => _reservations.Where(x => x.Goal == goal && x.Container == container && x.Material == material).Sum(x => x.Quantity);
    public static int NetNeed(int demand, int available, int ownTransit)
    {
        if (new[] { demand, available, ownTransit }.Any(x => x < 0 || x > Limit)) throw new ArgumentException("需求数量无效");
        return (int)Math.Max(0L, (long)demand - available - ownTransit);
    }
    public void AddContainer(string id, int capacity)
    {
        if (!ValidId(id) || capacity < 0 || capacity > Limit || _containers.ContainsKey(id) || _containers.Count >= 1000)
            throw new ArgumentException("容器登记无效");
        _containers.Add(id, new(id, capacity, new()));
    }
    public void Reserve(string goal, string container, Dictionary<string, int> amounts)
    {
        ValidateAmounts(amounts); Container(container);
        if (!ValidId(goal) || amounts.Any(x => Reserved(goal, container, x.Key) != 0 || Available(container, x.Key) < x.Value))
            throw new InvalidOperationException("库存不足或预约重复");
        foreach (var a in amounts) _reservations.Add(new(goal, container, a.Key, a.Value));
    }
    public void Release(string goal) => _reservations.RemoveAll(x => x.Goal == goal);
    public void Transfer(string operation, string goal, string source, string destination, Dictionary<string, int> amounts)
    {
        ValidateAmounts(amounts);
        if (!ValidId(operation) || !ValidId(goal) || source == destination) throw new ArgumentException("物料交接身份无效");
        var from = Container(source); var to = Container(destination);
        string signature = Signature(goal, source, destination, amounts);
        if (_operations.TryGetValue(operation, out var prior))
        { if (prior != signature) throw new InvalidOperationException("同一操作身份对应不同交接"); return; }
        if (_operations.Count >= 10_000) throw new InvalidOperationException("本档交易上限，未交接");
        if (Load(destination) + (long)amounts.Values.Sum() > to.Capacity ||
            amounts.Any(x => Available(source, x.Key) + Reserved(goal, source, x.Key) < x.Value))
            throw new InvalidOperationException("物料不足、属于其他目标或目的容量不足");
        // All checks precede the single-thread atomic mutation; reservation follows the physical transfer.
        foreach (var a in amounts)
        {
            from.Items[a.Key] = Count(source, a.Key) - a.Value;
            to.Items[a.Key] = Count(destination, a.Key) + a.Value;
            int index = _reservations.FindIndex(x => x.Goal == goal && x.Container == source && x.Material == a.Key);
            if (index >= 0)
            {
                var r = _reservations[index]; int remaining = Math.Max(0, r.Quantity - a.Value);
                if (remaining == 0) _reservations.RemoveAt(index); else _reservations[index] = r with { Quantity = remaining };
            }
        }
        _operations.Add(operation, signature);
    }
    internal static string Signature(string goal,string source,string destination,Dictionary<string,int> amounts)
        => goal+"|"+source+"|"+destination+"|"+string.Join(",",amounts.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+":"+x.Value));
    private static void ValidateAmounts(Dictionary<string, int> amounts)
    {
        if (amounts == null || amounts.Count == 0 || amounts.Any(x => !Materials.Contains(x.Key) || x.Value <= 0 || x.Value > Limit) || amounts.Values.Sum(x => (long)x) > Limit)
            throw new ArgumentException("交接数量无效");
    }
    public LedgerSnapshot Snapshot() => new(_containers.Values.Select(c => new ContainerStock(c.Id, c.Capacity, new(c.Items))).ToArray(), _reservations.ToArray(), new(_operations));
    public Dictionary<string, int> Totals() => Materials.ToDictionary(m => m, m => checked((int)_containers.Values.Sum(c => (long)c.Items.GetValueOrDefault(m))));
}
