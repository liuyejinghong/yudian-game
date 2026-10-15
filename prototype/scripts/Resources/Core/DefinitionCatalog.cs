#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Yudian.Resources;

// Immutable material identity set provided by the catalog; keeps array-like reads
// (Length/Count/indexer/foreach/LINQ) for existing consumers without exposing a mutable array.
public sealed class MaterialIdSet : IReadOnlyList<string>
{
    private readonly string[] _ids;
    internal MaterialIdSet(string[] ids) => _ids = ids;
    public int Count => _ids.Length;
    public int Length => _ids.Length;
    public string this[int index] => _ids[index];
    public bool Contains(string id) => Array.IndexOf(_ids, id) >= 0;
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_ids).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _ids.GetEnumerator();
}

public sealed record MaterialDefinition(string Id, string Name, string Description, string Category, string Unit, string VisualRef);
public sealed record RecipeCatalogDefinition(string Id, string Name, string Description, string Category, string Unit, string VisualRef,
    IReadOnlyDictionary<string, int> Input, IReadOnlyDictionary<string, int> Output, double WorkSeconds, double Power);
public sealed record BuildingCatalogDefinition(string Id, string Name, string Description, string Category, string Unit, string VisualRef,
    IReadOnlyDictionary<string, int> Cost, double Radius, double WorkSeconds);

// Read-only definition catalog: stable identities and display metadata live here; quantities are
// deep-copied from the loaded BootstrapConfig so neither catalog nor UI can mutate config or ledger.
// Metadata never joins the numeric config hash and the shipped JSON stays untouched.
public sealed class DefinitionCatalog
{
    private static readonly string[] MaterialOrder = ["iron_ore", "copper_ore", "iron", "copper", "parts", "cable", "kit"];
    public static MaterialIdSet MaterialIds { get; } = new(MaterialOrder);

    // Icons and models already presented by the game (cargo glyphs, HUD, facility instances);
    // kit has no dedicated asset yet and stays text-only, so its VisualRef is empty on purpose.
    private const string IconsRoot = "res://assets/resources/d12-r1/icons/";
    private static readonly Dictionary<string, MaterialDefinition> MaterialMeta = new(StringComparer.Ordinal)
    {
        ["iron_ore"] = new("iron_ore", "铁矿", "铁矿露头可开采的原料，在加工设施制成铁料。", "矿石", "单位", IconsRoot + "iron_ore.svg"),
        ["copper_ore"] = new("copper_ore", "铜矿", "铜矿露头可开采的原料，在加工设施制成铜料。", "矿石", "单位", IconsRoot + "copper_ore.svg"),
        ["iron"] = new("iron", "铁料", "由铁矿加工而来，用于结构件与多项建设成本。", "加工材", "单位", IconsRoot + "iron.svg"),
        ["copper"] = new("copper", "铜料", "由铜矿加工而来，用于线缆与部分建设成本。", "加工材", "单位", IconsRoot + "copper.svg"),
        ["parts"] = new("parts", "结构件", "由铁料加工而来，用于建设与维修站消耗。", "加工材", "单位", IconsRoot + "parts.svg"),
        ["cable"] = new("cable", "线缆", "由铜料加工而来，用于建设成本与设施接线。", "加工材", "单位", IconsRoot + "cable.svg"),
        ["kit"] = new("kit", "组件套件", "着陆器带来的成套组件，不可生产，随建设成本消耗。", "成套物资", "单位", ""),
    };

    private sealed record RecipeMeta(string Name, string Description, string VisualRef);
    private static readonly Dictionary<string, RecipeMeta> RecipeMetaById = new(StringComparer.Ordinal)
    {
        ["iron"] = new("铁料加工", "在加工设施把铁矿熔炼为铁料。", IconsRoot + "iron.svg"),
        ["copper"] = new("铜料加工", "在加工设施把铜矿熔炼为铜料。", IconsRoot + "copper.svg"),
        ["parts"] = new("结构件加工", "在加工设施把铁料加工为结构件。", IconsRoot + "parts.svg"),
        ["cable"] = new("线缆加工", "在加工设施把铜料拉制为线缆。", IconsRoot + "cable.svg"),
    };

    private sealed record BuildingMeta(string Description, string VisualRef);
    private static readonly Dictionary<string, BuildingMeta> BuildingMetaById = new(StringComparer.Ordinal)
    {
        ["solar"] = new("基础供电设施，昼间发电并向接线范围内设施供电。", "res://assets/facilities/solar-r1/solar-r1.glb"),
        ["processor"] = new("执行基础配方的加工设施，接线后运行。", "res://assets/facilities/processor-r1/processor-r1.glb"),
        ["charger"] = new("为停靠的机器人回充电量。", "res://assets/lowfi-batch-r1/models/charger.glb"),
        ["repair"] = new("消耗结构件修复机器人耐久。", "res://assets/lowfi-batch-r1/models/repair.glb"),
        ["storage"] = new("扩容仓储存放物资，提供100容量与取货位，材料先送入仓储再取用。", "res://assets/lowfi-batch-r1/models/storage.glb"),
    };

    public static MaterialDefinition Material(string id) =>
        MaterialMeta.GetValueOrDefault(id) ?? throw new ArgumentException("未知物资定义：" + id, nameof(id));

    public IReadOnlyList<MaterialDefinition> Materials { get; }
    public IReadOnlyList<RecipeCatalogDefinition> Recipes { get; }
    public IReadOnlyList<BuildingCatalogDefinition> Buildings { get; }

    public DefinitionCatalog(BootstrapConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.Validate();
        Materials = new ReadOnlyCollection<MaterialDefinition>(MaterialOrder.Select(id => MaterialMeta[id]).ToArray());
        Recipes = new ReadOnlyCollection<RecipeCatalogDefinition>(config.Recipes.Select(Copy).ToArray());
        Buildings = new ReadOnlyCollection<BuildingCatalogDefinition>(config.Buildings.Select(Copy).ToArray());
    }

    private static IReadOnlyDictionary<string, int> ReadOnly(Dictionary<string, int> source) =>
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(source, StringComparer.Ordinal));

    private static RecipeCatalogDefinition Copy(RecipeDefinition r)
    {
        var meta = RecipeMetaById.GetValueOrDefault(r.Id);
        return new(r.Id, meta?.Name ?? r.Id, meta?.Description ?? "扩展配方。",
            "加工", "批", meta?.VisualRef ?? "", ReadOnly(r.Input), ReadOnly(r.Output), r.WorkSeconds, r.Power);
    }

    private static BuildingCatalogDefinition Copy(BuildingDefinition b)
    {
        var meta = BuildingMetaById.GetValueOrDefault(b.Id);
        return new(b.Id, b.Name, meta?.Description ?? "扩展设施。",
            "设施", "座", meta?.VisualRef ?? "", ReadOnly(b.Cost), b.Radius, b.WorkSeconds);
    }
}
