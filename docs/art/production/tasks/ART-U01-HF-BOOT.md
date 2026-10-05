# ART-U01-HF-BOOT · Blender精细样板第一片

状态：REVIEW，主控此前已交Blender4.5.14独立静态几何试片revision2；重开/真实GLB/Godot16504tri/28mesh/51surface/4socket及16张旧新同条件PNG已核。近景可见盖板分件，正常镜头收益有限；完整高保真未接受。目标：保留现役U01结构/比例/动作依据，建立独立.blend→GLB→Godot链路，先试外壳倒角/分件层次和轮组局部细节。不是完成整车高保真/UV/烘焙/LOD的承诺。

输入：67c8bfb现役tuoyun-r1源/GLB/manifest、Mars-r2轮位/六轮金属轮/摇臂/封闭内电气模块/货箱。Blender4.5.14 AppleSilicon官方LTS，下载/官方SHA/实际版本记录在私有证据，工具在项目tools-bin，不进Git、不改全局PATH或系统安全设置。

独占：art/source/units/tuoyun-hf-r1/、prototype/assets/units/tuoyun-hf-r1/、docs/art/production/evidence/dual-r1/。实验geometry-facts放源目录；不写现役tuoyun-r1、公共M01/预览/工程合同、Main/fixture/根PLAN/TODO。

交付第一片：可重复Blender Python构建脚本、实验.blend、真实GLB、几何/工具报告与同条件Godot正常/近景旧新对照。先导入现役模型，局部改硬表面几何；保存原点/单位/方向、载荷区/活动pivot，不以平滑全部边缘掩盖机械分件。实验源允许重新组织网格，但接口路径与源轨保留情况需真实列出。源.blend重开再导出的GLB用规范化几何比较，不强求.blend二进制hash重复一致（软件文件元数据可能变化）。

正常：.blend可重开、无缺资源，GLB米制/+Y/-Z，六轮/货物区可辨，普通白昼无需特效就能看到倒角和零件层次；旧新相同相机/光/角色/姿态。异常：源缺失/导入空网格/坏轴向/无可导出对象/NaN坐标具体拒绝，不导出假PASS；至少一次负例。

验收：主控实际查看源/GLB/Godot画面和几何导出，architect只读复核事实。只接受“工具与几何试片”，完整七态/载荷复位/装配扫掠、新材质UV/烘焙/LOD/游戏集成/整场性能/所有者高保真接受按真实进展记NOT_RUN。A2待实际效果冻结。

后续版本：HF实验源已推进revision3，当前形体与未验范围见[HF-SHAPE](ART-U01-HF-SHAPE.md)和[dual-r2](../evidence/dual-r2/README.md)。本票16504tri等计数为工具切片当时真实r2事实，Git6a0028e及dual-r1原图保留，不当作当前模型计数。
