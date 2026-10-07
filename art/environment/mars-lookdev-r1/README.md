# 真实火星环境看样 r1

独立 Godot 原创场景：Gale / Stimson 风积砂岩—沙砾过渡带。正常RTS视距、近景、反向和地平线可以在同一光照下切换；三件现役设备只作米制尺度参照。它不加载Main，不运行模拟，也不表示所有素材已重制。

双击 `open-lookdev.command`，或用已安装Godot4.7.2打开 `project.godot`。默认依赖workspace已有tools-bin/Godot与用户已有.NET，未安装新依赖。`1`正常、`2`近景、`3`地平线、`4`反向、`L`低画质、`H`隐藏说明、右键拖动绕观察目标、Esc退出。

制作依据、全资产矩阵与科研边界见 [MARS-BRIEF](../../../docs/art/production/mars-research-r1/MARS-BRIEF.md)。英雄露头源见 [mars-outcrop-r1](../../source/environment/mars-outcrop-r1/)；支撑地表源在 [ground](ground/)。Blender是默认建模工具，Godot显式消费GLB；本project关闭自动blend转换，避免依赖全局editor的Blender路径。砂岩PBR随英雄GLB内嵌，地面shader仅用于独立看样，不回写M01或Main。

场景、材质、视距是美术设计推论；照片不作runtime纹理，500m网格不是实际火星高度场。所有源米制scale1；中心半径12m平缓，英雄允许14cm埋入余量。远景不具有地图情报、资源或碰撞意义。看样关闭自动LOD以固定作者网格；低画质保留相同光照/几何/机位，用0.65 render scale、无MSAA/SSAO；不是正式最低配置性能结论。

可复跑（从repo根目录，`Godot`代表workspace已有引擎）：

```sh
Godot --headless --editor --path art/environment/mars-lookdev-r1 --import --quit
YUDIAN_MARS_OUTPUT=/new/output/dir Godot --path art/environment/mars-lookdev-r1
python3 art/environment/mars-lookdev-r1/verify.py /new/output/dir
Godot --headless --path art/environment/mars-lookdev-r1 --script res://check_input.gd
```

输出目录须不存在，防止覆盖证据；生成5张PNG与capture.json。capture记录实际引擎/driver/viewport、相机/画质、导入AABB/triangles、源与帧hash，并确认源未变。主控另做保存blend重开再导出。截图/技术检查不能代替所有者审美接受或Main集成、模拟/保存/导航、整场性能与真人交互测试。
