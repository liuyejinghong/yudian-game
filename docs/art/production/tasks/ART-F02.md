# ART-F02 · 加工设施首样

**当前状态（2026-10-05，本轮火星优化）：REVIEW。** 本轮三件火星结构优化已实际制作；源/GLB/manifest与独立Godot检查完成，父票均REVIEW，完整视觉/所有者接受仍NOT_RUN。 F02当前1224三角面/33材质面；本轮源由主控完成，GLM两源票无交件已取消。[当前证据](../evidence/art-mars-models-r2-2026-10-05.md)、[执行切片](ART-MARS-R2.md)。

下文GEO/STATE描述原r1/r9派单与验收边界；四轮或旧尺寸按历史读取，新结构以[冻结r2](../requirements/mars-equipment-r2.md)与当前manifest为准。

独占：`art/source/facilities/processor-r1/`、`prototype/assets/facilities/processor-r1/`、`art/manifests/processor-r1.json`。公共六材质和预览工具只读；不写其他模型、公共道具/材质、桥/Main/project/fixture/根TODO/PLAN/工程合同，详细禁写见共同约束。GEO与STATE对相同目录**串行独占**，不能给两工人同时写。

## ART-F02-GEO · 静态几何片

目标：3.6×4.8×2.4m候选；三级体量主腔/低进料床/短出料架、局部压头/挡板/检修盖、两个私有静态货箱，Input/Output/PowerIn/Service四socket。进出区域与车货台同高，物件不生成资源。

前置：2051d7f U01-GEO与静态比例预核已通过，M01/PREVIEW已接受。仅消费车体1.172×1.6×.72、货台Y.48及冻结货箱外包络.64×.70×.38，不消费未接受U01-STATE/最终manifest。只按冻结分区/候选尺寸造一个版本，不整包探索设计；有真实造型冲突报主控修局部。输入再包括接受的六资源与预览commit。交可编辑源/生成器、真实GLB、geometry-report.md（含真实节点surface角色表、socket、静态/载荷AABB、源与GLB hash、来源、面数/纹理、简化说明）。GEO不需preview wrapper或最终manifest，动画留STATE，报告标尚未制作，不伪装N/A。几何报告供主控用Godot导入检查实际节点/包络和分面，必要的临时检查在工作树外进行，不要求工人改公共工具；GEO接受范围只含静态结构与接缝，正常镜头视觉在STATE最终包装后验。

正常案例：原点落Y0、米制-Y误写不得出现、前方-Z、identity scale；轮廓与关键结构按需求制作（0/90/180°真实图形检查留STATE）；压头、进料/停机挡板、检修盖和两个固定货箱各有独立节点，surface表完整。异常案例：缺Input/Output/PowerIn/Service、错误轴向/缩放、进出料箱或压头几何穿插、缺surface角色时诊断失败，不篡改公共断言来通过。主控针对真实导入证据查AABB/socket/角色，并确认没有假能力/规则；候选比例或结构不合格局部REWORK。

验收交付：本票源目录geometry-report中写真实导入方式、命令、检查结果与NOT_RUN；GLM仅交SUBMITTED，主控独立接受GEO后释放目录写权。每次一个小片commit，不顺带STATE或其他设施。

## ART-F02-STATE · 局部动作与预览包装片

目标：idle/work/disabled/maintenance四表现，标签offline只映射disabled，输入offline拒绝；move/charge/towed有依据N/A。压头/进料挡板2秒循环、停机大挡板与维修开盖可分；私有摆件不随work增减。

前置：本资产GEO接受commit且目录释放，M01/PREVIEW已接受。包装实现不消费U01动作或最终manifest；完整U01及三件关系图是最终视觉比较门槛，齐套后另验，不阻塞独立包装制作。保留GEO成果。修改同资产可编辑源并重新导出动画/分件GLB；交私有`preview.tscn`/`preview.gd`、最终canonical manifest、源目录state-report.md与新run证据。使用`apply_preview`和材质surface表，不调用旧I00旋转桥；GLB实录hash必须同步，资源/动画名称填真实值。源、wrapper与GLB同状态可复现，不只在wrapper藏一套不同模型。

正常：需求的每态/阶段/载荷按固定time拍，对照正常镜头与近景；state逐项static/animated/na声明。切到disabled/maintenance再回idle/work及idle→N/A→idle，phase始终completed、cargo始终empty；同输入重复两次不叠加、根固定。异常：未知第八态offline、非法phase/phase_t/cargo/reason、缺动画节点或丢surface/socket，先拒绝且原合法对象不变；从N/A回idle无残留。不吞异常、不以文件存在代替运行。

验收：主控先导入与包装接口检查，再用[视觉矩阵](../requirements/visual-acceptance-r1.md)独立看真实PNG/短视频，必要帧时刻/方向不可省；GLM图形未跑记NOT_RUN，不伪造图像来源。最终记录技术/主控可读性/所有者视觉三个结果，后者未看不写通过。真实游戏接入、性能预算与模拟事实均在本票之外。

### STATE源动画与包装分片（实际依赖）

经architect复核，设施动作不消费U01动画/manifest/箱体源字段。各自GEO独立接受且目录释放、源node/pivot固定后即可派STATE-A（源动画GLB+state-source-report），沿用已验车体/货台尺度及冻结状态；不因U七态未实现而阻塞此片。STATE-B才交wrapper/最终manifest、完整复位/材质/包络与正常镜头取证，保留上文最终比较条件。

STATE-A只写该资产源/GLB目录，不写最终manifest或公共工具；动画导入及指定时刻姿态实测，不以extras假声明loop。A交SUBMITTED停止，主控实际核并释放目录再派B。F01 phase_t与time_s独立、固定建设阶段不由动画自动提交；F02固定两箱/压头挡板/维护盖，offline仍映射disabled。源动画局部通过不代签最终包装、同场视觉或所有者效果。
