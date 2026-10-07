# Stimson 风积砂岩露头 · 高保真 r2

2026-10-07。root 已转达用户实施授权；所有者最终视觉、Main 接入、整场性能不在本件完成声明内。约 6 m 宽 / 2 m 高，米制，Blender Z 向上，根原点在地面中心；GLB 转 +Y 向上，由 root 放在世界 (6,0,0)。只改本目录及伴生 ground-patch-hifi-r2 目录。

已实际查看 stimson-close.jpg、murray-buttes-shape.jpg、color-processing.jpg、r1 renders/detail.png、r1 evidence/native/near.png。参考中的侵蚀层边不等厚、局部斜交层理被裂隙截断；合理的平整断面仍存在，但没有孤立大片薄板搁架。NASA 照片只作形体参考，不进入纹理。

1. [x] 读 brief / r2 总计划 / 五项 skill SKILL.md，读相关 procedures、critique、API deltas 与拟采用函数源码。采用 structure-first、explicit planes、geometry before surface、triangulate what ships、measure projection、per-pair bake、mask-driven materials。角色解剖、对称面部、卡通默认、收费纹理生成和 GUI 笔刷不适用。
2. [x] 原生 Blender 5.2.2 LTS / build d13f752e3b9c，CPU / 6 threads。compatibility.py 实际运行 bx_audit.audit、bx_review.review、bx_uvbake.prepare_low、measure_projection、bx_materials.pbr_from_textures / audit_material 和 selected-to-active 小样，退出 0 / RESULT_JSON PASS，见 verification/compatibility.json。4.5 未运行。
3. [x] 独立体积经09最终形体 gate：连续不对称母岩、开放侵蚀湾、错位节理、有限层边/缺板，已由 root 与独立6.1high实际八图检查；冻结09几何进入试投射，不继承r1 generator。
4. [x] 高模层边破损和局部侵蚀通过09形体检查；保留原始高模，另制227,160三角面交付网格。轮廓/悬缘/大裂隙仍为几何，小尺度表面另作材质细化。
5. [x] 512真实selected-to-active后，局部UV折叠经过实定位/局部增缝与40°支路投影消除；1024/2048检查0重叠/翻转/零面积。新UV重烘512、实看湾口和右节理同光六图，4K三图已完成。
6. [x] 11材质预览获独立6.1 high PASS；实际512色彩/粗糙度投射证明最终Principled输入进入贴图，无方向光/AO。最终PBR组合接地近景正在输出，最终Godot视觉仍归root。
7. [x] 原创压缩 .blend（高模/交付分集合）、内嵌PBR GLB、必要textures已生成并原生重开/独立GLB导入PASS；README与manifest记录方法、实际指标及未验证边界。交接配方另独立复跑，最终同机位/组合图待root验收。

两次同类失败后停止该步骤，记录失败输出并重计划；不在坏形体上继续烘焙。技能辅助的 headless 程序几何明确称为程序制作，不冒称手工雕刻。

## 阶段记录

- stage14 经独立architect与root重计划：512中性底在sRGB PNG写成56/49/43而非预期129/122/115，零黑像素并不代表填充值正确。native小图已验证byte/float两种buffer都须显式linear→sRGB编码；另读回实际PNG确认。root授权仅base/rough正式分辨率4K候选，原normal/UV/accessor保留；升分辨率本身不被称作编码修复。输出detail/rear两图与完整候选GLB，固定Godot复核后才允许替换正式包。

- stage13 固定 Godot 光线下色调分区过弱：法线 GPU 采样与光向对照已由 root 验证，不重做几何、UV 或 normal。正式文件/贴图先备份到 stages/13-meso-response/formal-before；只复用已通过的裸岩/新鲜断面/积尘遮罩，出 512 base/rough 两图和同机位预览，并提供保留原 GLB 全部几何、UV、切线、法线贴图字节的候选。先实看 front/rear/close，再由 root 固定游戏机位验证，未授权正式4K重烤。

- stage09 独立6.1 high 灰模PASS，固定 source-high.blend SHA 7f3c801c…，允许512真实高低模试投射；PBR单独REWORK不撤销几何通过。材质10只改左前为非周期、不等厚短续沉积透镜层，并让背部粗/细颗粒混合比例与起伏强度随风化区变化，保持平顺尘顶和原形体；正式4K仍在新PBR+投射实图后。

- stage08 独立 gate 通过湾口/左前缺板，要求保留，不再添孔/板。仅背顶两相似齐整台阶 REWORK；PBR 预览也过均匀。09 直接撤掉两失败背顶切体，恢复合理完整后断面与已有节理；08其余几何全部保留。材质预览新增旧暴露层面限定的约18mm细层理，被断面/积尘遮罩截断，3D矿粒起伏与粗糙度按裸岩/断面/尘分区，仍不提前最终投射。08两份source固定保留。

- stage07 第二次 REWORK 后已停并由 root 调 architect 复查代码和原图：残留 bay d-loss 与 .018 blend 导致软融；固定两侧界+底界产生台口和背孤槽；整板 x/q 选区沿 y 直挤。08 替换这四个切体和两个剥落，不新增凹口：沿边变深并渐出原表面、下缘斜出，左前两端穿深度异倾角，上下边非平行。保留母体、其余层和右舌；冻结 front/detail/top/rear-detail 检查，并同交高模 PBR 预览，但不提前做正式 UV/烘焙。

- stage06 无菱形刻面，相关退蚀已跨湾口和背顶棱；独立 6.1 判本方法第一次 REWORK，仅需修两局部：侵蚀带偏软融/鼓包，改带方向的破面/残板；选部分左前长槽做整片缺失/崩断，其他完整层可留。背墙平整本身不再构成否决。stage07 保留母体/节理，以跨现有棱边的斜剥落面和带不规则边界的两个整片缺失替换软凹，噪声仅限定破面终止边界。

- stage04 独立 REWORK：背长墙/右舌长平面与直棱未有地质侵蚀；左前层槽虽变薄但相似缺口/圆端仍像加工；湾口凹口像规则洞，内壁横台阶过于平滑。保留已通过03母体，只返中尺度。
- stage05 实际打开 front/reverse/detail/top/rear-detail，作者自判 REWORK：取消方形 cutter 后局部 facet 退缩函数留下菱形/三角宝石刻面，仍是规则切面装饰；后长墙自然破边不足。连续第二次中尺度失败，已停此法，通知 root 调独立 architect 重计划。此前把 no-grain 过度限制成没有地质起伏是判断失误；后续应允许受层理/暴露方向控制的厘米级侵蚀与连贯起伏，但不能满面无因噪声。尚未执行 UV/烘焙。

- stage03 独立 6.1 Sol high 通过母体：不再是六柱/窗口，连续偏侧高肩与开放凹湾成立。stage04 保留整体，只修不对称凹湾退缩/崩落薄缘、左前上部层板破端、节理变宽和错落；无颗粒噪声，先 front/back/top/detail 实看与独立验收，再进入 UV/烘焙。

- 01 灰模：实际看 clay-front/reverse/detail。原生退出 0，约 72 万三角面，但形体 FAIL：每块等截面直壁和平顶，较强薄层近全高/全周，像叠板。root 看图后独立指出同一问题；保留 source-high.blend、三图和 build-snapshot.py。不做最终烘焙。
- 对应修正方向：先明确倾斜破面、崩塌楔面与随高度后退的整体，强层边限定在侵蚀带；细薄层降低幅度，层组角度须被新鲜破面截断。等待独立视觉评审补充后再做第 02 形体。
- 独立 6.1 Sol high 逐图结论：stage01 REWORK。要求上/下截面改变、深缺口进入轮廓和投影；斜交层组被破面截断；顶部裂线必须连接实体破裂/开口/错位，不能只是软起伏盖板。可保留左高右低、前低后高布局。该结论与作者、root 自检一致，本轮第一次形体失败。
- stage02 重新选更直接的体积构造：使用技能 bx_sculpt.Clay / Prim / sd_halfspace / to_object(symmetric=False)，为每块定义倾斜半空间母体、有限层组和凹口，从体积等值面重建，避免等截面外挤壳直接产生叠板。只在检查通过后减面与烘焙。
- stage02 作者实际打开 front/reverse，仍判 REWORK：真实体积缺口和顶裂已产生，但 6 块相似屋顶形与重复方凹口变成新的程序模板痕迹；少量倾斜层组不能补救整体重复。连续第二次形体失败，已停止该方法，通知 root 调用独立 architect 重计划。原生源、图和脚本保留；没有正式烘焙。
- 待独立诊断的新方向：从一个连续侵蚀母岩体设计整体前/背/顶截面差异，再切出少量不等深节理；不再把同一种切口/顶裂/侵蚀规则重复施于 6 块。先低/中分辨率灰模单件通过，再做表面细化。
- architect 已独立诊断：01/02 共用 SPECS 六柱构图才是根因，SDF/面数没有改变它。冻结 03：一个偏侧高肩→宽缓鞍部→另一侧低岩舌的连续低矮母岩；一条从顶向前岩脚张开的开放侵蚀凹湾，邻侧一斜楔；两道错位倾斜、宽度变化节理；只在两个暴露面塑有限长短不等层边，终止于斜破面。03 无颗粒噪声，先 front/back/top/凹湾 detail 同光灰模。独立 6.1 也确认 02 REWORK，不涉及材质。

- stage13 512小样连续两次暴露三角黑斑：首轮黑底，第二轮中性底+use_clear=False仍保留细边斑。正式文件未动、几何/UV/原normal哈希一致；暂停第三次试烤，通知root调用architect复查小样分辨率/图像绑定。接下来只做只读定位，未把小样宣布可验收。

- stage14已实际4K两张EMIT、正确编码中性底及GLB完成；作者打开detail/rear两图，旧黑三角未见，独立GLB原生导入PASS（227160 tris/3×4K）。正式双资产文件哈希未变；candidate.glb SHA6bc4c9e9…，等待root固定Godot视觉后才决定promotion。

- stage15 root按独立architect方案授权：撤下stage14 cloud颜色方案，回正式12/11材质；仅右断面与后墙各一块带暴露边界的2–8cm浅剥蚀/断裂台阶，深数毫米至约1cm，同加HIGH与GAME真实表面。保持macro/轮廓/薄层/节理/落地/ground，优先现UV局部细分插值；先同固定机位高低灰模+未烘材质reference，再通过后做4K候选法线，禁止512反复试烤。独立阶段保存，正式包不覆盖。

- stage15第一轮native退出0、六张实际打开；作者形态REWORK：离散角状浅坑聚成椭圆点蚀章，非自然连通浅剥落。保留attempt01-discrete-stamp全套源/图/代码，不进入4K烤。局部细分与原UV插值断言、macro bounds均通过；下一轮只收敛剥蚀形态为少量连通有方向破面/残片，不改macro。

- stage15第二轮native退出0、六张实际打开；作者仍判形态REWORK：连通剥片撤掉散点坑，但窄条外轮廓与内部台阶偏刀刻，缺少自然破面。连续两次后停止第三次修改，交root与architect重计划；无4K烘焙或正式替换。独立只读检查：macro bounds精确保持，UV插值误差1.26e-7、原面面积分割误差9.06e-10；局部细分660004tris有24735个非三角面、6个新极小UV负面积碎片，须改拓扑方法。未位移原顶点角法线最大差.000767与原网格原地重设custom normal量化误差一致，新增插值角点最大差.00245；不能把此数值误判为全局重算法线。

- stage15独立architect重计划并获root授权：旧场内部74–78%坡度小于1°、最高10%面积集中了95–99%坡度能量，刀刻源于内部平底。备份attempt02后复用既有分网，仅把位移场换成2–8cm连续、弱层向、非周期浅起伏，目标2–6mm、最大8mm，12.5–14.5cm宽边缘淡出；不新增细分。局部清理切分退化并确定性三角化，原UV不重排，源11材质仅随真实新剥蚀露出。输出面内坡度统计与原两机位6张灰模/PBR，数值不替代实际看图；形态通过前仍不烘焙。

- stage15连续场第一轮经root实际四图：内部自然起伏保留，规则软矩形覆盖/后墙纹理岛仅边缘返工。内部P10–P90深约1.9–5.7mm、坡度中位4.7–4.9°；<1°为4–5%、top10能量约37%，不再用数值冒充视觉通过。原生去短边不适用近共线长边碎片；精确仅换7片所在同原面的内部对角线后，实核负UV/零面积/ngon均0、UV面积分区误差9.06e-10，未动顶点或UV坐标。下一次只换coverage为偏侧、宽衰减、相连不规则暴露尾部；保留相同内场频率/幅度系数与局部拓扑，无4K。

- stage15宽渐散coverage经root实际四图接受，可进入独立4K候选，不等于最终高保真PASS。逐角点对照原文件同版native roundtrip最大差2.748e-4/P99=0，沿用.0003阈值；旧绝对法线门槛的REWORK与控制证据保留。仅一次局部0.35°原生收简试验在原面111008产生不能同面重排的UV微片，结果REJECTED_KEEP_DENSE，未保存减面资产；不追加第二轮，保持659908三角。16阶段重测HIGH→GAME投射，烘3×4K候选，正式12源/贴图与ground仍不覆盖，最终Godot/Sol及生产入口收口另验。

- stage16候选已实际原生3×4K高低模投射、4张烘焙图全部打开、保存重开与空场GLB导入PASS。候选GLB SHA02755e27…/43071496 bytes，压缩源SHA0c1aa0b3…/87623481 bytes；stage私有属性已去掉，659908 GAME/1294746 HIGH。完整2048 UV分析零翻转/零面积/零重叠，286.7px/m、38px gap；新pair漏射0，保持原cage2.41mm/ray5.2mm。源11细颗粒/层理保留，正式12与ground文件哈希未变。仅候选通过技术核验，root固定Godot7图/Sol视觉及生产公开可复跑入口尚待完成。

- stage16独立Sol与root再次视觉REWORK：中央纹理岛配光滑外围本身是贴章，软化小窗边缘没有修复连续性；背面fresh分支欠固有色细节。17按architect与root授权取消人工小ROI，仅沿右实际斜断面及整片实际后墙按裸岩/积尘/断裂关系分布连续浅起伏；09轮廓、层理、节理、接地和ground冻结。高低模只在实际面的原生分网内补所需采样，不复做失败减面；保留16内场频率，新增2–8cm低对比矿物固有色，fresh更弱更细、dust安静、无AO/灯光/27cm色云。先原两机位完整面灰模/源PBR；接受后才用正确sRGB底的2048三图真实投射诊断，确认游戏距离连续性与背光可读后再4K。正式源与生产入口仍不修改。

- stage17完整面首轮六图已原生生成，作者与root实际四图接受连续覆盖方向，允许技术通过后2K真实诊断；不提前4K/最终PASS。HIGH 1,281,204 / GAME 930,876；同原拓扑法线控制差0.000602，原生同新拓扑零位移控制完整复现该差，确认主要为fan基底编码；但候选相同角索引仍有35角点达0.000329，0.0003门禁未过。一次仅内存“最后三角化”试验减少分网，但原面5363产生不可同面修补负UV，断言退出且无资产输出；旧候选与失败日志保留。暂停继续变换网格，已交root重计划，正式源/地面/生产入口不动。

- stage17独立architect按实际语义修正保留判据：35个旧零位移编码差角点完整分类为右面12/后墙23，真正范围外0，不再把许可面边缘当范围外。原法线目标/坐标/全平滑硬边保真；同候选坐标、拓扑、初始硬边的独立编码控制，真正范围外及外面共享fan角点差均0，沿用0.0003。旧0.000602/0.000329及新许可面编码细差另存，不调许可面法线。全UV检查发现1750片精确零UV，3D重复边≤5.960464e-8m；root授权只合并875条重复边/868新增顶点，原顶点删除0，最终929126tri。clean-preservation.json独立PASS：零/负UV/ngon均0，面积分区误差8.90e-10、插值9.81e-8、macro差0、范围外同状态法线差0。原17候选保留，source-clean独立保存SHA aac30d2b…。18只进入已授权2K真实三图投射及固定Godot/Sol诊断，不提前4K或formal晋升。

- stage18已实际原生2K三图selected-to-active：2048 UV全检查零翻转/零面积/零重叠，143.4px/m、19px gap；实际3502投射样本漏射/错件0，维持原cage2.41mm/ray5.2mm。实际PNG底色129/122/115、法线128/128/255、粗糙217；原生保存重开及空场GLB导入PASS，929126tri、3×2K RGB8+切线，stage属性清除。作者实际打开两固定烘焙图，连续性保留；candidate.blend 90,485,933bytes SHA92bbc97c…；GLB40,650,824bytes SHA792b5e2a…；正式双资产/贴图哈希不变。等待root固定Godot/Sol诊断，尚无4K授权/最终高保真PASS。

- stage18 root实际固定Godot7帧与完整候选验证PASS；独立Sol阶段PASS确认右完整面连续/贴章解除、后背光低对比纹理可读、无明显砂纸/重复/强云色且薄层接地ground无退步。root授权19独立4K三图，17clean/18形态、UV、材质图、色彩、灯光冻结；只升贴图分辨率，实核38px间距/8px扩边/已测安全包络。原生source/GLB均需<100MiB，已足够小则不再清理；canonical入口仅只读，最终独立PASS后才收口复跑。

- stage19原生4K三图完成；与18冻结GAME几何/UV/法线hash一致，实核gap38px/外扩8px，3502投射样本零漏射/错件。新detail/rear烘焙图均实际打开，完整面连续性与克制色彩保留。源压缩102,825,957bytes、GLB53,028,780bytes，均<100MiB，不追加清理/降面/降图；原生重开与空场GLB导入PASS，3×4K RGB8打包及切线PBR正确，正式12/ground无改动。source SHA7e50eb0d…、GLB SHAae5722b1…；待root最终固定Godot7帧/Sol，canonical入口仍未改。


## 正式晋升与公开重建收口

- [x] root 固定 Godot 七帧与独立 6.1 Sol high 最终内部核心样件 PASS 后，备份旧12正式源、GLB、贴图、清单和图片，再按字节晋升19。游戏网格929,126面、高模1,281,204面；GLB SHA ae5722b19aa99a1e3a0e5333d41eb5544ac9c2295c35685be9f41f5b05df4e78。
- [x] 原生只迁移源渲染输出为相对路径，保存重开后全几何/UV/角法线/颜色属性/可达材质输入/打包图指纹相等；新源SHA94ff01a08b6ef25a30dafeb2db2cb56c93589aeee280b23cf151ca705fa42ea0、102,824,670bytes。实际原生最终验证PASS，GLB与三图未重导出。所有者视觉/Main/整场性能仍NOT_RUN。
- [x] 旧12十张图与原来源保存在renders/historical-12；当前两张图按字节复制19实际渲染，记录旧源7e50…与正式94ff…等价证明，没有把旧图片换上新哈希。
- [x] 公开build.py支持独立输出；首次执行缺少脚本路径导致ModuleNotFoundError，保留失败日志，只补sys.path后重跑成功。实际20/09原生exit0，从体积算法生成946,516面高模与八张过程图。
- [x] 实际20/10从新09生成227,160面低模，完成原生UV修复、512真实法线和固定对照，验证新prepared.blend。
- [x] 公开finalize --finish --prepare-only从新10应用11材质、完整面浅起伏、精确重复边清理及独立同状态门禁，与正式交付HIGH/GAME/UV/法线/颜色/可达高模材质逐项比较；将实际结果存到verification，不能只留在被忽略的20临时目录。
- [x] 完成simplify与独立architect收口检查，将实际重建范围写入README/manifest。完整4K产物技术和视觉验收已有19真实执行；本次公开入口另验准备阶段，若未再烤整包需明确写明。

独立architect收口发现并已修正：finalize --finish原默认正式目录可能覆写冻结包，现必须显式--output-dir并拒绝正式目录；输入指纹补matrix_world和活动/渲染UV层，以覆盖Geometry.Position/Normal及UV选层输入。

- 实际20重建与原生双源比较均exit0。verification/rebuild-preparation.json PASS：HIGH/GAME逐字节indexed几何、UV、角法线、所有颜色、world矩阵、活动/渲染UV层以及HIGH可达材质图完全相同，无差异。639,681个真正范围外角点同状态法线差0；高模1,281,204、GAME929,126，38px UV间隔、零错误。公开整包4K本次未再跑，不将准备验证冒充整包复现。

- 最终simplify检查保留现有直接调用结构，未新增依赖、通用框架或几何参数变更。architect复核两处发现均修复、八份脚本哈希与实际回执一致、无新增阻塞；确认未将prepare证据扩称完整4K复跑。root已独立回读正式94ff源/ae57 GLB PASS，并验证Godot current6输入。脚本固定，无运行中的本代理Blender进程。
