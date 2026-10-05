# ART-F02 当前首样源与包装

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 2与本轮实测见[mars-r2-report](mars-r2-report.md)。

日期：2026-10-05。技术核验通过；父票REVIEW，完整视觉/所有者未接受。GLM源3e98c33→7f818c2→76121b5，主控包装。work极值平台修正经Godot实测；两箱固定。offline展示映射disabled，无第八态。

当前GLB SHA256：`773575a5e7bcc946864680e5cbead04b53e3a815bebed2ab15519ed3de4ebc5a`。源与prototype GLB字节相同；材质面角色表、四元数朝向、状态/phase/cargo声明和源文件hash以[canonical manifest](../../../../art/manifests/processor-r1.json)为准。

实际三角面1140、材质面27、接点4，纹理0。自包含GLB仅复制到prototype，bin仅随可编辑glTF源。

重生成：在本目录执行`python3 generate_processor_r1.py`；该生成器只写本源目录；随后将同字节`processor-r1.glb`复制到`prototype/assets/facilities/processor-r1/processor-r1.glb`，同步manifest的hash/表后再重新导入。不能用旧manifest声明新GLB。

源glTF hash：`43a71c974015f6b1c7095abdb2bc8eaea87a5c8985572af4c847ffe781be9958`。

| 外部状态 | 表现类别 | 源动作 | 时长/循环 |
|---|---|---|---|
| idle | static | 无 | 0 / False |
| move | na | 无 | 0 / False |
| work | animated | work | 2 / True |
| charge | na | 无 | 0 / False |
| disabled | animated | disabled | 1 / False |
| towed | na | 无 | 0 / False |
| maintenance | animated | maintenance | 1 / False |

合法输入先验证完整六键及源节点/clip，再全部复位、按绝对时间seek并pause。非法状态、数值、缺clip保持原对象；根固定。每surface直接使用M01角色资源，禁止整mesh覆盖。

当前Godot实际导入、状态矩阵、非法保持、倒拨/故障原因/缺clip、真实图与限制见[首样交付记录](../../../../docs/art/production/evidence/art-first-samples-2026-10-05.md)。生成期离散净空不是全模型连续运动证明；真实游戏适配/物理/性能/所有者效果均NOT_RUN。
