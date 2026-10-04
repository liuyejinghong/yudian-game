# tools/terrain-heightfield-tests

T3c-MESH（terrain-mesh-r1 冻结 HeightfieldMeshBuilder）的独立 console 测试。无包依赖、不启动 Godot；
被测实现 `prototype/scripts/TerrainCandidates/Heightfield/*.cs` 与合同冻结的
`prototype/scripts/TerrainData/*.cs`、`prototype/scripts/TerrainGeometry/*.cs`（命名空间 `Yudian.Terrain`）
以 `Compile Include` 链接进本程序集，快照/补丁只能经 `TerrainDataCodec` 解析构造。

## 运行（与票面命令一致，在仓库工作树根目录执行）

```sh
mkdir -p <空本地NuGet源目录>
"$HOME/.dotnet/dotnet" restore tools/terrain-heightfield-tests/Tests.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$HOME/.dotnet/dotnet" build tools/terrain-heightfield-tests/Tests.csproj -c Release --no-restore
"$HOME/.dotnet/dotnet" run --project tools/terrain-heightfield-tests/Tests.csproj -c Release -f net10.0 --no-build --no-restore
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL）。
net8.0 只编译（`-f net8.0` build），按票面不执行。

## 覆盖范围

- 金样 sample-a（2×3 非对称 → 3×5=15 顶点/48 索引）：全部 15 顶点精确值（原节点 (0,2)=(-0.5,4,2)、
  细分中心 (1,1)=(-1.25,6.75,2.25)）、全部 48 索引精确值、全三角 XZ 正 Y 绕序。
- 金样 sample-b + patch-b（3×4 → 5×7=35/144）：原节点逐个等于候选高度（内部 0/-1 保留）、
  中点/中心均值、原点/间距世界坐标、成功构建后 patch/base 序列化不变。
- 完全不变 patch 合法且与 base 几何一致（顶点+索引逐项相等）。
- patch 校验：过期 version、错误 region_id、同 version 内容不同、布局 rows 不符全部拒绝；
  失败后 patch/base 不变。
- 扭曲 cell（2×2 [0,0,0,4]）：3×3=9 顶点/24 索引（全部精确值，每 cell 固定 a,d,b,a,c,d 模式，
  与合同 2×2 输出金样 [0,3,1,0,2,3] 同绕序）、中心顶点高 1、
  (0.75,0.25) 处按三角重心独立复核面高 1 而精确双线性 0.75（平面三角 ≠ 双线性曲面）。
- 邻区接缝：两个内部互异的 3×3（共享边界列 [3,5,9]），从实际索引缓冲收集 x=2 边界三角边，
  在原节点/中点及边内 0.25/0.75 参数处插值世界 XYZ，左右误差 ≤1e-9 米且符合分段线性期望值。
- 尺寸：最小 2×2 → 3×3 精确模式索引；最大 257×257 → 513×513=263169 顶点/1572864 索引，
  全顶点有限、全索引在界、全部 cell 绕序为正、角点世界坐标精确。
- 确定性：同输入两次构建顶点/索引逐项相等且缓冲实例独立。
- 不可变：输出 Vertices/Indices 不可转数组/IList/ICollection（SyncRoot 不可达）、越界索引抛
  ArgumentException、IReadOnlyList 遍历正常；输入 HeightsM 不可转可变接口，构建与失败校验后 GetHeight 不变。
- null 参数（snapshot/patch/current）统一 ArgumentException 带参数名。
- 全量检查在 InvariantCulture 与 de-DE（逗号小数点）两个 CultureInfo 下各执行一遍。
