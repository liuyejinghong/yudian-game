# 单区域权威提交与显示/静态物理复验

2026-10-05：T3d-PROJECTION-RESOURCES与T3d-VIEW限定接受。独立可运行场景已将真实内存提交接到Stage画面与静态shape；主控独立复验、reviewer和architect未发现剩余实质问题。开发分支已集成，远端PR合并事实在发布后补记。

范围是单区域、单Godot物理写线程、完整重建。旧Main的机器人仍用原TerrainHeight，本场景未接玩法；通行/导航、邻区事务、存档/重载、工程收益与资源经济、正式美术、两候选性能比较/稳定预算未验收。工程自行启动游戏测试，不以美术回执为前置。

## 代码与运行身份

基线`909cca33b40206a1654b741868a4394fb8e31af5`，最终被测源码`f3b08bfa594501771eb802ff929d4d471fc85709`。Godot4.7.2 .NET开发引擎，CLR10.0.12；本机GUI为macOS arm64 / Forward+ / Metal4.0 / Apple M3 Pro (Apple9)，不是旧CLR8发行包的重测。

Debug Yudian.dll SHA256 `6c2d67a1937a5c6a420c5149db15ad82d0736adaf8032e22c6362801aff60171`；加载模块与文件MVID均`97832950-f200-4220-87a5-631e9ab6ba86`。Assembly.Location为空，未声称加载字节SHA。[运行回执](../evidence/2026-10-05-terrain-view/verification-receipt.json)、[原件与公开脱敏hash](../evidence/2026-10-05-terrain-view/source-receipts.json)。31份原日志保留，公开文本只替换个人目录与scratch前缀。

## 实际验证

| 检查 | 结果与证据 |
|---|---|
| 新Evaluate与Commit共用语义 | 29例通过，预判不消费ID、重放先于权限/取消判定；[日志](../evidence/2026-10-05-terrain-view/main/memory-with-evaluate.log) |
| 实际Godot项目 | 离线restore、最终Debug build exit0，0警告/0错误；[构建](../evidence/2026-10-05-terrain-view/main/build-final-r2.log) |
| GLM native资源束 | 8项通过，mesh/shape全部角、独立资源、输入不变、失败释放/幂等Dispose，无ERROR/FAIL；[日志](../evidence/2026-10-05-terrain-view/main/bundle-final-r2-stdout.log) |
| 权威与真实物理 | 7步通过，9→10→11、拒绝保留、准备后重验、提交后故障与Current恢复、节点失效及退出；[日志](../evidence/2026-10-05-terrain-view/main/world-final-r2-stdout.log) |
| 实际GUI | 原生Cua观察并点击：初始高地3.5m/v9→平整0.4m/v10→挖低−1.25m/v11；重复平整NoChange，重放、取消、旧版拒绝、Current投影恢复均保持v11；三个版本与视觉同变，真实关闭按钮退出0；[日志](../evidence/2026-10-05-terrain-view/main/gui-stdout.log) |

射线只在后续PhysicsProcess查询：初始9、成功10/11、重验竞争后的12、Current恢复后的13均独立确认。覆盖中心/非顶点高度、命中body/朝上法线、背面/区域外/错误mask不命中；全部mesh/shape角按既有1e-4m容差比对。setter成功只代表绑定，不直接标物理同步。

资源准备前先按成功请求去重；准备后Commit重验Current、权限与取消。准备拒绝不提交，绑定异常后权威13仍已提交，明确标投影版本未知，下一物理帧旧碰撞仍为−0.5m；从Current恢复13不增加版本。最后一次测试在绑定中真实Free collider，使恢复也失败：停止调用者但保留权威14，退出释放旧资源。准备中途异常与首个绑定异常为明确标记的合成注入，不冒称真实资源耗尽。

GUI截图通过Cua在会话内观察，未宣称导出截图文件；关窗退出由本次真实CLI进程回执确认，不使用信号。Cua退出后getAXState额外重启无DOTNET_ROOT的Godot管理器，产生hostfxr提示；两次结果后停止此观察路径，architect核对，最终native quit后只读应用清单与进程确认Godot/Yudian均停止。这个工具副作用不计作测试场景的引擎错误，未安装/修改工具链。

## 外包与审查

GLM原生模型回执为GLM-5.3-Flash，session`sess_dd53b2aefe`；模型确认task_89ba7f6e95、实现task_8b2f8b3248、一次退回task_b772ddba6e。原提交939c79d→0516dda，主控集成584b6d0→58f3e1d，再处理不适用Nullable注解/冗余注释f3b08bf。[97条工具调用已读完](../evidence/2026-10-05-terrain-view/worker-tool-calls.json)，修改仅冻结四文件；Bridge根目录快照混入其他会话与编译产物，不作工人归属依据。工人未push/merge或自验收。Bridge上下文读数66604不等于计费token，费用UNKNOWN。

首轮工人编译类型错误已修；首个native检查exit0/8PASS却故意查询已释放RID并产生3个引擎ERROR，主控不接受，退回改原实例IsInstanceValid并保留日志。返工实际有3个Nullable编译警告，主控读取原日志后去掉3处注解，最终零警告。未过滤或静音引擎输出。

主控首编译Environment歧义、首次角比对bit相等误拒绝shape量化的失败保留；分别明确命名空间与采用既有容差修正。reviewer还发现原准备失败用例先被codec拦截、冷启动旧版按钮可提交有效初始patch；两项已修并加入实际验收。最终reviewer核读源码与日志无剩余问题；architect核对提交后故障/资源边界与GUI收尾方式。二者未重跑GUI，不冒称新一次实机测量。

复跑入口：[运行说明](../../../tools/terrain-world-tests/README.md) / [冻结合同](../contracts/terrain-view-r1.md)。当前完整范围只看[产研TODO](../tasks/todo.md)，旧Main与PERF留项保持开放。
