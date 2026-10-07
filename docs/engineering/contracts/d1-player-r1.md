# D1.0 player-r1 · 冻结子片

2026-10-07，base259a8bf，所有者已授权。单区域、单活动任务、固定半径2m的有限选区整平；复用既有patch提交。普通默认入口是玩家模式；显式--live-terrain保留旧工程自测，benchmark不改负载。

## 交互消费端

实际类型见 prototype/scripts/PlayerContracts.cs。Main公开：ReadPlayerState() 返回 PlayerReadModel；ReadPlayerRobots() 返回 PlayerRobotView[]；PreviewLevel(Vector3 center, string? workerId=null) 返回 PlayerSitePreview；QueueLevel(Vector3 center,long observedVersion,string? workerId=null)；QueuePlayerAction(string action)，仅 cancel/pause/save/load/recover。命令进入下一物理帧；一帧仅一个待处理命令，重复不覆盖旧命令。preview及执行均重验有限值、完整范围、设施和机器人包络、观察版本、权限、单任务与实际站位。UI只持选择/预览和相机状态，不持任务/地形事实。

E01交 PlayerUI.PlayerController : Node，Initialize(Main world,Camera3D camera)。控制镜头/鼠标选择/固定选区、现场高亮与目标卡/进度/原因、下达取消、暂停保存读取；点击UI不穿透，空点无命令，载入/暂停时UI相机可用。普通场景可显示12台机器人，但无订单时停驻。保存只有一个槽并可见成功/失败，读取显式触发，不隐式清档。需鼠标实际可操作，不仅快捷键。

E03交 Presentation.ZhuleiVisual : Node3D，Initialize()挂现役 canonical res://assets/lowfi-batch-r1/models/zhulei.glb，Apply(string state,double delta,bool paused)只改子节点；六键idle/move/work/charge/disabled/maintenance，非法键先验再拒绝保旧有效状态。根identity、1m单位、脚底原点；移动据真实位移而非自发循环，work据真实工段。服务状态仅可测适配，不在普通D1.0伪造充电维修能力；不接运输。已有manifest/材质/导入设置复用。

工程主控所有Main及事实/保存/注册；GLM-A仅PlayerUI/**及tools/player-ui-tests；GLM-B仅Presentation/**及tools/player-visual-tests，不改shared/Main/TODO/AGENTS/材质库。各包提交自测与精确hash，主控独立验收合并；READY不等于ACCEPTED。

## 快照与物理恢复

D1.0 schema1，fixture hash、权威terrain JSON、模拟时间、用户暂停、任务序号、稳定实体名及姿态/速度；原任务patch/base、中心/目标、工人名/站位、阶段、已提交版本、进度与旅行/等待时钟。完整校验后才改变世界；临时文件flush后同目录替换，失败保旧有效档。已提交阶段只恢复Current，不重提patch；未提交阶段重验base一致/站位/权限，真正离场仍按连续作业语义。读档先验证→暂停→重建权威与投影→下一物理帧验证→实体贴地/任务重绑→解除加载屏障；不能解除用户暂停、重放完成、累计离线收益。坏档/不支持schema显示错误，保留当前世界与档。保存只在物理tick交易之间捕获。

A01候选视口与UI同批校准；A03消费半径2m区域与权威高度场的原状/施工/已提交阶段，标记选择/合法性/工程范围。工程接消费者，美术不写相机运行或第二高度账本。
