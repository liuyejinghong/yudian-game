# 岩面最终质感两次失败后的局部重计划

architect与Astra分别只读代码、GLB和实际图。当前两处根因不同：GLB射线命中detail右面约3.67m、3.4–3.6mm/屏幕像素、N·L=+.759；reverse后墙约9.05m、8.6–9.6mm/像素、N·L=−.557。64²局部normal窗按约286.7texel/m估算，87–89%变化能量在2cm以下，2–8cm仅9–11%；右面局部角RMS .58°、后面2.71°，2×/4×过滤进一步削弱。频率换算基于面积平均密度，是近似而非每三角测量。

源码ExposedSandstoneGrains Scale62、FineMineralGrains190、Bump Distance .0022m，加尘覆/暴露遮罩。右大坡、后墙缺少2–8cm真实浅剥落层，stage14仅增加约27cm色云，无法替代形态。COLOR ambient在背光面为constant×albedo，无normal方向响应；root实际只改SKY的固定机位诊断在sky-diagnostic/，仍未恢复足够质感，因此不改正式光照来代替资产修复。

stage15仅在右断面/后墙各一小块有明确暴露边界的2–8cm浅侵蚀/断裂台阶，HIGH和GAME均有真实表面形态。宏观轮廓、薄层、节理、接地与已通过地表保持；只开放必要局部几何/法线更新。优先保UV，尘覆平静、新断面克制，撤掉整面cloud颜色修补，不烘方向光或AO。

先实际两灰模关键机位与高/低对照，再当前碎UV布局的4K真投射候选；fixed detail约6–23px、reverse约2–9px可检验该尺度。候选通过后另做原生源/GLB和独立Sol复核；正式源未覆盖，流程交接仍未开始。
