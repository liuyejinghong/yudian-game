# Stimson 风积砂岩露头 · hifi r2

当前正式资产为已通过 root 固定 Godot 画面检查与独立 6.1 Sol high 内部核心样件验收的 stage19。可编辑高模 **1,281,204** 三角面，游戏网格 **929,126** 三角面；保留一个连续母岩体、有限斜交层理、错位节理及实际断面的连续浅起伏。NASA 照片只作形体与色彩参考，未进入贴图。所有者最终视觉、Main 接入和整场性能仍为 `NOT_RUN`。

`mars-outcrop-hifi-r2.blend` 将高模与游戏对象分别保存在 `SOURCE_HIGH_EDITABLE`、`GAME_EXPORT` 集合。`mars-outcrop-hifi-r2.glb` 内嵌 base color、tangent normal、roughness，metallic=0。三图均为 4096×4096 RGB PNG、每通道 8bit；源内打包并使用 `//textures/` 相对路径。原生压缩源与 GLB 均小于 100 MiB（104,857,600 bytes）。精确哈希、字节数和检查结果见 `manifest.json`、`verification/final-native.json`。

米制、Blender Z 向上、GLB Y 向上。尺寸 6.145660×3.929038×2.320955 m，局部范围约 `(-3.017900,-2.279540,-0.160003)` 至 `(3.127760,1.649498,2.160952)`。在测试场景中的位置为 `(6,0,0)`、缩放 1；岩脚低于局部 0 面，以埋入伴生地表。地面资产保持既有正式版本。

## 表面与验证

程序体积制作使用 Scenario `bx_sculpt.Clay/Prim`，不是手工笔刷雕刻。09 母岩与原有薄层、节理保持；右实际切面和后壁使用非周期、约 2–8 cm 尺度的连续浅起伏，最大位移约 7.3 mm。裸岩、积尘与新鲜断面的遮罩控制响应，低对比矿物固有色随裸露程度变化；积尘较安静。没有把方向光或 AO 烘进颜色。

原 227,160 面 UV 基线经局部原生分网后保留原面分区和 UV 插值。只清除已实证的重复短边：868 个新增重复顶点、1,750 个零宽三角；原顶点删除 0，最大合并距离 5.960464e−8 m。真正范围外坐标、原角点目标法线和硬边保持；相同坐标、拓扑、初始硬边状态下，独立原生编码对照误差为 0，门槛仍为 0.0003。许可面 fan 的旧量化诊断另存，不冒充范围外改动。

4K UV 分析未检出翻转、零面积、自重叠或岛间重叠；面积平均 286.7 texel/m（约 3.49 mm/texel），岛间至少 38 px、烘焙扩边 8 px。真实高模向游戏网格投射的 3,502 个测量样本漏射与错件均为 0；保留 cage 2.41 mm / ray 5.2 mm 的已测安全范围。最终 normal 无 NaN 或坏长度，材质 audit 通过。细层理与颗粒仍受实际纹理密度限制。

正式 GLB 和 PNG 与已验 stage19 按字节相同。源仅把渲染输出路径改为相对路径；原生保存前后对几何、UV、角法线、颜色属性、材质图及打包 PNG 做了完整指纹比较，结果一致，见 `verification/source-portability.json`。旧12图片与失败阶段均是历史证据，不用新源哈希替换旧图片的出处。

## 从公开脚本重建

在仓库根目录执行，`TASK_BLENDER` 指向 Blender 5.2.2 LTS 可执行文件。依赖为仓库 `.agents/skills/scenario-blender-*` 固定版本；CPU、最多6线程、单个 Blender 进程。下例写到独立目录，不覆盖正式包。

```sh
TASK_BUILD="art/source/environment/mars-outcrop-hifi-r2/stages/rebuild"
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/build.py -- --output-dir "$TASK_BUILD/09"
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/finalize.py -- --work-dir "$TASK_BUILD/10" --high-source "$TASK_BUILD/09/source-high.blend"
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/finalize.py -- --finish --work-dir "$TASK_BUILD/10" --output-dir "$TASK_BUILD/final"
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/verify_final.py -- --asset outcrop --asset-dir "$TASK_BUILD/final" --stdout-only
```

流程依次生成09母岩、原低模及局部 UV 修复、11材质、已验完整面浅起伏、精确重复边清理、同状态保留验证，再真实烘焙和导出4K。`complete_surface.py` 直接接收新生成的高低模，不读取已晋升源，不依赖历史17/18 `.blend` 缓存。固定基线或 UV 选区变化时会停止，不能把当前局部修复套在另一套几何上。

若只需检查重建的烘焙输入，可给第三条命令增加 `--prepare-only`；会输出 `surface-prepared.blend` 和 `prepared-inputs.json`，保留高低模、UV、源材质和保留门禁实测结果。重建结果是独立输出，参数变化后的新包需另行视觉验收。

本次已在全新 `stages/20-production-rebuild/` 实际执行公开09、低模/UV、完整面准备三个步骤，均原生退出0，再原生重开新准备源与正式源。HIGH/GAME的坐标、拓扑、UV、角法线、颜色属性、世界变换、活动UV层及高模可达材质图逐项精确一致，无差异；持久回执为 `verification/rebuild-preparation.json`。本次公开入口验证停在准备阶段，没有重烤整包；正式19的真实4K三图、导出及视觉验收另有完整记录。

若第三条命令使用了 `--prepare-only`，可用以下命令复查准备输入；它只读正式源并更新比较回执。

```sh
"$TASK_BLENDER" --background --factory-startup --threads 6 --python-exit-code 1 --python art/source/environment/mars-outcrop-hifi-r2/verify_preparation.py -- --prepared-source "$TASK_BUILD/final/surface-prepared.blend"
```

`--finish` 必须显式指定独立 `--output-dir`，并拒绝写入正式资产根目录。
