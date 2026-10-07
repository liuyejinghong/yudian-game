# F02 后置压头局部交付 · rev5

独立审查接受本候选的 **19 张静态有限证据**：7 张90°小样与12张受影响视向/停机/检修回归。90°正常尺寸与low中真实压头、固定导架及上下行程空隙更可见，三个静态姿态可区分；0°与所供端部/窗床/接合回归保持。**180°压头处于远侧，大部被壳遮住，该回归不代表远侧压头完整行程可读。**

[90°独立小样最终报告](independent/A02b-F02-rev5-sample-REVIEW.md)与[12图独立回归最终报告](independent/A02b-F02-rev5-regression-REVIEW.md)分别写明接受边界；[小样identity](independent/A02b-F02-rev5-sample-identity.json)与[回归identity](independent/A02b-F02-rev5-regression-identity.json)绑定同一候选。正常图使用Main参数(21.12,28.8,28.8)→0/FOV50，但仍是单件fixture。真实Main、连续动态速度/循环/流畅度、全路径碰撞、运行加工/库存规则、性能与所有者终审未验收；未提供的角度、状态与隐藏内部接合未评估。

只重布局PressGuide与PressRam，压头后移至Z1.2，同导架接原底座/屋沿，同移动连杆接腔内压杆与压座；其余15mesh的attribute/index/材料索引字节、23node/TRS/socket/pivot、共享材料及全部动画输入输出不变。原rev4窗床、端部与硬面成果保留；旧rev4 blob本身的90°REWORK结论不改写。资源r7的冻结rev3背景及独立结论保持，本档仅整理F02，未混入资源附件、地表或旧失败图。

候选[GLB](../../../../art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.glb) SHA256 `b439ab57f4c23fc9868fcb8c0d313ebbdb2956942f1ff3eac7f74080a5bd51e2`，1988tris/17mesh/23node；[原生可编辑blend](../../../../art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.blend)、[生成器](../../../../art/source/facilities/processor-d12-rev5-sample/generate_processor_rev5.py)、[分离glTF](../../../../art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.gltf)与[bin](../../../../art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.bin)保持冻结源。主控已将[候选manifest](../../../../art/manifests/processor-r1.json)提升至revision5；运行res://副本由工程复制并核哈希，本档案不替代运行接入或验收。

## 有效图证

以下均为原始1920×1200 PNG，未裁切、重照明或放大。normal普通档为scale1/4xMSAA，low仅scale.75/关闭MSAA；close为(3,3,4)→(0,.4,0)/FOV50，近景用于可见接合。

| 组别 | 条件 | 公共证据 |
|---|---|---|
| 90°小样 | close-90-work-0.5 | [PNG](godot-rev5-sample/close-90-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/close-90-work-0.5/blind_0001.json) |
| 90°小样 | normal-90-work-0.0 | [PNG](godot-rev5-sample/normal-90-work-0.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-0.0/blind_0001.json) |
| 90°小样 | normal-90-work-0.0-low | [PNG](godot-rev5-sample/normal-90-work-0.0-low/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-0.0-low/blind_0001.json) |
| 90°小样 | normal-90-work-0.5 | [PNG](godot-rev5-sample/normal-90-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-0.5/blind_0001.json) |
| 90°小样 | normal-90-work-0.5-low | [PNG](godot-rev5-sample/normal-90-work-0.5-low/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-0.5-low/blind_0001.json) |
| 90°小样 | normal-90-work-1.5 | [PNG](godot-rev5-sample/normal-90-work-1.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-1.5/blind_0001.json) |
| 90°小样 | normal-90-work-1.5-low | [PNG](godot-rev5-sample/normal-90-work-1.5-low/blind_0001.png) · [姿态/相机JSON](godot-rev5-sample/normal-90-work-1.5-low/blind_0001.json) |
| 受影响回归 | close-0-work-0.5 | [PNG](godot-rev5-regression/close-0-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/close-0-work-0.5/blind_0001.json) |
| 受影响回归 | close-180-maintenance-1.0 | [PNG](godot-rev5-regression/close-180-maintenance-1.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/close-180-maintenance-1.0/blind_0001.json) |
| 受影响回归 | close-180-work-0.5 | [PNG](godot-rev5-regression/close-180-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/close-180-work-0.5/blind_0001.json) |
| 受影响回归 | normal-0-work-0.0 | [PNG](godot-rev5-regression/normal-0-work-0.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-0-work-0.0/blind_0001.json) |
| 受影响回归 | normal-0-work-0.5 | [PNG](godot-rev5-regression/normal-0-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-0-work-0.5/blind_0001.json) |
| 受影响回归 | normal-0-work-1.5 | [PNG](godot-rev5-regression/normal-0-work-1.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-0-work-1.5/blind_0001.json) |
| 受影响回归 | normal-180-work-0.0 | [PNG](godot-rev5-regression/normal-180-work-0.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-180-work-0.0/blind_0001.json) |
| 受影响回归 | normal-180-work-0.5 | [PNG](godot-rev5-regression/normal-180-work-0.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-180-work-0.5/blind_0001.json) |
| 受影响回归 | normal-180-work-1.5 | [PNG](godot-rev5-regression/normal-180-work-1.5/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-180-work-1.5/blind_0001.json) |
| 受影响回归 | normal-90-disabled-1.0 | [PNG](godot-rev5-regression/normal-90-disabled-1.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-90-disabled-1.0/blind_0001.json) |
| 受影响回归 | normal-90-disabled-1.0-low | [PNG](godot-rev5-regression/normal-90-disabled-1.0-low/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-90-disabled-1.0-low/blind_0001.json) |
| 受影响回归 | normal-90-maintenance-1.0 | [PNG](godot-rev5-regression/normal-90-maintenance-1.0/blind_0001.png) · [姿态/相机JSON](godot-rev5-regression/normal-90-maintenance-1.0/blind_0001.json) |

## 核验与原始字节

仓库根执行 `python3 -B docs/art/production/d12-f02-rev5/check-rev5-sample.py` 或 `python3 -B docs/art/production/d12-f02-rev5/check-rev5-regression.py`；后者同时执行前者，已通过7+12张图的PNG/公共JSON/hash、源SHA、实际yaw/state/time/camera/quality及15mesh/动画保留检查。复制脚本调整ROOT层数与公共证据路径，另直接断言当前候选GLB SHA，以防仅修改两个机构网格后旧图检查仍通过。[公共检查记录](public-check.json)列执行结果。

必要作者技术记录包括[连续扫掠/接点](author/candidate-check.json)、[独立GLB闭合倒角/硬面](author/glb-parts-check.json)、[新进程原生回读](author/native-check.json)、[原生源闭包](author/source-portability.json)、[源hash](author/delivery-hashes.json)及[生成日志](author/build-r1.log)/[回读日志](author/native-readback-r1.log)。原生闭包新进程记录0外部库、17mesh/3actions、无外部image；16个修改构造件为真实3mm倒角，作者连续扫掠只在满行程压杆/压座接触。独立审美读取这些技术记录但没有重新运行几何/原生检查。日志与native-check中的“未签视觉”是拍图之前的作者记录，当前视觉接受仅由上述两个独立报告给出。[冻结rev4比较GLB](before-rev4/processor-r1.glb)保留原字节SHA `1fdb11e64ad3c8641e9d67066e140587b8c5e37c2a4597ab4e7097d5c2ab753e`。

[archive-map.json](archive-map.json)逐文件列原始/公共SHA、字节数及每个文本修改。PNG与比较GLB原字节不变；文本只归一私人路径、修公共脚本路径/报告相对链接，并明确原始与公共哈希。每组capture_hashes的`files`校验本公共PNG/JSON，`raw_files`保留原capture字节记录；两份identity的`raw_*`是独立会话核过的原始证据，`public_*`绑定本次公共副本。归一后的JSON/报告不能用旧raw SHA校验。identity里的`capture-fixture/`只是历史捕获路径记录，不是本档可执行工程入口；有效公共链接以上表、报告和archive-map为准。原制作批次/独立报告未改写，无新依赖。
