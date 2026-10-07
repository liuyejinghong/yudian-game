# tools/navigation-tests

D1.1 导航子票（d11-bootstrap-r1 合同冻结）的独立 console 自检。无包依赖、不启动 Godot；
被测实现 `prototype/scripts/Navigation/BoundedRoute.cs`（命名空间 `Yudian.Navigation`）与
现有 `prototype/scripts/TerrainData/*.cs` 一并以 `Compile Include` 链接进本程序集，
因此 TerrainSnapshot 只能经 `TerrainDataCodec` 解析构造（真实不可变快照）。

## 运行（在仓库工作树根目录执行）

```sh
mkdir -p <空本地NuGet源目录>
"$HOME/.dotnet/dotnet" restore tools/navigation-tests/Navigation.Tests.csproj --source <空本地NuGet源目录> -p:NuGetAudit=false
"$HOME/.dotnet/dotnet" build tools/navigation-tests/Navigation.Tests.csproj -c Release --no-restore
"$HOME/.dotnet/dotnet" run --project tools/navigation-tests/Navigation.Tests.csproj -c Release -f net10.0 --no-build --no-restore
```

目标框架为 `net8.0;net10.0`（保留 .NET8 目标，0 警告 0 错误）；本机若未安装 .NET 8
运行时则按上述命令以 net10.0 执行（与 tools/terrain-data-tests 同口径）。
退出码 0 = 全部检查通过；非 0 = 存在失败（每个检查逐行打印 PASS/FAIL，
在不变文化与 de-DE 两种文化环境下各跑一遍）。

## 覆盖范围（合同自检清单）

- 平地路线成功：Points 不含 start、末端严格 destination、LengthM 有限且 ≥ 2D 弦长、WorldVersion=快照版本。
- 格点 start 不重复出现在 Points；相邻航点为网格步（≤ √2·spacing）。
- 绕圆障碍成功：长度明显大于被挡直线，且每段通过独立采样校验（步长 spacing/4、场内、膨胀圆外、坡度）。
- 窄口：bodyRadius=0.75 可穿、1.05 拒绝（同一几何，膨胀代理行为）；拒绝时中文原因/空 Points/LengthM=0。
- 陡坡拒绝：20m 整列高墙在默认 30° 下不可达；0.5m 台阶默认 30° 可过、10° 全拒绝（maxSlopeDegrees 参数生效）。
- 输入负例全部 ArgumentException 且无副作用：null terrain/obstacles、非有限坐标/圆心、
  bodyRadius≤0/非有限、障碍 radius≤0/非有限、maxSlopeDegrees ∉ (0,35]；边界合法值 35 可用。
- 不同 version 快照：WorldVersion 各自跟随，几何相同则路线与长度逐位相同。
- start/destination 在场外或终点落在膨胀障碍内：合法输入但不可达，原因分别提及 起点/终点。
- start==destination（格点）：Found=true、单点 [destination]、LengthM=0。
- 重复调用逐位一致（确定性、只读、无缓存）。
