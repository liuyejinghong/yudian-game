# level-job-r1 · 单机器人整平任务

2026-10-05冻结，起点dd47fe9；用户授权继续推进最小整平闭环。真正Main显式--live-terrain，旧默认/benchmark保留；一名筑垒、默认土坡半径2m内绝对高度0，一个活动任务。工程灰模、有限直线到场，不接受完整导航、设施建设、经济/能源、存档或正式美术。

## 主控 · LEVEL-JOB

主控独占Main*、合同/PLAN/TODO/共享注册。开始固定task/request/patch身份与base版本，重复活动下单返回原任务，不重分配/不清进度；终态可新任务。选择最近筑垒及改造cell足迹外施工站（机器人半径.35），真实地形射线定位，不传送、不插值Y；有限直线到场，60s未到场明确失败。到场须实际XZ距离<=.25、IsOnFloor、XZ速度<=.05m/s、已验证物理与原base同版。站旁工作而不是站在待改造脚下。

连续有效施工3物理秒；条件失效则未完成计时清零，赶路/滑落/暂停/故障不计时。满额后重验工人到场、版本、权限、取消与所有机器人/设施受影响cell占用；他机占用等待至多15秒，期间原巡逻继续，工人单目标保持。过期/无权限/取消不静默重建patch；结果不是按时钟虚构。无改动明确“无需改造”，不增加版本/收益。

复用SubmitGround/TerrainRegionView，保留实际Status/AppliedVersion；提交后等待该版本下一物理帧核验才Completed。绑定故障保权威/回执、所有运动暂停，任务等待Current恢复并物理核验；不自动再提交。提交前取消不改世界；提交后取消仍保结果/版本，不回滚或发资源。任务释放工人单目标，恢复原巡逻；global Paused仍由地形同步负责，不能清目标顺便解除故障暂停。

面板显示同一任务id、工人、阶段、进度/失败原因，可下单、取消、Current恢复；测试用故障钩子不成为玩家按钮。旧手动整平保留工程入口并标明，活动任务时手动编辑可拒绝或明示版本冲突，不偷偷改任务base。

## GLM包A · GROUND-ORDER

唯一修改prototype/scripts/TerrainWorld/Actors/GroundPatrol.cs；新增prototype/scripts/TerrainWorld/Actors/Tests/GroundOrderTests.cs、prototype/scenes/ground-order-tests/GroundOrderTests.tscn、tools/ground-order-tests/README.md。不改旧GroundPatrolTests或Main/合同/TODO。

保留Initialize、旧巡逻/胶囊/速度/重力/Paused合同。增加void SetOrder(Vector3 target)、void ClearOrder()、只读bool HasOrder/OrderReached。SetOrder必须已Initialize，先验证target各坐标有限且绝对值<=10000；非法不改变旧目标。覆盖一个单目标，保留巡逻航点/TargetIndex；向目标只经MoveAndSlide前进，到XZ<=.25保持，不循环或恢复巡逻。OrderReached须真实IsOnFloor且距离<=.25，离场/空中为false；Paused仍落地、不推进航点/目标。ClearOrder幂等、只清目标，不重置巡逻索引/TravelledM，不解除Paused。无寻路/新collision层/传送。native检查实际到场保持、清除回巡逻、暂停空中重力、阻挡不到达、替换目标和非法输入原目标保留，保留完整stdout/stderr与真实exit，无ERROR。

## GLM包B · WORK-METER

唯一新增prototype/scripts/Construction/WorkMeter.cs、tools/work-meter-tests/{WorkMeter.Tests.csproj,Program.cs,README.md}。纯C# namespace Yudian.Construction，无Godot/依赖。sealed WorkMeter(double requiredSeconds)，公开double ElapsedSeconds、Fraction、bool IsComplete；void Advance(double deltaSeconds,bool eligible)。requiredSeconds有限(0,3600]，delta有限[0,3600]，非法ArgumentException且状态不变。未完成且eligible=false清零（连续作业）；eligible=true累计封顶required，Fraction[0,1]。完成后合法Advance保持完成，不提供重置/任务/权限/世界状态。新任务新meter。console net10标准库直接链接源，检查边界/累计/中断清零/封顶/完成幂等/非法无副作用，不增加framework。

## 验证与外包边界

两包同冻结commit独立工作树，原生/model GLM-5.3-Flash确认后执行，可用原生子代理但目录唯一写者。不安装/联网下载/改依赖/push/merge/自验收，不修改工人树外或美术。主控检查diff与实际日志、独立reviewer审关键任务状态；真实Main验证无到场不计时、取消、重复、占用/过期、绑定故障与Current恢复、提交后取消不回滚；Metal实际下单/进度/完成与关窗。费用未知记录UNKNOWN，父T3d保持未完成。
