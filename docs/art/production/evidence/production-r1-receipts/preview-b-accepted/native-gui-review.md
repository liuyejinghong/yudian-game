# PREVIEW-B 主控原生窗口检查

2026-10-04，CUA原生macOS输入，实际Godot4.7.2/Metal。不是GUI信号注入或所有者视觉。对话工具截图保留原始窗口观察；此文是观察记录，不冒充离线渲染图。

- 默认：normal/yaw0/idle/completed/phase_t0/empty/none/time0/evidence，与信息块一致。
- time框清空→Enter：框回到0，默认姿态保留；展开窗口后红字明确“--time is not a valid number”。
- 选work，再time .5，重复work：仍work/.5，未累积；主控看到标题、信息块、控件与mode animated一致。工人另有22项程序化重复/复位测试。
- 相机改close，work/.5保留；Reset：normal、idle、time0，阶段/载荷/原因/方向恢复默认。
- 窗口关闭，cua.listApps确认Godot非运行；以正确DOTNET_ROOT重新启动CLI，实际窗口恢复默认normal/yaw0/idle/time0。关闭结束。

原生AX只提供窗口级元素，控件按实际截图坐标操作。窗口展开过程中由3840像素换到3024像素，一次旧坐标输入被CUA拒绝windowNotFound；刷新截图后继续，无脚本或资产修改。键盘菜单Down未改变选择，不计通过，改用实际菜单点击。

采集画面仍1920×1200独立SubViewport，外部窗口大小不改变合同。默认小窗口底部状态提示会被截断，主控展开后可读；不影响采集证据。NOT_RUN：所有者看图、最终游戏接入、FPS/预算。

局部修正：默认外窗口最小1000×900，避免底部状态/错误被截断；3D SubViewport不变。实际重新启动，原生清空time→Enter，默认窗口直接显示红色错误与time回0；228自测重跑通过。关闭当前窗口核非运行。
