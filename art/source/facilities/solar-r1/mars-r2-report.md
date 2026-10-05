# solar 火星结构首样 r2

**当前版本：** F01已局部更新为manifest revision3；维护外盖可见性与新hash见[maintenance-r3-report](maintenance-r3-report.md)。下文保留当时版本事实。


2026-10-05，主控制作；manifest revision 2，父票REVIEW。新增五保护分件；四phase与七state继续分开，未新增清扫装置。内罩斜俯视视觉确认未完成。

GLB SHA256：`0b9b07ac221c55a50af1d15a9f3d0081c358354e86b40daf874e65b08742c347`。实际导入888三角面、74mesh、74surface；源与运行GLB字节相同。材质角色/源文件哈希/接点/状态和静态/活动候选包络以[canonical manifest](../../../manifests/solar-r1.json)为准。旧geometry/state报告为历史记录。

可编辑源：本目录Python标准库生成器、glTF+bin和自包含GLB；在本目录执行`python3 generate_solar_r1.py`可复现，重新导出后必须同步运行GLB和manifest。生成器实际运行及必要断言已通过。

当前导入、规定姿态、异常保持、图像/净空方法与限制见[本轮交付](../../../../docs/art/production/evidence/art-mars-models-r2-2026-10-05.md)。完整所有者视觉、正常距离完整盲比、工程性能/游戏集成均NOT_RUN；本报告不代签最终比例/配色。
