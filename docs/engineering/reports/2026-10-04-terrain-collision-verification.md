# 地形静态碰撞、候选切换与M01联调复验

2026-10-04，Codex主控。起点main991b78e，冻结cd61ec4；所有者授权持续产研、美术联调、合格直接合并。接受范围是独立候选的静态三角碰撞与显示同源，尚无动态机器人、导航、世界持久提交、游戏保存或完整T3。

## 已核交付

GLM原生模型确认task_b58e20c5f1返回`GLM-5.3-Flash`，同sess_14ebc7d603执行task_4016f0d285。observed_model为null，不冒称额外采样确认。原2ea7a23→集成9768674，真实git限定4新文件、工人树干净。Bridge的项目级files_changed还观测到并行美术和主控改动，不能将其归咎此工人；接受按该git commit diff核定。[回执](../evidence/2026-10-04-terrain-collision/worker-receipt.json)。耗时566s；usage.used64820/size1000000为会话上下文读数，非本票计费token，账单成本未知。

TerrainCollisionAdapter复用已接受的TerrainMeshAdapter验证/发出顺序，经临时ArrayMesh.CreateTrimeshShape返回独立ConcavePolygonShape3D；BackfaceCollision=false。临时mesh成功失败均释放，native空或face数不符拒绝并释放shape；输入错误继续原ArgumentException。不添加节点/变换/材质/缓存，也不复制float转换算法。全重建加额外临时mesh开销，没有性能优化结论。

主控778c1f2增加可选`--terrain-collision`与分帧自测；单槽pending在物理回合取出并清空，只处理最新请求。所有新mesh/shape准备成功才绑定、更新mode/count、释放旧资源；失败保持四资源。赋值帧之后才读实际物理世界查询，逻辑替换不等于引擎事务。

## 实际验证

| 检查 | 真实结果 | 限制 |
|---|---|---|
| 离线Debug构建 | 现有SDK10.0.401；0警告0错误 | 未装新依赖/未验证其他系统 |
| GLM适配独立复验 | 12命名PASS、0FAIL、真实exit0 | adapter未返回分支无法从合法公开输入注入；不强改被测代码 |
| 非顶点物理金样 | Stage局部Y1.25，Fine局部1.125/平移后6.125；命中目标body、世界XZ和单位向上normal | 真实三角平面，不用bilinear作oracle |
| 单面/过滤 | 从下方、XZ外、错mask均不命中 | 凹面静态壳，未测动态角色或实体体积 |
| 候选分帧检查 | 118条PROBE_CHECK PASS＋1条自身SELF_TEST PASS，exit0 | 包括中心/非顶点平面射线、独立body/显示transform，shape同源 |
| 最新请求/取消 | level→dig仅一次；level→当前base撤销；同pending与当前态重复noop | 单个预览实例，不是世界并发提交 |
| 拒绝/资源 | latest stale清空旧pending，外围拒绝，真实负绕序的第二shape失败；3半成资源释放、旧4资源释放和退出脱离均通过 | 无导航/save/cache |
| 默认入口回归 | 36条PROBE_CHECK PASS＋1条SELF_TEST PASS，exit0 | 35条旧检查加“无碰撞”模式互斥检查 |
| Metal真实GUI | 双击level只count2；4/5真实拒绝保持；重复dig只count3；撤销base count4；正常exit0 | GUI按键事件可能跨帧；同帧规则由分帧脚本证明 |
| 冷开/固定镜头PNG | 新进程base/count1/version9后base/level/dig，M01与debug各三PNG，exit0 | 独立工程探针，非正式游戏美术验收 |

原始输出与source/material/PNG SHA在[证据目录](../evidence/2026-10-04-terrain-collision/)和[manifest](../evidence/2026-10-04-terrain-collision/manifest.json)。日志已清除私人机器目录；实际退出码取进程结果，不取管道tail的状态。

工人报告首次测试把2×2细分face角数误写48后失败（实际3×3有4cell/24角），并承认最初管道打印EXIT=0是tail状态。其首次引擎log仅有启动header，未包含Console.WriteLine失败明细；保留该文件与工人回执，不将其当独立失败复现证据。主控没有改几何金样；独立实际12PASS/exit0验证正确face数量与射线高度。

## 身份与物理后端

实际Godot4.7.2、CLR10.0.12。最终Debug文件SHA`FDEB2ACCC2A2C3AE5F35263B74B0706B00A7941B4A6305393A2B586DFF945B1F`，loadedMVID与该文件PE MVID同为`9d8f7a44-84d1-4352-b5a3-166d2ecbd874`。Assembly.Location空；没有加载字节SHA声明。

GUI/PNG/default回归使用文件SHA`0E60CFB1ADB4B2E61D2018FEBE8234FADF8F82C44D9EC5629726227CB386F41E`、MVID`accde2ce-159c-4315-9b7d-21538329f5c5`；适配器独立run先于此小次构建，其SHA34C0DCA2…与MVID67ea3a5f…在原日志。最后仅给同步日志补space_state_class字段；几何、队列、资源和材质行为未改。最终native分帧已重跑，不声称三个运行的DLL完全相同。

物理配置读回DEFAULT、服务器公开class为PhysicsServer3D，二者不足以识别具体后端。主控在安全物理回合读取实际DirectSpaceState.GetClass，原生返回`GodotPhysicsDirectSpaceState3D`，因此此次默认工程实测为GodotPhysics3D；可对照[Godot4.7.2对应类源码](https://raw.githubusercontent.com/godotengine/godot/4.7.2-stable/modules/godot_physics_3d/godot_space_3d.h)。未声称测过Jolt或跨后端一致。

真实图形Godot4.7.2、Metal4.0、Forward+、Apple M3 Pro（Apple9）；视口1152×648。截图来自Viewport原始PNG，不是概念图。相机/灯光/几何固定，UI毫秒和同步帧数随运行变化，不用PNG字节相同验证几何一致。

## 美术联调

美术主控在其独立会话授权生产并接受M01六资源；技术只取原1b9146720ba1ac183ec0fee0ea93b785f8af9004（集成2559201）和其2401b6b的README原样链接修正，未争写材质或docs/art。soil_mars SHA`5f8aa3e483006e74e69cbda1385c2392c2f4f71f73b4129f957f2e15adf8a4f6`。

`--terrain-material res://assets/materials/art-r1/soil_mars.tres`真实加载StandardMaterial3D，两个网格逐surface0引用、MaterialOverride=null；重新替换保持接线。缺路径/错资源类型/双面/透明均明确拒绝，不替代材质。材质文件SHA/RID在日志。M01在同镜头三态实际PNG与debug对照已留；技术只接受资源/接线，不批准所有者最终色彩。

正式U01模型和T01土坡/T02矿点的可编辑源/GLB/manifest/逐surface角色/尺度未交付；ART-LINK-U01与ART-LINK-T01-T02保持BLOCKED。美术组正在独立修验预览工具，本批未将其错误接缝写为我们已复现或代为接受；不把I00/本候选网格充当正式素材。双线同步消息已发到现有美术会话，正式资产交付后再冻结接入票。

## 并行任务编排

用户要求用自己的ZCode workflow独立worktree并发GLM。已冻结[PERF-01-COMPARE](../packages/PERF-01-COMPARE.md)和[LIFE-01-AUDIT](../packages/LIFE-01-AUDIT.md)，基线`1ba4aa64573bc484578c03340e304787062dd127`；两包各自独占新增工具目录，复用现有统计/真实金样，无相互依赖。尚未收到领取回执，保持READY；当前碰撞票已完成复验，不重复派单。

模块是功能边界，包是一次可独立验收交付；同批并行实现、分别review、合格先合并，只有真实数据/接口依赖才串行。世界/nav/save/目标委托仍需先冻结权威状态接口，未包装成让GLM自行设计的大包。两离线工具完成后也不关闭父票实机职责。索引附可复制[workflow prompt](../packages/README.md)。

## 独立审查与发布

前置architect确定latest/base撤销/非法清pending、下一物理查询帧、非顶点/所有权/单面及美术归属；冻结包二次审查修正精确字段、路径基准与末行截断证据规则。reviewer预审及最终读取代码/原始日志未发现剩余实质缺陷；其未重新运行引擎，主控承担实际验证。

最终architect核源码/素材/全部证据hash与计数，唯一P2索引状态滞后已修；无其他发布前必修问题。它仅读取已有证据，未重复运行引擎。PR [#27](https://github.com/liuyejinghong/yudian-game/pull/27)已发布，最初head1c5bf805d661cd894d391faaf54da4bc9192f16b与本地一致、MERGEABLE/CLEAN；statusCheckRollup为空，不声称CI通过。本次后续仅补发布链接，不改源码；合并前仍核对最终head。原Issue6完整T3保持OPEN。

PR27已于2026-10-04T12:11:24Z按授权合并；最终head0d837ed48b7cb701da17c5cdf141c3c65702a6d9与本地一致、MERGEABLE/CLEAN，检查仍为空。merge43daffe88e5040df46134c5a5f09b0fea6cc336f；子Issue26 CLOSED、完整Issue6 OPEN；干净主检出已快进。合并后本条仅记事实并同步PLAN/TODO，源码与证据hash不变。
