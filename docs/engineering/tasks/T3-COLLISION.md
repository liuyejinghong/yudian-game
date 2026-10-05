# T3-COLLISION · 静态地形三角碰撞适配

状态由[产研TODO](todo.md)维护。唯一GLM票，固定[碰撞r1合同](../contracts/terrain-collision-r1.md)，main起点991b78e，派单前将实际冻结commit记录为base。

只实现TerrainCollisionAdapter.Create与小量实际Godot引擎测试、README；独占新增TerrainCollision、terrain-collision-tests及tools/terrain-collision-tests三个目录，禁止改任何既有文件。主控独立集成/物理时序与GUI/美术接口，不将世界状态/nav/save外包成一体化包。

现有$HOME/.dotnet/dotnet与$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot；PROJECT_ROOT为协调目录不是工作树。离线restore/Debugbuild/实际headless命令见合同，派单给实际绝对路径。无新工具/依赖/下载/GUI/push/merge，不自行接受或领取下一票。交付五行：范围/检查及原日志/限制/用量未知如实/干净限定commit。
