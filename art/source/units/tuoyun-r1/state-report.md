# ART-U01 当前首样源与包装

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 3与本轮实测见[mars-r2-report](mars-r2-report.md)。

日期：2026-10-05。技术核验通过；父票REVIEW，完整视觉/所有者未接受。源r9保留车身/货台/接点；锁扣双臂/带孔支承、暖色大口盖、轮面标记已修正。此前geometry-report/state-source-report保留旧版本事实，当前计数/哈希以本报告及manifest为准。

当前GLB SHA256：`42bb11a76a1317a66919066371ca77dd47b0efc409a9b0446990bae0b1dd2391`。源与prototype GLB字节相同；材质面角色表、四元数朝向、状态/phase/cargo声明和源文件hash以[canonical manifest](../../../../art/manifests/tuoyun-r1.json)为准。

实际三角面2040、材质面69、接点4，纹理0。自包含GLB仅复制到prototype，bin仅随可编辑glTF源。

重生成：在本目录执行`python3 generate_tuoyun_r1.py`；U01生成后将同字节GLB复制到prototype对应目录，manifest的hash/表同步后再重新导入。不能用旧manifest声明新GLB。

源glTF hash：`71aa74f54c85fa55dbd34a720cc64a0406bbaa58b0116cf51771cf63712633aa`。

| 外部状态 | 表现类别 | 源动作 | 时长/循环 |
|---|---|---|---|
| idle | static | 无 | 0 / False |
| move | animated | move | 1 / True |
| work | animated | work | 1 / False |
| charge | animated | charge | 1 / False |
| disabled | animated | disabled | 1 / False |
| towed | static | disabled | 1 / False |
| maintenance | animated | maintenance | 1 / False |

合法输入先验证完整六键及源节点/clip，再全部复位、按绝对时间seek并pause。非法状态、数值、缺clip保持原对象；根固定。每surface直接使用M01角色资源，禁止整mesh覆盖。

当前Godot实际导入、状态矩阵、非法保持、倒拨/故障原因/缺clip、真实图与限制见[首样交付记录](../../../../docs/art/production/evidence/art-first-samples-2026-10-05.md)。生成期离散净空不是全模型连续运动证明；真实游戏适配/物理/性能/所有者效果均NOT_RUN。
