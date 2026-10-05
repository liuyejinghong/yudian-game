# ART-F01 当前首样源与包装

**当前版本：** F01已局部更新为manifest revision3；维护外盖可见性与新hash见[maintenance-r3-report](maintenance-r3-report.md)。下文保留当时版本事实。


**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 2与本轮实测见[mars-r2-report](mars-r2-report.md)。

日期：2026-10-05。技术核验通过；父票REVIEW，完整视觉/所有者未接受。主控原创源与包装；GLM两次仅推演无交件，不归为GLM制作。四phase/七态分开，源5clip；局部seek只保留已评估的无关节点姿态。

当前GLB SHA256：`ea651b334806c070fa1fdf4d3adc6e99bc6f58ed657b110671cc8170ddf8d42d`。源与prototype GLB字节相同；材质面角色表、四元数朝向、状态/phase/cargo声明和源文件hash以[canonical manifest](../../../../art/manifests/solar-r1.json)为准。

实际三角面828、材质面69、接点2，纹理0。自包含GLB仅复制到prototype，bin仅随可编辑glTF源。

重生成：在本目录执行`python3 generate_solar_r1.py`；该生成器同时写源GLB和`prototype/assets/facilities/solar-r1/solar-r1.glb`；同步manifest的hash/表后再重新导入。不能用旧manifest声明新GLB。

源glTF hash：`57c59441ac587759e003b9c7cbe58911faa634b97135c7d25bf526c2075adeef`。

| 外部状态 | 表现类别 | 源动作 | 时长/循环 |
|---|---|---|---|
| idle | static | 无 | 0 / False |
| move | na | 无 | 0 / False |
| work | animated | work | 1 / True |
| charge | na | 无 | 0 / False |
| disabled | animated | disabled | 1 / False |
| towed | na | 无 | 0 / False |
| maintenance | animated | maintenance | 1 / False |

合法输入先验证完整六键及源节点/clip，再全部复位、按绝对时间seek并pause。非法状态、数值、缺clip保持原对象；根固定。每surface直接使用M01角色资源，禁止整mesh覆盖。

当前Godot实际导入、状态矩阵、非法保持、倒拨/故障原因/缺clip、真实图与限制见[首样交付记录](../../../../docs/art/production/evidence/art-first-samples-2026-10-05.md)。生成期离散净空不是全模型连续运动证明；真实游戏适配/物理/性能/所有者效果均NOT_RUN。
