# tutorial-r2 批1 · 集成返工合同

2026-10-14｜独立Sol整体规格/质量审查已返回，主控已核关键代码与真实UI失败，先返工再固定候选。原发现D001/B002状态不改；本文为实施合同的局部补充。

## 权威/公共协议（Sol独写）

1. 兼容`PlayerJobView.Id`保原实例身份：供应发展阶段为goal-N，建设阶段/终态为原build-N，整平为level-N；只修Stage语义，不让同一个construction从goal-N变build-N。真实UI接线双击断言失败不可仅删断言。
2. `PlayerWorkView.TaskId`在build/connection工段携带真实build实例ID，`GoalId`仍发展目标ID；已有history可由GoalId或TaskId解析ledger reservation.Goal。账本ID与交易原字节规则不改，不制造新映射持久化。
3. 旧暂停CargoWaiting恢复时FeedbackCode为Unknown，投影须基于真实stage/载荷给出“载荷保留，等待交付条件；具体原因未核实”，不能沿旧取货成功理由，也不凭空解释坡度。读取投影零副作用，不等推进tick。
4. 生命周期DTO尾加可选`long WorldTransitionRevision = 0`，Main持有单调临时计数；每次真正尝试load/newgame（含校验失败）在执行入口先递增，read投影回显。不同于ordinary Busy/菜单打开；不写存档，不改旧schema/hash/epochrollback。UI见revision变化即清site/placement/returncontext，失败同epoch也不复用草案。
5. 管理离开game/权威ManagementPaused解除时UI同步关闭；authority保已定临停/epoch门禁。普通暂停菜单同epoch不清placement，load/newgame才清；退出确认返回不复活旧管理。
6. 最小测试补旧暂停CargoWaiting投影、稳定兼容buildID、reservation任务映射、failedload attemptrevision；不改导航、数值、健康、库存、模拟步长。主控另跑实际合同。

## HUD（GLM独写PlayerUI与其测试）

1. 遮罩在所有局内控件上方；管理期间EnterPlacement/SelectMode/DoConfirm等动作门禁，不能只FocusMode抑制鼠标。取消确认是最顶层唯一pointer/focus，Esc只退该层，底下标签/工具不操作。
2. 第一次建设未选蓝图显示“选择蓝图”，无Level预览/确认fallback。建设模式选择合法蓝图才可提交；整地仅mode1。已有玩家键鼠映射保。
3. 消费WorldTransitionRevision清读档/新游戏尝试后的临时context；returncontext记录epoch并重验。普通同epoch菜单返回保context。观察Page离开game或authority临停解除同步丢管理状态。
4. 真reservation由GoalId/TaskId匹配到目标，同层提供实际点击“查看关联工程”详情，搜索不是新增需求；保过滤空态/滚动。不能只Label。
5. Why放展开目标核心，整卡可收成一行；教学结尾确认后freeplay无活动教学目标隐藏，不造pin。普通包隐藏诊断及自动接最近/旧档/恢复工程按钮（明确GUI工程或debug条件才显示）。普通维修补料为真实经营动作，需保合法入口且不能一起误藏。
6. 工程警报取typed Work.Status/ReasonCode，通知原文只通知；不用中文Contains猜障碍。计划详情用EligibleStock或准确解释PlanningAvailable区别。只有charger/repair/processor显示连接状态，仓库/着陆器/solar显示自己真实运行。
7. 管理/确认面板尺寸受viewport上限约束，高倍率内部scroll不把关闭/确认/关键反馈挤出；建设主按钮/当前阻塞固定可见。实际头lessgeometry与原生视觉分开。
8. 两UI失败先对照实际源码/日志：接线ID由authority修；layout合法候选只读诊断仍在跑，确定HUD遮挡或pick条件后最小修，不删断言或改FAIL为BLOCKED。设施pick可通过真实相机定位后用实际engine input覆盖，不假assert。

各owner不重叠写。审查返工后只核这批改动与依赖边界，不再泛全仓审；所有纯合同与原内容保护成果保留。