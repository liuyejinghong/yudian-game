# 火星高保真核心样件与流程交接 r2

用户授权：找Blender/建模技能，由Astra xhigh制作一到两件高保真核心素材；独立6.1 Sol high视觉验收通过后，将实际跑通的流程交给6.1 Sol。保留r1调研、风格方向、场景框架和技术成果，r1精细度未接受。本轮不替换Main、不改变资源/配方/保存，不合并PR。

选择两件互补核心素材：约6m层状砂岩露头；围绕它约12×12m的积沙/露岩/多尺度角状碎屑模块。它们验证自然岩体、材质和接地关系，设备仍是现役尺度参照。两件共用中心地面原点，露头低处允许埋入约14cm；模块外围过渡到r1平缓地面，外缘没有重面或台阶。精确范围以制作单记录为准，不冒充测量着陆点。

1. [x] 独立architect前审：先修中尺度结构，悬边轮廓不能只烘进normal；需要同光灰模与最终GLB对照，不以加面或4K代替形体。
2. [x] 筛选/安装固定版本技能并做当前原生版本兼容性小检查。证明：原始仓库/许可证/固定SHA与实际调用记载；不执行付费服务、不装MCP；Blender已按用户后续授权升级，有不适用内容就明确排除。
3. [x] Astra实施：只拥有art/source/environment/mars-outcrop-hifi-r2/**与mars-ground-patch-hifi-r2/**。先独立灰模正/背/近景，检查不等厚破层、斜交层理截断、缺口与侵蚀，达到形体标准再烘焙。高模与交付模型分开保存；normal由真实高低模投射，毫米颗粒用可读材质，不做无意义百万颗粒。证明：实际高低模、烘焙、UV检查与看过的渲染图；两失败记录并重计划。
4. [x] root集成独立r2看样并技术复验。复用r1镜头、光照、曝光与尺度锚，保留r1和新图作对照。证明：GLB实际导入、normal/near/reverse/horizon/low PNG、源/hash/纹理/尺寸核验、源重开；不以技术PASS代表视觉接受。
5. [x] 独立6.1 Sol high视觉验收。按用户要求另拉会话，只读实际参考、灰模、高模/交付对照与Godot成品图；先给PASS/REWORK及具体证据。失败只返对应部分，两失败重计划。此结论是独立质量检查，所有者最终审美与Main/性能另记。
6. [x] 验收通过后记录实际可复跑流程、失败点、版本适配与质量样板，交独立6.1 Sol实施会话试跑复用检查。证明：接收方实际读取、按流程完成独立验证或小样，不把“已发送”当流程已可复用；不自动批量生产全资产。
7. [x] architect收尾，提交草稿与可运行入口，记录完成/未验。证明：精确源身份、干净范围、实际证据和交接结果。

## 文件归属

researcher只写SKILL-RESEARCH.md；Astra只写上述两素材目录；root拥有本目录其他文件、技能本地安装与r2看样集成。视觉会话只报告不改资产；6.1制作流程接收方拥有另一个独立验证目录。跨会话消息仅按本次用户显式授权协调这些制作/验收/交接任务。

## 技能版本与限制

固定scenario-labs/skills提交91caa011e13774a220aba7136309b49964104040，MIT；工作树.agents/skills安装expert/sculpting/retopology/uv-baking/texturing-shading。它们下一回合可被发现，本轮通过绝对路径直接读取。用户后续明确授权升级；已核对官方最新稳定版5.2.2 LTS，官方包校验、应用签名与原生烘焙/旧源读取/保存重开/GLB回读已通过，Astra已切至5.2.2继续；角色解剖、stylized默认、外部Scenario生成/付费纹理、GUI-only笔刷不用于本轮。无Blender MCP桥；实际执行通道仍是Blender Python/原生headless，技能改变分阶段制作和检查，不能凭安装声称手工雕刻或高保真已实现。

## 当前状态

Blender已升级5.2.2 LTS；露头19与伴生地表通过独立6.1 Sol high最终内部视觉验收。19已晋升正式包；公开重建入口在独立目录从零到完整烘焙输入PASS，并与正式输入精确一致（没有重复完整4K bake/export）。6.1 Sol独立小砂岩碎块实际流程试跑及root原生复验PASS。Main、所有者最终审美和整场性能未运行。以下阶段记录为历史，不代表当前仍处于REWORK。

- 用户追问版本并授权升级：此前沿用4.5.14LTS，没有更新5.2。2026-10-07核对官方当前稳定5.2.2LTS；按用户要求安装Apple Silicon版到workspace tools-bin，保留4.5用于旧文件回查，不安装测试版/不改变系统安全设置。原生基础建模/烘焙/GLB导入能力实际检查后再恢复Astra。独立视觉会话已创建：01a1155a-4d21-7de0-ac95-08280870e47e，gpt-6.1-sol/high；候选尚未交付，不报视觉PASS。

- 升级实测完成：5.2.2LTS/build d13f752e3b9c，官方arm64DMG sha256 dc4125399b8bfefe283cc1624d6cfc7809d1cac20ace51072127eb371f31f210；codesign有效。原生小检查见evidence/blender52-smoke.json。旧47998面源原hash未变，真实normal高低模投射PASS，packed保存重开与GLB回读PASS；性能比较NOT_RUN。Astra收到新版执行路径后恢复。

- Astra新版技能小样已真实调用bx_audit.audit、bx_review.review、bx_uvbake.prepare_low/measure_projection、bx_materials.pbr_from_textures/audit_material并完成normal bake，5.2.2/CPU6threads。root读代码/结果并看review_sheet：闭合网格与真法线投射检查PASS，投射miss0/wrongpart0。此为基础技能可用性，不是火星样件质量。

- 第一阶段灰模01（source-high sha5312ee66…）：root与Astra逐图确认虽有真实层边，平顶/等截面直壁与全高强层纹仍像叠板，保留证据且停最终烘焙。已交独立6.1Sol high灰模预验收；当前非高保真PASS，修轮廓/破面根因后再推进。

- 独立6.1Sol high灰模01明确REWORK（evidence/visual-stage01.md）；Astra修02，先结构后材质。伴生地表01的310碎屑在实际图不可见；root与Astra读取放置公式定位min(g-z)错误，改max后验证露出/接地点，保留01再渲染新阶段；不把数量统计视为视觉存在。

- 露头02（source-high sha3164fe10…）：root与Astra实看front/reverse/detail仍判REWORK，6相似屋顶块+矩形洞不是连续侵蚀母岩，146万高模面/真实skill半空间函数不等于质量。连续两次形体不合格，停止此模板方法；architect独立只读重计划，先单一母岩体的前/背/顶差异和少量节理，再细化/烘焙。独立6.1Sol复验待返回。

- 03连续母岩由独立6.1 Sol high通过基础检查，但不是高保真完成；04中尺度仍REWORK，具体见evidence/visual-stage04.md。Astra保留03整体，05修右舌/背长墙的侵蚀体积、不同厚薄残板与凹湾连通破缘，未进入正式烘焙。

- 05作者与root实际图检查REWORK：局部facet产生宝石切面，长墙仍过于完整。04/05为连续两次中尺度失败，停止切面补丁，architect重计划（evidence/visual-stage05.md）。原始03母体与伴生地表工作保留。

- 伴生地表04由独立6.1 Sol high与root实际检查几何基础PASS（evidence/visual-ground04.md），可做512高低模投射及保轮廓对照；组合低机位/接合/材质/最终高保真仍待验。

- 06沿层侵蚀由独立6.1 Sol high检查REWORK（evidence/visual-stage06.md）；只修软化侵蚀和部分长槽的断板关系，保留母体与合理平断面，07局部返工。地表真实边界9.66mm误差已定位为解析高度与2m实际网格插值差异，修边后重新检查投射。

- 地表05真实接缝已由root原生GLB支持地面导入/高模1200边点实测PASS（evidence/ground05-join.json），最终交付GLB仍待验。露头07右舌修改通过，湾口/背顶与左前两段断板仍REWORK（evidence/visual-stage07.md），06/07两失败停止补丁并请architect重计划，伴生地表UV检查继续。

- 露头09中尺度几何由独立6.1 Sol high PASS冻结（evidence/visual-stage09.md），进入512投射；PBR仍返工等距细纹和均匀砂纸感，保留已通过几何。地表4K首轮16bit包过大，保留本地原包后全分辨率4K8bit交付，几何/UV不变，最终原生/GLB/接合复验待完成。

- 地表4K8bit最终包已由root独立原生重开、GLB回读和实际两GLB边界检查PASS（evidence/ground-final-native.json、ground-final-join.json）：374280交付三角面、六张4K贴图，600边点最大分离4.56e-9m。组合场景视觉和性能仍未验。露头10材质仍REWORK（evidence/visual-stage10.md），形体不改，architect局部重计划；UV仅修两处真实自重叠，不放宽门槛。

- UV局部求解与切支路后，剩当前岛2255的7228面真实折叠（1024/2048约0.13%）；无折叠备选有48°角误差和9.15倍密度跨度，未接受。停止重复换solver，architect读当前候选后确认仅该岛40°Smart Project、其他UV展开保持，重pack后再次定位与512投射。地表已实际Godot导入退出0、无ERROR/WARNING，六张提取PNG与GLB内嵌字节相同。

- 11材质预览由独立6.1 Sol high PASS（evidence/visual-stage11.md）；仅允许进入烘焙验证，不报成品完成。局部40°UV候选原生1024/2048均零重叠/翻转/零面积，范围外展开差值0、几何不变，root已读实际JSON。Astra绑定新候选做512同光与最终材质输入检查，通过后正式4K；最终源、GLB、Godot组合和流程接收方仍待验。

- 最终技术候选：露头227160交付/946516高模，三张4K；地表374280交付/1822392高模，六张4K。root实际独立重开两源、空场景导入GLB及内嵌图像检查PASS。公开GLB extras的本机绝对路径已清除；root独立比较完整BIN与非extras JSON证明全部视觉字节不变，再绑定新hash复验。当前证据为outcrop-final-native.json、ground-final-native.json、ground-final-join.json和native/capture.json。

- Godot实际亮方垫由色彩空间和Lambert/Burley着色差异定位；仅hifi支撑Shader克隆显式Burley、线性砂色转换和relief .08，已撤回无效临时边缘纹理混合。native七帧为当前源身份，verify.py PASS；六视图/画质/说明/拖动重置输入检查PASS。最终独立视觉结果待返回，所有者审美/Main/整场性能未运行。

- 最终视觉REWORK（新会话01a11623-f7ab-7d83-8824-33b9ba00654f）：层状形体、薄层与碎屑接地通过；ground/reverse直线颗粒边界和detail/reverse岩面区分仍不足。architect局部计划：支撑地面复用最终Sand PBR和连续worldUV，补齐600边点；岩面先同机位/光照对照再判断是否调局部材质。旧会话因图像历史413停止，用新会话完成最终复核。

- 地表边界局部复核PASS（visual-final-ground.md）：共享实际Sand PBR和UV、600边点/法线对齐后，用support原生镜像UV修非无缝纹理的period边界；核心patch保持。岩面固定镜头仍REWORK，Astra stage13中尺度base/rough的512小样两次黑边斑，停止第三次并请architect查技术根因。

- stage14修正底图背景的sRGB编码（真实PNG保存重开PASS），只做4K base/rough两图候选；升分辨率不是编码缺陷修复。root独立比较GLB所有非替换bufferViews逐字节相同、原normal仍完整4K，两个新PNG为4096²，公开extras相对路径。正式源尚未覆写，当前runtime暂载stage14候选准备固定机位复核。

- 最终岩面stage14再次独立REWORK（visual-final14.md）：云状色差未恢复可辨侵蚀/表面质感。与最终01为两次同一剩余问题，暂停继续颜色补丁，请architect重计划物理尺度/normal响应/像素密度；正式源未覆写，runtime暂载候选用于诊断。地表边界通过成果保留。

- architect重计划15（rock-replan15.md）明确区分两ROI主光方向与尺度：细节87–89%落在<2cm、右面角RMS仅.58°，stage14云色不补形态。root实际SKY单变量诊断也未足够恢复，正式光照不改。授权Astra仅两局部HIGH/GAME真实2–8cm浅剥落，保macro与UV、必要局部normal更新，先灰模/真投射，再固定视点验收。

- stage15首轮局部灰模作者与root同判REWORK：离散角状小窝聚成椭圆贴章，不是自然浅剥落，未烤4K。保留图/源后收敛为各一块连通、带方向的浅剥落及少量不等侧缘/残片；局部GAME过密细分也待原生收简，不将面数增长当精细度。宏观与地表通过成果不动。

- stage15第二轮角状连通剥落仍REWORK：坡度集中在轮廓、内部近乎平面，产生刻线感。architect只读实测后改连续2–8cm浅起伏，不加网格密度；第三轮内部坡度已改善，root实际四图仍发现规则覆盖框，现仅修不规则宽渐隐边缘。7个原面内切分的负UV微片单独定位，以不移动顶点/UV的局部内部换对角线修复；检查未过前不烘焙4K、不覆盖正式源。

- stage15宽渐散覆盖修订：root实际打开gray-game-detail/rear与source-pbr-detail/rear四图，规则覆盖框已消失，允许进入真实4K三图候选烘焙。原面内7处换对角线的UV面积误差0；独立诊断负UV空、范围外corner normal变化精确匹配同版本原地roundtrip。正式12源不覆盖；最终Godot/Sol尚未验，新候选不冒用旧12证据。

- 法线控制解释修正：前述约4e-11仅为两组最大差统计值的差，不能代表逐角一致。覆盖渐散候选已实际按相同角索引对照同版本原文件roundtrip，最大差2.748e-4、P99=0，低于原.0003门槛；旧绝对对原始值的REWORK报告保留。UV零负向/零零面积/全三角、宏观bounds差0。

- 仅一轮0.35°局部近共面收简未采用（15/local-reduction.json）：原面111008重三角化产生不能同面换对角线修复的UV微片，REJECTED_KEEP_DENSE。root读取实际报告；未保存减面结果、不做第二轮，保留已验证659908交付三角面候选，转入16独立真实4K三图投射。面数增加不构成视觉质量证明，性能仍NOT_RUN。

- stage16实际4K与Godot7帧完整技术检查PASS；独立Sol六图仍REWORK（visual-final16.md）：detail局部贴章、reverse细节仍弱，薄层/接地/ground无退步。root单变量SKY-stage16诊断仍不足，正式光照不改。暂停继续局部fade与烘焙，architect重计划面内连续性/背光响应；两小ROI仅实现选择，用户授权允许修这两裸露面内的真实根因，09宏观与其他通过成果保留。

- architect重计划17（rock-replan17.md）：贴章源图已经存在，取消两个人工小窗；依两实际面的裸岩/尘覆/断裂连续分布，补相应低对比固有矿物/侵蚀色差。先灰模/PBR，再2048真投射固定游戏诊断，成立后才4K。不改正式光照，不重复失败收简或小窗fade方法。Astra只拥有原两资产目录继续；formal12/ground与09宏观通过成果保留。

- stage17 root四图确认两完整面连续性方向，尚不报最终PASS。930876三角面原候选保留；一次末尾三角化替代分网产生不可同面修的UV负片，未采用/不继续减面。法线keep混入许可面边缘；architect独立校准真实保留契约（normal-contract17.md），要求相同候选几何/拓扑/初始硬边的目标编码控制、范围外目标/坐标/硬边实核，仍用.0003，保留旧REWORK而不提高门槛。控制通过后才进入2048。

- stage17真正范围外639681角点同状态编码控制、坐标、原角目标与硬边实际保留，差0；旧35个角点全在许可面（右12、后23）且fan受位移影响，旧报告保留。新增1750片为UV精确零、边长最大5.96e-8m的重复短边零宽三角，合并875边/868新增点，原顶点移除0，未继续减面。root读clean-preservation.json：929126三角面、UV零负/零面积/全三角、原面面积分区8.90e-10，范围外与共享fan控制差0，宏观边界0。进入18独立2048真投射，正式源不改。

- stage18 2048三图/UV/投射/原生源与GLB及root实际Godot7帧技术PASS；独立Sol六图阶段PASS可进入4K（visual-stage18.md），贴章解除、背光面不再纯平板，无明显砂纸/重复纹/强云色，通过成果保留。仍不是最终高保真PASS；19仅按相同形态/UV/材质烤4K候选，formal12与ground不覆盖，之后最终原生/Sol复验。

- stage19三张4K与18形态/UV/法线hash不变，gap38/外扩8，3×4K原生源/GLB与root固定7帧完整技术PASS；source102825957 bytes <100MiB，未降质量/额外清理。root实际打开source两图与Godot七图，最终Sol六图复验已派发，结果尚未返回；formal12与ground不覆盖，流程交接尚未开始。

- stage19独立最终内部视觉PASS（visual-final19.md，cursor:6），两件核心样板通过；不是Main/所有者最终审美/性能通过。已授权Astra备份12后收口正式19源/GLB/新证据及公开可复跑链，复跑新目录不覆盖冻结包；root随后复验并派6.1 Sol实际试跑。

- 正式19与GLB/贴图保真晋升完成；源仅把render输出改为相对路径，完整视觉输入保持，现source94ff01a0…。6.1 Sol独立worker拥有mars-hifi-sol-trial-r2目录，实际制作/烘焙/重开/GLB回读试跑已开始；architect收尾只读审查同步进行。

- 6.1 Sol实际小样试跑与root另进程原生复验PASS（sol-trial-root-native.json），8个输出/4个正式文件hash一致且正式不变；architect只读实看高低模确认暗三角修正。公开19重建链20仍等待完成，不由512试跑替代。

- 公开20新目录build/UV/512真投射/完整面准备均原生exit0，rebuild-preparation.json逐字段绑定当前公开脚本与正式94ff源，完整烘焙输入精确一致；此轮未重跑新公开完整4K bake/export。root重新打开94ff正式源并GLB回读PASS；正式看样6视图/画质/说明/拖动重置实际输入PASS（input-final19.json）。

- architect最终只读审查无阻断：身份、公开链边界、实际Sol交接、默认r1保留和Git缓存排除均通过。交付保留草稿PR44；不合并、不将内部核心样件PASS扩成Main/整场验收。
