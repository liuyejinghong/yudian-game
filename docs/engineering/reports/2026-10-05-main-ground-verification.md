# 2026-10-05 · Main权威地形与原生巡逻复验

限定接受 MAIN-GROUND、GROUND-PATROL、LIVE-TERRAIN-OPTIONS。真正Main通过显式`--live-terrain`接入已接受的TerrainRegionView与CharacterBody；机器人从当前碰撞真实射线定位、经MoveAndSlide运动。默认灰模/历史benchmark保留原负载，新模式与所有游戏benchmark触发互斥。手动整平/挖低不是完整建设工序。

运行源码`e2dbef102f3b02122da1925be2b32b9ae09dfb00`，冻结合同基线`89cb3fb474f1df1c1acdcf6bad755058216ae1b5`，主线起点7f6667e。最终Debug DLL SHA256 `6e4e2644be81a937762581acc96dfbc1a0146f0331d3550d808f286966680244`；loaded/file MVID `121f2816-4f15-4ac6-bb73-c4ebe309a780`一致。Godot4.7.2 mono、开发CLR10.0.12、macOS arm64、M3 Pro；Godot内存加载Location为空，SHA来自MVID核对后的构建文件，不称已加载字节哈希。最终验证后仅文档/证据改动。

## 实际门槛

| 验证 | 实际结果 | 证据 |
|---|---|---|
| 参数子件 | 主控21项通过；GLM旧QA-02 109项通过、reviewer无遗留 | [21项](../evidence/2026-10-05-main-ground/main/options-run.log) / [109项](../evidence/2026-10-05-main-ground/glm-options/run-qa02-tests.log) |
| 原生运动 | 10项、exit0、无引擎ERROR/WARNING；平面/20°真实往返/50°阻挡、空中暂停落地/连续60帧、暂停恢复/输入与防御复制 | [最终native](../evidence/2026-10-05-main-ground/main/final-v3/patrol.log) |
| 真正Main | 七步、exit0；12机贴地累计78.537m，设施及机器人独占拒绝保资源；0→1整平、1→2挖低；绑定故障后权威3保留，再次恢复失败仍暂停显示原因，Current恢复同版后继续走 | [最终Main](../evidence/2026-10-05-main-ground/main/final-v3/main.log) |
| 兼容/输入 | 11项；旧默认与短benchmark完成，segments512/spacing>100/height>1000/圈外布局旧模式仍合法，新模式建场景前拒绝；混合benchmark拒绝 | [逐项回执](../evidence/2026-10-05-main-ground/main/compat-final-v2/compat-receipt.json) |
| 最终短benchmark身份 | exit0、12机器人/6设施、SHA/MVID与最终构建一致，headless性能不合格标记保留 | [最终回执](../evidence/2026-10-05-main-ground/main/final-v3/receipt.json) / [summary](../evidence/2026-10-05-main-ground/main/final-v3/summary.json) |
| Metal真实GUI | 版本0→1→2；重复挖低NoChange不加版；Current恢复仍2且下一物理帧验证；各屏12贴地并持续移动；Cua真实关窗exit0，应用清单与进程确认无遗留 | [GUI日志](../evidence/2026-10-05-main-ground/main/main-gui.log) / [观察回执](../evidence/2026-10-05-main-ground/main/gui-receipt.json) / [初始1920×1200 PNG](../evidence/2026-10-05-main-ground/main/main-gui-initial.png) |

碰撞/渲染全部角按既有1e-4m容差，改动节点由后续物理帧真实射线核验；提交前冻结水平巡逻并按所有受影响cell与胶囊/含自转设施包络检查，占用拒绝恢复已验证旧运动。GUI未演示占用拒绝，独立Main真实引擎例覆盖；后续Cua画面是本会话内联观察，没有声称导出了修改后PNG。试玩由工程自行拉起，不等待美术回执。

## GLM来源与返工

参数包session `sess_2cb5bdaaa6`，原`68390afa`，主控集成0efd005；运动包session `sess_35827ec30e`，原`58e99d4`→返工`7e64c97`→`c1300cb`，集成49a33c6/a33559b/e2dbef1。两个会话均先收到原生`✓ model = GLM-5.3-Flash`，Bridge observed_model字段为空，不冒称额外Provider遥测。工人无push/merge/自验收，限定包归属；费用UNKNOWN。主控逐页读25/129条tool-call并核实际diff，不将根目录Blender复制归属给GLM。

独立reviewer先发现下坡用例只到坡顶、地面暂停不能证明重力，退回同包；再发现坡高预期漏算box厚度、连续贴地计数未重置，继续返工。最终用native向下射线与胶囊法线支撑校验：surface0.836、expectedFoot0.858、actual0.857754，±0.05m；不以放宽snap带掩盖错误。组件业务逻辑无变化，主控复验10项。

## 保留的失败与限制

- 基线第一次启动命中了旧ignored DLL，输出不符合当前schema，不作当前基线证据。重建7f6667e后，开发benchmark因Assembly.Location空真实exit1；统一Summary身份入口改为非空路径原规则、空路径核对Debug MVID再哈希，完成路径通过。
- Main首轮±1100m长射线高度误差：近零点测得0.00012207031m，超过既有1e-4；缩短至当前最高/最低高度外2m，测得0.000000238m，原容差未放宽。
- GLM首版纯逻辑节点RID泄漏由工人披露并改Free；原run1/2仅保存Godot banner，Console stdout不在--log-file，不把它们当完整通过/失败实测证据。首编译失败与泄漏细节见工人交回披露；run3错误坡底断言exit1、run4/5完整输出均保留，主控最终完整stdout/stderr独立通过。
- 主控最终兼容脚本一次复用输出路径被Recorder正确拒绝；旧产物保留，改新目录后11项通过。兼容11项发生在ddb4b13构建；后续仅测试oracle/日志标签变更，最终同构建补测短benchmark与全部native，不称每轮同一DLL。

原始文件留本机scratch与各工作树；[公开文件/原始SHA映射](../evidence/2026-10-05-main-ground/source-public-hashes.json)保留出处，个人路径已替换。源码复跑入口见[Main](../../../tools/main-ground-tests/README.md)与[运动组件](../../../tools/ground-patrol-tests/README.md)。独立reviewer缺陷已解决，最终architect无实质合并阻碍，独立核对99份公开文件hash、源码/GUI身份与限定接受边界；发布合并事实另记于集成记录。

仍未接受：完整导航/机器人与设施避让、目标委托、实际建设/资源收益、电耗/维修救援、生产存档、正式U01/T01/T02、增量/多区域、稳定性能或profiler原因、发行/无SDK环境与Windows。本票工程材质/胶囊代理不是正式美术；素材缺口继续由ART-LINK既有票跟踪。
