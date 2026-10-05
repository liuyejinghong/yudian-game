# T3a-DATA · 数据接缝复验

2026-10-04，Codex主控接受**只读局部高度数据子件**。本轮具备不可变快照/候选、严格JSON解析与值保持序列化、外围锁定、base/current内容和版本比对。没有Apply/Commit世界API、游戏保存文件写入、矿物结算、碰撞/导航或场景接入；不能据此宣布完整T3完成。

## 提交与职责

- 合并起点main `707258f12e400508c880166cd10e608f137d16d1`：所有者明确授权后，PR16底座与PR18双线需求已合并；PR13/14仍按原范围保留。
- 主控[合同r1](../contracts/terrain-patch-r1.md)/[执行票](../tasks/T3a-DATA.md)冻结于`2a37f4b`；独立工作树的GLM原提交`d1f7cd6307689f6ed846c1deb1e4acf70c0aaa9b`仅新增7个授权源码/测试文件。
- reviewer在原提交发现SyncRoot可变存储逃逸和孤立Unicode代理项异常类别不符；主控在旧集成代码复现，39项独立用例中28通过、11失败。原记录保留，未以首轮通过掩盖后续发现。
- GLM追加限定修正`1a0ec1c2a0efa2c116274eddd1fa6732b9d50a5c`，集成`80b8148`。主控只另改会话历史评论、增加独立验收/证据/任务状态；不重写工人实现。
- 实际Bridge session `sess_b3900411d0`；模型确认task_82aaa44bfd原生返回GLM-5.3-Flash，observed_model未提供。实现task_c2f9d7f145耗时1379秒、限定返工task_8ad63300d8耗时245秒；usage.used工具读数分别109485/78077，不能相加当作计费token或费用；费用未知。会话已结束。

## 独立结果

| 验证 | 实际结果 | 限制 |
|---|---|---|
| 主控重新运行GLM测试 | 388项PASS（194个案例×两种文化），exit0 | net10实际执行，不是388个独立业务场景 |
| 主控独立金样/负例 | 39项PASS，exit0 | 包含原11个失败回归、完整字段往返、只读snapshot/patch/base |
| reviewer复核两项缺陷 | 12项只读复现通过，追加diff无新实质缺陷 | 不替代主控全量检查或游戏验收 |
| 两个测试工程编译 | net8.0、net10.0，0警告0错误 | 独立net8运行时没有，net8执行NOT_RUN |
| 既有文件保持 | 130个原prototype/art/旧QA证据文件逐项SHA256不变 | 新数据类未被Main调用，旧基准不作新地形性能证据 |
| 游戏/原生/GPU/碰撞/导航/保存恢复 | NOT_RUN | 本数据票没有实现这些能力 |

只读高度存储为私有IReadOnlyList包装的防御复制，不提供ICollection/SyncRoot或可修改数组；解码错误明确转为带字段/json路径ArgumentException并保留inner。ValidateAgainst仅校验已捕获当前快照，真正提交前仍由未来权威层重验权限、取消和当前版本；取消不得回滚已发生改造。

实际SDK10.0.401、独立runtime10.0.12；net8引用包8.0.31使用本机缓存。空本地NuGet源、NuGetAudit=false、UseAppHost=false，无包下载或环境安装。复现命令/exit/source hash见[verification.json](../evidence/2026-10-04-terrain-data/verification.json)，初轮通过与新增回归失败另行保留。公开日志仅把个人绝对路径替换为$WORKTREE/$HOME，原日志保留本地，不改测试内容或结果。

下一步：冻结T3b/T3c候选网格的插值、区域/共享边与独占目录，再交GLM做确定子件。T3d权威提交/消费者发布与持久化恢复仍由主控定合同和审查；ART-T01/T02正式接入仍等待实际地形接缝验证，数据接受不自动解除其阻塞。
