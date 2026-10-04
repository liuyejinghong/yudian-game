using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-render-r1 冻结的 Godot 适配子件：把 CPU TerrainMesh 变为全新单 surface ArrayMesh。
/// 只有 Vertex/Normal 两通道，无索引/UV/颜色/材质/LOD/压缩选择/缓存；展开顶点数 = CPU Indices.Count，
/// 每 CPU 三角 (i0,i1,i2) 按 i0,i2,i1 发出——Godot 顺时针 front-face 与 CPU 正 Y 叉积之间的显式适配，
/// 不修改原 CPU 索引。全部转换/校验成功后才创建 native 资源，拒绝时无任何资源输出。
/// 调用者负责在已启动 Godot 的主线程调用并释放返回的资源；禁止以双面材质/关闭剔除掩盖绕序错误。
/// </summary>
public static class TerrainMeshAdapter
{
    public static ArrayMesh Create(TerrainMesh mesh)
    {
        if (mesh is null)
            throw new ArgumentException("mesh must not be null", nameof(mesh));
        if (mesh.Indices.Count % 3 != 0)
            throw new ArgumentException($"mesh Indices.Count {mesh.Indices.Count} is not a multiple of 3", nameof(mesh));

        // 公开 TerrainMesh 允许未被索引引用的顶点；先全部转换并校验一次，溢出不因未引用而漏过。
        var converted = new Vector3[mesh.Vertices.Count];
        for (int i = 0; i < mesh.Vertices.Count; i++)
            converted[i] = ToFiniteFloat(mesh.Vertices[i], i);

        var positions = new Vector3[mesh.Indices.Count];
        var normals = new Vector3[mesh.Indices.Count];
        int triangleCount = mesh.Indices.Count / 3;
        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            int baseIndex = triangle * 3;
            int i0 = mesh.Indices[baseIndex];
            int i1 = mesh.Indices[baseIndex + 1];
            int i2 = mesh.Indices[baseIndex + 2];

            Vector3 p0 = converted[i0];
            Vector3 p1 = converted[i1];
            Vector3 p2 = converted[i2];

            // 法线按原 CPU 顺序三 float 位置计算，同组三顶点同一平面法线。
            Vector3 cross = (p1 - p0).Cross(p2 - p0);
            if (!cross.IsFinite())
                throw new ArgumentException(
                    $"triangle[{triangle}] cross product is not finite (float overflow)", nameof(mesh));
            if (cross.Y <= 0)
                throw new ArgumentException(
                    $"triangle[{triangle}] winding must give positive cross Y, got {cross.Y}", nameof(mesh));
            float lengthSquared = cross.LengthSquared();
            if (!float.IsFinite(lengthSquared) || lengthSquared <= 0)
                throw new ArgumentException(
                    $"triangle[{triangle}] float-quantized degenerate or length-squared overflow "
                    + $"(lengthSquared={lengthSquared})", nameof(mesh));
            Vector3 normal = cross / Mathf.Sqrt(lengthSquared);
            if (!normal.IsFinite() || MathF.Abs(normal.Length() - 1f) > 1e-4f)
                throw new ArgumentException(
                    $"triangle[{triangle}] normal is not a finite unit vector", nameof(mesh));

            positions[baseIndex] = p0;
            positions[baseIndex + 1] = p2;
            positions[baseIndex + 2] = p1;
            normals[baseIndex] = normal;
            normals[baseIndex + 1] = normal;
            normals[baseIndex + 2] = normal;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        var result = new ArrayMesh();
        result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return result;
    }

    private static Vector3 ToFiniteFloat(TerrainVertex vertex, int index)
    {
        float x = (float)vertex.X;
        float y = (float)vertex.Y;
        float z = (float)vertex.Z;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            throw new ArgumentException(
                $"vertices[{index}] does not fit finite float after conversion "
                + $"(X={vertex.X}, Y={vertex.Y}, Z={vertex.Z})", "mesh");
        return new Vector3(x, y, z);
    }
}
