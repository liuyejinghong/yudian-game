# Mars ground patch · hifi r2

12 × 12 m 的原创伴生积沙、薄片碎屑与半埋露岩模块。源文件和 GLB 已在 Blender 5.2.2 LTS 原生重开/独立导入验证；与 stage19 主露头的固定 Godot 近景和组合已通过[独立内部核心样件验收](../../../../docs/art/production/mars-hifi-r2/evidence/visual-final19.md)。地表模型保持该验收版本，所有者最终视觉、Main 接入和整场性能仍为 `NOT_RUN`。

## 交付与放置

- `mars-ground-patch-hifi-r2.blend`：压缩源文件；`SOURCE_HIGH_EDITABLE` 保存高模，`GAME_EXPORT` 为交付对象，`GAME_PROTOTYPES` 是隐藏烘焙样本。
- `mars-ground-patch-hifi-r2.glb`：315 个节点、374,280 个实例累计三角面，复用碎片网格；2 个非金属 PBR 材质，6 张内嵌贴图。
- `textures/`：sand、clast 各一组 base_color / tangent normal / roughness；全部 4096²、RGB PNG、8 bit/channel。base_color 为 sRGB，其余 Non-Color；无烘焙 AO 或方向光。
- 米制，Blender Z 向上，XY 边界严格 ±6 m；GLB 自动转 Y 向上。在 Godot 世界 `(6, 0, 0)` 放置，与露头共用原点。支撑地面需要真实 12 m 方孔，不叠面掩盖接缝。

最终文件大小、SHA256、范围和原生检查见 `manifest.json` 与 `verification/final-native.json`。当前源文件约 56.2 MB、GLB 50.0 MB；两个都低于 100 MB。16 位实验包仅在本地阶段备份中保留。

## 真实制作方法

`build.py` 制作独立地形高模、12 种碎片原型、310 次确定性放置和 4 块半埋露岩。碎片包含斜楔、部分缺角和薄缘，坡向旋转先对齐地面法线，再在局部绕 Z 旋转；支撑点由地形与顶点的最大差值决定，并断言顶部露出、底部埋入。

交付地形网格步长 0.08 m，碎片和露岩由独立高模静态减面并在烘焙前固定三角化。17 对高低模逐对 selected-to-active 烘焙，避免同场邻体交叉投射；碎片的 16 个源 UV 分占 4×4 atlas，散布实例有意复用对应源 UV。材质的尘覆/断面字段来自几何法线、位置和独立变化场，细粒 shader 仅补充高模几何。

执行中使用 Scenario Blender toolkit 的投射测量、UV 检查、低模准备、材质组装和材质审计；依赖项目 `.agents/skills/scenario-blender-*` 中固定的 MIT 工具版本 `91caa011e13774a220aba7136309b49964104040`。NASA 图片只作外观参考，没有作为纹理输入；制作方式为程序几何与材质，不称手工雕刻。

## 验证与限制

先做了 512 几何法线小样，并实际查看同光高模 / 裸低模 / 映射低模近景。有效 UV 小样无翻转、无零面积，最终 atlas 最小岛距实测 40 px / 4K，正式烘焙外扩 8 px。地形约 341 texel/m；源碎片/露岩约 362–10,940 texel/m，实例缩放会改变该密度。较大露岩的纹理密度最低，不把 4K 分辨率等同于任意距离的近摄能力。

法线诊断保留实际警告：最终反向像素最高 0.56%，问题定位主要在薄尖和底缘，部分处于可见边缘；当前近景没有连续错误斑块。测量工具的 wrong_part、flat 提示没有改写成零，须结合位置图和同光对照解释。正式像素统计见 `manifest.json` 的 `final_normal`。

四边根据真实支撑网格的 2 m 直线边段校准；原解析函数一致并不能证明实际网格无缝。`verify_boundary.py` 读取支撑 `.blend`，再比对高模、试投射低模、最终源文件和独立导入 GLB。最终 600 个边界顶点最大间隙约 `4.56e-9 m`，容差 `1e-5 m`。这是几何位置验证，组合光照和材质过渡仍由场景近景检查。

## 复现

从仓库根目录运行，`TASK_BLENDER` 指向原生 Blender 5.2.2。每次覆盖前保留当前交付和阶段证据；顺序运行，不同时启动多个 Blender。

```sh
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-ground-patch-hifi-r2/build.py
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-ground-patch-hifi-r2/finalize.py
```

检查 `stages/05-bake-test/` 的 UV/投射 JSON、三张同光图以及异常定位。通过后才运行正式烘焙与原生校验：

```sh
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-ground-patch-hifi-r2/finalize.py -- --finish
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/verify_final.py --python art/source/environment/mars-ground-patch-hifi-r2/verify_boundary.py -- --asset ground
```

共享渲染/网格工具来自伴生 `mars-outcrop-hifi-r2/build.py`，PBR/烘焙工具来自同目录 `pipeline.py`。露头的 `build.py` 默认入口现已调用通过视觉检查的09连续母岩配方；地表只导入其公共工具。原生另存最终源文件使用 `relative_remap=False`，保留已指定的 `//textures/` 相对路径，贴图同时打包。
