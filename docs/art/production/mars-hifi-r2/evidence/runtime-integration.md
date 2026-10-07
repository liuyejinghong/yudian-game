# 原生Godot组合与当前身份

当前正式组合七帧在native-stage19/：Godot4.7.2、Metal4.0、AppleM3Pro，1600×1000。露头GLB ae5722b1…/视觉候选source7e50eb0d…；正式源在保持完整视觉输入后仅修相对渲染输出路径，现source94ff01a0…，地表GLB1b841614…/sourcefa48f2e4…，支撑GLB24936224…。完整身份在capture.json及源验证。stage19与晋升后正式GLB/三张PNG逐字节一致，root已运行默认正式source-copy校验PASS，未改旧截图hash。

独立6.1 Sol high最终内部视觉PASS（visual-final19.md）：两实际裸露面连续浅起伏与低对比固有色解除贴章和纯平板感，薄层、碎屑接地、ground边界保持。source背面仍比固定游戏图清楚，当前不构成阻断。公开可复跑入口从零至完整烘焙输入精确复现PASS（未再跑完整4K bake/export），6.1实际小样试跑与root原生复验均PASS；Main、所有者最终审美及整场性能NOT_RUN。

地表外围直接复用patch同一Sand PBR，原生12m镜像UV修复非无缝纹理重复边界；24个2m边段各细分25段，匹配600边点及法线。ground-final-join.json绑定未变化的两GLB：最大点距1.91e-6m、面边分离2.62e-8m、UV差1.79e-7、normal dot最小.99999976。独立局部验收在visual-final-ground.md，此成果在最终19保持。

camera-diagnostic/是旧12的相机/正确Sun轴对照，GPU normal4096RGB8、normal_scale1，2048抽样点与源差0；它证明当时图像未丢失，不替代当前源身份。sky-diagnostic/与sky-stage16/只改ambient来源，均未足够修复，因此正式Sun/ambient/镜头/曝光保持r1条件。岩面最终修的是完整裸露面分布及固有色，不是灯光。

native-first至native-normal-join、native/、native-stage14/、native-stage16/、native-stage18/均为历史定位/预验收，按各自capture输入身份解释。早期亮方垫由色彩空间、Burley差异和贴图重复边界分别定位；近似hifi shader已替换为同Sand PBR，默认r1 ground.gdshader恢复原文件。旧12源重开/metadata-only证据保留历史，当前正式原生验证随最终包收口更新。
