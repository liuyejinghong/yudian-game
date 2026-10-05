using System;
using System.Collections.Generic;

namespace Yudian.Terrain;

/// <summary>
/// terrain-mesh-r1 冻结的 T3c 候选网格生成子件：每原 cell 沿 X/Z 各细分 2 份（无可配置 LOD），
/// 输出 (2*rows-1)x(2*columns-1) 节点的纯 CPU 网格。原节点直接拷贝高度，水平/垂直中点取相邻
/// 节点均值，cell 中心取四角均值（等价于双线性在 0/.5/1 处采样）；三角面内部不是精确双线性曲面。
/// 坐标双精度米制、Y 向上、row+Z/column+X，全部输出世界坐标。无世界写入、无缓存、无 Apply/Commit。
/// </summary>
public static class HeightfieldMeshBuilder
{
    public static TerrainMesh Build(TerrainSnapshot snapshot)
    {
        if (snapshot is null)
            throw new ArgumentException("snapshot must not be null", nameof(snapshot));
        return BuildFineGrid(
            snapshot.HeightsM, snapshot.Rows, snapshot.Columns,
            snapshot.OriginXM, snapshot.OriginZM, snapshot.SpacingM);
    }

    public static TerrainMesh Build(TerrainPatch patch, TerrainSnapshot current)
    {
        if (patch is null)
            throw new ArgumentException("patch must not be null", nameof(patch));
        if (current is null)
            throw new ArgumentException("current must not be null", nameof(current));
        // 版本一致但内容不同也在此拒绝；失败无输出、无副作用。
        TerrainDataCodec.ValidateAgainst(patch, current);
        TerrainSnapshot layout = patch.Base;
        return BuildFineGrid(
            patch.HeightsM, layout.Rows, layout.Columns,
            layout.OriginXM, layout.OriginZM, layout.SpacingM);
    }

    private static TerrainMesh BuildFineGrid(
        IReadOnlyList<double> heights, int rows, int columns,
        double originXM, double originZM, double spacingM)
    {
        int fineRows = 2 * rows - 1;
        int fineColumns = 2 * columns - 1;

        var vertices = new TerrainVertex[fineRows * fineColumns];
        for (int fineRow = 0; fineRow < fineRows; fineRow++)
        {
            double z = originZM + (fineRow / 2.0) * spacingM;
            for (int fineColumn = 0; fineColumn < fineColumns; fineColumn++)
            {
                double x = originXM + (fineColumn / 2.0) * spacingM;
                double y = SampleHeight(heights, rows, columns, fineRow, fineColumn);
                vertices[fineRow * fineColumns + fineColumn] = new TerrainVertex(x, y, z);
            }
        }

        // 固定绕序 a,d,b,a,c,d（a=左上 b=右上 c=左下 d=右下），XZ 投影叉积 Y 为正；2x2 金样 [0,3,1,0,2,3]。
        var indices = new int[6 * (fineRows - 1) * (fineColumns - 1)];
        int cursor = 0;
        for (int cellRow = 0; cellRow < fineRows - 1; cellRow++)
        {
            for (int cellColumn = 0; cellColumn < fineColumns - 1; cellColumn++)
            {
                int a = cellRow * fineColumns + cellColumn;
                int b = a + 1;
                int c = a + fineColumns;
                int d = c + 1;
                indices[cursor++] = a;
                indices[cursor++] = d;
                indices[cursor++] = b;
                indices[cursor++] = a;
                indices[cursor++] = c;
                indices[cursor++] = d;
            }
        }

        return new TerrainMesh(fineRows, fineColumns, vertices, indices);
    }

    private static double SampleHeight(
        IReadOnlyList<double> heights, int rows, int columns, int fineRow, int fineColumn)
    {
        // 末位 fine 索引恒为偶数，odd 分支的 row+1/column+1 不会越界。
        int row = fineRow / 2;
        int column = fineColumn / 2;
        bool evenRow = fineRow % 2 == 0;
        bool evenColumn = fineColumn % 2 == 0;

        if (evenRow && evenColumn)
            return heights[row * columns + column];
        if (evenRow)
            return (heights[row * columns + column] + heights[row * columns + column + 1]) / 2.0;
        if (evenColumn)
            return (heights[row * columns + column] + heights[(row + 1) * columns + column]) / 2.0;
        return (heights[row * columns + column] + heights[row * columns + column + 1]
            + heights[(row + 1) * columns + column] + heights[(row + 1) * columns + column + 1]) / 4.0;
    }
}
