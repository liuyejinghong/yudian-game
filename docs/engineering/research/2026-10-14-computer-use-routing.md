# Computer use：人机并行与模型无关执行层

2026-10-14｜本机组件取证、公开资料核验与随后获准的有界测试见 [实际结果](2026-10-14-sky-mcp-test/RESULTS.md)：Sky握手/10工具发现通过，但真实get_app_state被`-10000 Sender process is not authenticated`拒绝。独立agent-desktop后台AX实际完成设置/返回和新游戏→game状态；六段采样未观测游戏前台，但另有两次启动后前台快照与game状态下旧主页截图。持续后台和新鲜窗口画面未通过，世界选址/键盘NOT_RUN。Cua Driver与VM未安装/运行，未改系统权限或Codex配置。本页不改变r2玩法规格，也不关闭原审核B001/B002或D001。

所有者指出：Codex的computer use已可用、有独立代理光标；自动化不应与人类争鼠标和屏幕。今后将“所有者在其他应用继续工作时，自动化仍能定向操作目标、保持系统指针与焦点”作为工具验收条件。

## 1. 本次失败能解释到哪里

依据 [r1基线验证](../plans/tutorial-r2/batch1/VERIFICATION.md)：当时Accessibility与Screen Recording查询均granted；AX只返回4个主页文字节点且truncated；Peekaboo定向see超时；focus-window未settle；agent-desktop物理点击请求与指针回读不符，返回delivered_unverified，回读仍主页。

这些事实不能证明所有者同时移动鼠标是根因，也不是未授权或游戏输入故障的证据。已定位的执行路线问题是：主控从语义AX回退到了共享桌面物理输入，而未先核对Codex已有可用后端。具体指针回读偏差和焦点失败仍未定位，不再盲试物理输入。

本机agent-desktop实际版本0.9.2；JSON顶层version2.4是响应信封协议版本，不能当工具版本。其binary自带文档明确：

- 默认headless ref动作只走语义AX，不隐式抢焦点、动指针或合成键盘；这不等于独立桌面。
- headed模式会前置精确目标窗口，物理指针命令共享OS pointer。
- cursor overlay只是每个agent的显示；文档原句“independent overlays do not create additional OS pointers”。interaction lease只串行化工具物理动作，不证明能约束真人输入。

来源：[agent-desktop作者仓库](https://github.com/lahfir/agent-desktop)及本机`agent-desktop version`、`skills get desktop --full`。本轮只查询说明，未再执行输入。

## 2. Codex成功链与可复用入口

本机已有：

- `Codex Computer Use.app`原生Sky服务与配套`SkyComputerUseClient`；旧Codex配置曾以`args=["mcp"]`注册该客户端，但旧直接入口当前disabled。
- 当前启用的unified-computer-use插件走`cua_repl`；REPL导入`@oai/sky`（本机0.7.6），通过可信runtime连接本地Sky原生服务。
- macOS公开给该runtime的窗口API按app标识调用`get_app_state/click/press_key/type_text/scroll/drag/set_value`等，坐标为目标应用截图坐标，优先AX元素索引。
- JavaScript native transport依赖trusted nodeRepl runtime；native错误类型包含调用方未鉴权、应用未允许、权限未授予、用户中断等，简单把库导入普通Node不能视为接入完成。

这些是本机安装产物/config指定节的只读证据，不发布二进制、完整配置、密钥、私有endpoint或会话内容。`mcp`入口存在，不等于已通过其他客户端的initialize、tools/list及应用授权。

原审核的同run续接记录曾明确使用`mcp__cua_repl.js`→mac原生app接口，并观察SkyComputerUseService；取图、菜单和一次正常保存成功。但原记录也保留Continue、焦点变化与退出未确认，不能将历史有限成功上调为任意操作稳定。

官方公开支持macOS后台任务，原文“running a scoped task in the background while you keep working elsewhere”。OpenAI公告还描述own cursor及多个agent在Mac并行且不影响人在其他应用工作。这个产品能力有公开依据，底层具体输入注入机制未披露，不应因此否定所有者已经获得的体验。Linux full-desktop API的“不激活窗口、不移动桌面指针”实现句仅针对Linux，不能外推macOS。

来源：

- [OpenAI Computer Use文档](https://learn.chatgpt.com/docs/computer-use)（本轮正文直接读取）。
- [Codex for almost everything](https://openai.com/index/codex-for-almost-everything/)（搜索命中官方own cursor与parallel原句；直接页面两次403，没有当作全文读取）。
- [官方仓库关于独立Computer Use MCP/provider接口的公开询问](https://github.com/openai/codex/issues/44155)（本轮gh读取正文/评论；未见维护者给出独立支持契约；其Intel问题不适用于本机arm64，也不证明本机无法复用）。

## 3. MCP与模型API的区别

可复用的是执行工具：外部模型经MCP请求目标窗口状态、点击、按键，Sky客户端交给同一本地原生服务执行。是否能保留调用方信任、应用许可、取消及清理，要由握手和实际操作核验。

`codex mcp-server`历史用途是把Codex代理作为MCP工具；`codex app-server`是面向Codex UI客户端的JSON-RPC接口与外部MCP管理。这两者不能冒称独立Sky桌面执行器。

OpenAI Computer Use API让模型返回动作，文档原句“You provide the environment and execute the model's requests”。它不提供本机Sky驱动；再购买一个模型API并不能自行消除共享鼠标问题。

来源：[app-server README](https://github.com/openai/codex/blob/main/codex-rs/app-server/README.md)、[Computer Use API指南](https://developers.openai.com/api/docs/guides/tools-computer-use)。

## 4. 少量方案比较与推荐

| 方案 | 人机并行依据 | 对当前Godot/macOS任务的结论 |
|---|---|---|
| 本机Codex Sky | 官方macOS后台使用与own cursor；所有者已报告成功，本机有匹配服务 | 现成MCP握手通过，但本CLI独立发送者真实状态查询被鉴权拒绝；不伪造身份或借宿主绕过 |
| Cua Driver | 公开模型无关SDK/CLI/MCP；quickstart明确“Your own window and pointer stay where they were: the moving cursor is the agent's overlay” | 开放替代首选候选；有只接受前台/HID输入等边界，Godot未在支持表验证，不能先签成功 |
| Cua Spaces/Lume macOS VM | Apple Silicon独立macOS guest桌面；guest输入与主机输入分离 | 完整隔离备选；需测试Godot.NET/Metal功能，VM回归不能代主机性能与真实触控板体验 |
| agent-desktop/Peekaboo | 定向语义动作可后台；物理输入不自动成为独立会话 | agent-desktop实际完成后台设置/返回和新游戏状态操作；持续后台启动、新鲜窗口画面未通过，世界坐标/键盘未跑 |

Cua Driver支持表列Electron/Tauri/AppKit/SwiftUI/WKWebView，未列Godot；其边界包括部分后台scroll/drag形态拒绝、minimized keyboard commit、仅HID-tap输入应用。应明确拒绝或回退独立环境，不强迫所有者错峰来掩盖局限。

Peekaboo当前公开README为4.9.2，有条件后台投递与显式foreground授权规则；本机为3.0.0-beta3，不能把新文档功能当已安装功能。升级前没有实测收益，本次不建议先升级碰运气。

Apple ParavirtualizedGraphics官方DocC JSON明确框架为macOS VM提供hardware-accelerated与Metal-accelerated graphics；不能泛称所有macOS VM无GPU。该文档不证明Lume配置、Godot特性完整支持或与主机性能等价。

来源：

- [Cua作者仓库](https://github.com/trycua/cua)
- [Cua Driver quickstart](https://cua.ai/docs/cua-driver/quickstart)
- [Cua Driver平台边界](https://cua.ai/docs/cua-driver/concepts/platform-support)
- [Lume quickstart](https://cua.ai/docs/lume/quickstart)
- [Peekaboo作者仓库](https://github.com/steipete/Peekaboo)
- [Apple ParavirtualizedGraphics](https://developer.apple.com/documentation/paravirtualizedgraphics)（正文从官方[DocC JSON](https://developer.apple.com/tutorials/data/documentation/paravirtualizedgraphics.json)核验）

## 5. 已测入口与后续最小验证

1. 原生Sky客户端通过官方launcher的`mcp`入口完成限时initialize/tools/list，未改disabled配置或调用身份。真实get_app_state被发送者鉴权拒绝后停止；不借受信任宿主、代理或私有IPC代此客户端绕过。
2. 对已在后台运行的私有Godot窗口，agent-desktop默认headless新鲜ref AXPress及控件/日志回读实际完成设置/返回和新游戏→game状态。`find`曾成功，也曾两次TIMEOUT；改新窗口snapshot恢复取得控件。所列六段采样均未观测目标前台，自然pointer活动未归因；仅接受菜单/状态范围。
3. `open -g -n`后一次前台快照不符已停止；fresh launch参数两次拒绝后按重计划移除不支持的`--cwd`，保绝对项目路径及私有槽，实际启动及新游戏状态操作成功。但game状态两次窗口截图仍为相同旧主页。再启现有viewport取图入口的实例，15s采样后约7s前台快照true，立即停止。持续后台启动与新鲜可见世界未通过，不再回退物理输入，原因仍未定位。
4. 当前工具不足以支持完整游戏computer use。后续评估公开Cua Driver并实际验证Godot、正常宿主权限、持续后台与取图；需要前台/HID输入时再考虑独立macOS guest。新工具/VM未安装，交互回归、原生Metal与主机性能分开记录。

早期含空格客户端直接命令曾被工作树守卫阻断；随后找到官方无空格launcher并正常取得help及MCP结果。当前结论来自真实执行：协议可发现，独立发送者未获Sky动作信任；菜单AX另有有限成功证据。
