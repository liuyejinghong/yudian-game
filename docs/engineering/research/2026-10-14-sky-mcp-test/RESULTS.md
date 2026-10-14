# Sky MCP 与独立 AX 后台操作 · 实际结果

2026-10-14｜实际执行：主控。探针实施与独立验证分开；Sol复核真实Sky返回及报告证据。所有者明确仍正常使用电脑；本轮只使用定向AX语义动作，没有物理鼠标/前置窗口输入回退，没有读取真人工作应用内容、键盘文字或剪贴板。范围与证明方式见 [PLAN.md](PLAN.md)。

## 结论

**Sky标准MCP初始化与10工具发现通过，但真实应用状态查询被发送者进程鉴权拒绝；当前不能将该客户端作为其他模型可直接使用的桌面执行器。独立agent-desktop的默认后台AX路径实际完成设置/返回，以及新游戏选槽→开始→game的状态操作。持续后台启动、实时窗口画面与世界键鼠仍未通过。**

Sky拒因原文：`Computer Use server error -10000: Sender process is not authenticated`。不归因所有者同时移动鼠标、不归因TCC未勾选、不归因Godot输入故障；没有借用Codex身份、改trusted surface或直连私有IPC来绕过。

AX成功依据是新鲜控件与游戏日志。另有两次启动后被观测为前台、两张窗口截图仍为旧主页的负向证据；均独立保留，不能用状态成功代替图形或全程隔离通过。本次没有建立满足所有游戏操作的通用隔离执行器，也不关闭原审核B001/B002或玩家理解问题D001。

## 1. Sky MCP协议与实际鉴权

| 阶段 | 实际输出 | 可证明范围 |
|---|---|---|
| 原始客户端入口 | 官方launcher的`--help`与`help mcp` exit0；`mcp`说明 Runs the Computer Use client as an MCP server | launcher来自computer-history包但仅exec同一Sky客户端；实际传入`mcp`，没有调用computer-history或活动历史工具 |
| 初始化 | protocolVersion2025-06-18，serverInfo.name=Computer Use，tools.listChanged=false | 实际clientInfo为yudian-sky-compatibility-probe，未发送Codex身份字段 |
| 工具发现 | 10工具，2.3884s，status=finished，child_exit=0，child_reaped=true，stderr=0 | 只发送initialize、notifications/initialized、tools/list；未执行工具，Sky服务未启动 |
| 后台启动游戏 | `open -g -n`，独立Godot实例；私有槽/设置根；Metal/M3Pro/CLR10.0.12、12机器人与1设施就绪 | 没有请求前置，不共享正式存档；未开启工程自动driver |
| 启动后观察 | 15s、751样本，game为前台0次、前台PID变化0次、pointer变化378次 | 此段未见前置；不覆盖启动前或两次采样间的瞬时变化 |
| get_app_state | 仅固定Godot完整路径，isError=true，-10000 Sender process is not authenticated；3.024672s | clientInfo为yudian-sky-app-state-probe；无图/AX，没有执行点击、按键、选址或输入 |
| 状态查询期间观察 | 15s、751样本，game为前台0次、前台PID变化0次、pointer变化0次 | 仅证这次被拒查询没有争用，不能代签成功输入隔离 |
| 客户端清理 | 两次客户端均stdinclose后exit0/reaped，未发terminate或kill | 不把正常清理当作桌面动作成功；Sky服务由官方客户端建立，未对其发退出/kill |

发现的10工具：list_apps、get_app_state、click、perform_secondary_action、set_value、select_text、scroll、drag、press_key、type_text。本轮未调用list_apps，避免读取14天应用使用记录。

官方无空格launcher可用，实际调用等价于已安装SkyComputerUseClient的`mcp`模式。临时Python客户端使用stdio JSON-line协议，capabilities为空，clientInfo保持自身身份；15s总deadline、2MiB输出上限，新私有输出目录/文件0700/0600；服务器反向请求不被自动接受。未更改原disabled配置。

“MCP入口存在”与“服务接受桌面动作调用方”是两个独立条件。普通探针请求有明确拒绝证据；这不证明所有正式受支持集成都永远不可用。OpenAI文档的macOS后台并行能力与所有者在Codex里的成功体验保留，本次只核本CLI独立客户端直接接入的边界。

## 2. 独立agent-desktop后台语义动作

使用agent-desktop自身已安装、已获系统权限的AX路径，与被拒的Sky服务分开。本机版本0.9.2，固定session、已知游戏PID对应窗口与新鲜snapshot ref；未使用`--headed`、focus-window、press、物理坐标或盲重复点击。

| 动作/观察 | 实际回读 | 15s观察结果 |
|---|---|---|
| 主页`find --role button --limit 0` | 8按钮完整返回，包含新游戏、继续（无档禁用）、存档、设置、退出及系统窗口按钮；无truncated | 751样本；游戏前台0，前台切换0，pointer变化219 |
| AXPress“设置” | 新find取得设置按钮；日志`PLAYER_GUI_PAGE page=settings epoch=0 slot=1` | 751样本；游戏前台0，前台切换0，pointer变化151 |
| 新ref AXPress“返回” | 主页按钮恢复enabled；日志`PLAYER_GUI_PAGE page=home epoch=0 slot=1` | 751样本；游戏前台0，前台切换0，pointer变化171 |
| 修正参数后的默认fresh launch | 非activate、非headed，返回新PID/window；日志主页就绪；启动后单点frontmost=false | 启动前开始采样，按返回游戏PID核751样本：游戏前台0，前台切换0，pointer变化224 |
| 新快照ref AXPress“新游戏” | 日志`page=newgame epoch=0 slot=1`；快照空槽与“开始新游戏” | 751样本；游戏前台0，前台切换0，pointer变化258 |
| 新ref AXPress“开始新游戏” | 日志`PLAYER_LOAD_READY ... time=0`及`page=game epoch=1 slot=1`；快照教学HUD、0/8、12机器人可用与建设工具 | 751样本；游戏前台0，前台切换0，pointer变化415 |

所有点击均返回AXPress/semantic_api/succeeded，同时仍标delivered_unverified、retry unsafe；主控以动作后的控件与日志回读确认，没有据工具成功字段单独签收。隐藏页面节点也可能留在AX树，必须核实际页面日志。

表中六段合计4506样本，未观测游戏前台或前台PID切换。工具机制为AXPress，默认模式文档保证不隐式合成物理输入；pointer变化只反映自然活动，无法归因真人、代理或其他来源。20ms采样可能漏掉更短变化；这些通过只适用于所列时段及动作，不是持续隔离保证。

### 启动合同、两次参数拒绝与重计划

前一个语义实例定时退出后，用新私有槽`open -g -n`启动另一实例；输入前窗口is_focused=true，NSWorkspace快照也frontmost_target=true。主控立即TERM这一自有PID并确认退出，未执行新游戏点击、未恢复真人应用焦点。这说明仅采用`open -g`参数不足以签收全程后台；该次未覆盖完整启动时序，不能归因启动器、引擎或所有者。

只读代码调查及主控关键行复核：`Main.ApplyWindowAndQuality`设置尺寸、缩放和MoveToCenter；菜单`GrabFocus`设置Godot控件焦点。所核应用代码未找到显式请求OS前置的调用，不能据此排除引擎或Launch Services行为。

随后收窄为agent-desktop默认非activate的fresh launch，两次均在投递前参数拒绝：完整app路径返回`INVALID_ARGS`（仅接受应用名/bundle ID）；按已安装Info.plist中的bundle ID调用、保`--cwd`返回`ACTION_NOT_SUPPORTED`（macOS Launch Services不支持精确启动工作目录）。两次delivery均not_delivered，没有启动应用。

按两失败规则停止并写重计划；之后只移除明确不支持的`--cwd`，保已核bundle ID、绝对项目`--path`和新私有槽。这次fresh launch实际成功，表中启动及新游戏状态操作来自该实例。

### 真实取图仍为旧主页

该实例第一次全按钮find返回hydration TIMEOUT，收窄新游戏find也返回locator deadline TIMEOUT；第二次activation.attempted=false。两失败后停止重复find，改取新窗口`snapshot -i --compact --max-depth 10`，取得完整12-ref主页，再取得19-ref选槽页与52-ref游戏HUD，随后才操作。

进入game后两次定向窗口截图均显示旧主页，PNG SHA256相同：

```text
00609aefa03227bf1edd37ba013bed464b9e549ba01862804d73be52f70250da
```

主控实际读取两张图，并核目标实例存活及前台false。因此不能把日志/AX的game状态签成可见世界通过，也不能用旧图指导世界坐标操作。随后经AX菜单→退出游戏→不保存离开正常退出，ps确认不存在，没有写档。

项目已有`YUDIAN_PLAYER_CAPTURE_DIR`测试入口，可直接保存viewport纹理以区分渲染内容与系统窗口取图；源码的GUI目录入口连续观察同一个状态key后保存，不是FramePostDraw等待。仅UI自测试的另一个CaptureViewport方法等待FramePostDraw，本轮未调用。

为此另建私有实例启用现有capture目录。launch返回is_focused=false，启动前15s/751采样按新PID计前台0、前台切换0、pointer变化0；采样结束约7s后的NSWorkspace单点却frontmost_target=true。主控立即在输入前TERM自有PID并确认退出，只留下主页viewport文件，未进入新游戏。没有再次启动或前置窗口取图。

**当前未定位旧帧来自游戏后台渲染、窗口合成还是捕获路径。后一次监测与后续快照不同，说明有限采样不能证明持续后台；未证明前置的来源，也未归因真人。**

## 3. 可复用的有限入口与后续选择

对已经运行、处于后台且确认是私有测试实例的Godot窗口，可用AX语义菜单。先用已知PID对应window ID；禁止重新绑定已退出应用以免重启。session保持相同，UI变化后重新snapshot/find：

```sh
agent-desktop --session yudian-background-test snapshot \
  --app Godot --window-id <当前私有实例的窗口ID> -i --compact --max-depth 10
agent-desktop --session yudian-background-test click @<本次snapshot>:<目标按钮ref>
```

占位ref只能来自当次实际回读。动作后重新观察控件、日志与NSWorkspace前台采样；工具返回unverified时不盲重复点击。命令没有headed、activate或focus-window。`find`曾通过，但新实例两次超时；新snapshot是本轮实测恢复入口。

启动工具只接受应用名或bundle ID；本机已核Godot标识为`org.godotengine.godot`。实际默认fresh launch不带activate/headed/cwd，绝对项目路径通过重复`--arg`传入；GUI_TEST、私有槽/设置根通过`--env`传入。这个合同已运行，但后续另一实例成为前台，**不推荐把它直接作为无需监测的默认持续后台启动器**。

现有AX适合有限菜单/状态回归，尚不能承担完整原生游戏computer use。下一候选是公开模型无关的Cua Driver，需实际核Godot兼容、自身宿主权限、持续后台与新鲜窗口取图；本机未安装它。完整隔离的macOS guest仍作备选，不能代主机Metal性能或真实触控板验收。不让所有者停用电脑、不通过伪装Sky身份补齐缺口。

## 4. 独立验证与清理

主控全文读取最终探针再实际执行。mock自测试实际输出：

```text
PASS outgoing tools/call, elicitation, resources/read, ping refused
PASS normal order/schema saved; private files 0700/0600; child_exit=0
PASS timeout own-child terminate/kill/reaped; child_exit=-9
MOCK_ONLY: all checks passed; real Sky NOT_RUN
```

最后一行仅说明mock自身不运行Sky；实际Sky在第1节记录。只读monitor实际编译通过，主控1s sample得51样本、非法target-pid拒绝exit2；不创建窗口、不激活应用、不改pointer、不装eventtap，不读真人键入。

Sol只读核实际initialize/tools/summary/state-result及菜单monitor，接受协议通过/状态拒绝/有限AX成功分层；两次client正常退出不被当桌面操作成功。state raw stdout386bytes、stderr0。新增启动、新游戏、旧图与capture实例的证据复核记录见[PLAN.md](PLAN.md)。

所有五个自有游戏及两次MCP子进程均经ps核对不存在：前两个游戏定时退出，两次前台条件不符只TERM自有PID，进入game的实例经正常“不保存离开”退出。五组游戏日志检索`ERROR|Exception`无匹配；五个私有槽目录无文件。最新两个实例采用native log，未另捕获stdout/stderr，不能声称存在并核过这些文件。未对Sky服务或用户应用发退出/kill；正式存档、默认试玩应用和游戏源码未改。

文档完整性命令与分支交付状态见PLAN；文档验证不代签完整图形、原生键鼠或真人无提示体验。

## 5. 未通过与证据保留

- Sky成功状态读取、点击、键盘、选址与成功输入隔离：NOT_RUN；明确发送者鉴权拒绝后停止。
- agent-desktop设置/返回、新游戏→game状态：已执行且回读确认；持续后台启动与新鲜可见世界画面未通过，坐标选址/世界对象选择/键盘NOT_RUN。
- viewport入口已保存主页文件；因新实例前台条件不符，game viewport核验NOT_RUN，未签旧帧根因。
- 原TCC查询为Accessibility/Screen Recording granted；不等于Sky调用方获信任，也不要求所有者重新批量授权。
- Cua Driver、VM、运行其他Codex会话代操作：NOT_RUN。未安装新执行器，未改系统权限或Codex配置。
- 原r2批1规格/任务/实现尚未产生；本轮电脑测试不改变已认可玩法方案，不关闭D001。

握手/schema/拒绝、mock日志、monitor坐标、目标窗口截图与游戏日志仅留当前job私有tmp。入仓保留不含私人路径、真人应用PID、坐标或活动历史的结果摘要，不公开专有二进制、完整Codex配置或正式存档。
