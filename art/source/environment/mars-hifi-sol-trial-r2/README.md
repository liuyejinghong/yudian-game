# 6.1 Sol Blender 高保真流程独立试跑

2026-10-07 实际原生试跑 **PASS**：可编辑高模、三角化 GAME 低模、有效 UV、512 selected-to-active 三通道真烘焙、打包相对纹理源文件与内嵌 GLB 均已生成，并在另一个原生 Blender 进程重开/回读核验。仅证明流程可移交；不是正式核心 4K、美术最终验收或 Godot 集成。

## 交付与检查

- `trial.blend`：`SOURCE_HIGH_EDITABLE` 保留 15,368 三角面原始砂岩碎块高模；默认显示 `GAME_EXPORT` 的 5,378 三角面低模。米制、scale=1，尺寸约 0.8874 × 0.6690 × 0.5600 m。原程序几何可用 `trial.py` 编辑，源文件不依赖脚本即可查看和编辑网格/材质。
- `trial.glb`：独立空场景真实导入，面数、范围、有限坐标和 UV 检查通过；Principled Base Color、Roughness、Normal 连接存在，metallic=0；3 张 512 PNG 实际存在于 GLB bufferView，无外部 URI，无 occlusionTexture。base/normal 内嵌字节与独立 PNG 相同；glTF 将 roughness 合为 8bit metallic-roughness 数据纹理，原始 roughness 小样为 16bit。
- `textures/{base_color,normal,roughness}.png`：512 RGB 16bit 原始烘焙小样；base_color=sRGB，其余 Non-Color。三张同时 packed，源路径 `//textures/`。base/rough 由最终 Principled 输入接入 Emission 进行 selected-to-active 投射，normal 为真实高模几何投射；没有照片、AO 或方向光烘色。
- `renders/high.png` 与 `renders/mapped-low.png`：960 × 720 同机位、同光照原生 Cycles 实际渲染，已实看。高模/低模层边和断面轮廓可读；当前深色光照用于同光比较，不作地点色彩或高保真审美结论。
- `build.json`、`verification.json`、`execution.json`：实际版本、技能函数、投射/UV/法线检查、可执行命令、逐文件 SHA 和内嵌 PNG 证据。两正式资产的原 blend/GLB 四文件 hash 全程未改。

## 复跑

在仓库根目录，令 `TASK_BLENDER` 指向项目已安装 Blender 5.2.2 可执行文件。必须原生运行；本机 sandbox 的 Metal/USD 初始化曾崩溃，因此此次按已有授权在原生进程执行。单个试跑进程、CPU 固定 4 线程；构建与核验按顺序执行。

```sh
"$TASK_BLENDER" --background --factory-startup --threads 4 --python-exit-code 1 --python art/source/environment/mars-hifi-sol-trial-r2/trial.py
"$TASK_BLENDER" --background --factory-startup --threads 4 --python-exit-code 1 --python art/source/environment/mars-hifi-sol-trial-r2/trial.py -- --verify
```

脚本复用现有 `mars-outcrop-hifi-r2/build.py` 与 `pipeline.py`，以及项目 Scenario `bx_uvbake`、`bx_materials`。烘焙直接用 bpy，避开 `pipeline.bake_pair` 内置 6 线程。复跑覆盖前自动将已有输出复制到自身 `backups/`；不写正式资产、文档或 viewer。

## 已观察限制与修正

首轮 60° smart-project 存在图岛自折叠，失败源脚本与日志留在本地；按碎块规模降低网格密度并改 30° 后，1024 分析得到 0 翻转、0 零面积、0 图岛间/岛内重叠。最终 935 岛，覆盖率 18.02%，密度约 143.2 texel/m、最小岛距 7.5 px、边界 4.1 px；部分小岛低于 4×4 px。这里保留充分隔离与几何细节，512 不能证明所有细小表面采样完整，更高分辨率正式制作须重新评估 UV 密度与可读性。

首张映射低模渲染可见少量未覆盖背景暗三角。修正采用正确 sRGB 编码中性 base 背景、平法线背景与 roughness=.86 后，`use_clear=False` **真实重新烘焙三通道**，没有全图替换黑像素。PNG 保存重开实测 base 线性约 (.219995,.195006,.170991)，normal 约 (.500008,.500008,1)，roughness 约 .859998；同光重渲染已消除暗三角。

最终 4,035 点投射 miss/uncovered/wrong_part 均 0；normal 检查无问题（精确平值约 0.61%、倒向约 0.002%、无 NaN、无坏长度），源材质 audit 无问题。未做 Godot、4K 重制、所有者视觉验收或核心改动。
