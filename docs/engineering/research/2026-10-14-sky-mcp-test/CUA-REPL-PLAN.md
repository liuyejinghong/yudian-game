# 官方 cua_repl 正常接入 · 测试计划

2026-10-14｜所有者明确授权按截图方法测试，目标是打通游戏开发所需computer use。使用本机已安装官方unified-computer-use的cua_repl command/args/env；保自身客户端身份，不复制Codex会话元数据，不借社区签名launcher，不自动接受审批，不改系统权限。所有者继续正常用电脑。

1. 正常MCP连接：核官方配置与已装执行文件，私有有界客户端initialize/tools.list，保实际工具schema；正常注册Claude用户MCP时只新增codex-cu，备份对应配置，不覆盖其他服务器。证明：真实响应、进程清理和单服务器配置回读。只读动作前若有审批，保真实请求，不伪造或批量accept。
2. 后台计算器：限定Calculator窗口并核它非真人既有工作，获取新鲜状态，按实际工具schema定向输入12×12，回读144；动作期间只采系统pointer/前台PID，不读真人内容。出现争用停止自有测试，不让所有者停电脑。证明：新鲜截图/状态与实际采样，不以工具success代结果。
3. 私有Godot：已知实例、私有槽/设置根、原生Metal；正常菜单进入世界，核新鲜图像，执行一件可恢复输入/必要拖拽或选址，不提交建设。证明：日志+画面+输入反馈+焦点保持；键鼠及触控板/真人理解分层。
4. 独立验收与交付：实施probe与验证分开，主控执行最小合同，重要执行/审批边界由Sol独立审；保正负证据、准确NOT_RUN、清理自有进程，不影响用户应用与Sky服务。必要文档提交推送当前独立分支。

同一步两次失败记录证据并重计划，不无限重试。若正常cua_repl也明确拒调用方，停止该调用路径，不能以伪造身份或信任祖先解决。新配置/工具连接不证明完整computer use已经通过。

## 实际进度

- 官方运行时真实握手完成：protocol2025-06-18、rmcp1.5.0、4工具，0.171618s、exit0/reaped、stderr0、未执行tool、无审批请求；stdinclose后自有进程组不存在。
- 主控独立12项mock检查实际PASS，但实施方另复现清理存活探测`killpg(group,0)`偶发EPERM；原失败保留，按明确根因改只读owned-PGID检查并保证finally reap。此为临时测试客户端问题，不是Sky鉴权结论。
- session01连接通过但本地请求文件因Write/chmod竞态被poll提前读为非0600而拒；实际toolcalls0、exit0、独立ps无进程。后续按先外部准备0600再原子mv投递，不重复无效步骤。
- session02真实js到达Calculator的get_app_state访问审批（low，仅指定app），未出现发送者拒绝；探针RPC30s等待在主控核请求时超时，manualreplies0，客户端TERM后reaped、进程组EMPTY、stderr0。两次投递/等待失败原件保留，重计划暂停pending期间RPC计时、手动审批120s并恢复后30s；不自动accept。
- session03按修正合同逐项审批8次，真实6按钮12×12，AX回读表达式及144，isError=false；正常stop exit0/reaped/groupEMPTY/stderr0，未伪造Codexmetadata。两个15s/751采样calculator前台均0、前台切换0、pointer0，起后/结果后单点也非前台；有限采样不扩大为持续保证。
- 正常CLI已注册用户MCP codex-cu，目标与官方command/args/env精确一致，其他配置等价；备份留privatejobtmp。当前会话未热加载新工具，实际操作由同一官方stdio运行时完成。
- godot01外部open-g-n后15s采样未见前台，但稍后单点frontmost=true；发送game工具前只TERM自有实例。下一次open-g-j-n隐藏launch也单点frontmost=true，立即停自有实例；game getApp实际返回完整主页AX/isError=false，随后stop正常exit0/groupEMPTY，未继续点击或绑定已退对象。此启动步骤两失败停止，重计划核Godot原生激活与正常窗口参数。
- 主控独立核官方4.7.2-stable源码：macOS show_window主窗口先activateApplication，其实现先无条件activateIgnoringOtherApps；no_focus仅影响后续窗口key选择。源码不证明本轮切前台的精确时刻，但外部background flag不足以挡引擎激活有依据。
- 下一正常路径仅复用原r1导出包到新私有副本，入口wrapper设置已存在测试根与原生参数，真实独立测试bundle ID；原引擎/PCK/runtime不变，由官方cua.getApp正常托管启动副本，而非外部open后绑定。未测试前不承诺Sky能抑制引擎自激活；不伪造身份、注入焦点或修改引擎。
- 后台键盘/截图补验的另一次Calculator首次绑定返回-10005 cgWindowNotFound，未发键盘动作；客户端exit0/组EMPTY，原计算器进程仍存活。保留不同于鉴权拒绝的窗口错误，未签键盘/截图通过。
- 私有原包副本的C入口、arm64构建、正常独立测试签名已验；引擎/PCK/allResources与原包字节一致。Sky正常getApp启动该新app，限定审批后first timeoutReached，但私有进程存在/非前台且已保存home viewport；一秒进程采样支持Godot主循环/Metal运行，未见启动死锁。
- 同已渲染实例second getApp返回cgWindowNotFound，未输入；两调用各客户端exit0/reaped/groupEMPTY/stderr0。停止blindread，定向NSRunningApplication/CG实际确认一个可见自有窗口、身份正确；按已核bundle ID再读仍cgWindowNotFound。第三客户端exit0/groupEMPTY，继续保负向。
- 自有PID单次AX探针windows status0/count1，但返回role=AXApplication且position/size unsupported；没确定Sky内部映射原因。五分钟15001样本private游戏前台0、前台切换0、pointer0，说明正常Sky托管后台运行成立，但不代签回读/输入。
- 旧privatewrapper实例只TERM自有PID后停止；正常直接入口对照新app CFBundleExecutable=Yudian、LSEnvironment五测试变量，原引擎去签后字节一致（仅新签名区变化），PCK/allResources未改。Sky首次getApp timeoutReached，已renderhome；后续单点前台true即只TERM自有PID。未证wrapper根因，不继续盲启动。
- 真实claude-p只加载codex-cu，MCPconnected，但首次Calculator应用访问因无Elicitation handler返回notapproved（不同于用户未授权/发送者拒绝）。主控独立核官方hook文档，临时--settings请求/主控回应hook第二次实际工作：请求限定Calculator emptyform，主控按nonce单次accept，后续getApp返回cgWindowNotFound。180s9001样本目标前台0/前台切换0/pointer0；未运行键盘/截图，无默认accept或改existing配置。

## 最终验收与续接

- 主控独立核原始Calculator144、真实宿主hook请求/回复/窗口错误、私有NS/CG/AX事实、长短monitor与清理。Sol只读规格/证据核验及最终增量均PASS，未宣称窗口根因已解或图形/输入已通过。
- 原生副本退出日志实际35条ERROR（33staticString+renderbuffer/reflectionatlas及PagedAllocatorexit各1），准确保留，不能签无ERROR通过。原工程两日志无匹配；直接入口日志未单独捕获，明确未核。
- 所有自有游戏、MCP子进程及Calculator已退出，四个私有槽无文件，用户codex-cu正常注册保留；原包/默认应用/正式槽/系统权限与Codex信任未改。临时hook不写existinguser/projectsettings。
- `python3 tools/verify_documents.py`主控实际PASS，格式/隐私残留检查干净，记录仅脱敏摘要；当前独立分支`feat/tutorial-r2-batch1-20261014`提交推送后核远端HEAD。
- 可复用入口：新Claude会话加载已注册codex-cu；涉及app访问须正常Elicitation处理，工具Connected不代许可成功。现成运行时接入可用，完整Godot窗口捕获/AX匹配与持续后台仍是未解任务；不再重复已失败启动，不改信任身份或让所有者停电脑。
