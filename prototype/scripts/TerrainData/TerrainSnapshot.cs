using System;
using System.Collections;
using System.Collections.Generic;

namespace Yudian.Terrain;

/// <summary>
/// terrain-patch-r1 冻结的不可变高度快照。构造入口内部化：
/// 合法实例只能经 <see cref="TerrainDataCodec"/> 解析产生，不存在公开 setter 或可修改集合。
/// </summary>
public sealed class TerrainSnapshot
{
    private readonly IReadOnlyList<double> _heightsM;

    internal TerrainSnapshot(
        int schemaVersion,
        string regionId,
        long version,
        double originXM,
        double originZM,
        double spacingM,
        int rows,
        int columns,
        double[] heightsM)
    {
        SchemaVersion = schemaVersion;
        RegionId = regionId;
        Version = version;
        OriginXM = originXM;
        OriginZM = originZM;
        SpacingM = spacingM;
        Rows = rows;
        Columns = columns;
        _heightsM = CopyHeights(heightsM);
    }

    public int SchemaVersion { get; }
    public string RegionId { get; }
    public long Version { get; }
    public double OriginXM { get; }
    public double OriginZM { get; }
    public double SpacingM { get; }
    public int Rows { get; }
    public int Columns { get; }

    /// <summary>行主序绝对高度；底层存储不可经任何公开集合接口取得，消费者需要数组时自行复制。</summary>
    public IReadOnlyList<double> HeightsM => _heightsM;

    public double GetHeight(int row, int column)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentException($"row index {row} out of range [0,{Rows - 1}]", nameof(row));
        if (column < 0 || column >= Columns)
            throw new ArgumentException($"column index {column} out of range [0,{Columns - 1}]", nameof(column));
        return _heightsM[row * Columns + column];
    }

    // 冻结修正（评审P1）：internal 复制入口，快照与补丁共用，防御复制后交给私有包装。
    internal static IReadOnlyList<double> CopyHeights(double[] source)
        => new ImmutableHeightList((double[])source.Clone());

    // 只实现 IReadOnlyList<double>（Count/索引/枚举）：不是 ReadOnlyCollection，
    // 不实现 ICollection，因此 as ICollection 为 null，SyncRoot 无法触达底层 double[]。
    private sealed class ImmutableHeightList : IReadOnlyList<double>
    {
        private readonly double[] _values;

        internal ImmutableHeightList(double[] values) => _values = values;

        public int Count => _values.Length;

        public double this[int index] => _values[index];

        public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)_values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
