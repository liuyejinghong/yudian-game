#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Yudian.Resources;

public sealed record ContainerStock(string Id, int Capacity, Dictionary<string, int> Items);
public sealed record MaterialReservation(string Goal, string Container, string Material, int Quantity);
public sealed record MiningReceipt(string Operation, string Site, string Material, int Quantity, long BaseVersion, long AppliedVersion);
public sealed record RecipeReceipt(string Operation, string Container, string Recipe, string Version, int Batches, Dictionary<string,int> Input, Dictionary<string,int> Output);
public sealed record LedgerSnapshot(ContainerStock[] Containers, MaterialReservation[] Reservations, Dictionary<string, string> Operations,
    MiningReceipt[]? Mining = null, RecipeReceipt[]? Recipes = null);

// One simulation-thread writer. Cargo and spent materials remain physical custody locations.
public sealed class MaterialLedger
{
    public static readonly string[] Materials = ["iron_ore", "copper_ore", "iron", "copper", "parts", "cable", "kit"];
    public const int Limit = 100_000;
    private readonly Dictionary<string, ContainerStock> _containers;
    private readonly List<MaterialReservation> _reservations;
    private readonly Dictionary<string, string> _operations;
    private readonly List<MiningReceipt> _mining;
    private readonly List<RecipeReceipt> _recipes;

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
        _mining = new(snapshot.Mining ?? []); _recipes = new(snapshot.Recipes ?? []);
        if (_mining.Count + _recipes.Count > 1000) throw new ArgumentException("生产回执过多");
        foreach (var r in _mining)
            if (r == null || !ValidId(r.Operation) || !_containers.ContainsKey(r.Site) || r.Material is not ("iron_ore" or "copper_ore") || r.Quantity <= 0 || r.Quantity > 8 || r.BaseVersion < 0 || r.AppliedVersion != r.BaseVersion + 1)
                throw new ArgumentException("采矿回执无效");
        foreach (var r in _recipes)
        {
            if (r == null || !ValidId(r.Operation) || !_containers.ContainsKey(r.Container) || !ValidId(r.Version) || !ValidId(r.Recipe) || r.Batches <= 0 || r.Batches > 4) throw new ArgumentException("加工回执无效");
            ValidateAmounts(r.Input); ValidateAmounts(r.Output);
            if (r.Output.ContainsKey("kit")) throw new ArgumentException("组件套件不可自产");
        }
        var ids = _operations.Keys.Concat(_mining.Select(r=>r.Operation)).Concat(_recipes.Select(r=>r.Operation)).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("重复交易身份");
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
        if (_mining.Any(r=>r.Operation==operation) || _recipes.Any(r=>r.Operation==operation)) throw new InvalidOperationException("交易身份类型冲突");
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
    public void Extract(MiningReceipt receipt)
    {
        var prior = _mining.FirstOrDefault(r=>r.Operation==receipt.Operation);
        if (prior != null) { if (prior != receipt) throw new InvalidOperationException("采矿身份内容冲突"); return; }
        if (!ValidId(receipt.Operation) || _operations.ContainsKey(receipt.Operation) || _recipes.Any(r=>r.Operation==receipt.Operation) ||
            receipt.Material is not ("iron_ore" or "copper_ore") || receipt.Quantity < 1 || receipt.Quantity > 8 || receipt.BaseVersion < 0 || receipt.AppliedVersion != receipt.BaseVersion+1 || _mining.Count+_recipes.Count>=1000)
            throw new ArgumentException("采矿提交无效");
        var site=Container(receipt.Site);
        if (Load(site.Id)+(long)receipt.Quantity>site.Capacity || Totals()[receipt.Material]+(long)receipt.Quantity>Limit) throw new InvalidOperationException("矿点现场容量不足");
        site.Items[receipt.Material]=Count(site.Id,receipt.Material)+receipt.Quantity; _mining.Add(receipt);
    }
    private static bool SameAmounts(Dictionary<string,int> a, Dictionary<string,int> b) => a.Count==b.Count && a.All(x=>b.GetValueOrDefault(x.Key)==x.Value);
    public void Convert(string operation, string container, string version, RecipeDefinition recipe, int batches)
    {
        if (batches<1 || batches>4 || !ValidId(operation) || !ValidId(version) || recipe==null) throw new ArgumentException("配方批次无效");
        ValidateAmounts(recipe.Input); ValidateAmounts(recipe.Output);
        var input=recipe.Input.ToDictionary(x=>x.Key,x=>checked(x.Value*batches));
        var output=recipe.Output.ToDictionary(x=>x.Key,x=>checked(x.Value*batches));
        var receipt=new RecipeReceipt(operation,container,recipe.Id,version,batches,input,output);
        var prior=_recipes.FirstOrDefault(r=>r.Operation==operation);
        if(prior!=null)
        {
            if(prior.Container!=container||prior.Version!=version||prior.Recipe!=recipe.Id||prior.Batches!=batches||!SameAmounts(prior.Input,input)||!SameAmounts(prior.Output,output)) throw new InvalidOperationException("配方身份内容冲突");
            return;
        }
        if(recipe.Output.ContainsKey("kit") || _operations.ContainsKey(operation) || _mining.Any(r=>r.Operation==operation) || _recipes.Count+_mining.Count>=1000) throw new InvalidOperationException("配方或交易身份无效");
        var target=Container(container);
        var totals=Totals();
        if(input.Any(x=>Available(container,x.Key)<x.Value)||Load(container)-(long)input.Values.Sum()+output.Values.Sum()>target.Capacity||output.Any(x=>totals[x.Key]-(long)input.GetValueOrDefault(x.Key)+x.Value>Limit)) throw new InvalidOperationException("加工输入不足或输出空间不足");
        foreach(var x in input)target.Items[x.Key]=Count(container,x.Key)-x.Value;
        foreach(var x in output)target.Items[x.Key]=Count(container,x.Key)+x.Value;
        _recipes.Add(receipt);
    }
    public void ValidateBalance(Dictionary<string,int> initial, RecipeDefinition[] recipes, string version)
    {
        var expected=Materials.ToDictionary(m=>m,m=>(long)initial.GetValueOrDefault(m));
        foreach(var r in _mining)expected[r.Material]+=r.Quantity;
        foreach(var r in _recipes)
        {
            var recipe=recipes.SingleOrDefault(x=>x.Id==r.Recipe);
            if(recipe==null||r.Version!=version||!SameAmounts(r.Input,recipe.Input.ToDictionary(x=>x.Key,x=>x.Value*r.Batches))||!SameAmounts(r.Output,recipe.Output.ToDictionary(x=>x.Key,x=>x.Value*r.Batches))) throw new ArgumentException("配方版本或投入产出不一致");
            foreach(var x in r.Input)expected[x.Key]-=x.Value;
            foreach(var x in r.Output)expected[x.Key]+=x.Value;
        }
        if(Totals().Any(x=>expected[x.Key]!=x.Value))throw new ArgumentException("物料与采矿/配方回执不守恒");
    }
    public LedgerSnapshot Snapshot() => new(_containers.Values.Select(c => new ContainerStock(c.Id, c.Capacity, new(c.Items))).ToArray(), _reservations.ToArray(), new(_operations), _mining.ToArray(), _recipes.Select(r=>r with {Input=new(r.Input),Output=new(r.Output)}).ToArray());
    public Dictionary<string, int> Totals() => Materials.ToDictionary(m => m, m => checked((int)_containers.Values.Sum(c => (long)c.Items.GetValueOrDefault(m))));
}
