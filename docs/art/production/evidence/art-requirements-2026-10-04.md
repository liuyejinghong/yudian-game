# 美术需求补齐记录 · 2026-10-04

范围：本轮所有者明确授权需求/小票/美术TODO，正式生产另按指令。本轮不修改根PLAN/TODO、Main/fixture/公共桥或技术合同，不自动发消息给技术会话，不推送/合并。独立分支`art/requirements-r1-20261004`；Git基线`3999d302b4066d92ec7582eb8a62a400d9b91d9d`。

## 已核事实

- 本轮本地main与远端`git ls-remote origin refs/heads/main`均为上述commit；起点主工作树干净。首次沙箱网络查询未能连接代理，随后获工具执行许可的只读查询成功；不是用旧快照猜远端。
- `gh pr view 16/18/20 --json number,state,mergedAt,mergeCommit`实际返回MERGED；合并commit分别c2113ca、707258f、3999d30，时间分别2026-10-04 09:08:24Z/09:09:14Z/10:01:17Z。
- 已读项目指令、根TODO、art-direction、首批r1、美术TODO/I00/M01、产品定义/决定/返航与机群补充、工程TODO及T3a相关边界。旧阶段授权和工具报告不直接视作当前授权/状态。
- ART-I00是校准标尺，不是正式机器人；现桥代码work强制RotateY(WorkPart)，切态只复位WorkPart。新独立预览用局部适配，不改旧桥；游戏接入需求另列[派单索引](../tasks/dispatch-r1.md)。
- T3a及只读数据子件已接受，不等于完整地形系统。T01/T02继续BLOCKED，原因改为候选网格与T3d正式接缝未完成。
- 本轮实际`Godot --version`=4.7.2.stable.mono.official.ed1daf0bf，`python3 --version`=3.14.3；只读命令，不代表模型流水线。Blender本轮未重测，历史预检为三处路径未找到。

## 保留与补齐

保留已选方向、六角色/试样表、两档镜头、U01无载候选1.2×1.6×0.9m、七态与F02 offline→disabled。补U01分区/载荷/socket/逐态、F01/F02候选尺度依据、四建设阶段与运行态分离、固定白昼/色彩接线/样件输入、带标注证据与无答案辨认、小票串行/并行归属。

候选色值/尺度/光照未获所有者视觉接受；冻结的是“工人按什么做首样与公平比较”，不是最终规范。M01仍READY；PREVIEW/U01/F01/F02实际前置未齐备保持BLOCKED；P2维持DRAFT。

## 独立复核与验证

- 计划前architect只读审查已完成；主控核对WorkPart复位/旋转代码、M01接线缺口与工程TODO后采纳。成品architect发现三处：跨对象取证归属、U01设施联验前置回环、F01部署端点盲比。主控分别改为齐套后临时组合场景、单件先验、以中点盲比端点查连续；第二次只读复核三处关闭，并核Y0.85+2sin12°+0.08≈1.35m在1.4m候选包络内。
- GLM清单核对已真实派出并完成：ZCode同会话原生模型切换返回`✓ model = GLM-5.3-Flash`，随后执行只读需求清单核对。提交`fe02c8b`，主控实际`git show --stat/--name-only`确认仅新增[机械核对报告](glm-requirements-check-2026-10-04.md)，未写需求/代码/资产。
- GLM发现三资产票复制了不适用部件清单，主控已按货箱/四轮/盖、翼片/束带/脚、压头/挡板/固定货箱分别裁剪正常/异常与切态案例；M01补显式目标。索引不是执行票，不另造重复用例章节。其余关键角色/socket/状态/phase/路径与状态核对结论由主控逐项复核。
- Bridge的files_changed报告包含主控同期在其他文件上的修正，不能据此归到GLM；真实GLM commit仅报告一文件。GLM原报告保留其读取基线与当时问题，最终修正以本轮主控提交为准。
- reviewer接缝只读审查：未发现阻塞实施缺陷；指出F01/F02标题前置措辞可能误读已完成，已改为明确“等待”前置。确认七态/四阶段/离线映射、surface接线、错误原对象保持和权限边界成立。最终接受由主控，不由审查者代签。
- 已运行`python3 tools/verify_documents.py`：必需文档/当前本地链接/六份历史原文hash/A01–A24与B01–B08检查PASS；`python3 -m unittest discover -s tools -p 'test_verify_documents.py'`：7项OK；`git diff --check`通过。审查修正后主控再次核当前文档及改动路径，结果见末尾；这些结果不作图形通过。

## NOT_RUN

正式M01资源、U01/F01/F02模型、预览场景实现/GUI/实际光照、Godot新样件导入与状态图形、概念图/离线渲染/实机截图生产、盲比/所有者视觉、生产比例/色规范冻结、游戏接入、性能/LOD预算及完整地形联合验证均NOT_RUN。未制作资产，不存在可签视觉的图。

## 临时Godot属性核查（不产资产）

为确保预览票可以执行，本轮用临时空工程与GDScript读取ClassDB属性/常量和RenderingServer方法签名；不导入模型、不启GUI、不写正式材质。首次失败：沙箱userdata与未设DOTNET_ROOT导致初始化失败；第二次错误枚举名使脚本解析失败，即使引擎exit0也不当通过。按“两次失败缩票”记录PLAN，改为不硬编码待核常量的只读枚举。

缩票后的真实命令：`env DOTNET_ROOT=<已有SDK目录> PATH=<SDK目录>:<原工具路径> <Godot> --headless --path <临时工程> --log-file <可写日志> --script <属性探针>`；本机两次完成读取且输出`ART_API_PROPERTY_READ_OK`、无SCRIPT ERROR。检查结果：

- Viewport存在msaa_3d/screen_space_aa/use_taa/scaling_3d_scale；MSAA_4X=2；不存在directional_shadow_atlas_size。
- Environment存在ambient_light_sky_contribution/reflected_light_source/tonemap_mode/tonemap_exposure；REFLECTION_SOURCE_DISABLED=1、AMBIENT_SOURCE_COLOR=2、TONE_MAPPER_LINEAR=0；Camera3D KEEP_HEIGHT=1。
- RenderingServer方法`directional_shadow_atlas_set_size(size:int,is_16bits:bool)`存在；需求改为4096/true，本进程设置，不改共享project.godot。只核签名，不代表阴影画面通过。
- 内存中StandardMaterial3D赋Color("D8DCD6")，属性回读HTML为d8dcd6，RGB约(0.8471,0.8627,0.8392)、alpha1。这是API属性/颜色口径证据，不是正式M01六资源加载或视觉配色通过。

## Agent Bridge实际任务记录

| 项 | 真实值 |
|---|---|
| 会话 | `sess_1c55774362`，agent=zcode；不是OpenAI原生子代理 |
| 模型切换任务 | `task_dceeb0b3e0`，request `7e819c2f-b7a4-4b24-9680-87b07bdd1c8a`；原生/model回执确认Flash；1秒，completed |
| 清单核对任务 | `task_0ec63c068f`，request `0da029c0-ea6e-4c36-a2e5-7f8b8916fcb2`；读取commit `adabb1b`，同一独立需求工作树，唯一可写报告 |
| 开始/结束 | 2026-10-04T10:22:19.483310Z → 10:28:40.275241Z；Bridge elapsed_sec=380（约6分20秒），不采用工人约8分钟的估计 |
| 交付/主控核验 | `fe02c8b`，一份185行报告；完成/无工具warning，主控读报告与commit独立接受其机械核对范围 |
| 用量/成本 | Bridge usage.used=66290、size=1000000；界面显示65.2k cache-read tokens。是工具可见会话信息，不等于计费token或本票成本；成本未知。observed_model字段为null，模型依据是本会话原生/model回执 |
| 结束 | 主控已end_session，proc_state=dead；未领取下一票 |

主控独立OpenAI architect/reviewer只读审查与GLM清单是不同渠道/角色；记录不混称。初次远端事实查询约2026-10-04T10:06Z，本记录不声称此后远端永远无变化。

## 最终主控接受（仅文档）

成品architect三项问题已复核关闭；reviewer局部复核指出U01票仍有设施模板的N/A回idle句，主控已删除（U01七态均适用），设施票保留。phase_state_modes与中性blind文件名复核无新增接缝矛盾；GLM复制部件案例问题已局部修复。原报告保留当时意见，未回写成虚构的无问题结果。

最终文档验证PASS、diff空白检查PASS；改动路径检查确认13文件全部在docs/art/production，源/运行资产、根PLAN/TODO、Main/fixture/工程合同零改动。文档工具自测试7项已读输出OK（工具未改，无需反复跑）。需求、小票与美术TODO接受；正式生产/图形/所有者视觉及NOT_RUN清单不变。需求提交adabb1b、GLM报告fe02c8b，审查修正与本记录由最终主控提交保存；未推送或合并，main保持原工作树。
