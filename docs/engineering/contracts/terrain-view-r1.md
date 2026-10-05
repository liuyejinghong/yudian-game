# terrain-view-r1 · 单区域权威提交投影

2026-10-05冻结；基线909cca3。只实现一个Stage网格区域的内存权威与ArrayMesh/静态shape接线，独立可运行场景；旧Main机器人高度仍未接入，不签通行、邻区、导航、保存、资源收益、正式美术或性能稳定。

## GLM资源束

`Yudian.Terrain.TerrainProjectionResources : IDisposable`，文件`prototype/scripts/TerrainProjection/TerrainProjectionResources.cs`。`static Create(TerrainMesh mesh)`复用现有TerrainMeshAdapter.Create与TerrainCollisionAdapter.Create，返回新资源对；只读`ArrayMesh Mesh`、`ConcavePolygonShape3D Shape`。创建任一阶段抛异常时释放已创建资源；成功由接收者持有整个bundle，替换/退出后Dispose，重复Dispose无害。不缓存、不持有RegionState、不绑定节点、不修改输入或材质，不引入依赖。

唯一内部测试入口`internal static Create(TerrainMesh mesh, bool failBeforeShape)`，true在mesh创建成功之后、shape创建之前抛InvalidOperationException；测试记录这是模拟异常，不冒称真实native分配失败。公开Create默认false。内部测试入口可记录释放前mesh RID并用RenderingServer有效性或原实例IsInstanceValid证明确实释放（不用计数猜测）；若平台无可用查询则在README准确写限制。

GLM仅写资源束、`prototype/scripts/TerrainProjection/Tests/ProjectionResourcesTests.cs`、`prototype/scenes/terrain-projection-tests/ProjectionResourcesTests.tscn`、`tools/terrain-projection-tests/README.md`。运行现有Godot工程无新csproj；测试至少覆盖实际mesh/shape全部角逐一等值、两个独立束RID不同、输入不变、非法输入、模拟中途失败与重复Dispose。native测试限headless；不跑GUI/性能，不改公共合同/TODO/PLAN，不自验收、push/merge或领取其他票。

## 主控权威入口

RegionState增加只读Evaluate(requestId,patch,permission,cancel)，与Commit共享校验/已提交去重顺序；不消费请求、不变版本。权威组件先Evaluate：重放/冲突/拒绝直接返回，无旧patch重投影；Ready才从不可变patch生成Stage资源束。准备失败不调用Commit。准备后再次读取入口持有的权限/取消并由Commit复核Current；拒绝释放新束、旧Current与投影不变。单Godot物理写线程，不提供跨线程承诺。

Committed表示内存权威已发布。随后绑定一对节点；setter失败不可回滚Current或撤销去重回执。旧资源在绑定完成之前保留，绑定异常尽力恢复旧节点资源，标记投影故障且投影版本未知；从Current显式重建投影恢复，不再Commit推进版本。恢复绑定也失败时停止场景，不能继续报告同步。成功替换后释放旧束；退出先解绑再释放。已释放或不在树中节点在Commit前拒绝。

分别记录权威版本、已绑定投影版本、后续物理帧验证版本；两setter成功不构成native同步证明。主控场景在下一PhysicsProcess用真实DirectSpaceState射线及mesh/shape同源检查确认版本。调试注入准备后取消/权限撤销/其他请求推进与绑定中途异常只能为内部测试钩子，默认null，不挂用户生产操作。

验收：9→10、后续新patch10→11、同请求旧AppliedVersion重放且保持当前投影；无权限/取消/无变化/旧版/冲突拒绝；资源束模拟中途准备失败不消费请求；准备后状态变化重验；绑定中途异常保留已提交版本、恢复旧投影但明确故障、从Current恢复不增版本；下一帧真实上射/下射/越界/掩码与mesh全角比对。GUI操作版本/中心高程/同源资源和物理版本变化，固定调试材质，不使用缺失正式模型替代。
