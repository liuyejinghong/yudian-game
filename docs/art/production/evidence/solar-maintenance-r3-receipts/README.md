# F01维护可见性 r3 证据

主控实际制作/运行；没有新GLM任务。[交付记录](../art-solar-maintenance-r3-2026-10-05.md)说明当前revision与视觉边界。索引记录54个归档输入/运行/图像文件，10张PNG不修改字节，JSON/log机器路径脱敏前后哈希分列。

当前一致性检查：`python3 docs/art/production/evidence/solar-maintenance-r3-receipts/checks/verify_archive.py`。实际PASS，不能将它当作重新运行GPU。

源有限净空：`checks/check_cover.py`在仓库内运行，复用U01现有凸体SAT；会重生成F01验证字节一致，并把报告写在脚本目录。111整数角两件144对，不是工程连续扫掠认证。第一次AABB判据不足、第二次旧SAT接口误读见PLAN工具诊断摘要，未靠改模型/门槛放行。

Godot检查复用上轮独立缓存项目/公共只读预览、当前F01私有包装/GLB/manifest。本目录保存新增16维护Basis核对的实际solar-actual.gd与导入/clip检查快照；首次392姿态与10非法保持已实跑，新hash在runs与采集JSON中。

上轮r2独立项目搭建办法见[旧receipt README](../mars-r2-receipts/README.md)；复现当前F01时换当前GLB/manifest及本目录solar检查。仅修改忽略目录的独立项目，不运行Main，不改公共材质/预览。完整盲比、所有者、工程可靠性与游戏性能仍NOT_RUN。
