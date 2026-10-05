# U01 Blender形体候选 · revision3

主控沿用6a0028e静态试片制作成形盖板/双侧货台壳/六轮外毂盖，原六轮/摇臂/货物工作区/四socket不变；现役tuoyun-r1只读。不是整车高保真成品。

- Blender4.5.14 .blend实际重开、真实GLB；Godot19504tri/46mesh/4socket，源与运行副本hash一致。revision2为16504tri/28mesh，三角增加不等于性能通过。
- 形成护盖+暖肩条随HoodLid；侧壳贴外护栏，货物横向净空≥.13m、侧壳底Y≥.479高于轮顶.41；毂盖最外|X|≤.65。具体几何与真实导入见facts及下方证据。
- 四socket实际位置/朝向/上向与现役manifest≤1e-6，候选整体宽≤1.30m、原点/落地不变。
- 重开导出结构/索引相同、浮点最大差1.1920929e-7≤1e-6，GLB字节不同。背景构建时间不是作者制作耗时。
- 16张相同白昼/材质/normal/close/yaw0/90空有载旧新Godot图：近景成形边/壳/毂层次更明显，normal尺度下改善有限，不以新细节代签高保真目标。

当前证据与可运行核验：[dual-r2](../../../../docs/art/production/evidence/dual-r2/README.md)、verify_hf.py；原revision2及首版历史图在dual-r1和Git6a0028e保留，不覆盖原PNG。

NOT_RUN：完整动作转换/七态/扫掠、UV/私有材质生产/烘焙/LOD、整场性能、正式游戏接入、所有者高保真接受。下一片以外壳/金属/防护角色差别做私有材质与UV试板，再决定必要烘焙；不把模板倒角当作完整质量方法论。
