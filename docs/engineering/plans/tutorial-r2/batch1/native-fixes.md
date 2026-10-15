# 批1 · 原生验收缺陷修复合同

2026-10-15。基线 `bc00e1d78627fdf84e35f5e67f3b143a77fe3005`，修复分支 `fix/tutorial-r2-batch1-native-20261015`。所有者已授权本轮修复与原生复验，先修批1。原生 run01/run02 的原件保留，不覆盖历史结果。

原生已通过的范围继续保留：首套保障、真实缺料采矿/加工/搬运/施工、新仓储供料、另址太阳能/充电桩及付费接线、教学8/8、同世界经营与保存冷载。该路线包含取消换址，不能签所有已受理目标都能完成。

## 独立分诊

| 编号 | 分诊 | 已证事实与修复边界 |
|---|---|---|
| N003 | 建议P1，独立于旧正式台账 | 合法受理太阳能 goal-9/build-9，真实取货后没有卸货工位，Waiting180/Blocked，4铁4结构保留载荷；另址成功不能代原目标恢复。先取得具体候选与路线失败证据，再修根因。 |
| N001 | 建议P2 | 1280×800/UI150%，活动接线摘要和拒因增高，确认/关闭按钮落在外层滚动遮罩外。固定操作区，候选列表用剩余预算内部滚动。完整成本、选中target/source和拒因均保留。 |
| N002 | 建议P2 | HUD重复加入相同preview/plan拒因。按精确文本保序去重，不丢不同真实阻塞原因。 |
| N004 | 建议P2 | 关联详情显示启用取消按钮，但入口因关联ID非空直接返回。关联当前工程可以打开已有后果确认，历史关联不得取消别的当前目标；Esc逐层返回与确认重验保持。不是底层取消队列失败。 |
| B002 | 既有技术候选 | 旧已受理太阳能正常加载后仍Blocked180，原目标恢复FAIL。取消后的货物再利用另项PASS；不得互相替代。 |
| reuse-save | 建议P1，run-02新增、独立分诊 | 再利用链至新storage Completed成立，但完成后normal save失败（E034）；失败原因实有记录：E035第39行 `PLAYER_COMMAND_REJECTED`“采矿任务阶段或工位无效”（此前搜索漏关键字，曾误记“无异常日志”）。不保存退出E035 ERROR0，先前valid档字节未变。根因主控已核定：Main.DevelopmentSave.cs:123历史Completed mine AtDock用当前terrain高度，新仓储整平terrain v13→14，历史站高-0.21634348对当前地面0差0.216343477m，throw早于写文件；非磁盘/权限。不改正式P0=0/P1=1/P2=0账；修法已冻结派Sol实施。 |

旧正式台账仍为P0=0/P1=1/P2=0。D001为确认P1，须无提示真人G001关闭，AI教学8/8不能关闭。

## 文件所有权与证明

- 物流只读调查：Sol读取Main/导航/工位与原字节快照，报告确定事实与仍待运行核对的假设；诊断不改世界。根因与方案由主控冻结后再委托唯一writer实施。
- HUD实施：单一worker独写PlayerController.Hud.cs、必要PlayerController.cs、PlayerUiSelfTest.cs及测试说明；不修改Main/DTO/物料数值。
- 集成：一次独立Sol审关键取消/货物/保存恢复边界；主控独立编译、必要合同与新包原生复验。实施自测与主控验收分列。
- 原生：原现场仅原字节复制进新私有根，通过正常UI加载。原目标恢复、取消保货/地形、货物再利用分别记录；不编辑JSON、健康、库存、地形或时间，不放宽路径约束制造通过。
- 新包：固定commit、manifest、PCK、runtime和私有入口；旧候选、默认应用、正式槽与旧证据不动。A/B各独立新私有根；精确1920×1080/1920×1200若被屏幕钳制，记录实际尺寸和BLOCKED，不冒签。

## 当前证据状态

- 当前HEAD与已有改动已核：起点为bc00e1d，干净；修复分支已建立。
- N003原生基线：新私有槽正常菜单加载E248原字节副本，当前工程/阶段/执行者与Blocked反馈一致，现场截图已保存。
- 工具阻断：后续CUA读状态被应用访问确认打断，用户要求调查默认批准机制。该次被拒操作未重试；系统AX/截图权限与MCP应用确认分开处理。代码修复与只读调查继续。
- 本页是执行合同，尚不代表修复或原生复验通过；后续实际结果另记。

## 2026-10-15 · 已取得的原生根因证据与局部方案

只读诊断提交 `221f705` 的新私有诊断包已真实导出（诊断时包含未提交HUD差异，完整源指纹与构建逐项一致；不是最终干净交付包）。主控分别在独立私有根正常UI载入N003/E248与B002/E140原字节副本，并点击原目标重试；正常保存/退出后读取完整日志，两实例均已退出、ERROR=0。

- N003：原四轴卸货候选均 `Found=false`，理由为终点无法接入通行网格。+X的九个接网段首先命中筑垒2实体障碍，另外三个方向命中严格地形扫掠足迹。原目标/中心/朝向与载荷保留。原生结论限失败来源，尚非修复后恢复通过。
- B002：同样四轴失败；+X首先为筑垒2障碍，−X/−Z为扫掠地形三角，+Z为设施障碍或中心线坡度。不得将其概括成朝向错误或仅筑垒挡住。
- 实际C#原路线求解器反例：N003原5.5m环32候选有9个严格可达；B002原4.5–5.5m环160个离散采样无可达，移除筑垒的只读反事实亦无可达，因此忽略筑垒不是修法。B002在现有施工安全外缘6.175m环上有6/32个可达。探针保持原坡角/体半径/.25m余量及全部生产障碍，只有诊断反事实单列移除筑垒且绝不用于执行。离散采样不证明整个连续区域有解或无解。

主控冻结：仅未建设工地的实际卸货，在原四轴顺序失败后补原距离有限斜向候选，再检查不超过现有施工安全外缘的有限候选；确定顺序、严格寻路、真实行走到站、工位预约与实际交接均保留。已建设施的取货/服务/接线不全局改变；不改变整平patch、坡角、碰撞、健康/库存/时间，不瞬移或跨距离直接移交。未找到仍如实等待。新规则的真实原目标恢复、取消保货/地形和再利用各自待新包独立复验。

应用访问确认已按用户随后明确选择处理：官方Elicitation hook限定本项目、codex-cu、精确“余电 验收”空表单请求自动接受，其他请求仍询问；真实窗口读取已无弹窗且审计新增匹配。配置仅本机，不入公开仓库，不改变系统权限或工具信任。

## 独立审查后的返工边界

- HUD：三修后的主控独立headless1122/0/0、ERROR0成立；N001原生诊断包活动接线画面已见按钮常驻。但Sol指出候选列表96px保底可能超出实际剩余空间，补修列表真实预算/末项可滚达；另关联关闭重开测试此前先Esc清详情，须补“详情仍开直接关闭”的独立路径。N002与N004取消安全静态符合，最终原生仍另签。
- 物流：新候选纯合同与主工程编译通过，原严格路由/实际到站链保持。但Sol确认旧保存校验只接受四轴，新增HaulStation会被拒绝；必须先让非接线建设工程卸货站校验复用同一有限集合，涵盖活动/取消/完成阶段、保旧四轴与高度/目的地检查。该问题在导出最终包前返工，未将未验证增量作为交付。

已落地整改：物流与保存兼容提交 `41e6037`，HUD提交 `07b7aed`。新增工位保存增量经Sol复核原P1已修妥；实施者隔离工程合同84个校验反例/6次公共保存恢复有真实输出，主控亲读关键日志。主控编译build03为0警告0错误，导航100项PASS，通用R2权威合同exit0/ERROR0（不代新增工位专用验收）。HUD列表预算去掉96保底，新增真实末项可见/点击与关联详情仍开直接关闭回归；主控独立终轮1130/0/0、exit0、ERROR0，Sol增量复核两处原发现已修妥。旧红例实际1119/20/0含可解释的测试级联，原件保留，不改写为终版18失败。

## 固定新候选与未完成的原生范围

新候选真实导出成功，源commit `4f311e6235bf14ee4aaeeb7a316c12cb0b0f3c9e`、导出时工作树干净，304个raw文件当前逐项一致；arm64与严格签名有效，内嵌CLR8.0.31。PCK SHA256 `f28a71696ad982160b95071b48983c58b18afab8e68673d6160e29007777c267`。这只是固定候选，不是原生验收通过。

最终候选的首个N003私有入口启动到主页，但两次观察分别timeoutReached/cgWindowNotFound，未执行读档；停止该观察路径后仅终止自有PID，种子原字节未变。退出日志出现34条Godot静态字符串/渲染清理ERROR，保留原件，不签该run日志干净或原目标恢复。此前诊断包两原目标正常重试日志均ERROR0；不能替代最终修复包验收。

新包原目标恢复与正常保存冷载已由主控run-02亲自原生PASS（N003、B002）；N004关联取消与取消保货/保地形/预约释放亦已run-02原生PASS，较早生产起点自然生产→运输→施工完成/保存已PASS（见下节）。N001接线原生本轮FAIL（普通CoreAudio启动另记BLOCKED，不判游戏逻辑；静音实例复现真实布局缺陷），writer修法已commit `262033d`：主控专用合同39/0/0、ERROR0（8pairs、longList98px、last59px、scrollMax500、inListTrue、短/长list98/98）与full终轮1130/0/0、ERROR0均过，Sol静态复审无可证新增阻断、首帧/多pair三倍率/长reason内滚待新包原生100/125/150补证，原生复验未过前仍记FAIL；货物再利用链至新storage Completed成立但完成后保存失败（reuse-save建议P1；根因已核定：历史Completed mine保存校验用当前地表高度、整平后差0.216343477m，throw早于写文件、非磁盘/权限；最小修复已commit `e080363`并过主控run-03-controller复验，28cases全PASS），整链不签PASS；N002待最终图形复验；精确1920×1080/1920×1200实测被屏幕钳制到1860×889记BLOCKED，A/B两独立根（原seed hash同cb347b8a）两流程PASS：A组直接建设（E044/E045）、B组先整平再solar（E046–E048），不作严格同点同相效率比较。新候选待导出（HUD `262033d` 与保存 `e080363` 两commit均尚未push；相较4f311e6只三个raw源码改变，其余301个raw含全部配置/美术/导航相同），N001/reuse-save原生仍FAIL待修后新包复验。主控另在隔离Main工程驱动实际复验N003/B002同目标Completed与各两次公共保存恢复、取消后公共保存恢复，三组ERROR0；只读结果核原ID/位置/朝向未换，取消物料/交易/地形不变而cable2/kit1预约释放。此为工程证据，其中原目标恢复与取消事实现另有run-02原生签认，货物再利用仍不代原生。A/B两独立根已备，B根未跑仍不签，不以实际小窗代签。代码和工程修复已完成，批1原生验收总项仍进行（已结清N003/B002/N004/取消保货保地形/较早生产起点；N001与reuse-save待新候选原生复验），批2/批3未开。

首次修复分支推送与API核对遇GitHub TLS连接失败，原失败保留；网络恢复后的正常重试已推送至 `52f4750`，draft PR为 [#63](https://github.com/liuyejinghong/yudian-game/pull/63)，基于原批1分支、不合并。候选游戏源仍固定4f311e6，后续仅文档更新。结果与可继续执行的本机验收prompt保留于新的私有run目录。

## 2026-10-15 · run-02 主控亲自原生复验（N003/B002/N004与取消保货保地形PASS）

所有者新授权：run-02由主控亲自操作、核验与记录，独立于旧run-01；run-01工具阻断与退出34条ERROR历史原件保留，不覆盖、不改判。固定源 `4f311e6` 候选（PCK同 `f28a716…`），正常CUA输入、GUI_TEST与独立私有根，无自动driver/资源注入/时钟修改。原目标恢复轨（N003/B002）原现场仅原字节复制进新私有根，不换址、不取消、不编辑存档；取消轨（N004、cancel-materials）按各自步骤经确认真实取消；N001静音clone仅添加官方 `--audio-driver Dummy`（Metal/正常CUA/同源不变，audio不在PASS）。

- N003 原目标恢复与正常保存冷载：PASS。E248原字节副本正常UI载入Blocked现场，原步骤诊断→重试工程/保障，保持 goal-9/build-9、solar、中心(17.129562,0,-10.15947)、yaw0，真实进入Delivering。运输中正常菜单保存E001（Time 7075.733333314515，同半径22.5°斜向HaulStation(22.2109,0.09208831,-8.054711)），E002退出ERROR0。同app/根冷启动→继续，运输档读取并实际续交货/取下一批；原目标落成，教学5/8→6/8，E003（Time 7175.133333313069）goal/build均Completed、build-9设施Built=true、ID/中心/朝向不变，E004退出ERROR0。再冷载继续显示已落成、教学6/8、无进行中工程，E005（Time 7217.333333312455）事实保持、Terrain与E003完全相同，E006退出ERROR0、自有进程退出。运输冷载与完成成果冷载分别有原字节保存与退出日志，不宣称全世界字节不变；正常原生画面为逻辑1280×800，不代其他尺寸。
- B002 原目标恢复与正常保存冷载：PASS。E140原字节副本正常UI载入，保持 goal-9/build-8、中心(12.406353,0,-11.329565)、yaw0。运输中正常保存E007（Time 2430.7833333366734，HaulStation(6.701397,0.5676698,-13.692635)）；E008退出日志ERROR0，DELIVERY_STATION_SET 四轴先行、按序评估前51候选即于SelectedIndex50命中施工安全外缘6.175m、202.5°斜向候选Found=true，站址与HaulStation一致（同半径202.5°候选仍Found=false；总64候选未全评估）。正常运输存档冷载→继续→原目标Completed：E009（Time 2478.2166666706107）goal/build均Completed、build-8设施Built=true、ID/中心/朝向不变，E010退出ERROR0。完成后冷载E011（Time 2486.833333337387）原目标/中心/朝向保持、Terrain与E009完全相同，E012退出ERROR0、进程退出。
- N004 关联取消与普通工程取消：PASS。原E350现场正常UI载入，真实建设goal-1/build-1至运输阶段；经物资预约查看关联详情，取消打开点名确认，Esc后画面仅退确认层；关联详情仍开时直接Close再重开无旧详情残留（截图留证）。确认后真实取消：E013（Time 371.4333333333242，build-1 Delivering）→E014（同Time，goal-1/build-1均Cancelled），Terrain与Ledger的Containers/Operations/Mining/Recipes四字段全等；E015退出ERROR0。
- 取消保货/保地形/预约释放（cancel-materials）：PASS。原E248独立根载入，初Blocked时预约已释放、不是有预约样本；正常手动暂停→重试原goal-9/build-9进入Fetching，真实预约cable2/kit1（build-8）落账后保存E017（Time 7053.566666648171，Paused）；经物资预约相关详情点取消→确认真实取消→E018（同Time，Cancelled）。E019判据：time/terrain/Containers/Operations/Mining/Recipes全等，预约两行→[]，驮运1货舱铁4/结构4及其余原值保持；释放的是线缆2/套件1预约，未移动库存；E020退出ERROR0。
- N001 活动接线原生：本轮FAIL，返修中。普通CoreAudio两次启动（PID 26421/35903）主线程样本均等待 AudioComponentInstanceNew/AudioDeviceGetProperty/HALC_ProxyObject 同步IPC，未进场景、原生日志0字节、captures空，两sample保留：普通启动BLOCKED，不判游戏逻辑、不签普通启动。仅新私有clone添加官方 `--audio-driver Dummy`（Metal/正常CUA/同源4f311e6与PCK `f28a716…`不变，audio不在PASS），静音实例正常UI设置150%→载入原档继续→暂停→接线：确认/关闭按钮可见（E027）；真实滚动到末端，最后多行pair被裁只余尾行（E028）；点击末pair拒因变更为“迁移目标须明确接入新增太阳能”，更长文本致候选列表高度0（E029）。E030活动接线态正常保存（connection Delivering，Paused）、E031正常退出ERROR0；1280×800 telemetry实证。此前headless1130 PASS与AX点击可达不代完整可读，N001不得据此签过。
- 货物再利用与保存失败（reuse-materials）：整链不签PASS。同静音clone正常载入原E248→暂停取消goal-9/build-9→正常保存E032（Cancelled、预约空、驮运1铁4/结构4在舱）；正常UI新storage预览约(-5.3,10.8) yaw0合法→提交unpause，旧货真实退仓、仓储铁4/结构4出现，新build-10 storage Completed（E033截图）。但完成后normal save失败（E034）；失败原因实有记录：E035第39行 `PLAYER_COMMAND_REJECTED`“采矿任务阶段或工位无效”（此前搜索漏关键字）。不保存正常退出E035 ERROR0，先前valid档槽与E032字节完全相同。再利用事实与冷载无持久化对比不签PASS；reuse-save单独记建议P1，不改已过的物流/取消结论。
- reuse-save根因主控已实际核定：独立Sol私有scratch以正常公开命令在精确center(-5.286148,0,10.807249)复现取throw；Main.DevelopmentSave.cs:123对历史Completed mine的AtDock校验使用当前terrain高度，新仓储整平使terrain v13→14，历史站高-0.21634348对当前地面0差0.216343477m，真实throw早于写文件（旧档因此未损）；并非磁盘/权限。修法已冻结并派Sol实施：仅Completed mine免除当前地表高度相等校验，finite/轴向半径/场界/robot/id/work/paid/receipt/patch检查全保；live/Blocked/Cancelled仍严格，schema/旧档/模拟不改。
- 返修收尾：reuse-save最小修复commit `e080363`（Main.DevelopmentSave唯一7+3 diff，带Constraint/Rejected/Directive trailers），主控独立复跑 `run-03-controller` 28cases全PASS（公共保存/载入/保存、全量snapshot与slot3=.bak字节、ERROR0/command_rejected0）。N001接线修法commit `262033d`（Hud.cs+SelfTest）：主控独立编译0警告0错误；专用合同conn log 39/0/0、ERROR0（8pairs、longList98px、last59px、scrollMax500、inListTrue、短/长list98/98）；首次主控测试传归档路径被既有私有root门拒（该ERROR在conn日志保留原件），已停止该进程，原字节复制进允许根后完成；full终轮主控已亲读1130/0/0、ERROR0，后台exit0。独立Sol静态复审无可证新增阻断，首帧/多pair三倍率/长reason内滚未直接合同覆盖，主控新包原生优先补100/125/150。两commit合计只三个raw源码（Main.DevelopmentSave/PlayerController.Hud/PlayerUiSelfTest）相较4f311e6改变，其余301个raw含全部配置/美术/导航相同；两commit均尚未push。新候选待导出；N001与reuse-save原生记录仍为FAIL，待修后新包原生复验方可翻绿。所有红例原件保留（含主控白名单/私有root门调用失败、writer layout旧红例与E030原备份同样486/1），不改写为绿。b002-active原生正常载入后paused CargoWaiting→正常unpause（无需retry）→Completed，正常保存E036、E037退出ERROR0（Metal+Dummy静音clone，非普通音频）；冷载证据未取，本轮不另签。
- reuse-save冻结修法已过主控独立工程验收：`run_fixed.py --run-name run-03-controller` exit0，主控亲读stdout 39–83——28 cases、公共保存/载入/保存PASS、snapshot全等、slot3与.bak字节相等、ERROR0/command_rejected0。仅工程证据不代原生；新候选待N001 HUD完成后一并导出复验。
- 较早生产起点（n003-production）：PASS（主控签认）。自然从夜加工等待推进到采集/运输/施工：E039 goal-9/build-9 Completed、原中心(17.129562,0,-10.15947)/yaw0、build-9设施Built=true、Time 6910.266666650256（自然推进约6558.43→6910.27），较起点新增mining1/recipes4（主控记录），configHash与基线同（A43C8E6E…）；正常保存成功、E040退出ERROR0。无单独冷载轨，同目标冷载证据已在N003恢复轨另列，不并入本签认；音频Dummy。
- 精确大尺寸：BLOCKED（exact-dimensions-status.json）。E041/E042请求1920×1080/1920×1200均被屏幕钳制到逻辑1860×889（像素3720×1778、scale2、dpi163）；主页与设置三倍率实操真实执行，钳制后游戏布局未测不签，系统display设置未改。
- A/B独立根：两流程PASS（音频Dummy；两根原seed hash同cb347b8a）。A组直接建设：E044 goal-9/build-8 solar Completed、center(12.505365,0,-11.181097)/yaw0、Time 2045.9166666651347、正常保存成功，E045退出ERROR0。B组先手动整平（E046 Completed、center(12.52281,0,-11.197887)、terrain v5、Message“整平已提交，物理验证完成”、已保存）再solar：E047 goal-9/build-8 Completed、设施Built=true、center(12.43391,0,-11.311815)/yaw0、Time 2210.4500000005346、正常保存成功，E048退出ERROR0。不作严格同点同相效率比较。
- E001–E046 checkpoint、退出日志、截图与启动样本留存于私有验收目录，档身份SHA在私有记录；公开仓库只记E编号与本节事实，不收私人路径或存档数据。N002待最终图形复验；N001与reuse-save原生仍FAIL待新候选复验；均不代签。
