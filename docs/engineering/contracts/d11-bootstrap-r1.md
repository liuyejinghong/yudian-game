# D1.1 自举建设合同 r1

2026-10-07｜执行基线main718905d。Codex权威状态/交易/恢复，GLM限定纯函数和只读消费者。单活动建设或整平目标；保障可中断，载荷/已提交地形/已付材料保留。数值为可调安全自举探针，不是最终平衡。

## 导航子票（唯一写者GLM）

仅新增prototype/scripts/Navigation/BoundedRoute.cs与tools/navigation-tests/（纯.NET console自检，可链接TerrainData现有源，不依赖Godot运行）；不改Main/GroundPatrol/资源/合同。
namespace Yudian.Navigation；public readonly record struct NavPoint(double X,double Z)；public readonly record struct NavObstacle(NavPoint Center,double Radius)；public sealed record RouteResult(bool Found,string Reason,long WorldVersion,NavPoint[] Points,double LengthM)。public static class BoundedRoute，Find(TerrainSnapshot terrain,NavPoint start,NavPoint destination,double bodyRadius,NavObstacle[] obstacles,double maxSlopeDegrees=30) -> RouteResult。输入坐标/radius/obstacles有限、radius>0、坡角(0,35]；无效输入ArgumentException且无副作用。合法不可达返回Found=false/中文原因/空Points/LengthM=0。成功Points不含start，末端严格destination，按真实高度网格连线；WorldVersion=terrain.Version。

有限现有网格A*，最多rows*columns有效结点扩展；路径段必须全程场内，碰撞代理按bodyRadius扩障碍；连续高度用TerrainSnapshot现有a-d三角对角线。采样坡度与边界/圆障碍须复验每段（步长<=spacing/4），不可斜穿窄口/角落；start/destination不在网格也须校验连接段，不能仅检测最近格点。机器人障碍由调用者提供并排除自身。不要新导航框架/缓存/外部包，不改TerrainData。自检：平地、绕圆障碍、窄口拒绝、陡坡拒绝、边界/非有限输入、不同version结果，长度有限且段通过独立校验。提交只含独占文件，不push/merge。

## 自举与恢复边界（Codex）

正式新档只着陆器（位置7,-22）及有限物料，不设置免费电源/恢复。六材料iron_ore,copper_ore,iron,copper,parts,cable和有限kit；六设施lander/solar/processor/storage/charger/repair独立ID/type/位置/朝向，不靠数组取模识别动态设施。路线带地形/设施版本，GroundPatrol只原生移动，抵达贴地+容差+停驻。工位单预约表，冲突等待有界，运输取消保留原cargo并需真实退回或改派，不直接退款。

保存schema2新槽d11-player-v2.json包含本批新增事实，schema1槽保留；保存/读取必须整体验证引用/资源守恒/阶段/健康/设备占用，再复用地形物理屏障，恢复不重放已提交取卸货或维修扣料。旧D1.0自测保留具名预建测试基地，不冒称正式新档。加工设施建成能力在本批可登记，真实加工留D1.2。


## r1 实际消费冻结补充

配置prototype/config/bootstrap-v1.json包含五种设施成本/时间/半径、六材料+kit、配方定义（本批不启用生产）、运动/有效工段电耗磨损、有限初始库存100容量电池/耐久、驮运8单位载荷、600秒日长/400秒日照、阵列4能量每秒。直连≤12m，线缆每段4单位，另走真实驮运+筑垒3秒工段；无全图免费连接。配置.Validate核首套太阳能+充电+维修+加工成本及三段线缆/两次维修耗材；真实路径和耗能另由native安全样本核验。

维修站 construction cost 中另运输两次维修的4结构件储备，落成只消耗建筑本体成本，储备留在同一现场容器；维修再从现场扣2结构件、有电累计8秒才恢复耐久，不恢复电量。充电有实际分配最多3能量每秒；无日照保留工位与路线等待，不产生能量。机器人健康为不同事实；停机阻止自身移动/工段，正常保障保留工作进度和在途货物。新服务请求先核真实返程路径预算，基本10%门槛可提前。救援仍是D2后续，不从低电/低耐久样本删去故障规则。

建设与整平同时间只有一项当前工程；切换时退役旧终态job，已提交地形/设施/物料仍保留。单份cargo始终是物理容器，不另加在途库存。取消不撤销独立服务的工位/路线，只清原工程恢复目的；运输重试退货也须实际返回，新取货尝试使用新operation_id。保存schema2校验配置hash、固定/现场/cargo容器、建成与精确消耗记录、spent对账、工程阶段/地形、交接序号、服务/站位/目的地引用，再重建原生场景与地形屏障；上一有效档.bak保留。路径重算不重放交易。

公开只读消费者：ReadBootstrap()、PreviewBuild(type,center,yaw)、QueueBuild(type,center,yaw,observedVersion)，QueuePlayerAction("connect"/"retry")；PlayerSitePreview.Radius可选默认2。GLM界面只发命令/展示事实，不计算成本或改变库存/健康。

移动成本按实际位移结算，在玩家暂停/保存/加载命令之前结算上一物理帧，Capture复用同一幂等结算。健康与运动预算在父帧最后采纳，零电/零耐久不得多走一帧。有效工段最多扣剩余工时和剩余健康预算，Levelling档内进度上限3秒。保障Returning至真实返抵原站才恢复工作，并保存其返程路线；途中取消停该回程，赴服务/服务中的取消保独立保障且只清原工作目的。

原生回归入口：tools/bootstrap-world-tests/run.py（要求先成功构建；支持源project或导出engine），九独立进程覆盖全流、在途取消、活动运输、已扣维修材料和充电中恢复。所有日志保留加载MVID/hash；未运行不得据入口宣称通过。

保障路线临时受阻以.6秒间隔重寻路；120秒未到站进入显式阻塞、释放工位，保留服务进度/原任务/扣料事实。既有重试按钮重新核电量、实际路线和可用工位后重派，阻塞状态保存/加载不自动抢站或重放维修。
