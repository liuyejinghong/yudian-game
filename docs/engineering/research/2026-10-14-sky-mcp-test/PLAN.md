# Sky MCP 与人机并行 · 有界测试计划与续接

2026-10-14｜所有者授权按建议实际测试，并说明仍会正常操作电脑做其他工作。范围为电脑执行层兼容与最小后台游戏动作；不改游戏玩法、默认应用、正式存档、系统权限或Codex可信身份。结果与限制见 [RESULTS.md](RESULTS.md)。

## 1. 协议与授权兼容

- [x] 核本机原始客户端/官方launcher。computer-history包launcher仅exec同一Sky客户端；实际`--help`和`help mcp` exit0，明确普通`mcp`入口，未调用computer-history或应用活动历史。
- [x] 真实yudian-sky-compatibility-probe身份、capabilities为空，限时initialize/tools/list实际通过：2.3884s、protocol2025-06-18、10工具、client exit0/reaped、stderr0；此阶段未启动Sky服务。主控独立mock顺序/schema保存、禁止非允许方法、挂起清理均通过。
- [x] 一次固定Godot路径get_app_state实际isError，`-10000 Sender process is not authenticated`；3.024672s、client exit0/reaped、stderr0。随后停止全部Sky调用，不切换可信宿主/身份或直连IPC；不推断TCC或真人鼠标为原因。

## 2. 独立AX与后台门槛

- [x] 私有Godot实例与槽/设置根，Metal/CLR10.0.12/12机器人就绪。Sky状态调用被拒后另测已安装agent-desktop自身合法AX路径；不调用Sky或换宿主绕过。
- [x] 默认headless新鲜find/AXPress实际home→settings→home；查询/设置/返回各15s、751样本，游戏前台均0、前台切换均0，pointer自然变化219/151/171。调用unverified由控件及页面日志回读确认，不盲重复。
- [x] 后续`open -g -n`新私有实例的窗口/NSWorkspace快照均显示前台，输入前立即TERM自有PID并确认退出；该次原因未定位，不归因所有者。
- [x] 改fresh launch两次参数拒绝均not_delivered：不接受完整app路径；改已核bundle ID后不接受macOS `--cwd`。按两失败规则停、记录重计划，再只移除不支持`--cwd`，保绝对项目`--path`与私有槽；默认非activate/headed launch成功。
- [x] 修正启动前开始15s监测，按新游戏PID核751样本前台0、前台切换0，pointer自然变化224；启动后单点false。仅该段通过，不外推持续后台。
- [x] 新实例两次find TIMEOUT后停止重复；记录重计划改新snapshot。初次误传不支持`--max-elements`被参数拒绝未投递，读当前help后仅用受支持`-i --compact --max-depth 10`；主页12-ref完整回读。
- [x] 新ref AXPress新游戏→私有空槽开始，19-ref选槽/52-ref游戏HUD和日志home→newgame→game epoch1共同确认。两段各751样本，游戏前台均0、前台切换均0、pointer自然变化258/415；没有世界坐标或键盘输入。

## 3. 可见画面差异与停止条件

- [x] game状态后两次仅目标窗口截图仍是旧主页且同SHA256；实例存活、前台false。可见世界未通过，未根据旧图选址。该实例经AX菜单→退出→不保存离开正常结束，ps确认无进程。
- [x] 只读调查并复核已存在`YUDIAN_PLAYER_CAPTURE_DIR`入口：主循环连续状态key后保存viewport，并非等待FramePostDraw；UI自测试另有FramePostDraw方法，本轮未用。无新增代码或依赖。
- [x] 新私有实例启用viewport目录，只产生主页文件；launch返回非focused，启动前15s/751采样按游戏PID前台0，但约7s后NSWorkspace单点frontmost=true。输入前立即TERM自有PID并确认退出。有限时段不证明持续后台，前置及旧帧原因均未定位。
- [x] 停止再次启动与输入测试，保留上述正负证据；不恢复真人焦点、不要求所有者停电脑、不回退物理键鼠。
- [ ] 世界选址/对象选择、键盘、game viewport图像：NOT_RUN；持续后台启动与新鲜窗口画面未通过。现有AX只能用于已核菜单/状态回归。

## 4. 验证、清理与交付

- [x] 主控显式切换验收视角核协议/状态/图形分层。六段通过采样共4506样本未观测游戏前台，保留两次后续前台快照及旧图；pointer自然活动不归因。20ms采样可能漏短变化。
- [x] 五个自有游戏与两次MCP子进程ps均不存在。两个游戏定时退，两次前台条件不符只TERM自有PID，新游戏实例正常不保存退出；五个私有槽无文件。五组实际日志无ERROR/Exception匹配；最后两组仅native log，没有单独stdout/stderr记录。
- [x] 未结束Sky服务或用户应用，未读真人内容/键入/剪贴板/历史，未操作正式槽、默认应用或游戏代码。
- [x] Sol已独立核Sky和首组AX证据及文档，指出缺文档命令记录；主控补已执行命令后Sol复核关闭。新增启动/新游戏/旧图/capture差异由同一verifier续核，结果在下方更新。
- [x] 主控首次文档检查实际PASS：`python3 tools/verify_documents.py`（完整性、内部链接、6历史哈希、A01–A24/B01–B08）；`git diff --cached --check`无输出exit0；私人路径、PID、pointer坐标、TODO/debug检索无匹配。后续报告更新后作最终针对检查。
- [x] 同一Sol verifier最终增量复核通过：实际Read两张旧主页图并核同hash；新增三段各751样本、前台/切换0、pointer224/258/415，capture采样后6.984秒快照true，日志newgame→game→pause→leave，报告声明与证据一致。主控最终文档完整性PASS、格式检查干净；仅检索说明提到检查词，未见调试残留。
- 交付分支为`feat/tutorial-r2-batch1-20261014`；测试记录提交与推送由主控在最终交付核对远端HEAD。不合并/main/force-push。raw schema/log/monitor/截图在job私有tmp，入仓仅脱敏摘要。

## 5. 当前续接点

本次执行已收束。Sky原生客户端版本26.1005.1001425；本机agent-desktop/Peekaboo查询Accessibility和Screen Recording granted，不外推Sky调用方信任。普通MCP被拒后没有借可信Codex runtime、daemon或remote bridge。

独立AX可操作菜单和新游戏状态，`find`超时时完整新snapshot曾恢复，但持续后台与新鲜窗口画面不足以支持世界测试。下一方案优先公开模型无关Cua Driver；需先按其自身公开契约核安装范围、宿主正常权限及Godot兼容，再小范围实际测。完整macOS guest仅作备选，不下载大量镜像，不代主机性能或触控板体验。当前二者均未安装/运行。

原r2批1规格、GitHub任务与游戏实现未在本轮产生；既有认可方案、基线与D001状态保留。后续新执行器未测前，不能让玩家停电脑来补工具局限，也不能把AX状态成功签为真人可玩或完整computer use通过。
