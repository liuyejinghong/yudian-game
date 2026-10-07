# Stimson 风积砂岩露头 · r1

一件原创自然环境试片：宽低基床承接三块被斜向节理切开的退台母岩，两块脱落岩板和十四片不等大的角状碎片。形体参考实际查看的 PIA21044 / PIA21042；照片处理未声明，不用其像素作为材质色值，也没有照片纹理。无矿种、资源、碰撞或通行语义。

交付是 `mars-outcrop-r1.blend`、`mars-outcrop-r1.glb`、`textures/`、生成脚本 `build.py`、检查脚本 `check.py` 和 `manifest.json`。源文件只含一个可编辑网格，二十个命名顶点组保留各岩块；原创程序材质也保存在源文件，运行材质使用已烘焙的 PBR。三张 2048 PNG 均在 blend 中打包、使用相对路径，并内嵌到 GLB。

- 47,998 个三角面；Blender 外包络约 **6.149 × 4.142 × 2.332 m**。
- 根原点在 XY 中心、Z 地面；最低点 -0.14 m，最高点 2.192 m。GLB 为米制、+Y 向上、scale 1。
- 图集含 174 个明确分开的面岛，约 177 px/m；底面缩小以保留可见表面的密度。扩边单侧 8 px，岛间约 19.5 px。
- 材质为非金属粗糙砂岩，使用 basecolor、tangent normal、roughness；粗床组、斜楔破口及局部不等厚薄层悬边有真实几何，细粒与更细的斜交层理使用原创烘焙。

在此目录中运行（Blender 4.5.14，使用已有 Blender 可执行程序）：

```sh
blender --background --factory-startup --python-exit-code 1 --python build.py
blender --background --factory-startup --python-exit-code 1 --python build.py -- --clay
blender --background mars-outcrop-r1.blend --python-exit-code 1 --python build.py -- --reexport
blender --background mars-outcrop-r1.blend --python-exit-code 1 --python check.py
python3 check.py
```

完整构建会保存、重开再导出，然后用 Cycles CPU 渲染正面、背面和近景到 `renders/`。灯、相机和预览地面只在保存后临时加入，不包含在源文件和 GLB 中。Blender 检查验证封闭面、二十个连通块、packed 相对贴图、GLB 独立导入后的轴向/尺寸/三角面/PBR 连接。再导出的 JSON、几何、法线、UV、索引及 PNG 必须逐字节一致；Blender 导出器将切线舍入到四位小数，少数 xyz 分量可能变化约 0.0001，仅此项允许 ≤0.00011，w 符号仍须完全相同，并核验长度/正交性。报告单独记录 `exact_binary`、实际最大差和分量数，不宣称整文件字节一致。单独 Python 调用只检查 GLB，不能代替源重开检查。

这是看样候选，不是 NASA 地点的测量复原；还没有 LOD 或整场性能结论。Godot normal / near / reverse / low、地形接合及所有者视觉接受交主控独立核验。`verification/check-report.json` 记录完整 Blender 检查，普通 Python 另写 `verification/glb-check-report.json`，避免覆盖源重开证据；不以技术通过代替视觉接受。

生成脚本是制作配方，精确交付和再次导出以已保存的 `.blend` 与 manifest 为准。第一轮错误的图集和柱墙形体已否决，证据留在 `iterations/failed-r1/`；随后 native UV packing 触发 Blender allocator 崩溃，改为本资产面岛的直接排版。`iterations/rejected-031a6/` 保留另一版圆润厚床的未接受证据；当前候选改成分段锐转折、局部薄悬边和斜截破口，详见 `PLAN.md`。重复的大源备份、验证 GLB 和本机日志保留在本机并忽略提交。

主控最终独立核验与同场Godot图见 [本轮交付](../../../../docs/art/production/mars-research-r1/README.md)：source/导入/原P1复核已收口到REVIEW候选，所有者最终视觉未接受。
