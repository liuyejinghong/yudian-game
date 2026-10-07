# 火星地形支撑看样 r1

`mars-ground-r1.glb` 为直接集成文件；`mars-ground-r1.blend` 为真实 Blender 4.5.14 源。米制，Blender +Z 上，经 glTF 转换为 Godot +Y 上；不缩放。仅原创景观支撑，无碰撞、资源或地图语义，英雄露头另交付。

连续地表 X/Z 为 −250～250m，2m 网格。中心半径12m为 Y=0，柔和接外部低坡；英雄(6,0,0)、驮运(−5,0,5)、太阳能(0,0,−6)、加工(−6,0,−2)四处可直接落地。中央不含方台或道路。中景有两条断续低碎板带，偏北远景为低台地脊线；不模拟真实高程。

三个合并网格分别使用 `Regolith`、`Bedrock`、`Clast`；Regolith 为平滑法线，片状岩面保留硬边。材质仅工作色板、非金属粗糙底色；正式细粒和距离响应由集成方 shader 提供。无照片贴图。763个几何碎屑，常见长轴约4.5～40cm、少量约50cm，岩脚/中景露岩附近聚集，其他地表稀疏。

参考实际看过 `PIA21042` 形体（处理未声明）和 `PIA16800` 中列自然色；参考图片只作形体、覆盖关系和处理依据，非真实颜色/尺寸测量。

生成：`Blender --background --python generate.py`；重开检查：`Blender --background mars-ground-r1.blend --python check_source.py`（在本目录运行）。前者导出源/GLB/manifest并渲染normal，后者核验源、GLB轴与范围、材质、法线，并渲染horizon/near。脚本只依赖Blender内置Python。覆写源时Blender保留 `.blend1` 备份。

`manifest.json` 记录实际统计、哈希与检查；`preview-*.png` 为无英雄与设备、未覆盖Godot shader的Blender检查图，不代表主游戏实机或所有者视觉接受。集成后仍须验正常/近景/反向/地平线、接地、shader细粒及设备尺度。
