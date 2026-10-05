# TODO-ART-01 · 美术待办整理票

批次 todo-r1；基线 `06f70c9f346adc0f7eaa87b5765d2ed986cfe9a3`；分支 `worker/todo-art-20261004`。目的：把既有美术工单整理成可分配、可验收的任务行。本轮只写文档，不制作资产或修改游戏代码。

执行：ZCode原生GLM；验收：Codex。唯一可写文件 `docs/art/production/todo.md`；只能提交该文件，禁止改任何其他文件/合同/金样，不安装、不购买、不push、不派生其他工人。输入副本todo-input仅供阅读，不提交。

最小阅读包：固定PR #14提交171c610的工单/14行清单（派单附只读副本），现役art-direction.md、art-i00-interface.md、本轮takeover-verification.md。不能把旧清单“待核定”覆盖新I00技术验收；不能把校准件当驮运正式模型。

格式：中文Markdown；更新日期；逐行固定ID、优先级、状态、前置、拟执行槽、交付/验收、来源。链接使用仓库相对路径或固定GitHub提交，无私人路径。状态允许DRAFT/READY/BLOCKED/IN_PROGRESS/SUBMITTED/REVIEW/REWORK/ACCEPTED；是否获准执行、是否合并另说明。未真实派单不得写IN_PROGRESS。

冻结事实：ART-I00=ACCEPTED（仅技术校准、PR #16未合并、视觉验收未做）。ART-T01/T02=BLOCKED（正式接入等待T3区域/边界高程/版本合同；参考造型不必等性能复核）。其余11个原ART ID均DRAFT，未启动/未生产；第一批U01/F01/F02/M01优先，第二批须第一批导入/比例/实际成本与所有者效果通过。加ART-TOOLS-01=DRAFT，预检合法本机可编辑工具链，上次Blender未安装，不自动安装或把图片冒称GLB。

拟执行槽：GLM-Art-A拥有U01/U02/U03/P01；GLM-Art-B拥有F01..F06；GLM-Art-T拥有T01/T02；GLM-Material单一共享材质写者M01。槽是将来角色不是已启动agent；实际派单须独立工作树、单票冻结目录。I00公共合同/集成由Codex；技术验收Codex、视觉效果所有者。代理体尺寸只是参考，材质/面数预算未实测，不编数值，不让艺术决定产矿/通行/发电/救援。

验收：保留14个原ART ID且无重复；加工具预检行；每行有上述字段/依赖；全中文解释本轮只整理、未来待排批；链接正确，无代码/资产变更。可用Python标准库检查表格ID/状态/范围，需报告真实命令和结果；不能借自测写ACCEPTED生产状态。

提交文件、commit及5行报告（范围/检查/限制）。遇合同冲突或越界需求停相关部分报主控。主控独立核对工单与最终文件后接受。连续两轮同问题不收敛则缩票，不无限重试。

## 实际交付与主控验收

状态ACCEPTED（只接受TODO文档）。原生会话确认GLM-5.3-Flash；Bridge observed_model仍null，不冒称API独立识别。工人commit3961cc78f2b0a7d120c4d0953b711066c7012bf0，仅一个授权文件；主控核对固定清单14 ID加工具行、15状态/8字段与提交范围通过，cherry-pick=f5458f9。工人自检输出真实PASS；主控另以assert核对，验收不依赖自报。

单轮交付，无外包返工派单。主控整合时统一P0/P1/P2、加入工具/材质接口前置、明确offline须映射现有七态、未适用动作才标N/A，并把调试检查移出美术任务表。原交付保留在commit中，未制作资产或代码。Bridge任务耗时549秒；usage.used=61596是会话可见字段，不等价生成token数或账单，费用未知。worker根快照含主控并行文件，归属仍以真实commit单文件为准；会话已结束。
