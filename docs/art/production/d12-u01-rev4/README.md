# U01 载货箱共面局部修复 · rev4

箱身顶面从 0.86m 降至 0.80m，盖顶保持 0.86m，消除旧盖面白三角。只改箱身中心和高度；外包络、其余21节点、socket与全部动画轨不变。候选 GLB SHA `929d3b716c435c6ed428a6ab61c14ab8e5b8442093da8c4aea7f11d498c2b341`。

[独立审美报告](independent/A02a-U01-cargo-rev4-REVIEW.md) PASS 仅绑定共面消除和八张静态图的相关无回归。完整设备、高保真、动态全部姿态、Main货物可读性、性能与所有者终审未签。本交付仅更新 art 源与候选 manifest；其 res:// 路径仍指运行副本，工程复制 GLB 并更新运行 manifest 后才能混验，默认应用由工程持有。

## 证据

- `before-close90/180` 对照 `current-close90/180`；固定 preview-r1 相机、光照与材质，实际 Godot4.7.2 Metal/Forward+，1920×1200。另含正常载/空、维护、低画质四图；low仅关闭MSAA/内部采样.75。
- `python3 -B docs/art/production/d12-u01-rev4/check_cargo.py` 检查源顶面、外包络、其他节点、socket和动画。`reopen.py` 可用 Blender5.2.2 重开源glTF与独立GLB，各7312tri/19mesh/5clip；结果见 source-reopen.json。
- actual-matrix.json 为提交方实际98姿态、10非法保持、24轮姿态检查，零失败；godot-mesh-audit.json 为实际导入几何。独立审美会话没有重跑这些原生检查。

## 归档边界

原始证据和失败源/缓存图保留在本地制作批次，失败 after-* 未收入有效交付。公开副本只去掉私人路径，可运行脚本调整目录深度；独立结论不改。archive-map.json 同时列原字节 SHA 和公开副本 SHA；原 capture_hashes/identity 中 JSON SHA 指原字节，不能用来校验去路径后的 JSON。PNG、源与GLB字节不变，图位路径按相同目录名对应本目录。

before-close90 原源解析失败如实保留，GLB SHA 正确且独立核旧源；before-close180 与 current 源 SHA 匹配。曾出现输入GLB SHA已变、引擎仍渲染旧缓存，已清缓存重导并实际核箱身/盖局部顶点 .32/.38；current才是有效新图。normal为固定独立预览，不能代签Main。
