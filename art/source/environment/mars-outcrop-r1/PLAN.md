# 单件露头执行/修正记录

授权范围：按已冻结 ASTRA-OUTCROP 制作单完成一件原创资产；仅本目录。

1. 已完成：实际查看 PIA21044、PIA21042、PIA16800，读冻结资料。
2. 已完成但否决：第一轮几何、烘焙、正背渲染；UV tile错误与柱墙形体不满足视觉要求。保留 failed-r1 源和图片，不称作通过。
3. 已完成：宽基床、三块削斜退台上层、局部窄斜节理、断面纹理遮罩；实际看过重烘焙后的正背/近景，并修正了托盘状基床和顶部薄鳍。
4. 已完成UV重计划：第一次 native pack 选错tile，第二次 Blender pack_islands 在 convex-hull packing 中触发 allocator SIGTRAP（本机诊断）。停用该算子，经 architect 独立审查，改为生成阶段已知面岛的纯Python矩形排版；174岛、约177 px/m、岛间19.4 px、单侧bake扩边8 px。已检查ID保留、UV面积、范围、不重叠与边距。
5. 已完成源/导出检查：保存重开.blend、3张原始纹理pack/相对路径、20个封闭连通块、GLB结构/轴/AABB/法线/纹理，以及独立GLB导入后的尺寸/材质连接。当时的阶段检查报告绑定候选GLB SHA256 `031a6e12551cdfed95b528539d6717e3e30b0f89eb10a9db774a61fe6e75d893`。
6. 两次逐字节重导出断言失败后，已查明唯一差异是Blender内置exporter四位切线舍入，交architect独立复核且获主控确认。JSON及除切线外的所有buffer bytes严格一致；只给TANGENT.xyz ≤0.00011容差，w完全一致，并核验有限值、长度、正交性。本次实际2个分量变化，max=0.00010001659393310547，`exact_binary=false`；没有宣称整文件字节相同。失败/进行中会写false状态，避免留下旧版成功报告。
7. 技术检查完成不代表视觉通过。root与收尾architect对照NASA和Godot后否决031a6候选：前面厚床边太圆、像熔化阶梯；背面大片平滑壁缺少薄层破口。当前源/hash/图已备份至 `iterations/rejected-031a6/`；原构图、研究与场景继续保留。
8. 局部返修计划（architect已独立审查）：去掉Gaussian凹槽和宽sin起伏，改分段线性退台与角形缺口；仅在A/B前侧、C背面局部加入不等厚尖薄层唇，两个斜向楔状破口，禁止全周规则环。采样必须经过层唇上下棱/折点，指定边加局部横向采样；法线分界配合真实几何，不靠法线假造。仍是20块、同一尺度与UV/烘焙流程，不增加系统。
9. 返修验证：先出灰模正背和指定细节检查实际轮廓，再烘焙PBR、检查非退化UV与封闭网格/预算，完整重开与独立导入报告绑定新hash，最后交root同光复验。未修改其他目录，未commit/push/merge。重复大备份与本机日志保留但忽略提交。
10. 新候选已完成工人验证：47,998三角、20封闭连通块、174个UV岛、3×2048原始PBR；实际查看灰模正/背/细节和新烘焙正/背/双向细节。GLB `141380133cb362aae0dcf52deb3ac1e62f33c31851d7437f92240b44da05cacf` 已保存重开及独立导入PASS。工人本轮切线1个xyz分量变化，max=0.00010001659393310547；其他JSON和buffer bytes严格相同，`exact_binary=false`。报告已绑定此新hash，不使用031a6的成功记录。
11. 新候选冻结交root进行无自动LOD的Godot五帧及原P1独立复核；工人不修改已保存.blend/GLB/PBR，所有者视觉接受仍待定。普通Python检查独立写glb-check-report.json，完整Blender报告保留在check-report.json。

12. root已独立重开/再导出/GLB导入，最新核验47,998tri/20封闭块/3张PBR通过；只有1个切线xyz分量舍入差0.00010001659393310547，未报整文件byte相同。root无缓存Godot5帧、LOD0固定网格及输入复验通过，architect复核原P1已解除到首件看样候选。所有者最终视觉仍NOT_RUN；完整记录在本repo docs/art/production/mars-research-r1。
