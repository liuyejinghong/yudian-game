# T3d-MEM · 单区域内存提交合同 r1

2026-10-04，Codex主控。只将已接受的TerrainSnapshot/Patch接入单区域内存权威状态；不接Main/渲染/物理/通行/导航/资源/文件保存。不是世界事务或完整T3。调用者在同一模拟写线程串行提交，候选可离线计算；此类型不声明跨线程安全。现有terrain-patch-r1的数据与JSON合同不变。

## 纯判定（GLM唯一写者）

命名空间Yudian.Terrain；只用net8标准库和已有TerrainDataCodec，不新增依赖。

`TerrainCommitStatus`枚举：Ready、Cancelled、PermissionDenied、StaleBase、NoChange、VersionLimit、Committed、AlreadyCommitted、RequestConflict。

`TerrainCommitPolicy.Evaluate(TerrainPatch patch, TerrainSnapshot current, bool permissionGranted, bool cancellationRequested)`返回该枚举。只返回前六项，不改对象、不创建新版本、不写文件、不登记请求。权限/取消输入由权威调用者取得，模型不得自报授权。

判定顺序固定：

1. patch/current为null：ArgumentException，参数名明确；即使权限为false或已取消也检查null。
2. cancellationRequested为true：Cancelled。
3. permissionGranted为false：PermissionDenied。
4. 调用已有ValidateAgainst，合法对象的base与current不完全匹配：StaleBase；不能仅比version。
5. 候选每个高度与current数值严格相等（+0/-0同值）：NoChange。
6. current.Version为9007199254740991：VersionLimit。
7. 其余Ready。

因此取消优先于无权限；二者优先于旧base；合法无变化在版本上限也NoChange。不能吞异常变为Ready。对象由已有严格codec创建，纯判定不复制解析/统计算法。

## 权威提交（Codex唯一写者）

`TerrainRegionState(TerrainSnapshot initial)`、只读`Current`；`Commit(string requestId, TerrainPatch patch, bool permissionGranted, bool cancellationRequested)`返回不可变TerrainCommitResult。初始快照/null、requestId格式/null、patch/null非法均ArgumentException；requestId用既有ASCII ID规则`[a-z][a-z0-9_-]{0,63}`。对象负责一个region，跨region由ValidateAgainst拒绝。

Result只读Status、RequestId、PatchId、Snapshot、AppliedVersion(long?)。Snapshot始终表示本次调用结束时的权威快照；AppliedVersion仅在Committed/AlreadyCommitted表示该请求原成功版本，其余null。回执不被当作渲染/物理已同步的证据。

先校验参数，再查询成功请求记录：同requestId且patch语义相同，AlreadyCommitted，返回当前Snapshot和原AppliedVersion，不再推进；同requestId但patch_id/base/候选高度不同，RequestConflict，状态不变。patch语义比较涵盖完整base与候选，数值按严格double相等，+0/-0同值，不按JSON文本或仅patch_id判断。已成功请求的重复查询不因随后权限撤销/取消重写世界。

尚未成功请求由Policy按上述顺序判定。Ready才创建新不可变快照：布局/region与current相同，高度取patch候选，version=current.Version+1；新快照与成功回执准备完整后才发布Current。Cancelled/PermissionDenied/StaleBase/NoChange/VersionLimit不修改Current或成功请求记录。NoChange不推进版本，也不消费requestId；失败请求可在权限/输入恢复后重试。

取消只阻止未提交或迟到候选。已提交结果永久保留在当前状态中，不还原base、不重放发资源。此票没有资源结算。单写者顺序例：两个同base候选均可离线Ready，先提交者成功，后提交者在入口重验后StaleBase。版本上限不得溢出/绕回。

成功请求去重仅在此对象存活期有效；内存记录随成功请求增加，此票不签持久化或长期内存预算。以后保存/恢复/回执保留策略另冻结，不先搭通用日志平台。

## 金样与独立验证

使用既有3×4非对称金样：region=sample-b、version=9、origin=(-2,3)、spacing=1，高度10/11/12/13、20/21/22/23、30/31/32/33。patch-a仅内部21/22→0/-1；成功version10；旧快照仍version9/旧高度不变。第二个同base不同patch拒绝；新base第二次成功到11。取消第二次保留第一次；相同请求重放不加版本，同ID不同payload拒绝。覆盖无变化/上限/同版不同内容/权限恢复重试/空参数/非法ID/集合不泄露，以及枚举判定优先级。

GLM纯判定测试与Codex权威状态验收分别独占目录；net8/net10离线编译，按实际可用runtime执行并记录，不假称本机安装net8 runtime。交付前关键状态reviewer只读审查；未执行的引擎、导航、保存均NOT_RUN。
