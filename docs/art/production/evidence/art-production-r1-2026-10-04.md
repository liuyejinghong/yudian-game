# 首批美术生产记录 · 2026-10-04

授权：所有者在需求交付后明确“好，那么开始制作吧”。实际范围M01→PREVIEW-A/B→U01-GEO/STATE→F01/F02各自GEO/STATE；正式地形、P2、游戏规则接入不在本批。需求输入b5190f3，主线输入0a1e54e，生产合流ae72bc6，计划输入b5eadfd；独立分支art/production-r1-20261004。

M01已实际派出：ZCode原生GLM-5.3-Flash，会话sess_4074d2d8a1；原生/model切换task_e397be7a71返回成功（observed_model字段null，不当作额外运行观测）。独立分支worker/art-m01-20261004，base b5eadfd，只能写prototype/assets/materials/art-r1/。M01交付1b91467已独立读取，集成6b6198b。PREVIEW-A已解除材质前置；其他首样仍BLOCKED。实际任务ID与回执在本线receipts追加；主控独立读取交付和加载后才更新。

技术可用、主控可读性与所有者最终效果分别记录；当前六材质资源技术已通过；图形/所有者看样尚未通过。真实Bridge用量记录不折算账单成本，未知写未知。

## ART-M01实际交付与独立接受

ZCode task_8dd9fbb3e5，原生模型确认与实际任务同会话；Bridge实际elapsed=533秒（工人估计213秒不采用），usage.used=61805/size1000000是会话上下文读数，非本票计费token；可见cache-read提示60.7k，账单成本未知。已读取完整结果并关闭session。仅7文件材质目录，无越界；六SHA256与README逐一匹配；主控修正文档相对链接。

独立Godot4.7.2真实加载六StandardMaterial3D，Color字节/255容差1e-6、roughness/metallic、alpha1、透明关闭/无发光/对象类型纹理属性为null均通过。缺路径返回null并有预期原生ERROR，无SCRIPT ERROR；exit0与自身M01_INDEPENDENT_OK同时成立。检查器初次布尔误报、第二次替换未命中已保留；停止后缩检修正，不把失败记通过。日志/脚本在[独立记录](production-r1-receipts/m01-independent/object-resource-check.log)，Bridge完整回执在[结果](production-r1-receipts/ART-M01-result.json)。临时空工程只有六资源，无共享project修改。

接受范围：资源可用和参数一致。NOT_RUN：M01真实试板、模型逐面接线、所有者最终配色、性能。PREVIEW-A可以使用这六资源；不把候选色值升为最终规范。

PREVIEW-A已实际派单，原生模型已确认；base2401b6b，worker/art-preview-20261004，只写场景/专属工具两目录；实际task/session/request见[派单回执](production-r1-receipts/ART-PREVIEW-A-dispatch.json)。B、U01/F01/F02尚未启动。

技术线可取M01限定资产提交1b9146720ba1ac183ec0fee0ea93b785f8af9004（7文件，无docs改动）；生产集成6b6198b，README链接修正2401b6b。六hash以README为准，主控已核。soil_mars为StandardMaterial3D，固定逐surface set_surface_override_material(0, resource)，不能整件material_override；缺失/错类型明确拒绝，无替代。仅资源技术接受，地形/白昼试板与所有者视觉NOT_RUN。技术线991b78e的消息为协作背景，未自行修改其Main/工程TODO/材质目录。

U01生产前只读reviewer核货箱与轮组尺寸无必然冲突；明确静态车体包络与载荷/开盖/前杆活动包络分开（前杆可达Z=-1.15）。未制造模型、铰链实际穿插/正常镜头仍NOT_RUN；没有把理论判断代签图形。

## M01主控独立白昼实机试板

使用已接受六资源在工作树外临时GDScript空工程拍摄，未争写GLM的预览目录。真实macOS窗口，Godot4.7.2/Metal4.0/Forward+/Apple M3 Pro，1920×1200 SubViewport、MSAA4、比例1、FXAA/TAA关闭；两次frame_post_draw后取原始Viewport PNG。固定方向光/环境/相机按合同；球/方块沿Z偏移±.35避免邻组穿插（补清制作布局，组中心/尺寸不变）。实际exit0、自身M01_BOARD_CAPTURE_OK，无SCRIPT ERROR。脚本/空工程/日志/JSON/PNG/六资源与文件hash在[试板记录](production-r1-receipts/m01-board/hashes.json)，[原图](production-r1-receipts/m01-board/board.png)。

主控2026-10-04看原始PNG：浅壳/深框可分，橡胶更暗，土色衬出浅壳，solar_face未大范围镜面白化；本轮暂保留候选值。这是Godot独立预览实机画面，非概念图/离线渲染/游戏接入。NOT_RUN：预览正式工具B的GUI/采集、三个模型、所有者最终配色、性能。独立试板不冒充PREVIEW-A/B正式工具接受。

## PREVIEW-A首次实际交付审查

GLM task_29574b89b9交3970f2f，仅9个成果文件在两个独占目录；Bridge列22个触碰文件含引擎自动缓存，并不等于commit越界，主控查git实际提交与clean。Bridge elapsed1562秒（工人35分钟估计不采用）；usage.used104218是上下文读数，非计费，账单未知。主控在临时独立工程重跑自测PASS122/122/exit0，无SCRIPT ERROR（坏JSON原生ERROR为预期）；没有因此接受工具。

独立reviewer指出5类接缝问题，主控实际另测8项失败并出现SCRIPT ERROR：资源先赋PackedScene/坏preset类型、额外wrapper方法、static阶段残留、整件材质覆盖、Model与声明GLB不一致、static包络虚报、SampleRoot缩放。另只读glTF面检证实24三角面顺序与外向法线相反，依据[Khronos glTF正det winding规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#instantiation)，要求索引反向而非改材质双面。试板布局已补清Z±.35。实际日志/复现脚本留[独立失败记录](production-r1-receipts/preview-a-independent/contract-audit.log)，一次局部返修派单见[回执](production-r1-receipts/ART-PREVIEW-A-rework-dispatch.json)；B与U01/F01/F02仍BLOCKED，未将probe算正式资产。

M01 README默认枚举笔误由主控实际加载核清：六角色shading_mode=1（逐像素）、cull_mode=0（背面剔除）、transparency=0；只修README，六.tres与hash不变。实际日志见m01-independent/default-shading-check.log。仓内PREVIEW-A诊断文本仅去行尾空白，原始与发布hash见preview-a-independent/log-source-hashes.json，原日志仍留原临时目录；不修改诊断结论。

## PREVIEW-A技术接受

GLM返修5b02e4a，Bridge task_4a8a807247实际elapsed484秒（工人30分钟不采用），上下文used104726，非计费用量；完整结果/14个触碰文件含缓存已读，仅7成果修改文件在允许目录。已关闭原session。主控独立重跑145/145与原8失败全清零、无SCRIPT ERROR；glTF24tri全部CCW与外向法线一致。

完整672确定姿态进一步发现probe清单active少记工作/packed下沉及towed倾斜（354越界）；停止整片再派，缩为主控修实测min=(-.216670096,-.05,-.2)，max=(1.2,.57,.2)，新增4回归，简化共用String返回检查。GLB/源hash不变。最后实际149/149、原8独立项全通过、672/672包络与根固定通过；源码/日志/实测在[接受记录](production-r1-receipts/preview-a-accepted/matrix-master.json)。不以局部AABB保守角点伪造轮姿包络。

A已技术接受，B READY；M01已独立白昼比较，正式预览GUI/捕获和U01/F01/F02/所有者视觉仍NOT_RUN。source返回apply_preview是唯一必需方法；mode/reason由manifest读取。源/manifest/GLB身份、每surface含hidden共享绑定、实际可见顶点bounds、根不动/非法请求不改姿态均已有实际证据。

生产分支已继承技术线main93cdd4f（包含PR27/43daffe碰撞与M01限定接入），不争写根PLAN/TODO/Main/工程合同。唯一README合流冲突仅默认shading枚举，保留实际1；六资源与hash完全不变。技术线材料联调不代替所有者最终视觉。

## PREVIEW-B实际启动

独立worker/art-preview-20261004快进至b3cd934；新session sess_481d1bda76先执行原生/model，task_dc63584393完整返回“model = GLM-5.3-Flash”，再在同会话实际派task_ca0eb86c4b，request c41bbfa6-a801-42ac-b1e6-2d4d9e08c9dc。只写原预览/工具两目录，证据另存工作树外。B IN_PROGRESS；主控尚未接受GUI/图形，U01/F01/F02依赖保持BLOCKED。真实派单回执随本线保存；账单成本未知。

U01-GEO开工依赖局部修正：经architect只读复核与主控查票，GEO明确不交wrapper/最终manifest且图形留STATE，原完整B前置过重。M01/A+现有独立导入检查足够；GEO READY，STATE仍等GEO/B，设施仍等最终U01比例。检查器控制探针真实导入2mesh/24tri/1socket通过（临时路径/private/tmp/yudian-art-import-independent-20261004/probe-audit.json），不冒充正式模型检查。

## U01-GEO实际启动

原生模型确认task_6c9828f3a4返回GLM-5.3-Flash；同session sess_b50ad496b6实际派task_6c5c6c9e68，request6716be8f-8e14-4e38-8d2e-bf271f820ebc，base7fedebd77f010eacd93195c928f3c0a775fb0334，worker/art-u01-20261004。只写tuoyun-r1源/资产/私有manifest，不写预览；GEO与B真正并行。源与GLB尚待提交/独立导入，正常镜头/七态/所有者视觉仍NOT_RUN，STATE未派。

## PREVIEW-B缩票修复

B首次实现未提交/未接受：两次实际日志同一956动态返回类型推断错误，Godot脚本未启动；主控check-only独立重现（引擎仍exit0但SCRIPT ERROR，不能签通过）。按两次同错停止约定通过Bridge取消task_ca0eb86c4b，elapsed2710秒，完整结果已读，usage空/成本未知；取消回执files_changed为空不代表没写代码，主控git实际查两tracked修改及新局部helper。源码与草稿快照保留。

只读reviewer找到5项真实草稿问题，architect核缩票；主控查源并用本机ClassDB确认LineEdit/HSlider.editable、WorldEnvironment.camera_attributes、RenderingServer实际method/driver接口，DirectionalLight3D.shadow_filter与Environment.camera_attributes不存在。原日志/独立复现/API探针与raw/published hash在preview-b-before-fix。B-FIX已实际派task_5d2aca8ff4同session，request78fa4253-1779-44fb-91b3-bfbbd1b89d8a，仅修类型、初始化/回填、属性、实际hash/后端与箭头坐标；先有界check-only再自测/GPU。明确禁止工人曾使用的全局pkill，仅本票已知PID/TaskStop；未因此宣称其他会话数据丢失。U01-GEO继续独立运行，B仍未接受。

## U01首轮结束但无资产交付

Bridge task_6c5c6c9e68以completed/end_turn结束（elapsed1448秒，context used86987，非计费/成本未知）；主控读取完整465字符结果，只有确定几何方案与准备写生成器，没有文件或commit，git clean仍7fedebd。所以GEO未SUBMITTED/未接受，模型/导入NOT_RUN。主控曾在中间消息把任务结束误称“已提交”，已立即核仓并纠正。实际继续执行派task_a7f624880e，同session/requestf3f0fa6b-7889-409e-8367-ffeb57e95f21，明确现有授权yes、沿已确定方案直接Write而非再规划；不增加设计范围。


## PREVIEW-B独立接受（工具）

GLM局部修复task_5d2aca8ff4提交ed4f856，已完整读结果，session已关闭。原五项草稿问题修复；独立重跑221/221、原8合同检查与672/672姿态通过。工人22项真实窗口程序化GUI、16份Metal采集与10异常命令日志已读，未当作原生输入验收。

独立reviewer另发现source.files损坏元素。主控真实Metal复现：[原日志](production-r1-receipts/preview-b-accepted/bad-source-files.log)出现SCRIPT ERROR但仍CAPTURE_OK/exit0/输出PNG，这次候选未接受。缩为主控可选来源字段类型/哈希格式校验，增加6负例及省略字段正常例；实际[228/228](production-r1-receipts/preview-b-accepted/fixed-selftest.log)，损坏清单exit1、无SCRIPT ERROR/无输出目录/无PASS。省略files仍合法。第一次集成工作树自测缺GLB导入缓存，先真实--import后通过，未篡改断言。

主控真实Metal重新采集blind normal、evidence close loaded/work、return-charge箭头：均CAPTURE_OK/exit0、1920×1200、实际相机/灯光/环境回读符合合同，GLB/来源文件hashmatch，根恒等。已看[六材质试板](production-r1-receipts/preview-b-accepted/board.png)与上述原图；盲图无注记/辅助，标注图有信息/连接点。默认阴影filter记录本机项目默认2；atlas仅requested4096无getter，未伪造回读。

[主控原生窗口输入检查](production-r1-receipts/preview-b-accepted/native-gui-review.md)完成空输入/重复切换/近景+work+.5后Reset/关闭重开，实际观察通过。正式PREVIEW-A/B工具技术/图形接受，解除STATE的工具前置。探针仍test-only，不算正式资产；所有者视觉、资产单件可读性、游戏/FPS仍NOT_RUN。README已纠正“149自测包括完整672”误述，完整672是外部独立扫描。

## U01 GEO真实提交与局部返修

续做task_a7f624880e实际交e29c945七文件（1245秒、上下文used131080非计费）；这次git/source/两份GLB确实存在。主控独立生成两次字节级一致、1512tri CCW法线检查通过。只读explorer与主控算出腔体内壁.36/.375不达冻结.38，报告“.375≥.38”错误；task_b4f368612b局部返修166秒交da82a6f，墙/衬移到真实内壁.38，其它结构保留。

主控实际导入又发现GLB仍引用外部tuoyun-r1.bin，工人源目录有bin掩盖资产目录单GLB失败；已停止两次独立导入并保留诊断，缩为GLB自包含修复+单份GLB验证。当前GEO仍REWORK，未签技术/视觉，STATE等本项。另原sat_gap钳到0，不能证明穿透，需有限正确SAT或独立补证。


architect复核GLB外部uri与非负SAT属真实阻塞。停止原独立导入后第三/最后GEO局部返修task_4688e49f44已通过Bridge真实派出：GLB专用JSON删外部uri、源glTF保留bin；新工程只放一份GLB核真实网格；带符号SAT先分离/接触/重叠三例，再1°有限角样本。不能宣称连续扫掠，旧无效PASS撤销。原[导入诊断](production-r1-receipts/u01-geo-before-final-fix/)保留，工人不得继续领取STATE。

当前main只读核实已前进3541b09（技术线证据工具），生产仍固定继承93cdd4f；此次不合流无关改动，不争写公共目录。主控预览GUI已关闭，接下来以离线源/清单核验为主。


## U01 GEO主控技术接受

第三次GLM返修task_4688e49f44交6243ad4（470秒，上下文used178003非计费），会话已关闭；主控读取全部结果和实际7文件。GLB自包含修复成功，但有限采样漏后壁，主控原始GLB完整SAT106角扫描出现35后壁穿插。按Bridge最多3followups边界，主控局部修后壁顶.63→.60，追加后壁/后衬自检；未放宽几何门槛。最终signed控制正/零/负、106/106实际GLB角样本通过，无旁置bin的新Godot工程11mesh/55surface/1512tri/四socket位置与空/有载包络通过（真实日志零错误），源连续两次字节级一致，CCW面检通过。证据在[u01-geo-accepted](production-r1-receipts/u01-geo-accepted/)。只接受GEO；后/+X socket朝向由STATE烘入，正常镜头与动画仍NOT_RUN。

PREVIEW默认小窗口底部错误曾截断，主控局部设外窗口最小1000×900，不影响1920×1200采集；实际原生默认窗口空输入错误直接可读，228自测通过。


## U01 STATE实际启动

固定2051d7f GEO接受后，worker/art-u01-20261004已真实FF，新session sess_710e3b5826仅原生/model GLM-5.3-Flash实际确认，再派task_468e1a8920（request c1ea8d5c-82ec-46f7-bae1-1d6db96c9b08）。首次/model后附说明被原生客户端当作模型名而失败，无文件修改；缩成单一/model指令即真实确认，不冒称失败切换已成功。STATE独占原三个私有路径，源烘动作/连接点朝向、支撑固定/货箱外沿按冻结尺寸/GLB自包含/逐surface接M01/包装完整复位，主控后续独立看样。

architect核F GEO只消费U几何尺度/货台/货箱，完整七态非真实依赖；主控先在工作树外复用接受B/M01做固定GEO静态normal三方向空有载比例预核，观察通过才解除设施GEO。并不签完整U/设施STATE/所有者效果；现仍未派F。


## U01静态比例预核与设施GEO前置解除

主控实际采集并逐张看六张normal三方向空有载Metal原图，输入固定2051d7f GLB dce2eb0b…，无裁剪/改灯；接受本批静态尺寸参照。只支持idle的外部test-only包装与原PNG/JSON/日志留[u01-geo-static-proportion](production-r1-receipts/u01-geo-static-proportion/review.md)。F GEO可独立消费已验车体/货台与冻结货箱尺寸；STATE仍等完整U01及各自GEO，所有者视觉NOT_RUN。architect已核该依赖缩减，不代签动作与最终manifest。


## F01/F02 GEO实际启动

两独立工作树clean后FF ceffd42；原sess_7084b23fec与sess_00a033faf9已分别以原生task_f9fce3e26f/task_bffe979345确认Flash，同session实际派太阳能task_e2225ffe24（request af566de8-ac8f-4cfa-bbb7-d3f5f47b7ed9）与加工task_341310019a（request fb97d4ee-3342-46b5-a9bd-2d7e2641fef9）。只做各自GEO、不同独占目录，与U STATE可并行；工人仅headless单GLB导入，不跑本机GUI/GPU。不把派出写成提交/接受，STATE仍BLOCKED。


## U STATE工具预检停止与缩片

原task_468e1a8920已cancel（elapsed1012秒，usage空/成本未知），完整结果只是读文档/确认环境，git clean2051d7f，没有STATE文件。两次 --headless --import --path . 缺 --editor --quit，第二次原日志为no main scene；不是资产导入失败，也不能视为通过。cancel后曾定位33732，实际定点TERM尝试时进程已退出，随后ps -p确认无该PID；没有全局杀进程。原日志/回执保留。

architect独立核缩为STATE-A源动画GLB、STATE-B包装manifest合理，无真实同交依赖。原形体/七态要求全部保留；A不求最终理论活动包络，先实际写源/有界GLB-only导入。相同Flash session真实派task_533ebb0690（request 05d40d7e-c1c1-487b-85cb-f6ab79d7af87），B仍BLOCKED，主控视觉未通过。


## 新近景发现轮胎侧环缺面（待局部修正）

主控额外用合同close镜头真实Metal采集已验2051d7f GLB，原图/日志在[u01-geo-close-review](production-r1-receipts/u01-geo-close-review/capture/blind_0001.png)。近景轮胎侧环呈扇瓣缺面；初算annulus_caps两端winding均朝内，inner ring_x与outer同向（内孔面应反向）。旧法线一致检查使用源自身法线，只证明索引/法线一致，不能单独证明所有外表面朝外。此为真实新疑点，已请只读explorer复算；当前U完整视觉不接受，STATE-A写者不中途争写，源交付后局部返修。车身比例/货台/盖板净空未受影响，不回滚未受影响的设施GEO参照。


architect进一步查F动作源没有U动画/manifest消费字段；只按真实依赖串行，各自GEO接受后可STATE-A源动画分目录并行，STATE-B/最终同场验收不代签。已在票/派单表单列，不增加资产种类或游戏规则。当前各F GEO仍实际制作中，尚未因此派STATE。


## 设施首轮未产出与执行缩片

F01 task_e2225ffe24（1699秒）与F02 task_341310019a（1700秒）都只有阅读/推演，git clean、无源/GLB；用户追问卡了后主控已停止并读取完整结果，不能算制作交付。F01尚未完全明确的折叠运动不应整包让工人设计；主控选择直立双折，packed/installed候选高1.2→1.9，保留两级铰链与completed尺度，architect核几何成立/动态中点2.85/主控授权内无需新审批。同步需求与票。F02结构已确定，不再长预检，续派GEO-WRITE task_ea59265629，先源+真实GLB/报告，导入视觉由主控后验；不降低最终门槛。


## U源r6技术接受；设施WRITE实际续派

主控读2b2abd6全部8文件/完整Bridge結果，实际耗时1181秒/context161065非计费。局部轮胎144个反向三角修正、正确后轮断言恢复、move-loop命名实际导入loop_mode=1。新空工程12mesh/56surface/1524tri/4socket无ERROR；74根/loop/quarter检查+9实际端点/货箱/socket检查通过，源重跑byte一致，192轮胎面物理方向全通过。实际Metal close看旧缺面消除，[实证](production-r1-receipts/u01-state-a-accepted-r6/)。只源技术接受，七态包装/normal视觉仍待B。

F01 worker clean FF e0fc333后同Flash session派GEO-WRITE task_4fb2c9fb5d（request8480ba31-d634-4f03-9db4-34e19e0b2107），只机械执行主控冻结直立双折，先模型文件再主控导入验收。F02续派task_ea59265629，两票无图形预签。
