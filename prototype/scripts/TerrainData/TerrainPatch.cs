using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Yudian.Terrain;

/// <summary>
/// terrain-patch-r1 冻结的不可变候选补丁：schema_version/patch_id/base/heights_m 四字段。
/// 构造入口内部化，只能经 <see cref="TerrainDataCodec.ParsePatch"/> 创建
/// （解析时已校验候选继承 base 采样布局且外围与 base 严格同值）。
/// </summary>
public sealed class TerrainPatch
{
    private readonly ReadOnlyCollection<double> _heightsM;

    internal TerrainPatch(int schemaVersion, string patchId, TerrainSnapshot baseSnapshot, double[] heightsM)
    {
        SchemaVersion = schemaVersion;
        PatchId = patchId;
        Base = baseSnapshot;
        _heightsM = new ReadOnlyCollection<double>((double[])heightsM.Clone());
    }

    public int SchemaVersion { get; }
    public string PatchId { get; }
    public TerrainSnapshot Base { get; }

    /// <summary>候选高度，继承 base 的行列布局；底层不是数组，消费者需要数组时自行复制。</summary>
    public IReadOnlyList<double> HeightsM => _heightsM;
}
