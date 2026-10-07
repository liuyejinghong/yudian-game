# D1.2真实地表：只读美术输入 r1

2026-10-08。从已有隔离测试存档提取，不读取用户正式槽、不启动引擎、不占GUI、不改权威状态或runtime。所有样本FixtureHash相同、main-region、65×65、4225高度；manifest记录完整源文件及导出快照hash。只包含地形和必要任务元数据，未提交完整存档、机器人集合或账本。

| 样本 | 来源与范围 |
|---|---|
| initial | D1.2 schema3/d12-firstplay-1的源码UI隔离新档，Current版本0；不是声称由r3原生导出进程另录的initial |
| mining-working | 固定r3矿采工作存档备份，Current版本4；真实ProductionTask Working、未结算，已有首套施工痕迹 |
| mining-committed | 同r3同矿任务完成后存档，Current版本5；采矿receipt AppliedVersion5，地形已提交，无LevelJob |
| current-terminal | 同r3完整流程结束，Current版本21；采矿/建设/整平结果共同保留，用于当前边缘样本，不是某一次单独动作的形体归因 |

每个`*.terrain.json`是存档TerrainDataCodec序列化文本的原样提取，仅补末尾换行；每个`*.state.json`仅保留真实Stage/Center/工时/提交回执。采矿不是LevelJob：LevelJob为空，AppliedVersion仅从真实MiningReceipt另字段给出，MiningSiteCenter来自同存档矿点，DevelopmentGoal.Center是建设目标而非采矿中心；不得混用。

美术用同一initial对每个current逐高度比较：`mask[i] = current.heights_m[i] != initial.heights_m[i]`，origin/span/spacing沿网格元数据消费。Working样本仍有此前建设痕迹，不能解释为正在施工的矿点已经变化；v4→v5的相邻样本可用于辨识首次采矿提交。current-terminal全局mask包含多次动作。

提交后取消：**NOT_SAMPLED**。现有D1.2产物回执可以证明已提交阶段，但未找到独立committed-then-cancelled存档，未把旧D1.1或修改JSON伪作新样本。美术视觉结论仍由独立fixture和实际图判定，本数据交付不签地表边缘或GUI；默认包仍D1.1 r5。

## 补充LevelJob样本与消费语义

`level-working-legacy-r3`来自已有同r3原生包的D1.1回归模式隔离存档：schema2、d11-bootstrap-1、同FixtureHash、Current版本6；真实LevelJob.Stage=Working、AppliedVersion=null、Center=(14,0,-8)、Work=0.2666666667。此帧已暂停并有保障返程，保留的Working阶段不等于当前连续有效施工；不是D1.2默认配置采样，没有重跑或修改世界。

UpdatePlayerSurface只看LevelJob Working/WaitingForSpace设置stage1；此前四份样本LevelJob为空，采矿ProductionTask.Working应stage0。补充帧依此消费为stage1，派生值明确写在state中，不假称它是存档保存字段。

committed_mask表达**高度已改变**：包含施工整平与矿点开挖，不能一概标“压实”。独立fixture按实际动作/样本解释；working→commit的矿样本v4→v5有15个高度变化格。D1.2默认模式LevelJob Working与提交后取消仍未有独立保存，不以这份旧规则辅助帧替签。
