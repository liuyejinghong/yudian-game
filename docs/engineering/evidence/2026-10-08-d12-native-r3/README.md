# D1.2 r3 原生证据

2026-10-08。公开副本只替换私人本机路径，原件保留；不提交玩家存档或应用二进制。build-manifest的源/资产/二进制/PCK哈希属于固定包；evidence-sha256针对公开副本。

r3固定源码5add887d991c12ae5ad7606119fcd1a8bda85a74，实际runtimeCLR8.0.31。package-world为13个独立原生进程：full＋矿提交/载货/加工投入/产出/仓储取货/让位途中各prepare与resume。legacy-world为同包D11规则9进程。另有3进程旧schema2迁移、旧r1 schema3缺新字段、加工先建的首套保护。最终结果以runner正向标记和退出状态为准，源/UI检查另标身份。

source-clearance-red-r6/r7保留真实失败；绿replay用该让位现场的私有副本、实际加载/行走/后续建设。source-blocked-cancel-red-r3保留原取消阶段错误。合成采矿投影故障有意抛出后从Current恢复，不能把错误栈单独作通过或失败结论。

实际GUI仍NOT_RUN（Mac锁屏）。无实际窗口截图，不把headless、导出签名、Metal启动头或UI源码自测签为窗口操作、真人试玩、性能、模型或发行。入口与原档保持见entry-and-save-state.json。
