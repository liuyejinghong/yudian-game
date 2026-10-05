# 主控独立网格验收

依据terrain-mesh-r1在两个GLM子件交付前准备。独立非对称/扭曲cell金样，按实际三角重心采样邻边与面内高度，不调用工人插值助手；同时核对版本拒绝、计数/绕序、最大/最小和输出所有权。只验证CPU网格，不证明Godot剔除、更新成本、导航或保存。

零外部包，现有SDK离线restore空本地源（NuGetAudit=false），net8/net10 Release编译；net10 run实际执行，net8执行NOT_RUN。命令和结果在本批工程证据记录，生成bin/obj不提交。
