using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Yudian.Terrain;

/// <summary>
/// terrain-patch-r1 冻结的不可变高度快照。构造入口内部化：
/// 合法实例只能经 <see cref="TerrainDataCodec"/> 解析产生，不存在公开 setter 或可修改集合。
/// </summary>
public sealed class TerrainSnapshot
{
    private readonly ReadOnlyCollection<double> _heightsM;

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
        _heightsM = new ReadOnlyCollection<double>((double[])heightsM.Clone());
    }

    public int SchemaVersion { get; }
    public string RegionId { get; }
    public long Version { get; }
    public double OriginXM { get; }
    public double OriginZM { get; }
    public double SpacingM { get; }
    public int Rows { get; }
    public int Columns { get; }

    /// <summary>行主序绝对高度；底层不是数组，消费者需要数组时自行复制。</summary>
    public IReadOnlyList<double> HeightsM => _heightsM;

    public double GetHeight(int row, int column)
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentException($"row index {row} out of range [0,{Rows - 1}]", nameof(row));
        if (column < 0 || column >= Columns)
            throw new ArgumentException($"column index {column} out of range [0,{Columns - 1}]", nameof(column));
        return _heightsM[row * Columns + column];
    }
}
