using System;
using Godot;

namespace Yudian.Terrain;

/// <summary>
/// terrain-collision-r1 冻结的静态地形三角碰撞适配子件：直接重用已验收 TerrainMeshAdapter.Create
/// 的全部顶点 float/三角/单位法线/正绕序验证，临时 ArrayMesh 经 CreateTrimeshShape() 生成
/// 全新单面 ConcavePolygonShape3D（BackfaceCollision=false）。临时 ArrayMesh 无论成功失败均释放，
/// 返回 shape 不依赖其存活；native 空结果或 face 数与 CPU Indices.Count 不符抛
/// InvalidOperationException，异常时同步释放已产生但未返回的 shape，不返回半合法资源；
/// 非法 CPU/null 保持原 adapter 的 ArgumentException 与消息路径，不重写转换/法线算法。
/// 仅用于 StaticBody3D 静态地形：凹三角壳不是实体体积，不提供动态角色碰撞；
/// 调用者须在已启动 Godot 的主线程调用，接收者拥有返回 shape 及释放责任。
/// </summary>
public static class TerrainCollisionAdapter
{
    public static ConcavePolygonShape3D Create(TerrainMesh mesh)
    {
        // 非法 CPU/null 在此处以原 ArgumentException 路径拒绝，此时尚无任何 native 资源。
        ArrayMesh temp = TerrainMeshAdapter.Create(mesh);
        ConcavePolygonShape3D shape;
        try
        {
            shape = temp.CreateTrimeshShape();
        }
        finally
        {
            temp.Dispose();
        }

        try
        {
            if (shape is null)
                throw new InvalidOperationException(
                    "native CreateTrimeshShape returned null for a validated mesh");
            int expectedFaceCorners = mesh.Indices.Count;
            int actualFaceCorners = shape.GetFaces().Length;
            if (actualFaceCorners != expectedFaceCorners)
                throw new InvalidOperationException(
                    $"native trimesh shape has {actualFaceCorners} face corners, "
                    + $"expected CPU Indices.Count {expectedFaceCorners}");
            shape.BackfaceCollision = false;
            return shape;
        }
        catch (Exception)
        {
            shape?.Dispose();
            throw;
        }
    }
}
