# 伴生积沙、露岩与碎屑模块 · 高保真 r2

2026-10-07。与露头同原点；米制 Blender XY 范围严格 [-6,6] × [-6,6]。GLB 转 +Y 向上，由 root 放在世界 (6,0,0)。root 将对应 r1 支撑地表开真实孔，模块不用 alpha 方贴片或重面遮盖。

1. [x] 已看母岩、积沙和碎屑参考；采用与露头同一地质形体和低饱和灰褐材料逻辑。
2. [x] 四边每个顶点落在 r1 实际支撑网格 2m 边线段上，使用原解析高度的网格角点插值；中部自然 cm–dm 起伏逐渐消失于边缘。已原生读取支撑 .blend 校验，高低模最大边缝 4.56e-9m。导出地表顶面开放边界为明确设计。
3. [ ] 岩脚较高、凹处积沙、局部露岩被埋入地面；碎屑包含薄片、棱角厚块、小砾石，大小/朝向/密度随坡脚与搬运距离变化。避免均匀撒满、平片瓷砖、浮空碎石。
4. [ ] 高模与交付分集合，岩块真实几何对应烘焙；沙的物理高度起伏保留，细粒仅需正常图/粗糙度。与露头同光灰模/PBR 三向检查，阶段备份。
5. [ ] 压缩 .blend、GLB、原创新纹理、构建脚本/流程、manifest/hash/边界与重开导入检查。root 集成 Godot 五帧另验；所有者最终视觉、Main 和整场性能 NOT_RUN。

已切换用户授权的新稳定 Blender 5.2.2 LTS，未启动 4.5 建模或烘焙。主露头目录有实际 toolkit 兼容检查 JSON。

## 阶段记录

- 第二个有效 512 probe 恢复并核对 prepared.blend SHA：atlas 实测岛距40px/4K、0翻转和0零面积，正式烘焙 margin=8px。尝试放大至 .04 的 pack 产生 Subcrop02 UV 塌缩，已保存失败备份并回到 .023；不继续盲增 padding。已实际打开 high/bare/mapped 三张同光近景，以及 Clast06/Subcrop01/Subcrop03 两视角异常定位；反向像素0–0.584%位于薄尖和底缘，未见当前近景连续错误斑块。粗平面的 flat 警告保留在 JSON，以几何/投射/实图解释，不修改工具数值。原生 4K 正式烘焙已开始，最终像素与接缝仍须复核。

- 独立 6.1 Sol high / root 实看04基础 PASS，可进入高低模检查，最终材质/接地/GLB另验。root 用实际支撑网格发现边界最大缝9.663mm；原解析height一致不是实际2m边线一致。已将基准改为原2m四角双线性值，使四边严格落在真实直线段上；另明确开放地形面法线为+Z。05-boundary-calibrated 原生完成，保持碎片结构。
- 512首轮真实selected-to-active已完成17对与同光高/裸低/带normal三图，mapped-low恢复了裸低厚块面变化。诊断发现atlas缩放后岛距不足，已按最终缩放增大padding并缩小bake margin；不忽略边缘反向像素。平断面/低起伏沙面会有flat提示，须与投射miss、坏像素位置及图像一起判断。正式4K仍待修后复查。

- stage03 改为薄板/楔形，独立检查保留分布基础，要求大薄片破角/残层与低机位接地检查。组合 low 看见厚黑底缝，定位坡面倾角先旋转后yaw导致不再对齐世界坡向；采用 z→地面法线的旋转再乘局部yaw，减小额外倾斜并适度加深埋入。
- stage04 原生退出0，实际看 front/detail，薄片黑缝明显收敛；部分大碎片真实凹角及上层退缩进入轮廓。高模1,822,392 tris，315个可见对象，外围函数误差6.12e-9m。已交 root 预看，未执行正式烘焙。

- stage01 原生退出 0；边界误差 6.12e-9 m。但实际打开 front/detail，仅 4 个 subcrop，310 碎屑被埋没：放置函数误用 min(g-z) 将最高点压到表面。作者/root 独立检查定位一致。旧源/图/脚本快照保留。
- stage02 根因修正为 max(g-z) 定位最低支撑点并减允许埋入量；增加每个碎屑顶部露出与底部埋入断言，按同机位重渲染。
- When placing an irregular fragment on terrain, use the maximum terrain-minus-vertex gap for the support offset, then subtract burial allowance; assert both exposed top and buried contact before rendering.
