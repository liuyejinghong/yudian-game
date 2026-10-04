# QA 接管批次 · takeover-r1

基线 `902a662`；代码集成 `7341ea8`；公开分支 `qa/t1-t2-reviewed-20261004`，公开代码 `9128523`（运行文件hash与已测包一致）。本票按 PR #13 的 QA 编号管理，不改变 T1/T2 原工单范围。验收人为 Codex；实际证据见[复验记录](../reports/2026-10-04-takeover-verification.md)。

| 票 | 单一交付 | 写者与依赖 | 本轮状态 |
|---|---|---|---|
| QA-01 | 可获取原始证据、独立统计、有效内存及措辞纠正 | Codex；依赖 QA-03/04/05 新包 | ACCEPTED：证据与复算通过；性能稳定性未签通过 |
| QA-02 | 冻结 fixture/CLI 输入合同 | GLM 独立分支；只写 Configuration 和 qa02-tests | ACCEPTED：109 个合同用例，主控实际 Godot 异常矩阵通过 |
| QA-03 | 可写输出、失败/中断、禁止覆盖 | Codex 独占 Main/Recorder；依赖 QA-02 | ACCEPTED：移动只读应用、不同 cwd、重复默认运行、引擎提前退出、不可写输出通过 |
| QA-04 | requested/applied/observed 和真实构建身份 | Codex；依赖 QA-03/05 | ACCEPTED：Forward+/Metal 与 Mobile/Metal 实测；headless 明确排除；内部尺寸标 estimated |
| QA-05 | 原始模板到签名包的可重复流程 | Codex；固定既有版本，不升级 | ACCEPTED：干净检出构建启动、两次模板处理同 hash；无 SDK 新系统验证 NOT_RUN |
| QA-06 | 输入/测量纯组件及独立资产接缝 | Codex；冻结 ART-I00 | ACCEPTED：纯组件可独测、旧几何代码不变、真实 GLB 导入及七态桥通过；场景生成仍在探针 Main 内 |
| ART-I00 | 地面/前向/尺度/根节点/附件/动作合同 | Codex 单一公共合同写者 | ACCEPTED：技术校准；生产资产和所有者视觉认可 NOT_RUN |

QA-02 有真实外包状态历史与交付 commit，见[冻结票](QA-02.md)。其余本轮由主控完成，没有伪造 GLM 派单。共同 fixture、入口、项目配置、构建、共享材质与公共合同均由 Codex 独占；旧基准不混入新资产。

下一批只建议 T3a：主控先冻结独立地形状态与版本提交合同。约定高度单位、边界、区域 patch、取消/旧版本拒绝，以及网格/碰撞/导航如何消费同一版本；暂不建立完整导航或存档系统。合同明确后可给 GLM 一张纯数据校验/序列化样例票，不能让它选择权威模型、批准提交或裁定性能。三轮帧率变动须先以固定前台/后台与节奏条件复核，再使用本底座作 T3 对照。
