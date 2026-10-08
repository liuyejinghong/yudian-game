# D12 六资源灰模、cargo 类标识与 carrier r7

2026-10-08。**限定静态 PASS**：四个新增灰模的单体/车载近景形体、已知映射下六类单车 normal/normal-low 16px 标识、carrier 可见静态承托与 empty 隐藏，以及铜料/结构件/线缆出料近景的静态接合。独立评审逐张打开了全部 57 张原始 1920×1200 PNG，原文件 SHA 保持。[独立报告](independent/REVIEW.md)与[身份清单](independent/identity.json)保留原结论。

normal/normal-low 出料类别辨认及精确接触**未签收**，近景接合 PASS 不扩成远距识别 PASS。r5 normal 距离纯模型 mixed 的 **REWORK 仍有效**，本次 cargo glyph 的 known-mapping PASS 不覆盖它。截图没有名称图例，不是新玩家无图例盲认。Main/C02 真实账本与 HUD、多人或多车遮挡、动态运输/加工、圆杆与卷材滚动稳定、最终 PBR、压缩后完整导入属性、性能及所有者终审均未签。

[六类候选清单](../../../../art/manifests/resources-d12-r1.json)与[承托清单](../../../../art/manifests/tuoyun-cargo-carrier-d12-r1.json)登记候选与有限静态范围；portable 源身份接受见下方独立附录，运行接入由工程另验。

六个固定 ID 与名称：iron_ore 铁矿、copper_ore 铜矿、iron 铁料、copper 铜料、parts 结构件、cable 线缆。所有模型同一灰色材质；glyph 靠形状区分类别，每个非零类别一枚，不表达数量。mock items 是测试输入，四聚合只是可视表达，不规定账本单位。

## 原图与技术依据

[capture.json](godot-r7/capture.json)逐图记录 SHA、相机、实际载入来源、mock items、glyph、carrier 与落位。36 张 cargo、12 张单体、9 张新加工品 output，共 57 张。以下链接直接指向原 PNG；没有拼图、裁切或重新编码。

| cargo | normal | near | reverse | normal-low |
|---|---|---|---|---|
| 铁矿 | [图](godot-r7/cargo-iron_ore-normal.png) | [图](godot-r7/cargo-iron_ore-near.png) | [图](godot-r7/cargo-iron_ore-reverse.png) | [图](godot-r7/cargo-iron_ore-normal-low.png) |
| 铜矿 | [图](godot-r7/cargo-copper_ore-normal.png) | [图](godot-r7/cargo-copper_ore-near.png) | [图](godot-r7/cargo-copper_ore-reverse.png) | [图](godot-r7/cargo-copper_ore-normal-low.png) |
| 铁料 | [图](godot-r7/cargo-iron-normal.png) | [图](godot-r7/cargo-iron-near.png) | [图](godot-r7/cargo-iron-reverse.png) | [图](godot-r7/cargo-iron-normal-low.png) |
| 铜料 | [图](godot-r7/cargo-copper-normal.png) | [图](godot-r7/cargo-copper-near.png) | [图](godot-r7/cargo-copper-reverse.png) | [图](godot-r7/cargo-copper-normal-low.png) |
| 结构件 | [图](godot-r7/cargo-parts-normal.png) | [图](godot-r7/cargo-parts-near.png) | [图](godot-r7/cargo-parts-reverse.png) | [图](godot-r7/cargo-parts-normal-low.png) |
| 线缆 | [图](godot-r7/cargo-cable-normal.png) | [图](godot-r7/cargo-cable-near.png) | [图](godot-r7/cargo-cable-reverse.png) | [图](godot-r7/cargo-cable-normal-low.png) |
| mixed-a | [图](godot-r7/cargo-mixed-a-normal.png) | [图](godot-r7/cargo-mixed-a-near.png) | [图](godot-r7/cargo-mixed-a-reverse.png) | [图](godot-r7/cargo-mixed-a-normal-low.png) |
| mixed-b | [图](godot-r7/cargo-mixed-b-normal.png) | [图](godot-r7/cargo-mixed-b-near.png) | [图](godot-r7/cargo-mixed-b-reverse.png) | [图](godot-r7/cargo-mixed-b-normal-low.png) |
| empty | [图](godot-r7/cargo-empty-normal.png) | [图](godot-r7/cargo-empty-near.png) | [图](godot-r7/cargo-empty-reverse.png) | [图](godot-r7/cargo-empty-normal-low.png) |

| 单体 | near | reverse |
|---|---|---|
| 铁矿 | [图](godot-r7/single-iron_ore-near.png) | [图](godot-r7/single-iron_ore-reverse.png) |
| 铜矿 | [图](godot-r7/single-copper_ore-near.png) | [图](godot-r7/single-copper_ore-reverse.png) |
| 铁料 | [图](godot-r7/single-iron-near.png) | [图](godot-r7/single-iron-reverse.png) |
| 铜料 | [图](godot-r7/single-copper-near.png) | [图](godot-r7/single-copper-reverse.png) |
| 结构件 | [图](godot-r7/single-parts-near.png) | [图](godot-r7/single-parts-reverse.png) |
| 线缆 | [图](godot-r7/single-cable-near.png) | [图](godot-r7/single-cable-reverse.png) |

| 新加工品出料（无 glyph） | normal：观察，辨认未签 | near：静态接合 PASS | normal-low：观察，辨认未签 |
|---|---|---|---|
| 铜料 | [图](godot-r7/output-copper-normal.png) | [图](godot-r7/output-copper-near.png) | [图](godot-r7/output-copper-normal-low.png) |
| 结构件 | [图](godot-r7/output-parts-normal.png) | [图](godot-r7/output-parts-near.png) | [图](godot-r7/output-parts-normal-low.png) |
| 线缆 | [图](godot-r7/output-cable-normal.png) | [图](godot-r7/output-cable-near.png) | [图](godot-r7/output-cable-normal-low.png) |

carrier 直接挂现有 CargoSocket，scale1、局部 transform identity，甲板世界顶面 Y.72；货物 scale.65、底面 Y.72，x±.19、z.10/.50，铁料 yaw90。[carrier 源/GLB 检查](technical/carrier-check.json)与[独立回读身份](technical/readback-identity-check.json)记录真实 Socket 和接合；四脚部分遮挡，不声称全可见。empty 无 carrier/goods/glyph。[捕获技术检查](technical/fixture-check.json)与[原 native capture 日志](technical/fixture-capture.log)记录实际 57 图执行成功。

出料背景是冻结 F02 rev3 `cf401d19…`，不是后续 F02 版本。[冻结床面 GLB](context/f02-rev3-processor.glb)逐字节保留。[床面接触检查](technical/output-contact-check-r7.json)直接解码 OutputRack/body_light 三个床顶矩形，12 个落位的所有最低顶点在实床上，不在沟缝；这是静态几何接触，不证明动态稳定。原 r5 铁料两层出料的局部 PASS 保留，本包不重拍或扩签它。

## Portable 源身份

57 图的原独立身份绑定 r7 源 `73b927f43df0c179f80bc08696531719e201f3f2bb1bbb452b69c5eb96e396ce`。公开候选 portable r7 是新的 blob `5b5eeb3a68fcd3160e40efe47e5af6e816e4d6543e221d3dc523c61b7a2cb200`，其 r5 依赖也是新的 blob `de0fbdcc1af74b3e830c5ae41a62c46720e1993dc30cfd8e91cfacf3a6d1636b`。**独立 portable 身份附录 PASS_SOURCE_IDENTITY_PORTABILITY_LIMITED**：[报告](independent/portable-source-addendum-REVIEW.md)与[身份记录](independent/portable-source-addendum-identity.json)已独立核对 351 项文件与完整快照/实际导出字节，接受这两份新 blob 的源身份与当前六资源可移植性。附录未新看图、未重复原生重开/重建/渲染，不扩大原 57 图的限定静态视觉范围。

提交方 [native-proof](portable/native-proof.json)及[日志](portable/native-proof.log)把原源与 portable 源完整语义快照、全部 GLB 视觉 attributes/indices/材质/node transform/extras 精确比较，原生重开外部 libraries/linked IDs 均为零，Geometry Nodes modifier 仍可编辑。跨进程排除 ID.session_uid；r5 重开只清掉未绑定、零用户的默认 Material，[真实差异](portable/fresh-reopen-differences.json)保留。[隔离重建证明](portable/isolated-rebuild-proof.json)、[证明日志](portable/isolated-rebuild-proof.log)与[builder 日志](portable/isolated-rebuild.log)证明 builder 也生成零外部库，六 GLB 与冻结版本逐字节相等；新重建 blend 不声称同一源 blob。

[current-verifier receipt](portable/current-verifier-receipt.json)和[stage](portable/current-verifier-stage.json)记录当前 verifier `4465626e…` 的实际隔离 source/glb 两入口，均 exit0，原链接 blend 故意缺席，98 项原成果未变。对应[源日志](portable/current-verifier-source.log)、[GLB 日志](portable/current-verifier-glb.log)、[源回读](portable/output/current-source-readback.json)、[GLB 回读](portable/output/current-glb-readback.json)都在本包。旧 [isolated-stage](portable/isolated-stage.json)是历史 builder 输入回执，其记录 verifier SHA 与后来实际脚本不同，不能用来声称当前 verifier 已运行；当前 receipt 明确区分了三版 SHA，没有改写旧 stage。

## 归档核对与包外依赖

运行 `python3 docs/art/production/d12-resources-r7/check_archive.py`。只使用 Python 标准库；不需要临时 Godot fixture 或 Blender。它核对 89 组 raw/public 身份、全部 57 PNG、17 个当前 capture 输入、carrier 源/生成器、两份 portable 源；解析六 GLB 的 bounds/tri/identity、重算 cargo AABB 与 12 个实床接触，并核对 portable、独立源身份附录与当前 native receipt 衔接。[归档检查结果](archive-check.json)及[独立目录重放回执](isolated-archive-check.json)是技术检查，不是新视觉签收。14 个历史捕获输入（例如旧 manifest、导入配置与 preview 脚本）保留 SHA，但本包不重放完整临时工程。[bindings](bindings.json)列出实际核对和未重放两类来源。

[archive-map](archive-map.json)分别记录原 raw 与公开副本 SHA/字节数。PNG/GLB 没有变化；文本只归一化私人绝对路径与已归档引用。报告、identity、receipt 内历史 SHA 字段仍指 raw 文件，**不能拿它与脱敏副本直接比较**；通过 archive-map 的 original_sha256/archive_sha256 衔接。`historical-evidence/not-published/`、`historical-local-project/` 等是未公开历史来源标记，不是假装可点击的文件路径，也不是运行依赖。原 153 项身份全部匹配的独立结论保留，新增附录的 351 项 raw 身份记录也完整保留；本包不批量搬运 153/351 项或旧失败图。

本目录是证据归档，不内嵌正式素材。归档核对还读取树中六 r7 GLB、六 SVG/mapping、两 portable blend、carrier 源/生成器/GLB，以及 **`art/source/units/tuoyun-r1/tuoyun-r1.glb`**（SHA `929d3b…`）；当前 prototype 的另一份 GLB 不可替代。所有路径见 bindings。主控选择性发布这些素材，本任务未改公共 manifest、真实运行时或 Git。

最小重建/日常 native 验证闭包保持原目录层数：`art/source/resources/d12-r1/r7-portable` 中 build_resources.py、verify_resources.py、make_portable.py、prove_portable.py（verifier 会导入其函数）；上级 r5 portable blend、r5 两 GLB；日常验证另需 r7 portable blend、r7 六 GLB，以及本包逐字节保留的 [r7 before 快照](portable/resources-grey-r7-before.json)。Scenario 的 bx_hardsurface.py、bx_audit.py、bx_review.py、bx_gui.py 已在 main 合入；U01 是实际捕获背景依赖。可选重建证明入口还需同目录 prove_rebuild.py。原 r5/r7 链接 blend 只供历史 prove_portable 转换证明，日常 verifier 不需要它们，不把它们列为公开依赖。

先将上述闭包复制到**独立树**；保持 source 路径不变，并在独立树根准备目录、将公开快照复制到 verifier 的固定基准路径：

```sh
mkdir -p art/exports/resources/d12-r1/r7 docs/art/production/d12-r1/resources/portable-r7/output
cp docs/art/production/d12-resources-r7/portable/resources-grey-r7-before.json docs/art/production/d12-r1/resources/portable-r7/resources-grey-r7-before.json
BLENDER_APP=/path/to/native/Blender
"$BLENDER_APP" --background --factory-startup --threads 4 --python-exit-code 1 --python art/source/resources/d12-r1/r7-portable/verify_resources.py -- source
"$BLENDER_APP" --background --factory-startup --threads 4 --python-exit-code 1 --python art/source/resources/d12-r1/r7-portable/verify_resources.py -- glb
```

这两个入口向 output 写审计/作者图，不更新固定 57 图。builder 会覆盖该独立树的 r7 blend/六 GLB，故只在隔离副本运行；make_portable.py 是保存前本地化的直接依赖，不能省略。本次归档没有重新执行 native 检查或渲染；保存的是此前真实执行的 SHA/日志/回执。Blender 5.2.2 与原生图形运行环境仍是 native 验证所需的包外工具，无新增 Python 包。
