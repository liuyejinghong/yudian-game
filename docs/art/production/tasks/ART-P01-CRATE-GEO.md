# ART-P01-CRATE-GEO · 共用货箱静态子片

状态：REVIEW，真实Bridge task_94dc7ef663已交561d896，主控集成9aa0b2b；独立Godot48三角面/4surface/3role与包络通过，图形/所有者接受另记。母票ART-P01的回收连接件仍DRAFT。本票仅制作可独立导入货箱，不替换U01已有私有CargoBox。

目标/输入：沿用U01 geometry-facts与生成器geom_cargo的.64宽×.70深×.38高候选包络；米制、+Y上、前-Z、底面中心原点。外沿严格X±.32/Y0… .38/Z±.35。以frame_dark下框、body_light箱体、frame_dark上沿分区；可加小型accent_warm识别片，零件都在包络内；不需要solar_face/soil_mars/rubber凑六角色。基本尺寸源于已制作货箱，非最终量产规格。

独占：art/source/props/crate-r1/、prototype/assets/props/crate-r1/。只读现役U01源/六材质。禁写其他模型、manifest公共表、预览公共工具、根PLAN/TODO/AGENTS、Main、fixtures、project、产研/产品合同；不装依赖、不运行GUI、不派下一票、不push/merge。

交付：独立Python标准库生成器（不得运行时依赖U01的私人输出路径）、可编辑glTF/bin、同源自包含GLB、geometry-facts.json、geometry-report.md；报告列真实节点/surface角色/源与GLB SHA/三角面/AABB/原创来源、重跑方式/耗时/NOT_RUN。GLB副本在私有prototype目录；GLB根名Crate，子部件保持可识别。无动画，无虚构socket；原点本身供未来落位。写前先自查合法索引/有限数/外向绕序与包络，错误非零退出，不靠删检查通过。

正常案例：重跑两次GLB字节一致、从任意cwd运行脚本、落地Y0、从各面无缺面、.64/.70/.38包络、逐surface角色完整。异常：错包络/负尺寸/非有限坐标/未知角色/越界索引应拒绝；至少保留一个可运行的负例断言。

验收：GLM只交SUBMITTED，主控读源/GLB，独立Godot导入实测角色与AABB，并用冻结白昼normal/close取证。源生成通过不等于实机/所有者/正式库存接入通过。母票连接件、最终比例、LOD及游戏接入NOT_RUN。

主控实机返修：原body顶与top_rim顶共面Y.38，近景出现三角形伪影；改body中心Y.23/高度.18（顶.32），总包络与角色不变。已重生成、实际6负例/48tri导入/4图复验；当前源及证据以[双线索引](../evidence/dual-r1/README.md)为准，561d896仍是GLM原始提交。
