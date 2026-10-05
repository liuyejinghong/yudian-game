# ART-F01 · 太阳能首样

**当前状态（2026-10-05，本轮火星优化）：REVIEW。** 本轮三件火星结构优化已实际制作；源/GLB/manifest与独立Godot检查完成，父票均REVIEW，完整视觉/所有者接受仍NOT_RUN。 F01当前888三角面/74材质面；本轮源由主控完成，GLM两源票无交件已取消。[当前F01证据](../evidence/art-solar-maintenance-r3-2026-10-05.md)、[三件r2记录](../evidence/art-mars-models-r2-2026-10-05.md)、[执行切片](ART-MARS-R2.md)。

**F01维护局部片：技术/指定近景复核通过，整票REVIEW。** 主控将源盖板终点改为-110°，revision3、active.max.Z=1.85；392姿态/10非法保持/16维护Basis、111角净空和10张新实机图已完成。内罩close0/90可见；正常距离完整盲比/所有者仍NOT_RUN。

下文GEO/STATE描述原r1/r9派单与验收边界；四轮或旧尺寸按历史读取，新结构以[冻结r2](../requirements/mars-equipment-r2.md)与当前manifest为准。

独占：`art/source/facilities/solar-r1/`、`prototype/assets/facilities/solar-r1/`、`art/manifests/solar-r1.json`。公共六材质和预览工具只读；不写其他模型、公共道具/材质、桥/Main/project/fixture/根TODO/PLAN/工程合同，详细禁写见共同约束。GEO与STATE对相同目录**串行独占**，不能给两工人同时写。

## ART-F01-GEO · 静态几何片

目标：中央脊/四脚/两侧各两折叠翼、运输束带/检修箱；packed/installed/completed静态端点。展开4.8×3.2×1.4m候选，折叠与展开端点同基座，PowerOut/Service两个socket。

前置：2051d7f U01-GEO与静态比例预核已通过，M01/PREVIEW已接受。仅消费车体1.172×1.6×.72、货台Y.48及冻结货箱外包络.64×.70×.38，不消费未接受U01-STATE/最终manifest。只按冻结分区/候选尺寸造一个版本，不整包探索设计；有真实造型冲突报主控修局部。输入再包括接受的六资源与预览commit。交可编辑源/生成器、真实GLB、geometry-report.md（含真实节点surface角色表、socket、静态/载荷AABB、源与GLB hash、来源、面数/纹理、简化说明）。GEO不需preview wrapper或最终manifest，动画留STATE，报告标尚未制作，不伪装N/A。几何报告供主控用Godot导入检查实际节点/包络和分面，必要的临时检查在工作树外进行，不要求工人改公共工具；GEO接受范围只含静态结构与接缝，正常镜头视觉在STATE最终包装后验。

正常案例：原点落Y0、米制-Y误写不得出现、前方-Z、identity scale；轮廓与关键结构按需求制作（0/90/180°真实图形检查留STATE）；翼片、束带、支脚、停机挡片和检修盖各有独立节点，surface表完整。异常案例：缺PowerOut/Service、错误轴向/缩放、折叠/展开端点穿插、缺surface角色时诊断失败，不篡改公共断言来通过。主控针对真实导入证据查AABB/socket/角色，并确认没有假能力/规则；候选比例或结构不合格局部REWORK。

验收交付：本票源目录geometry-report中写真实导入方式、命令、检查结果与NOT_RUN；GLM仅交SUBMITTED，主控独立接受GEO后释放目录写权。每次一个小片commit，不顺带STATE或其他设施。

## ART-F01-STATE · 局部动作与预览包装片

目标：四建设阶段和独立七态：deploying外部phase_t驱动4秒局部展开；completed work指示片、disabled停机挡片、maintenance大盖，move/charge/towed为有依据N/A。未建成work不运行，状态不重写phase。

前置：本资产GEO接受commit且目录释放，M01/PREVIEW已接受。包装实现不消费U01动作或最终manifest；完整U01及三件关系图是最终视觉比较门槛，齐套后另验，不阻塞独立包装制作。保留GEO成果。修改同资产可编辑源并重新导出动画/分件GLB；交私有`preview.tscn`/`preview.gd`、最终canonical manifest、源目录state-report.md与新run证据。使用`apply_preview`和材质surface表，不调用旧I00旋转桥；GLB实录hash必须同步，资源/动画名称填真实值。源、wrapper与GLB同状态可复现，不只在wrapper藏一套不同模型。

正常：需求的每态/阶段/载荷按固定time拍，对照正常镜头与近景；state逐项static/animated/na声明。切到disabled/maintenance再回idle/work、四phase往返及idle→N/A→idle，cargo始终empty；同输入重复两次不叠加、根固定。异常：未知第八态offline、非法phase/phase_t/cargo/reason、缺动画节点或丢surface/socket，先拒绝且原合法对象不变；从N/A回idle无残留。不吞异常、不以文件存在代替运行。

验收：主控先导入与包装接口检查，再用[视觉矩阵](../requirements/visual-acceptance-r1.md)独立看真实PNG/短视频，必要帧时刻/方向不可省；GLM图形未跑记NOT_RUN，不伪造图像来源。最终记录技术/主控可读性/所有者视觉三个结果，后者未看不写通过。真实游戏接入、性能预算与模拟事实均在本票之外。

### STATE源动画与包装分片（实际依赖）

经architect复核，设施动作不消费U01动画/manifest/箱体源字段。各自GEO独立接受且目录释放、源node/pivot固定后即可派STATE-A（源动画GLB+state-source-report），沿用已验车体/货台尺度及冻结状态；不因U七态未实现而阻塞此片。STATE-B才交wrapper/最终manifest、完整复位/材质/包络与正常镜头取证，保留上文最终比较条件。

STATE-A只写该资产源/GLB目录，不写最终manifest或公共工具；动画导入及指定时刻姿态实测，不以extras假声明loop。A交SUBMITTED停止，主控实际核并释放目录再派B。F01 phase_t与time_s独立、固定建设阶段不由动画自动提交；F02固定两箱/压头挡板/维护盖，offline仍映射disabled。源动画局部通过不代签最终包装、同场视觉或所有者效果。

### 首样折叠输入 r2 · 主控冻结（2026-10-04）

原1.2m折叠高使四层低平堆叠的两级铰链需要额外避让机构；architect已核下列最小可实施方案。只将packed/installed候选高调整为1.9m，宽/长及completed包络保留；这是首样候选而非量产/游戏占地。

- RightInner pivot=(.4,.85,0)，板local X0…1、Y±.04、Z±1.4；RightOuter子pivot=(1,-.10,0)，板local同上。LeftInner pivot=(-.4,.85,0)，板local X-1…0；LeftOuter子pivot=(-1,-.10,0)，板同左。用真实坐标镜像、保持正scale，索引实际朝外；板深蓝大面/浅框都在该.08厚和1宽内。
- packed/installed：inner局部Z右+90°/左-90°，outer相对局部Z右+180°/左-180°；内板X±.4、外板X±.5，板间净隙.02，板组Y.85…1.85。小铰链与束带最高≤1.9；packed双宽束带/收脚宽≤1.2，installed去带展四脚宽≤1.6，最低落Y0。
- completed：inner Z右+12°/左-12°，outer相对0°；展开≤4.8×3.2×1.4，中央脊顶.85，根/基座固定，结构连续的两级铰链。后检修箱/指示片/停机挡片/盖板仍按原需求，不能被折板遮死。
- deploying4秒：前半outer右180→270→360°、左-180→-270→-360°，inner保持±90°，向外绕而非穿中心；后半inner从±90→±12°、outer保持±360°。中点四片连续直立最高约2.85，属于动态包络；末端±360与completed0物理同姿。必须中间quat关键帧，不只将360归零导致走错路径。

GEO只造结构/端点，不提前烘STATE；先交可editable glTF/自包含GLB及实算报告，活动扫掠未跑写NOT_RUN。主控实际导入/看样再接受。
