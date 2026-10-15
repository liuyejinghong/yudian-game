# OpenTTD 地形与建设命令底层框架研究（余电地形/建设参照）

- 日期：2026-10-13
- 研究方式：仅只读公开源码；未克隆全库、未下载发行包、未运行任何取自仓库的代码（读取外部资料的 Python 均以 `-I` 运行）。
- 定位：OpenTTD 是 2D 等距运输经营游戏，**与余电的 3D 火星目标委托不同**。本报告只把它当作“开源可核验的地形-建设-命令实现证据”，**不整体借其玩法**，也不把其架构当作余电应当照搬的目标。

## 0. 固定版本与来源（可复现）

| 项 | 值 |
| --- | --- |
| 仓库 | https://github.com/OpenTTD/OpenTTD |
| Release tag | `15.3`（官方 `releases/latest`，GitHub API，published 2026-04-04T19:56:48Z） |
| Tag → commit | `14ec60f248547d4d062a1160f0fc26d742319888`（`git/ref/tags/15.3` 为轻量 tag，`object.type = commit`；`commits/15.3` 返回同一 SHA，两处互证） |
| 代码引用 | 全部指向该 SHA 的固定源码，下文每条 `[事实]` 均给出可点击的永久 URL |

URL 前缀（下文所有链接）：`https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/`

访问日期：2026-10-13。来源优先级：官方仓库固定 SHA 原始源码 > 仓库内官方文档 `docs/landscape.html` > 官方 API。未使用第三方转述。

**证据分级约定**：`[事实]` = 有源码行段直接支撑；`[推导]` = 由多条事实推出；`[未核]` = 未取得一手证据，不得当作结论。

---

## 1. 地形数据模型：权威高度是“北角点”，不是 tile 整体值

- `[事实]` [`src/tile_map.h#L29-L33`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.h#L29-L33)：`TileHeight(tile)` 返回 `tile.height()`，注释写明“returns the height of the **northern corner** of a tile. This is saved in the global map-array. It does not take affect by any slope-data of the tile.”
- `[事实]` [`src/tile_map.h#L57-L62`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.h#L57-L62)：`SetTileHeight` 也只写北角点，并 `assert(height <= MAX_TILE_HEIGHT)`。
- `[事实]` [`src/map_func.h#L32-L40`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/map_func.h#L32-L40)：每个 tile 的持久数据是 `TileBase { uint8_t type; uint8_t height; uint16_t m2; uint8_t m1, m3, m4, m5; }`（另有 `TileExtended { m6, m7, m8 }`）。`height` 是**独立 1 字节**，与 `type`（bits 4..7 为 tile 类型）分列。
- `[事实]` [`src/map_func.h#L101-L104`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/map_func.h#L101-L104)：`height()` 访问器注释再次点明 “The height of the northern corner”。
- `[事实]` [`src/tile_type.h#L18`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_type.h#L18)：`TILE_HEIGHT = 8`（1 个 height level = 8 世界单位 = ZOOM_BASE 下 8 像素）；[`#L15`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_type.h#L15) `TILE_SIZE = 16`；[`#L24`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_type.h#L24) `MAX_TILE_HEIGHT = 255`；[`#L29-L30`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_type.h#L29-L30) `MIN_MAP_HEIGHT_LIMIT = 15`、`MAX_MAP_HEIGHT_LIMIT = 255`。

结论 `[事实]`：**权威高度是“每个 tile 的北角点一个整数层高”，范围 0..255**；tile 没有一个单独存储的整体高度值，其他三角高度不存储，而是在需要时从相邻 tile 的北角值推导。

---

## 2. 坡度如何推导；为什么“同高可不同类型、同高可不同坡”

### 2.1 坡度是“4 角相对最低角”的位掩码，不是斜率数值

- `[事实]` [`src/slope_type.h#L48-L69`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/slope_type.h#L48-L69)：`enum Slope` 用 5 位编码——4 位对应 N/E/S/W 四角“是否高于本 tile 最低角”，第 5 位 `SLOPE_STEEP` 表示陡坡。[`#L71-L77`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/slope_type.h#L71-L77)：额外 3 位编码 halftile（半格）非连续坡。
- `[事实]` [`src/tile_map.cpp#L23-L48`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.cpp#L23-L48)（`GetTileSlopeGivenHeight`）：以四角最小值 `hmin` 为基准，`if (hnorth != hmin) r |= SLOPE_N;` 等，`if (hmax - hmin == 2) r |= SLOPE_STEEP;`。注释明确约束：“the biggest difference in height between any corner and 'min' is between 0, 1, or 2. Also, there is at most 1 corner with height difference of 2.”
- `[事实]` [`src/tile_map.cpp#L55-L68`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.cpp#L55-L68)（`GetTileSlopeZ`）：一 tile 的四角高度取法为
  N=`TileHeight(tile)`、W=`TileHeight(TileXY(x2, y1))`、E=`TileHeight(TileXY(x1, y2))`、S=`TileHeight(TileXY(x2, y2))`，其中 `x2=min(x1+1,MaxX)`、`y2=min(y1+1,MaxY)`。
  → **本 tile 只存北角，其余三角借邻格的“北角”值拼出。**
- `[事实]` [`src/slope_func.h#L160-L165`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/slope_func.h#L160-L165)（`GetSlopeMaxZ`）：最高角相对最低角，flat=0、普通=1、steep=2。[`src/landscape.cpp#L340-L344`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/landscape.cpp#L340-L344)（`GetSlopeZInCorner`）给出某角相对 TileZ 的高度。

### 2.2 “同高不同类型”与“同高不同坡”

- `[事实]` 类型与高度是两个独立字节（[`src/map_func.h#L33-L34`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/map_func.h#L33-L34)）。→ 两 tile 可**高度相同而类型不同**（草地/岩石/水/建筑…），因为类型存在 `type` 字节、高度存在 `height` 字节。
- `[推导]` 坡度是由**邻格**拼出的（2.1），所以两 tile 即便自身北角高度相同，只要**邻格高度分布不同**，推导出的 `Slope` 就会不同——“同高不同坡”是邻接决定的，不是本 tile 决定的。
- `[事实]` 一个 tile 内**最高角与最低角之差可为 2**（`SLOPE_STEEP`，通常出现在对角），但**相邻角点之差始终 ≤1**（terraform 的递归约束见 §3.2）；halftile 允许半格非连续（[`src/slope_type.h#L71-L77`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/slope_type.h#L71-L77)，[`src/landscape.cpp#L232-L294`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/landscape.cpp#L232-L294) `GetPartialPixelZ`）。→ 存储与查询都在**角点离散层**上进行；格子内部（tile 内的中间像素高度）**不存储**，而是运行时按坡形计算（`GetPartialPixelZ` 对给定 x/y 与 slope 算出格内高度）。

### 2.3 关于所有者“0/-1/1 层”的说法

`[事实]`：
- OpenTTD 的权威高度是 **0..255 的绝对整数层**（每层 = `TILE_HEIGHT` = 8 世界单位），并不是把数值限定在三档。
- 所有者举例的“0/-1/1”在 OpenTTD 里对应的是**相对基准高度的偏移**：terraform 的目标高度是“当前高度 ±1”（[`src/terraform_cmd.cpp#L172`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L172) 的 `direction = dir_up ? 1 : -1`，[`#L178`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L178) 的 `TileHeight(t) + direction`）。
- 另外，OpenTTD **额外**加了一条“相邻角点高差 ≤ ±1”的规则（见 §3.2）。

`[推导]` 因此：“相邻差 ±1”是 OpenTTD 额外加的一条规则，**不是**高度表示本身的要求；余电若要保留“相对基准的负高程/连续高度”，**不必照搬**这条 ±1 约束。

---

## 3. Terraform（地形升降）合法性 / 邻接 / 代价

命令入口：`[事实]` [`src/terraform_cmd.cpp#L169-L303`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L169-L303)（`CmdTerraformLand`），签名 `(flags, TileIndex tile, Slope slope, bool dir_up)`——传入“要动的角”，`dir_up` 决定升/降。

### 3.1 合法性检查（顺序即源码顺序）

- `[事实]` 目标高度必须在 `0 .. _settings_game.construction.map_height_limit`：[`src/terraform_cmd.cpp#L104-L105`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L104-L105)（低于 0 → `STR_ERROR_ALREADY_AT_SEA_LEVEL`；高于限 → `STR_ERROR_TOO_HIGH`）。
- `[事实]` 无效果检查：[`#L112`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L112) 若目标高度等于当前高度 → `CMD_ERROR`（注释说明这是多角一起改时才会触发的失败路径）。
- `[事实]` 边缘检查：[`#L114-L124`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L114-L124) 仅在 `freeform_edges = false` 时，距地图边 ≤1 的 tile 报 `STR_ERROR_TOO_CLOSE_TO_EDGE_OF_MAP`。
- `[事实]` 桥/隧道破坏检查：[`#L232-L249`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L232-L249)（抬升撞桥 → `STR_ERROR_MUST_DEMOLISH_BRIDGE_FIRST`；下降伤隧道 → `STR_ERROR_EXCAVATION_WOULD_DAMAGE`）。
- `[事实]` 施工次数配额：[`#L278-L281`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L278-L281) `GB(c->terraform_limit,16,16) < ts.tile_to_new_height.size()` → `STR_ERROR_TERRAFORM_LIMIT_REACHED`；`terraform_limit` 定义为 [`src/company_base.h#L105`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/company_base.h#L105)，初值 `terraform_frame_burst << 16`（[`src/company_cmd.cpp#L73`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/company_cmd.cpp#L73)），默认参数 `terraform_per_64k_frames = 64<<16`、`terraform_frame_burst = 4096`（[`src/table/settings/world_settings.ini#L378-L396`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/table/settings/world_settings.ini#L378-L396)）。

### 3.2 邻接约束（关键）

- `[事实]` [`src/terraform_cmd.cpp#L137-L156`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L137-L156)：对被移动角点的四个对角邻居递归；[`#L148-L155`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L148-L155) `if (abs(height_diff) > 1)` 时把邻居也改成“差为 1”。源码注释：“the resulting height difference should be 1”。
- `[推导]` 因此 OpenTTD 的地形**额外**要求不出现相邻角点高差 > 1 的连续面（陡坡是“单 tile 内最高与最低角之差 2”的特殊允许形，该 2 通常出现在对角，相邻角仍 ≤1）；地图生成后仍需满足该不变量，[`src/heightmap.cpp#L399-L402`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/heightmap.cpp#L399-L402)（`FixSlopes` 函数注释，函数体 `#L403-L470`）亦重申“land in OpenTTD can never differ more than 1 in height”，并在 [`#L430-L437`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/heightmap.cpp#L430-L437) 破坏性地把超高崖降为差 1。

### 3.3 代价构成（实际字段，不编土方模拟）

- `[事实]` 每移动一个角点加一次固定价：[`src/terraform_cmd.cpp#L135`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L135) `total_cost.AddCost(_price[PR_TERRAFORM])`（`TerraformTileHeight` 每调用一次加一次，含递归邻居）。
- `[事实]` 每个受影响 tile 再叠加“按 tile 类型”的额外代价：[`#L256-L274`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L256-L274)，走 `_tile_type_procs[GetTileType(t)]->terraform_tile_proc(...)`；若为间接清除则走 `CMD_LANDSCAPE_CLEAR`（[`#L265-L269`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L265-L269)）。平整后的地形额外清理由 `DoCommandFlag::ForceClearTile` 触发（[`#L259`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L259)）。
- `[事实]` 计费类型（expense type）为 `EXPENSES_CONSTRUCTION`（[`#L132`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L132)、[`#L171`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L171)、[`#L335`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L335)）。
- `[事实]` `CommandCost` 实际字段：[`src/command_type.h#L24-L31`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_type.h#L24-L31) —— `Money cost`、`StringID message`、`ExpensesType expense_type`、`bool success`、`Owner owner`、`StringID extra_message`、`EncodedString encoded_message`。
- `[推导]` 代价 = “移动角点数 × `_price[PR_TERRAFORM]`” + “各受影响 tile 的清除/类型代价”。**没有**按挖方/填方体积、距离或质量做的“土方模拟”；不要为参照而引入体素级土方模型。

---

## 4. 命令估价 / 执行架构与失败语义（不要把 test/do 直接读成事务回滚）

### 4.1 test 与 execute 是同一 proc 的两次调用

- `[事实]` [`src/command_func.h#L140-L166`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L140-L166)（`Do`）：先以 `flags.Reset(DoCommandFlag::Execute)` 跑一次（test），若带 `Execute` 再跑一次真执行。嵌套调用（非顶层）带 `Execute` 时跳过 test（[`#L151`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L151)）。
- `[事实]` [`src/command_func.h#L353-L415`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L353-L415)（`Execute`）：先按 `command flags` 调 proc 做 test；[`#L316-L318`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L316-L318) 若 test 失败、或 `estimate_only`、或（非 `NoTest` 且）钱不够 → `exit_test` 直接返回；否则 [`#L400`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L400) 以 `flags | DoCommandFlag::Execute` 调**同一个** proc。
- `[事实]` [`src/command.cpp#L182-L196`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command.cpp#L182-L196)（`InternalDoAfter`）：test 分支改为 `CheckCompanyHasMoney(res)`（钱不够则把 res 改成错误）；非 test 且顶层才 `SubtractMoneyFromCompany`。
- `[事实]` [`src/command.cpp#L302-L329`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command.cpp#L302-L329)（`InternalExecuteValidateTestAndPrepExec`）：test 成功且非估价且钱够，才准备进入执行。
- `[事实]` [`src/command_func.h#L362-L366`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L362-L366)：非 `NoTest` 命令才做 sanity check `assert(res_test.GetCost() == res_exec.GetCost() && res_test.Failed() == res_exec.Failed())`。`NoTest` 的定义见 [`src/command_type.h#L412`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_type.h#L412)：“the command's output may differ between test and execute due to town rating changes etc.”。
- `[事实]` [`src/command_type.h#L386-L399`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_type.h#L386-L399)：`DoCommandFlag` 枚举含 `Execute / Auto / QueryCost / NoWater / NoTestTownRating / Bankrupt / AutoReplace / NoCargoCapacityCheck / AllTiles / NoModifyTownRating / ForceClearTile`。（注意：**没有名为 `DC_EXEC` 的宏**，源码里就是 `DoCommandFlag::Execute` 这一 bit。）[`#L406-L419`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_type.h#L406-L419) 另有 `CommandFlag`（`Server/Spectator/Offline/Auto/AllTiles/NoTest/NoWater/ClientID/Deity/StrCtrl/NoEst/Location`），[`#L49-L56`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command_func.h#L49-L56) 只把 `NoWater/Auto/AllTiles` 透传成 `DoCommandFlag`。

### 4.2 为什么“test/do = 完整事务回滚”是错的

- `[事实]` 本次所读的**命令与地形路径**中，未见对整张地图做快照再回滚的通用设施：读到的只是“写入前检查 `Execute` 位”。本报告**不**据此断言整个引擎不存在任何 rollback 机制（未做全量检索）。
- `[事实]` 地图写入是**各命令 proc 自己按 `Execute` 位门控**。明证：
  - [`src/terraform_cmd.cpp#L283-L301`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L283-L301)：`if (flags.Test(DoCommandFlag::Execute))` 才 `SetTileHeight`（[`#L297`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L297)）与扣 `terraform_limit`（[`#L300`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L300)）。
  - [`src/terraform_cmd.cpp#L259-L263`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L259-L263)：平整时构造 `tile_flags = flags|Auto|ForceClearTile`，pass 0 再 `Reset(DoCommandFlag::Execute)`，使清除在试算时**不落图**。
  - [`src/clear_cmd.cpp#L48`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_cmd.cpp#L48)：`if (flags.Test(DoCommandFlag::Execute)) DoClearSquare(tile);` —— 清除落图同样按位门控。
- `[事实]` 本次读到的“临时/可回退”语义只出现在 newgrf 持久存储：[`src/newgrf_storage.h#L20-L26`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/newgrf_storage.h#L20-L26) `PSM_ENTER_TESTMODE ///< Enter command test mode, changes will be temporary.`；[`src/command.cpp#L286-L305`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/command.cpp#L286-L305) 在 test 前后 `SwitchMode(PSM_ENTER_TESTMODE/PSM_LEAVE_TESTMODE)`。**它不覆盖地图 tile 数据。**
- `[事实]` 命令并非天然原子：[`src/terraform_cmd.cpp#L315-L387`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L315-L387)（`CmdLevelLand`）在循环内**手工**先试算（[`#L350`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L350) `Reset(Execute)`）再执行（[`#L364`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L364)），且 [`#L361-L363`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L361-L363) 钱不够时返回**已累计的部分代价**；[`#L366-L374`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.cpp#L366-L374) 注释承认接近 terraform 限时会**低估**代价。`CMD_LEVEL_LAND` 因此标了 `NoTest`（[`src/terraform_cmd.h#L21`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/terraform_cmd.h#L21)，理由：“test run might clear tiles multiple times, in execution that only happens once”）。

结论 `[事实]`：所谓“test / do”是**同一函数的两次调用 + 各 proc 自门控写操作**，失败时通过“不进入执行分支”避免落图；它**不是**“先改、失败再回滚”的事务。`[推导]` 对余电：preview/执行都应在**同一份校验函数**里重跑，真实写入集中在一处；不要假设框架会替你回滚整张地图，也不要因“preview 通过”就认为“执行必然一致”。（`DoCommandFlag::Execute` 位门控只是其中一种实现方式，非唯一。）

---

## 5. 建设 / clear / ownership / 保存接口

### 5.1 清除与所有权

- `[事实]` `CMD_LANDSCAPE_CLEAR` trait：[`src/landscape_cmd.h#L18`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/landscape_cmd.h#L18) —— `CommandFlag::Deity`，`CommandType::LandscapeConstruction`。
- `[事实]` [`src/clear_cmd.cpp#L26-L51`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_cmd.cpp#L26-L51)（`ClearTile_Clear`）：按地面类型计价（`_price[PR_CLEAR_GRASS/ROUGH/ROCKS/FIELDS]`，[`#L28-L46`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_cmd.cpp#L28-L46)），仅 `Execute` 时 `DoClearSquare`。
- `[事实]` [`src/landscape.cpp#L536-L548`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/landscape.cpp#L536-L548)（`DoClearSquare`）→ `MakeClear(tile, CLEAR_GRASS, ...)`；[`src/clear_map.h#L247-L259`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_map.h#L247-L259)（`MakeClear`）会 `SetTileOwner(t, OWNER_NONE)` 并清 `m1..m8`。
- `[事实]` 所有权读写在 [`src/tile_map.h#L178-L217`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.h#L178-L217)：`GetTileOwner/SetTileOwner` 用 `m1` 的 bits 0..4；且断言 house/industry 无 owner。
- `[事实]` `TileType` 枚举 [`src/tile_type.h#L47-L59`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_type.h#L47-L59)（MP_CLEAR/RAILWAY/ROAD/HOUSE/TREES/STATION/WATER/VOID/INDUSTRY/TUNNELBRIDGE/OBJECT）；`IsValidTile` 排除 `MP_VOID`（[`src/tile_map.h#L161-L164`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/tile_map.h#L161-L164)）。
- `[事实]` per-tile-type 行为表：[`src/clear_cmd.cpp#L399-L415`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_cmd.cpp#L399-L415)（`_tile_type_clear_procs`，含 `terraform_tile_proc = TerraformTile_Clear`，其实现 [`#L388-L391`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/clear_cmd.cpp#L388-L391) 直接转调 `CMD_LANDSCAPE_CLEAR`）。
- `[未核]` 非 clear 类型（铁路/道路/建筑等）的 `terraform_tile_proc` 对“他人所有权/城镇评价”的**具体**拒绝逻辑，未取得对应 `*_cmd.cpp` 一手行段；故“terraform 会因所有权失败”目前只算 `[推导]`（经 per-tile-type proc 拒绝），不作为已核事实。

### 5.2 保存格式（只列实际读到的字段）

- `[事实]` [`src/saveload/map_sl.cpp#L24-L27`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/saveload/map_sl.cpp#L24-L27)：`MAPS` 块存 `dim_x`、`dim_y`（`SLE_UINT32`）；[`#L51`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/saveload/map_sl.cpp#L51) 载入后 `Map::Allocate`。
- `[事实]` [`src/saveload/map_sl.cpp#L69-L121`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/saveload/map_sl.cpp#L69-L121)：`MAPH` 块把 `Tile(i).height()` 以 **`SLE_UINT8` / 每 tile 1 字节** 存/取。
- `[事实]` 其余按字段宽度逐 tile 直存（[`src/saveload/map_sl.cpp#L69-L353`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/saveload/map_sl.cpp#L69-L353)）：`MAPT`(type, `SLE_UINT8`)、`MAPO`(m1, `SLE_UINT8`)、`MAP2`(m2, `SLE_UINT16` → 每 tile 2 字节)、`M3LO`(m3, `SLE_UINT8`)、`M3HI`(m4, `SLE_UINT8`)、`MAP5`(m5, `SLE_UINT8`)、`MAPE`(m6, `SLE_UINT8`)、`MAP7`(m7, `SLE_UINT8`)、`MAP8`(m8, `SLE_UINT16` → 每 tile 2 字节)。即 `m2` 与 `m8` 各 2 字节，其余 `type`/`height`/`m1`/`m3`..`m7` 各 1 字节。
- `[事实]` 地图尺寸范围 [`src/map_type.h#L37-L40`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/map_type.h#L37-L40)：`MIN 64`、`MAX 4096`（边长，2^6..2^12）。
- `[推导]` 持久化的地形就是**每 tile 1 字节高度 + 1 字节类型 + 其余 m 字段（多为 1 字节，`m2`/`m8` 各 2 字节）**；**没有**无限世界、没有任意地下洞穴表示。`[未核]` 具体地图最大高度上限在不同 `map_height_limit` 设置下的历史兼容细节未逐一展开。

### 5.3 地形生成（辅助理解顶层不变量）

- `[事实]` [`src/heightmap.cpp#L380-L389`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/heightmap.cpp#L380-L389)：导入高度图时 `SetTileHeight`，注释“0 is sea level… scaled evenly to available height levels”；随后的 `FixSlopes`（[`#L403-L470`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/heightmap.cpp#L403-L470)）强制相邻差 ≤1（[`#L430-L437`](https://github.com/OpenTTD/OpenTTD/blob/14ec60f248547d4d062a1160f0fc26d742319888/src/heightmap.cpp#L430-L437)），把超高差降为差 1，并可能生成岩石。

### 5.4 货物（cargo）说明边界

`[未核]` 本任务只需简述：OpenTTD 的 **cargo spec/ID 与存货实例是分离的**（cargo 规格是静态定义，具体存量挂在载具/站点实例上）。此为对本任务边界的说明，**未在本轮取证**，不作为结论使用；不作为本报告重心。

---

## 6. 给余电的推导建议（不构成实施授权）

以下 `[推导]` 基于上述 `[事实]`，只服务余电的选址/导航/地形规则设计，**不要求照搬 OpenTTD**：

1. **高度可保留连续或离散，不必为参照重做体素。** 余电若已有 `TerrainSnapshot` 类似连续高度，可继续；OpenTTD 证明“每格单值高度（取北角）+ 可选邻接约束”已足够支撑大量建设逻辑，无需 3D 体素。
2. **高度可用“相对基准的连续高程”，不必照搬 ±1 层规则。** 所有者关心的 0/-1/1 在 OpenTTD 是“相对基准偏移 + 邻接高差上限”，属**可选规则**；余电可保留连续高度与相对基准的负高程。若采纳“相邻高差上限”，建议抽成显式不变量并在生成与 terraform 两条路径都校验（对应 `FixSlopes` + `TerraformTileHeight` 递归）。
3. **地形规则统一服务选址/导航。** 坡度/可通行性应由同一份派生函数（类似 `GetTileSlope`）产出，被建设预览、寻路、渲染共用，避免多处各算一遍。
4. **preview 与执行共享校验、执行前重验。** 不要假设框架会回滚。余电可采用独立的 **Evaluate / Commit 两个入口**，或把真实写入集中在一处门控；关键是 preview 与 commit **复用同一份校验函数**，并在 commit 前重跑一次——不把“preview 通过”当作“执行必然一致”。（OpenTTD 以 `DoCommandFlag::Execute` 位区分 test/exec，只是其中一种实现方式，非必须。）
5. **版本化与失败反馈。** 参考 `CommandCost`：把“代价 + 失败原因 + 责任方”作为统一返回体；失败以结构化原因回传，而非静默。
6. **区分“有高度数据”与“地形玩法成熟”。** OpenTTD 有完备高度/坡度/命令/存档，但那是**运输经营**的成熟度；余电的地形玩法目标不同，不能以“参照实现了”代签“余电玩法成立”。

---

## 7. 事实 / 推导 / 未核 一览

**事实（有源码行段）**：北角点权威高度；type/height 分列字节；坡度=四角位掩码由邻格推导；terraform 高度范围/边缘/桥隧/配额检查；邻接 ±1 递归；代价=每角 `_price[PR_TERRAFORM]` + 类型清除代价，`EXPENSES_CONSTRUCTION`；test/execute 是同 proc 两次调用、写入按 `Execute` 位自门控；`CmdLevelLand` 手工 test→exec 且可返回部分代价、可低估代价；保存为每 tile 1 字节高度 + 1 字节 type + 各 m 字段（多为 1 字节，`m2`/`m8` 各 2 字节）；地图尺寸 64..4096。

**推导（由事实推出，未单列源码直证）**：同高不同坡由邻接决定；OpenTTD 额外要求“相邻角差 ≤1”（非高度表示必需）；terraform 所有权拒绝经由 per-tile-type proc；持久化无无限世界/地下洞穴。

**未核（不得当结论）**：非 clear 类型 `terraform_tile_proc` 的所有权/城镇评价具体拒绝行段；`_price[PR_TERRAFORM]` 的具体数值；地图最大高度的历史兼容细节；货物 spec/实例分离细节（本任务重心外）。

---

## 8. 限制

- **无实机**：本报告全部来自只读源码与官方文档，**未在 OpenTTD 中实际试玩或运行 terraform/建设**；所有行为均为静态源码判定。
- **未访问闭源引擎内部**：仅使用公开仓库固定 SHA 源码与官方 API；未访问任何闭源竞品实现。
- **无 P 级裁决**：本报告不含项目 P0/P1/P2 裁决，也未修改任何游戏代码、未提交、未推送；唯一产出即本文件。
