# ART-M01 · 共享材质试样 r1（六 StandardMaterial3D）

日期：2026-10-04。执行：GLM-Material；独立接受：主控。本目录是 M01 唯一写入范围：六个 `.tres` + 本 README。

**配色是首轮试样候选，未获所有者视觉认可，不是最终配色规范。**数值冻结自[首批美术需求 r1](../../../../docs/art/production/requirements/first-assets-r1.md) 的 M01 六表（本轮实现输入）；视觉比较等预览场景（preview-r1），本票视觉项 NOT_RUN。

## 引用方法（preview-r1 接线合同）

- 稳定六角色字典按 `res://assets/materials/art-r1/<role>.tres` 加载；仓库写入路径是 `prototype/assets/materials/art-r1/<role>.tres`，preview 工程内的 res 路径映射由预览实现负责，本票不改 project.godot。
- 上层逐面绑定：manifest `material_bindings` 每行 `{"node":"Model/<实际Mesh节点>","surface":N,"role":"<角色>"}`（surface 从 0 起），预览加载共享 `.tres` 后调 `set_surface_override_material(surface, mat)`；不 `set` 整件 `material_override`，同角色复用同一资源。
- 缺 `.tres`/节点/角色、surface 越界、重复绑定或遗漏面：报错停止，不悄悄补灰色、不生成替代材质。本票不实现模型接入，不改 glTF 导入器。
- 六个文件均无 `uid` 属性、无 `[ext_resource]`、无 `[sub_resource]`：按 res 路径直接引用，零外部依赖。

## 颜色口径

样件色值是非线性 sRGB 目标；`albedo_color` 按 **RGB 字节/255** 存入（alpha=1），**未**手工 `srgb_to_linear` 预转（preview-r1/first-assets-r1 冻结口径）。显示颜色受光照与曝光影响，不用截图取色代替资源核参。六个 `.tres` 的 `albedo_color` 与六表逐一按浮点误差 1e-6 核对通过（临时工程真实加载，见下）。

## 六材质实录

类型均为 `StandardMaterial3D`，`format=3`，文件仅含以下三行非默认/显式属性（属性名 = 实际序列化内容，未列出的属性全部使用 StandardMaterial3D 默认 PBR 值）：

| 角色 | sRGB hex | 序列化 albedo_color | roughness | metallic | 用途 |
|---|---|---|---|---|---|
| body_light | #D8DCD6 | Color(0.847059, 0.862745, 0.839216, 1) | 0.65 | 0.0 | 浅色外壳与设备面板 |
| frame_dark | #38454B | Color(0.219608, 0.270588, 0.294118, 1) | 0.75 | 0.2 | 底盘、框架与功能结构 |
| rubber | #24292B | Color(0.141176, 0.160784, 0.168627, 1) | 0.9 | 0.0 | 轮胎、缓冲件 |
| accent_warm | #D77B3D | Color(0.843137, 0.482353, 0.239216, 1) | 0.6 | 0.0 | 克制的鼻标、维修口或危险边缘 |
| solar_face | #203B52 | Color(0.12549, 0.231373, 0.321569, 1) | 0.35 | 0.1 | 面板有效受光表面 |
| soil_mars | #98684D | Color(0.596078, 0.407843, 0.301961, 1) | 0.95 | 0.0 | 比较场景地面，不代表真实地形状态 |

说明：

- 六位小数序列化与字节/255 的最大浮点偏差 4.71e-07，在 1e-6 容差内；Godot 4.7.2 加载后 `roughness`/`metallic` 以 32 位浮点存储（如 0.65 → 0.64999997615814208984），与目标差 <3e-8。
- `metallic = 0.0` 与默认值相同，为六个文件统一模式而显式写出；`roughness` 默认为 1.0，六个文件全部为非默认显式值。
- 默认使用（未写入文件、加载实测确认）：`transparency = 0`（禁用，不透明）、`emission_enabled = false`（无发光）、全部 `*_texture` 属性无 Texture 实例（无外部纹理）、`shading_mode = 0`（逐像素）、无自定义 shader、无附加脚本。

SHA256（采样时点，以提交内容为准）：

| 文件 | SHA256 |
|---|---|
| body_light.tres | c42d893024832f054751605a3cbb672222c022a83825cfa8fb45a9d19af8ffa1 |
| frame_dark.tres | 3d98442ab026c3ebaf73b98e397428e97c3185012a16bf1ace5917ddf6ad6936 |
| rubber.tres | 82c0f80a962402f8c204dbb73680062b1e2fcf80389895b3cce8bf243ffe246a |
| accent_warm.tres | fe01e258db57c387c7dbba6aa9b1febe67a8b24c6fbc479d1fc71a3824faa290 |
| solar_face.tres | 1b1be352ed4d6694b2883a4a6392a60b15a8c31dcc386517dd7d94f52571c209 |
| soil_mars.tres | 5f8aa3e483006e74e69cbda1385c2392c2f4f71f73b4129f957f2e15adf8a4f6 |

## 来源与版本

- 来源：本项目原创参数（需求方冻结的首样候选表），无第三方素材、无生成依赖。
- 实测引擎：`/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot` → `4.7.2.stable.mono.official.ed1daf0bf`；.NET SDK `10.0.401`（`/Users/ethan/.dotnet`）。
- 资源格式：Godot 4 文本资源 `format=3`；未在编辑器中打开过，故无引擎生成的 `uid`；若日后在编辑器保存，引擎可能补写 `uid` 并重排序列化，角色与参数不应变化。

## 检查记录

命令（临时空工程 `/private/tmp/art-m01-matcheck.XdXIVF`，六资源复制为 `res://assets/materials/art-r1/`，仓库 project.godot 未动）：

```
DOTNET_ROOT=/Users/ethan/.dotnet PATH="/Users/ethan/.dotnet:$PATH" \
  Godot --headless --path <临时工程> --log-file <可写log> --script res://check_materials.gd
```

结果：exit 0，自身成功标记 `M01_MATERIAL_CHECK_PASS 6/6`，log 无 SCRIPT ERROR。逐资源核验：文件存在、加载成功、类型 `StandardMaterial3D`、`albedo_color` 三通道对字节/255 误差 ≤1e-6、alpha=1、roughness/metallic 命中、transparency 禁用、emission 关、无 Texture 实例、无脚本。缺失路径探针（`nonexistent_probe.tres`）加载返回 null 并留 ERROR 诊断：缺路径明确失败，不生成替代。首轮运行的检查脚本曾把布尔属性 `heightmap_flip_texture`（值 false）误判为贴图，属检查器误报，已改为按 Texture 实例判定后通过；六资源文件本身两轮零改动。

## NOT_RUN

视觉比较（preview-r1 白昼渲染/试板）、`node/surface/role` 实际接线（依赖 U01/F01/F02 模型与预览实现）、所有者视觉确认、任何性能测量。接受本资源仅代表可用与参数一致。
