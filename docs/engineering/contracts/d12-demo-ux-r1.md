# D12-DEMO-UX / E13 普通试玩闭环 r1

2026-10-08，所有者已授权继续。基线0b6347c，运行中的r3包冻结不变。本票在独立eng-demo-ux-20261008实施，r3功能验收保留；先构建验收UI修正后的整合具名包，再精确集成PR52，不把预先合并作为构建依赖。

## 最小行为

- 启动进入薄入口，模拟时间/经营命令暂不运行，物理落地/加载验证仍能进行。新游戏只选择本次启动的初始新局，不运行中重置；旧档直到玩家主动保存才替换。
- 继续沿用load，只有本次真正完成加载才离开入口；坏档/物理失败回滚留入口，保持当前世界与原档。保留存档暂停状态。入口禁透传普通保存/暂停/建造操作。
- 运行中保存退出：SavePlayer原子写盘成功才quit；早退/校验/写盘失败留世界/旧档，可继续操作。正常窗口关闭仍沿既有不自动保存语义。
- 普通界面固定目标、中文阶段/实际当前动作、下一步、仓库与运输/投入状态、功率、保障；设施/历史/长操作说明折叠。内部goal/build IDs留工程日志。地图仍有足够可点击区域。
- 两方向成本/真实缺口/后果只由Main读取配置/账本输出，UI不规划/扣料/推算产物。只读分清可用现货、在途、已付配方尚未产出与新增生产缺口；建设完成不继续显示旧成本为缺口。

## 公共接缝与唯一写者

Codex独占Main*.cs、PlayerContracts.cs、manifest/资源复制、报告/TODO/PLAN；GLM独占PlayerUI/PlayerController.cs与PlayerUiSelfTest.cs，可新增一个tools/player-ui-tests内最小自测入口（无依赖）。不改其他文件，不push/merge，不操作GUI。

- PlayerReadModel追加默认字段EntryOpen(bool)、EntryLoading(bool)。普通入口状态使用这两个字段和Ready、SaveExists、Notice；不能由UI自己判断存档加载成功。
- QueuePlayerAction新增newgame、savequit。入口继续仍load；仅入口允许newgame；保存成功判断由Main完成。
- DevelopmentReadModel追加默认字符串Supply、Prepared、Transit、InProcess及Directions(nullable DevelopmentDirectionView[])。Direction字段Id/Name/Cost/Need/Consequence/Feasible/Reason，UI可为空时沿旧Choices展示，不硬编码成本。
- Goal/Stage/Reason/Need由Main提供面向玩家中文；Worker名字沿既有ReadPlayerRobots，保障沿BootstrapReadModel.Robots真实状态。UI可聚合状态人数，不计算经济/保障预算。

## 验证

原生同包入口等待不改旧档/不推进时间，空档/坏档拒绝，读取成功/失败屏障，贴地之后保存成功，保存拒绝不退出；重复确认一次。HUD两个阶段前后数值与权威一致，长原因/未选机/已选机，实际1920及1280尺寸分开记证据。普通1x鼠标走首建、两方向、缺料、暂停取消、保存退出重开；不扩签真人盲玩、模型、自然平衡、动态美术或发行。
