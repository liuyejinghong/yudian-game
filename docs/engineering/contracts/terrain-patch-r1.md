# T3a · 局部高度数据合同 r1

日期2026-10-04；主控Codex。基于已合并main `707258f12e400508c880166cd10e608f137d16d1`。本轮冻结受控矩形采样域及只读候选patch的**数据接缝**，供两个地形候选共用；不是已经实现的世界保存、矿物结算、导航或提交系统。旧Main/fixture不改；旧PlaneMesh的segments不能推定此合同的行列数。

## 1. 坐标与数据

一单位一米，Y为高度；行row沿+Z、列column沿+X。采样点位置为`(origin_x_m + column*spacing_m, heights_m[row*columns+column], origin_z_m + row*spacing_m)`。row/column从0开始。heights_m为**绝对高度**，不是增量。新数据不依赖Godot类型。

本批探针边界：rows/columns整数2…257，spacing_m有限double且0.01…100，origin_x_m/origin_z_m有限且-10000…10000，高度有限且-1000…1000。限制是单票输入上限，不是最终地图/性能/可挖深度承诺。要求派生末端XZ有限；索引乘积用受限尺寸检查后计算，数组必须恰为rows*columns项。

ID大小写敏感，ASCII正则`[a-z][a-z0-9_-]{0,63}`。schema_version是整数1，version（包括base.version）为0…9007199254740991的整数，不接收浮点/指数写法。该上限固定数据交换范围；版本推进属于主控后续提交系统。

快照JSON只有以下九个字段，全部必需；无默认值：

```json
{"schema_version":1,"region_id":"sample-a","version":7,"origin_x_m":-1.5,"origin_z_m":2,"spacing_m":0.5,"rows":2,"columns":3,"heights_m":[1,2,4,8,16,32]}
```

上例(0,2)点为(-0.5,4,2)，(1,0)为(-1.5,8,2.5)，用于防止非对称尺寸转置。快照可以有任意符合范围的外围高度，不要求全为零。

patch JSON只有四字段，同样全部必需：

```json
{"schema_version":1,"patch_id":"patch-a","base":{"schema_version":1,"region_id":"sample-a","version":7,"origin_x_m":-1.5,"origin_z_m":2,"spacing_m":0.5,"rows":2,"columns":3,"heights_m":[1,2,4,8,16,32]},"heights_m":[1,2,4,8,16,32]}
```

base为完整不可变快照，候选heights_m继承base采样布局。携带完整base是本轮小探针最直接的可复验输入，不做增量压缩/通用事件框架；以后若有实际成本再调整版本。候选允许完全不变；2×N或N×2没有内部采样，因外围锁定只能表达不变候选。不在结构验证中冒称有有效工作量。

## 2. 边界、版本与职责

外围`row==0 || row==rows-1 || column==0 || column==columns-1`必须与base相同采样高度，按double数值严格相等（+0/-0同值），不引入容差；内部可在范围内变化。理由是先固定受控改造边缘，减少候选比较的接缝变量；不因此宣布相邻区域已无裂缝。区域划分/共享边/插值/碰撞与导航消费仍需主控接入验证。

`ValidateAgainst(patch,current)`仅验证输入合法、region_id/version/布局/每个base高度与current完全匹配；失败抛带字段路径ArgumentException，不返回部分成功、不修改双方。不同patch_id但相同base可以同时作为候选校验成功，**不代表允许两个提交**。仅version一致仍不够，base内容不一致也拒绝。

后续权威层必须在实际提交时重验当前版本及目标权限/取消状态；一个实际提交统一推进版本并发布同一份不可变高度给渲染、碰撞、通行和保存。候选计算/解析不持有世界写锁，不从视觉动作反推进度或资源。重复请求、相邻工程并发、发布顺序和崩溃恢复待提交系统票实现，GLM数据票不得自行新增Apply/Commit世界状态API。

取消保留已经发生的改造；只拒绝未提交或迟到候选。已提交的部分结果不能为了取消而还原base，也不能重放patch再次发矿。数据往返是候选/快照值保持测试，不等于文件原子保存、游戏重载或矿量一致性验收。

## 3. 冻结C#接口

命名空间`Yudian.Terrain`，只用net8.0标准库，不新增NuGet/Godot依赖。三种类型即可：

- `TerrainSnapshot`：只读属性SchemaVersion(int)、RegionId(string)、Version(long)、OriginXM/OriginZM/SpacingM(double)、Rows/Columns(int)、HeightsM(IReadOnlyList<double>)；`GetHeight(int row,int column)`。禁止公开setter及可修改集合，不与输入数组/另一快照共享可变存储。
- `TerrainPatch`：只读SchemaVersion、PatchId、Base(TerrainSnapshot)、HeightsM；候选数据同样不可变。构造入口内部化，由下述验证解析入口创建；不得给外部无校验构造半合法实例。
- `TerrainDataCodec`静态：`TerrainSnapshot ParseSnapshot(string json)`、`TerrainPatch ParsePatch(string json)`、两个`string Serialize(TerrainSnapshot value)`/`Serialize(TerrainPatch value)`重载、`void ValidateAgainst(TerrainPatch patch,TerrainSnapshot current)`。

所有非法输入（含空/null参数、行列索引越界）统一ArgumentException，消息带JSON字段路径或参数/索引名。解析JSON大小不超过4194304个UTF-16字符；标准JSON，不允许注释/尾逗号；字段区分大小写，重复/未知/遗漏/null/错类型/范围/长度错误都拒绝，嵌套base同样严格。整数token只接收十进制整数字面量，不接受1.0/1e0；实数字段接受标准JSON整数/小数/指数，但必须有限且符合范围。拒绝时无对象泄漏/副作用。

Serialize的输出是合法严格JSON，使用固定上述字段顺序与InvariantCulture，浮点往返保持double数值；不要求输入字面量或空白逐字保持。不得序列化无效/空对象。HeightsM不得把数组暴露为可变IList/数组；如果消费者需要数组自行复制。GetHeight以row-major读取。

## 4. 主控提供的候选金样

3×4快照：region_id=sample-b、version=9、origin=(-2,3)、spacing=1；基准高度逐行为`[10,11,12,13] / [20,21,22,23] / [30,31,32,33]`。patch-id=patch-b，候选仅内部两点改为`[20,0,-1,23]`，其他行不变。这是数值金样，不冒称已发生整平/开挖。

独立验收覆盖：两金样往返/坐标索引、内部修改通过、每条外围边变化拒绝、非零基准外围、过期版本/错误region/同版但内容不同拒绝、输入与候选均不被改变、集合不对外可改；尺寸上限与非法长度、整数语法、有限/溢出/数值范围、所有字段遗漏/null/重复/未知/大小写、损坏JSON与文化区域。拒绝行为和输入保持同时检查。至少用两个CultureInfo环境保证与系统小数点设置无关。

数据接缝接受后允许准备T3b/T3c独立候选网格票，但候选自己的插值/网格布局/边界/碰撞适配还须冻结；不能因本票完成把完整T3或ART-T01/T02正式接入标READY。
