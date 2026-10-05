# 单机器人整平任务 · 2026-10-05 复验

限定接受 GROUND-ORDER、WORK-METER、LEVEL-JOB。真正Main的--live-terrain模式可下达一项土坡整平：筑垒实际走到改造cell外施工站，连续有效作业3物理秒，重验占用/权限/base后提交，下一物理帧验证才完成。取消、重复、无改动与故障有真实结果反馈。旧默认与benchmark保持原灰模负载；这不是完整设施建设、导航、经济/能源或存档。

## 固定源码与运行身份

冻结4cbdf3ca，主控生产/自测最终0b1339a200ad3b033c2a28e0137e3ef68390f47b；随后只更新跟踪/证据/文档。最终DLL SHA `e39f6833ad49c79c96b4a0ceeab4e354576de1386d804443908cc8be19b6cb47`，加载MVID `a044a503-c077-451f-998f-0fb888a92a5e`。Godot4.7.2 mono、CLR10.0.12、Mac arm64；GUI Metal4.0 Forward+ M3 Pro。开发Assembly.Location为空，现有Summary身份入口核PE文件MVID，不冒称已加载字节哈希。

本批三构建分开：初始Main/旧七步/默认与短benchmark/GUI1使用fc69af45…ad445；补悬空测试与无作业文案后，新Order7/Main15/GUI2使用e8a4cd74…0d917；完成提示修正后，Main15/GUI3同用最终e39f6833…6cb47。Order7组件逻辑未再变，不因提示修正重跑同一套；不称每轮同DLL。

## 实际检查

| 范围 | 结果与边界 | 证据 |
|---|---|---|
| 最终项目构建 | 0警告/0错误，exit0；已有离线SDK/缓存，无新依赖 | [build](../evidence/2026-10-05-level-job/main/build-notice.log) |
| WorkMeter | GLM net10 console两文化各18例，共36PASS；累计/未完成中断清零/封顶/完成幂等/非法无副作用，主控核源码与实际完整通过日志 | [36项](../evidence/2026-10-05-level-job/work-meter/03-run-net10.log) |
| GroundOrder | GLM7原生PASS；返工补37个同XZ悬空帧逐帧false、真实落地后true；主控集成DLL新7项PASS，非手动调用物理方法 | [主控7项](../evidence/2026-10-05-level-job/main/order-final.log) |
| 旧GroundPatrol | GLM同一修正前组件构建10PASS，包含真实20°上下坡/50°阻挡/空中暂停重力；返工仅改测试，组件字节不变 | [10项](../evidence/2026-10-05-level-job/ground-order/ground-patrol-regression.log) |
| 真正Main任务 | 最终15步PASS/exit0，12贴地；实际派工/赶路不计时/暂停/施工取消/重复；权限/过期；他机实际到施工区占用等待；绑定故障保权威/重放/Current恢复后下一帧才完成；NoChange/回巡逻/提交后取消保地形；完成清掉活动提示 | [最终15步](../evidence/2026-10-05-level-job/main/level-notice.log) |
| Main旧接缝与兼容 | 原七步、默认30帧、短benchmark0.2s均exit0无引擎ERROR；benchmark只是兼容，不签性能 | [回执](../evidence/2026-10-05-level-job/main/regression-receipts.json) / [七步](../evidence/2026-10-05-level-job/main/ground-regression.log) |
| 真实Metal UI | GUI1施工41%→完成100%/v1；GUI2下单取消保v0、双击保同任务、重新完成v1、无需施工不增版；GUI3施工60%→完成100%/v1并清残留提示；三轮Cua真实关窗exit0，清单与只读ps确认无遗留 | [观察回执](../evidence/2026-10-05-level-job/main/gui-observations.json) / [最后GUI日志](../evidence/2026-10-05-level-job/main/level-gui-notice.log) |

Main超时例只注入任务时钟60秒，机器人改指向而不传送，验证未到场失败分支；不声称实际跑满60秒阻挡。占用者与成功任务工人均真实移动、实际原生贴地；连续3秒由physics delta且有效条件决定。GUI占用/绑定故障未演示，Main原生例覆盖。导出PNG仅初始viewport（已查看），后续UI截图为会话内联观察，没有伪造修改后导出图。

## GLM来源、review及失败披露

两独立工作树同基线4cbdf3c；两个ZCode会话先原生`✓ model = GLM-5.3-Flash`。A session sess_a371c1f521、原任务task_be0d211ea4，d80c81e集成d609dc4；review返工task_85ee79c97f、60bffed集成0d10adc。B session sess_54983ddecd、task_d835b7fc31，79e2b09集成316de05。Bridge observed_model为空，费用UNKNOWN，不把缓存读数当计费。实际worker diff均限定四文件；Bridge根目录files_changed包含美术/其他工人的并行变化，不归属给本工人。公共状态/合同/集成由Codex，工人无push/merge/自验收。

独立reviewer主控状态流未发现实质问题；子件review发现空中但XZ已达目标漏测，退回同包，37个真实空中帧补齐后复核关闭。最小生产代码复用MoveAndSlide、RegionView和标准库；WorkMeter只缩掉工单历史注释，未改逻辑。最终architect结论在收尾记录，不以GLM工人代替接受。

失败如实保留：A第一次编译Dot静态/实例API误用、第一次native越界测试向量10000.0001f被float舍入至10000，改可表示10000.5f后通过，未放宽规则。返工构建先cwd重置/MSB1009、再未限定常量/CS0103，工人第二失败后未按约停下，主控核两根因及最终diff/实测后接受成果；不宣称过程完全符合门禁，目录与逐轮日志约束已写Lessons。B编译命名空间遮蔽类、首跑epsilon测试误把delta0当完成，最终正确；原失败构建/首跑文件已被工人覆盖，公开保留交回披露/最终日志/epsilon取证，不把缺失原件称完整失败日志（工人交回关于build文件含首次失败的说法与实查不符）。主控首编译四个Nullable警告，用准确的抛错守卫注解消除。Cua首轮全量盘点超时，直接绑定已运行窗口恢复；初次sandbox ps拒绝后授权只读成功。游戏日志没有因此出现异常。

实机发现两条真实反馈问题：无需施工却显示0%已改明确“无需施工”；重复下单活动提示在完成后残留已清除并加原生断言、最后GUI复核。失败/修正前后日志均保留，未抹掉成果或伪造恢复。

[原件/公开SHA映射](../evidence/2026-10-05-level-job/source-public-hashes.json)；公开文本仅替换个人路径，原件留本机scratch/工人树。复跑入口：[任务Main](../../../tools/level-job-tests/README.md)、[单目标](../../../tools/ground-order-tests/README.md)、[连续作业](../../../tools/work-meter-tests/README.md)。

仍未接受：完整寻路/避让、其他设施建设或自动补前置、资源收益/库存运输、电量耐久/充电维修救援、模型目标规划、存档/重载、正式U01/T01/T02、增量/多区域、稳定性能/归因、发行或无SDK/Windows。工程材料与代理体保持美术既有ART-LINK待交回联调，不把本票替代正式视觉验收；父T3d/#6继续未完成。

收尾：最终architect未发现实质合并阻碍，独立核对48份公开证据SHA与Main/GUI身份，接受边界一致，无需扩大验证。两个GLM会话已结束（proc_state dead），保留原工作树/分支/日志；精确PR head、merge与main同步待集成记录。
