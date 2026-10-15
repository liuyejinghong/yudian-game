# 官方 cua_repl 正常接入 · 实际结果

2026-10-14｜所有者明确授权按截图方法测试，并希望打通游戏开发自测所需computer use。测试按[计划](CUA-REPL-PLAN.md)进行，原始返回与本机配置备份仅留job私有目录。

## 当前结论

官方插件cua_repl command/args/env正常启动可用，用户MCP `codex-cu` 已注册，Claude CLI回读 `Connected`。独立客户端以自身真实clientInfo运行，指定应用正常审批后，Calculator六次按钮点击实际得到表达式`12 × 12`、结果`144`。这推翻了将此前直接SkyClient的发送者拒绝外推到cua_repl的判断。

完整游戏computer use仍未通过：外部启动Godot会前置；Sky正常托管启动原r1私有副本可后台运行和渲染，但目标窗口状态读取返回`timeoutReached/cgWindowNotFound`。新Claude print宿主先因没有应用级elicitation处理而拒绝；使用官方临时逐项处理后审批已接通，但仍cgWindowNotFound。前一拒绝不是所有者未授权，后一窗口错误也不被混写为鉴权拒绝。

## 连接与注册

- 本机unified-computer-use版本26.1007.30912；官方Node24.21.0、cua-repl0.1.0、cua0.2.6、sky0.7.6；原生Sky客户端26.1005.1001425；Claude Code2.1.296。
- 私有协议客户端用官方entry命令/参数/17项env，clientInfo为`yudian-cua-repl-test`，未注入Codex会话/turn身份。删除继承的第三方身份字段后，仅保官方配置原值；没有社区签名launcher、私有IPC直连或系统权限改动。
- 真实握手protocol2025-06-18、rmcp1.5.0、4工具：js、js_add_node_module_dir、js_reset、turn_ended。只调用js，没有新增模块目录或伪造turn_ended参数。
- 首握手0.171618s、exit0/reaped、stderr0、无tool与审批；正常stdinclose后自有进程组不存在。
- 通过正常`claude mcp add-json --scope user codex-cu`注册stdio，仅复制command/args/env。执行前精确字节备份用户配置；执行后`target_exact=true other_fields_equivalent=true`，再prepare回读equivalent。`claude mcp get codex-cu`实际Connected。
- 当前会话工具目录未热加载新MCP；本轮真实动作经同一stdio官方运行时，另起真实Claude print宿主做对照。不据注册成功签桌面输入通过。

## Calculator真实动作

首次`cua.getApp("com.apple.calculator")`触发get_app_state应用访问审批；主控核仅本轮Calculator后显式accept。当前客户端不自动accept，未知请求/拒绝会停；一次尝试`_meta.persist=session`没有建立持久许可，后续仍逐项决定，不伪造宿主持久化状态。

新鲜主页AX显示值0及按钮One/Two/Multiply/Equals，随后按这些实际index执行六按钮1、2、乘、1、2、等于，并回读状态。8次限定Calculator审批包括首次读取、6次click和结果读取。

实际最终工具结果：

```text
上个表达式, Value: 12 × 12
StandardInputView;value:144
编辑字段, Value: 144
isError=false
```

该会话2次js调用，8审批/8显式回复；正常stop后exit0/reaped，stderr0，组EMPTY，无未决请求。计算器起后/结果后单点frontmost=false；两个15s采样各751样本，Calculator前台0、前台切换0、pointer变化0。采样只证明所覆盖时段，不能证明真人每段都在移动或保证全部时间输入隔离。

另起补验键盘/截图的连接，首次getApp返回`-10005: cgWindowNotFound`，未发送键盘或取图。客户端正常exit0/groupEMPTY，原Calculator进程仍在；这类窗口失败与发送者鉴权拒绝分开，键盘/截图未通过。

## Godot启动与窗口诊断

### 外部启动

两个私有工程实例分别`open -g -n`和`open -g -j -n`，启动后单点读到游戏前台，立即仅TERM自有PID。第二实例与getApp读取交叠，正常cua_repl实际取得主页完整AX/isError=false，未点击；所有实例与客户端已退出。

主控独立核官方[display_server_macos](https://github.com/godotengine/godot/blob/4.7.2-stable/platform/macos/display_server_macos.mm)的主窗口show_window会先activateApplication；[godot_application](https://github.com/godotengine/godot/blob/4.7.2-stable/platform/macos/godot_application.mm)先无条件activateIgnoringOtherApps，再执行其他条件。no_focus只影响后续窗口key选择，外部background参数不能签收后台。源码不证明本轮精确前置时刻。

### 官方Sky正常托管私有原包

从原教学r1完整包复制211MB到新私有测试app，真实独立bundle标识，C入口仅设置已存在的GUI_TEST/私有槽/设置/viewport根并正常exec包内引擎。原引擎PCK及全部Resources字节相同，.NET仍内嵌8.0.31；主控独立签名验证有效。原包未改，不把此副本当新r2试玩交付。

正常getApp启动新测试app并批准仅该app：首次返回`-10005 timeoutReached`。该进程实际运行、非前台，保存了主页viewport；主控实际Read图像，一秒采样显示NSApplication/CFRunLoop与Metal绘制，没有启动死锁证据。

连续300s、15001样本中按自有PID计前台0、前台切换0、pointer0。正常Sky托管后台运行在此段成立，但状态回读失败，不签输入可用。

同已渲染实例再读完整路径、随后按已核bundle ID读，均`cgWindowNotFound`，未点击。每次客户端exit0/reaped/stderr0/groupEMPTY。定向NSRunningApplication/CG检查确认身份/包路径正确、一个可见layer0窗口存在；排除当前CG无窗口与NS身份不符。单次目标AX检查windows status0/count1，但元素role为AXApplication，position/size返回attributeUnsupported；这仍不能证明Sky内部匹配的失败原因。完成诊断后仅TERM自有实例。

### 正常直接入口对照

下一独立新副本以`CFBundleExecutable=Yudian`和普通LSEnvironment五测试变量直接启动，真实新bundle ID，无C wrapper；新副本仅正常签名区变化。去签名后的两个临时引擎副本cmp一致，PCK/allResources不变，原包保持字节不变。主控独立codesign/resourcesdiff通过。

正常Sky getApp启动对照同样`timeoutReached`，已产生主页viewport；稍后单点读到frontmost=true，立即TERM，未输入。不能据此认定wrapper为原因。连续采样的时段另保原件，单点负向不被采样0取代；游戏窗口匹配根因仍未确定。

## 真实Claude宿主对照

本机`claude -p`只加载官方codex-cu、禁用内建工具、保正常权限；实际init显示codex-cu connected，默认模型沿用当前会话配置。它只调用一次`getApp("com.apple.calculator")`，返回`Computer Use was not approved to use Calculator`，没有键盘/计算/取图。

CLI进程exit0只是本轮正常结束，结果的permission_denials为空也不等于应用许可成功；MCP工具调用可用与服务器应用访问elicitation是不同层。主控独立读取[官方Elicitation文档](https://code.claude.com/docs/en/hooks#elicitation)，采用本CLI临时`--settings`处理器；不改既有用户/项目settings或hooks。它只记录指定server、应用的空form请求，等待nonce匹配的主控决定；陌生app/schema或未决定decline，没有批量自动accept。

真实宿主第二次运行产生Calculator请求，主控读取后按既有测试授权写单次accept。宿主随后返回`-10005: cgWindowNotFound`，停止，没有计算/键盘/截图。正常Elicitation处理入口已实际工作，窗口读取依然失败；不能把hook接通签为完整桌面操作成功。两个真实宿主进程均exit0，未忽略tool错误。第二次180s/9001样本中Calculator前台0、前台切换0、pointer0；此段仅失败状态读取，无成功输入隔离证据。

## 验证与当前限制

主控独立客户端mock首次12项PASS；实施方另复现存活检测killpg(group,0)偶发EPERM，失败原件保留，修正为只读owned-PGID检查并保证finally wait/close，查询失败记UNKNOWN而非PASS。另按真实等待修正人工审批120s、暂停未决期间RPC30s并回复后重置；局部模拟有实际输出，真实后续客户端每次清理均独立核。

前两次测试客户端失败分别是本地请求0600投递竞态（零tool）与审批等待30s超时（未accept）；已重计划原子投递/独立审批计时，没有把它们混写为Sky拒绝或抹去。

主控单服务器注册16项mock实际全部PASS；图像提取器和长monitor仅测试工具，不代签真实截图或游戏交互。必要文档验证当前PASS，最终独立证据验收与交付记录写入PLAN。

尚未通过：Godot新鲜系统截图、世界坐标/对象选择/拖拽、后台键盘、完整持续游戏操作；没有新r2实现或真人无提示验收，不关闭D001。所有自有游戏和MCP子进程已独立ps核对不存在；本轮从未运行状态创建的测试Calculator也已清理。四个私有槽目录无文件，未保存进度或更改正式槽。两次工程启动日志没有ERROR/Exception匹配；私有wrapper副本清理后native.log实际出现35条ERROR：33条`BUG: Unreferenced static string to 0`，另有`No render buffer nor reflection atlas, bug`及`Pages in use exist at exit in PagedAllocator`。该日志不能写成无ERROR通过；错误是否与窗口失败有关未确定。直接入口副本未单独捕获stdout/stderr/native.log，不宣称已核这些日志。

正式存档、默认应用、原包、系统权限和Codex信任配置未改；用户配置只新增已授权codex-cu，临时hook设置不写入现有配置。公开仓库仅保存脱敏报告；user配置备份、raw状态、采样坐标、测试副本与图像保留私有目录。后续若要全游戏后台验收，必须先解决正常窗口捕获/AX匹配和持续后台边界；不再把注册或计算器子测试代签完整能力。
