using System;
using System.Collections.Generic;

namespace Yudian.Terrain;

/// <summary>
/// T3b-MESH 阶段网格候选：保留原 rows×columns 节点与原高度，每原 cell 沿固定 a-d 对角线生成两三角。
/// 顶点与 cell 遍历均 row-major；cell 四角 a=左上、b=右上、c=左下、d=右下，索引顺序固定 a,d,b,a,c,d
/// （XZ 投影叉积 Y 为正）。输出经主控 TerrainMesh 防御复制，本类不持有任何可变状态。
/// </summary>
public static class StageMeshBuilder
{
    public static TerrainMesh Build(TerrainSnapshot snapshot)
    {
        if (snapshot is null)
            throw new ArgumentException("snapshot must not be null", nameof(snapshot));
        return BuildCore(
            snapshot.Rows, snapshot.Columns, snapshot.OriginXM, snapshot.OriginZM, snapshot.SpacingM,
            snapshot.HeightsM);
    }

    public static TerrainMesh Build(TerrainPatch patch, TerrainSnapshot current)
    {
        if (patch is null)
            throw new ArgumentException("patch must not be null", nameof(patch));
        if (current is null)
            throw new ArgumentException("current must not be null", nameof(current));
        TerrainDataCodec.ValidateAgainst(patch, current);
        TerrainSnapshot layout = patch.Base;
        return BuildCore(
            layout.Rows, layout.Columns, layout.OriginXM, layout.OriginZM, layout.SpacingM,
            patch.HeightsM);
    }

    private static TerrainMesh BuildCore(
        int rows, int columns, double originXM, double originZM, double spacingM,
        IReadOnlyList<double> heightsM)
    {
        var vertices = new TerrainVertex[rows * columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                vertices[index] = new TerrainVertex(
                    originXM + column * spacingM,
                    heightsM[index],
                    originZM + row * spacingM);
            }
        }

        var indices = new int[6 * (rows - 1) * (columns - 1)];
        int write = 0;
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                int a = row * columns + column;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                indices[write++] = a;
                indices[write++] = d;
                indices[write++] = b;
                indices[write++] = a;
                indices[write++] = c;
                indices[write++] = d;
            }
        }

        return new TerrainMesh(rows, columns, vertices, indices);
    }
}
