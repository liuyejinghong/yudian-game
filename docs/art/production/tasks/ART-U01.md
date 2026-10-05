# ART-U01 · 驮运首样

**当前状态（2026-10-05，本轮火星优化）：REVIEW。** 本轮三件火星结构优化已实际制作；源/GLB/manifest与独立Godot检查完成，父票均REVIEW，完整视觉/所有者接受仍NOT_RUN。 U01当前7312三角面/109材质面；本轮源由主控完成，GLM两源票无交件已取消。[当前证据](../evidence/art-mars-models-r2-2026-10-05.md)、[执行切片](ART-MARS-R2.md)。

下文GEO/STATE描述原r1/r9派单与验收边界；四轮或旧尺寸按历史读取，新结构以[冻结r2](../requirements/mars-equipment-r2.md)与当前manifest为准。

独占：`art/source/units/tuoyun-r1/`、`prototype/assets/units/tuoyun-r1/`、`art/manifests/tuoyun-r1.json`。公共六材质和预览工具只读；不写其他模型、公共道具/材质、桥/Main/project/fixture/根TODO/PLAN/工程合同，详细禁写见共同约束。GEO与STATE对相同目录**串行独占**，不能给两工人同时写。

## ART-U01-GEO · 静态几何片

目标：前低头/开放后台/四轮/一个独立货箱、四个局部socket。候选无载1.2×1.6×0.9m，源尺寸与轮间隙/货台净空按需求分区表；有载不穿插，移除货箱车身完整。

前置：M01与PREVIEW-A独立接受；GEO不依赖B的GUI/采集，正常镜头比例/可读性留STATE。只按冻结分区/候选尺寸造一个版本，不整包探索设计；有真实造型冲突报主控修局部。输入再包括接受的六资源与预览commit。交可编辑源/生成器、真实GLB、geometry-report.md（含真实节点surface角色表、socket、静态/载荷AABB、源与GLB hash、来源、面数/纹理、简化说明）。GEO不需preview wrapper或最终manifest，动画留STATE，报告标尚未制作，不伪装N/A。几何报告供主控用Godot导入检查实际节点/包络和分面，必要的临时检查在工作树外进行，不要求工人改公共工具；GEO接受范围只含静态结构与接缝，正常镜头视觉在STATE最终包装后验。

正常案例：原点落Y0、米制-Y误写不得出现、前方-Z、identity scale；轮廓与关键结构按需求制作（0/90/180°真实图形检查留STATE）；货箱、四轮、前顶盖、锁扣和充电盖各有独立节点，surface表完整。异常案例：缺socket、错误轴向/缩放、空载漏拆货箱或几何穿插、缺surface角色时诊断失败，不篡改公共断言来通过。主控针对真实导入证据查AABB/socket/角色，并确认没有假能力/规则；候选比例或结构不合格局部REWORK。

验收交付：本票源目录geometry-report中写真实导入方式、命令、检查结果与NOT_RUN；GLM仅交SUBMITTED，主控独立接受GEO后释放目录写权。每次一个小片commit，不顺带STATE或其他设施。

## ART-U01-STATE · 局部动作与预览包装片

目标：七态全提供；四轮move、锁扣work、侧盖charge、顶盖disabled/maintenance、牵引示意towed。每次完全复位再seek，两个故障原因只读注记，空/有载是独立输入。

前置：本资产GEO接受commit且目录释放，PREVIEW-B正式独立接受；保留其成果。修改同资产可编辑源并重新导出动画/分件GLB；交私有`preview.tscn`/`preview.gd`、最终canonical manifest、源目录state-report.md与新run证据。使用`apply_preview`和材质surface表，不调用旧I00旋转桥；GLB实录hash必须同步，资源/动画名称填真实值。源、wrapper与GLB同状态可复现，不只在wrapper藏一套不同模型。

正常：需求的每态/阶段/载荷按固定time拍，对照正常镜头与近景；state逐项static/animated/na声明。切到disabled/maintenance再回idle/move，cargo empty/loaded往返、phase始终completed；同输入重复两次不叠加、根固定。异常：未知第八态offline、非法phase/phase_t/cargo/reason、缺动画节点或丢surface/socket，先拒绝且原合法对象不变。不吞异常、不以文件存在代替运行。

验收：主控先导入与包装接口检查，再用[视觉矩阵](../requirements/visual-acceptance-r1.md)独立看真实PNG/短视频，必要帧时刻/方向不可省；GLM图形未跑记NOT_RUN，不伪造图像来源。最终记录技术/主控可读性/所有者视觉三个结果，后者未看不写通过。真实游戏接入、性能预算与模拟事实均在本票之外。

### STATE实际缩片（2026-10-04）

原turn两次缺显式编辑器退出的导入命令停在no main scene，没有STATE交付；主控取消并确认专属PID退出。architect核A/B无必须同时完成的接口依赖，范围不变：

| 实际小片 | 目标与交付 | 前置/状态 |
|---|---|---|
| STATE-A | 现生成器烘局部clips/牵引杆几何、固定支耳/冻结货箱外沿/socket方向，源与GLB真实导入核clip/时长/loop/指定姿态，state-source-report | ACCEPTED 2b2abd6+主控r6，仅原源/资产目录；不交wrapper/manifest |
| STATE-B | 保留A接受成果；包装/最终manifest、完整复位/非法保持/逐面M01及正常镜头证据，state-report | 旧版技术核验通过；当时父票设计REWORK，主控完成旧版未交部分，见首样记录 |

新导入必须外部墙钟timeout：单GLB新空工程显式 --headless --editor --import --quit，再独立 --script 检查；两步骤读日志/真实网格，不因exit0代签。历史A交件时七态/视觉未验；当前技术与画面结果分别见首样记录，所有者仍NOT_RUN。

新近景真实疑点：轮胎侧环缺面，端面/内孔朝向须在STATE-A交付后局部复核修正；现有索引/自带法线一致检查不证明外向。完整视觉仍待检，主控不争写正在执行的源目录。

轮胎端面/内圈物理朝向已主控局部修正并实际close确认缺面消除；move源命名-loop实际导入loop_mode=1。源r6已接受，当前完整接口已由主控核验；所有者与完整可读性接受仍待看样。
