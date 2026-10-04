# tools/terrain-data-tests

T3a-DATA（terrain-patch-r1 冻结数据接缝）的独立 console 测试。无包依赖、不启动 Godot；
被测实现 `prototype/scripts/TerrainData/*.cs`（命名空间 `Yudian.Terrain`）以
`Compile Include` 链接进本程序集，因此快照/补丁只能经 `TerrainDataCodec` 解析构造。

## 运行（与票面命令一致，在仓库工作树根目录执行）

```sh
mkdir -p <空本地NuGet源目录>
"$HOME/.dotnet/dotnet" restore tools/terrain-data-tests/TerrainData.Tests.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$HOME/.dotnet/dotnet" build tools/terrain-data-tests/TerrainData.Tests.csproj -c Release --no-restore
"$HOME/.dotnet/dotnet" run --project tools/terrain-data-tests/TerrainData.Tests.csproj -c Release -f net10.0 --no-build --no-restore
```

退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL）。

## 覆盖范围

- 两个合同金样（2×3 sample-a / 3×4 sample-b + patch-b）往返、合同坐标探针 (0,2)/(1,0)、行列主序 `GetHeight`。
- 外围锁定：逐外围格改动拒绝（含 2×N 全外围、全零外围、非零外围）、±0 同值与不同写法同值接受、内部改动接受。
- `ValidateAgainst`：region/version/rows/columns/origin_x/origin_z/spacing/每个 base 高度全部不匹配路径，null 参数，失败后双方不变。
- 只读性：HeightsM 为私有只读包装（仅 IReadOnlyList 的 Count/索引/枚举），snapshot/patch/base 均无法经 ICollection/SyncRoot 或任何可变集合接口触达底层存储；IReadOnlyList 索引与遍历正常。
- JSON 转义中的孤立代理项（\uD800/\uDC00）出现在 region_id/patch_id/字段名（含嵌套 base）时统一转为带字段或 json 参数路径的 ArgumentException（保留原 InvalidOperationException 为 inner）；合法代理对按 ASCII-ID/未知字段规则拒绝。
- JSON：九字段/四字段逐一缺失、null、错类型、重复、未知、大小写（字段名与值）、嵌套 base 同等严格、文档类型不可互换。
- 整数语法（1.0/1e0/超 long 拒绝；version 0 与 2^53-1 边界）、数值范围与边界（尺寸 2/257、spacing 0.01/100、origin ±10000、height ±1000）、
  有限/溢出（1e400 拒绝、1e-400 归零接受）、高度长度（含 257×257 与 ±1）、4194304 字符上限含边界、损坏 JSON。
- 序列化：固定字段顺序的精确文本、-0 与指数形式往返位精确、Serialize(null) 拒绝。
- 全量检查在 InvariantCulture 与 de-DE（逗号小数点）两个 CultureInfo 下各执行一遍。

## 限制

- net8.0 仅做编译验证（本机只有独立 net10 运行时，不安装 net8）；测试执行只在 net10.0。
- 本测试只覆盖数据接缝：不含 Apply/Commit 世界状态、文件保存、导航/矿物/权限语义。
