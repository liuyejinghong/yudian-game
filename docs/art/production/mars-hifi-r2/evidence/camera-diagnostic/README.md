# 岩面运行时诊断

root实际原生Metal运行；法线GPU图4096² RGB8、normal_scale=1，2048确定性抽样点与原PNG RGB最大差0。bed/rear相机、target、72/56mm镜头转换为Godot垂直FOV，1500×1100，源未改。

先看bed-camera/rear-camera与最终baked同机位图：细节存在但现有游戏光向弱化颗粒。再看bed-camera-light-axis/rear-camera-light-axis：只对齐Sun世界方向、能量2.8及ambient .55，细节恢复；跨引擎光照/AgX/天空并不严格相等，不将此作为成品验收。

早期*-camera-light图的灯光轴转换错误，保留作失败记录；以*-light-axis为正确方向。GPU图原文件读取发出“导出中不能直接读取PNG”警告，属于只读诊断，不进入产品运行脚本。

capture.gd把OUTPUT_DIRECTORY换为不存在的新绝对目录后可运行。旧文件不覆盖。当前结论是导入没有法线丢失证据，固定游戏镜头仍需局部中尺度颜色/粗糙度区分，不能只依赖掠射光。
