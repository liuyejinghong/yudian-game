# 火星高保真 Blender 流程 r2

状态：露头与伴生地表已通过内部最终视觉验收；正式源已收口，公开入口从零重建至完整烘焙输入并与正式样板精确一致；6.1 Sol独立小样试跑及root原生复验已通过。这里记录已实际执行的方法；完成记录以PLAN与绑定文件SHA的证据为准。

## 环境与参考

使用原生Blender 5.2.2 LTS。项目workspace `tools-bin/Blender.app` 已指向5.2.2，4.5.14独立保留。固定Scenario技能提交 `91caa011e13774a220aba7136309b49964104040`，MIT，项目 `.agents/skills/scenario-blender-*` 保存expert/sculpting/retopology/uv-baking/texturing-shading；许可证与文件身份在SCENARIO-SOURCE.json。实际通过bpy调用工具，不依赖MCP、付费服务或额外Python安装。

先读 [火星制作依据](../mars-research-r1/MARS-BRIEF.md) 与 [照片处理说明](../mars-research-r1/VISUAL-SOURCES.md)。Gale/Stimson用于风积砂岩形体，natural色彩参考另列，不能把白平衡照片或Jezero的地质直接混成同一地点。NASA照片只作参考，不作运行纹理。

## 制作与门槛

1. **形体先行。** 建一块连续母岩，先同光灰模正、背、顶和局部近景。检查高肩、侵蚀湾、低舌、少量错位节理，以及不等厚断板的真实体积。悬边、遮挡、破口必须在几何中；不以面数、normal或4K替代。每阶段保留源与图，失败只修对应部分，两次失败停该方法并独立重计划。
2. **冻结通过高模，再做交付网格。** 保留 `SOURCE_HIGH_EDITABLE`，静态减面后固定三角化，交付集合 `GAME_EXPORT`。比较同机位高模、裸低模、映射低模，检查轮廓与遮挡。微小退化边的清理须记录实际容差和删面数，不改通过的高模。
3. **UV先定位，再修。** 检查翻转、零面积、图岛间重叠、图岛自身折叠、边距与密度。solver的综合最佳解不保证无折叠；保留面索引，只有问题图岛重展开，必要时沿既有层边/节理切支路。实际位置和高一档分析分辨率用于区别真实重叠与栅格误报；不能用很小的全图百分比掩盖不同表面共用UV。
4. **512真投射小样。** 高低模selected-to-active逐对烘焙，不把照片或任意噪声当高模法线。投射工具的miss、wrong_part与normal flat/inverted警告按实际位置解释，再看三张同光图。没有miss不代表材质或UV通过。岛距与外扩按最终分辨率实测；地表正式4K最小岛距40px、外扩8px。
5. **材质独立验收。** 旧裸岩、新断面、尘覆有不同覆盖与粗糙度；只在暴露层面保留不等厚、变向、局部终止的细层理。禁止满周等距条纹或全表同密度砂纸。base_color与roughness烘焙取最终Principled输入，不能绕开新增材质节点。毫米级shader relief明确是几何投射之外的补充；实际贴图密度决定可读范围。
6. **正式贴图与源。** 保留真实高模后烘焙base_color/tangent normal/roughness，无烘焙方向光或AO。当前地表采用全分辨率4K、RGB 8bit PNG：base_color=sRGB，其余Non-Color。16bit实验包体积过大，保留本地后改变通道精度，不减几何或分辨率。贴图同时打包，源中路径 `//textures/`；另存用 `relative_remap=False`。
7. **原生重开与GLB回读。** 从空场景导入GLB，核对真实三角面、米制范围、UV、有限值、PBR连接与内嵌图像；源必须保留编辑高模。主控另跑只输出的校验，绑定当前源和GLB SHA，不复用旧候选结果。
8. **实际Godot与独立视觉。** 使用现役尺度锚和r1固定光照，关闭自动LOD；normal/near/reverse/horizon/normal-low/detail/ground七张实际PNG。normal-low只变采样和画质，不能换几何、光照或机位。验证Godot提取贴图与GLB内嵌字节一致，地表接缝读取两个实际GLB。独立6.1 Sol high实看照片、灰模、高低模和运行成品后判PASS/REWORK；技术PASS不替代视觉。

原生集成曾出现亮方垫：色彩空间与Lambert/Burley差异先修，剩余直线通过无光照图定位为非无缝贴图重复边界。当前支撑直接复用patch同一Sand PBR，匹配600边点/UV/法线，外围原生12m镜像UV折返；几何没有重面。实际四固定视点与无光照图由独立Sol局部通过，核心patch未改。此看样外围重复同一贴图，大范围真实地形还需更多变体。默认r1原shader已恢复原文件。

最终岩面修正沿两个实际裸露面的尘覆、侵蚀与断裂关系分布2–8cm连续浅起伏，并加入相应低对比固有色差；fresh比old弱且细，dust安静。仅在人工小窗添加细节会形成贴章；连续性先用2048真实投射在固定游戏视点通过，再保持形态/UV/材质烤4096。最终19内部验收见[visual-final19](evidence/visual-final19.md)，不等于Main或所有者最终审美接受。

局部分网可能产生UV重合的重复顶点。17只合并已实证的875条短边（最长5.96e-8m），保留所有原顶点；修后零负UV/零面积，未采用失败减面。法线保留按真正范围外及相邻fan分类，先核编码前目标/坐标/硬边，再用相同候选几何/拓扑/初始硬边独立编码对照。零位移会改变编码基底，不能把其误差直接当范围外回归；旧失败记录保留，门槛没有放宽。

公开GLB的extras也必须检查。Scenario工具的bx_texture_set会记录本机绝对贴图路径；现由P.export递归转换为repo相对路径。metadata-only收口保留原完整视觉BIN与非extras结构，源集合、显隐、modifier、网格、UV、材质图和打包图像必须保持。另存会清理无引用材质，孤立评审clay只加fake-user保留标记；不以改宽可见数据门槛掩盖变化。两个metadata-portability.json及root复验绑定当前文件身份。

## 可执行入口

完整重建命令见[露头公开入口](../../../../art/source/environment/mars-outcrop-hifi-r2/README.md)与[伴生地表入口](../../../../art/source/environment/mars-ground-patch-hifi-r2/README.md)。露头公开顺序为build.py的新09 → finalize.py的新10/UV/512真实投射 → finalize.py --finish；finish必须显式独立--output-dir，并拒绝正式目录。新链原生复跑实际做到--prepare-only，几何、索引、UV、角法线、world变换、颜色属性和高模材质输入与正式19精确一致，见[rebuild-preparation.json](../../../../art/source/environment/mars-outcrop-hifi-r2/verification/rebuild-preparation.json)。完整公开4K bake/export没有再重跑；当前正式4K已由原19实际烘焙、源重开、GLB导入和Godot/Sol验证。

```sh
# 从仓库根目录执行；TASK_BLENDER为项目安装的5.2.2原生可执行文件。
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/verify_final.py -- --asset outcrop --stdout-only
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/verify_final.py -- --asset ground --stdout-only
"$TASK_BLENDER" --background --factory-startup --threads 4 --python-exit-code 1 --python art/environment/mars-lookdev-r1/check_join.py
```

最终两件GLB齐备后，`art/environment/mars-lookdev-r1/open-hifi.command` 打开独立看样；自动捕获需设置 `YUDIAN_MARS_HIFI=1` 与不存在的新输出目录 `YUDIAN_MARS_OUTPUT`，再运行 `verify.py` 检查。检查输入用 `YUDIAN_MARS_HIFI=1 Godot --headless --path art/environment/mars-lookdev-r1 --script res://check_input.gd`。

## 交给6.1 Sol的边界

最终内部视觉通过后，接收方读取当前流程、两件冻结样板和证据，在独立目录实际跑一个小样或复用校验，原资产只读。主控检查其真实输出后才记录“流程可复用”；发送消息不等于试跑完成。此次不批量重制所有素材，所有者最终审美、Main集成与整场性能仍单独记载。

材质小样的冻结UV可能很碎，512不能证明细小图岛覆盖。未烤背景若要中性linear底色，手工sRGB image.pixels填充必须先转sRGB，并用PNG保存重开证明；零纯黑像素不代表底色正确。stage13意图linear(.22,.195,.171)却写PNG[56,49,43]，产生暗小三角，这是编码缺陷，不是美术形体失败。stage14先验证正确填充，仅重烤正式大小base/rough候选，geometry/UV/tangent/4Knormal字节保留；不以增加normal或调光遮掉问题。

实际交接：6.1 Sol已按上述流程完成[独立碎块小样](../../../../art/source/environment/mars-hifi-sol-trial-r2/README.md)，高模15368/交付5378三角面、有效UV、3×512真投射、打包源和GLB空场景导入均PASS。root另进程重开/回读及SHA核验见[sol-trial-root-native.json](evidence/sol-trial-root-native.json)，architect只读复审也实看同光高低模确认暗三角消除。935小岛与512采样上限、roughness导出精度变化如实保留；这是流程移交证明，不是小样4K终验。
