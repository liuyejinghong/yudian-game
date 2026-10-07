# A02a-U01-cargo-rev4 独立局部复审

2026-10-08｜**PASS：仅载货箱身/盖顶面共面消除，以及送审静态图中未见相关可见回归。无须本切片返工。**

候选 GLB：`929d3b716c435c6ed428a6ab61c14ab8e5b8442093da8c4aea7f11d498c2b341`。
来源：`docs/art/production/d12-r1/u01/REVIEW-INPUT.md`。本报告只写独立审查目录，不修改制作方状态。

## 文件与对照有效性

独立计算 candidate-hashes.json 的五个源/GLB/manifest SHA，全部匹配；八个有效截图目录的 PNG 与 JSON 共十六文件也匹配各自 capture_hashes。当前六图均声明同一 rev4 GLB，源三项 SHA 经本会话实算匹配。before 两图 GLB 为旧 `63c82349…27fd9`；before-close90 的源解析失败仍如实保留，本会话已分别核 before/ 三个原源字节与其声明 SHA 一致，不能将原捕获的失败栏改写为成功。

90/180 前后对照的相机、光照、曝光、Viewport和共享材质记录一致；空/载 normal 条件一致。normal-low 仅 Viewport 关闭 MSAA并将内部采样改为 .75，其他 view 项及材质记录一致。**after-* 全部排除，不作新候选证据。** SHA 或元数据一致本身不能排除引擎旧缓存，所以下述结论依据有效图的实际变化与独立 GLB 顶点读取。

本会话独立运行 `python3 -B docs/art/production/d12-r1/u01/check_cargo.py`，PASS：箱身顶 .86→.80、盖顶 .86，源几何外包络、其余21节点、socket及动画轨不变。另直接解析旧/新 GLB 的 CargoBox POSITION：局部箱身/盖顶由 `.38/.38` 变为 `.32/.38`，节点 Y 平移均 .48，实际相隔 .06m。它证明旧顶面共面被消除；不是新一轮 Blender/Godot 全流程验收。

完整身份、条件与独立读取结果见 [A02a-U01-cargo-rev4-identity.json](A02a-U01-cargo-rev4-identity.json)。制作方原生重开、98姿态等检查是提交背景，本会话没有重跑，不将其写成本会话新验收。

## 实看与采用的判据

沿用已核固定 Scenario commit `91caa011e13774a220aba7136309b49964104040`、MIT 方法：hard-surface critique #3（平面明暗）、#6（共面）、#11/12（结构与使用距离）；texturing-shading critique §6（目标引擎表现）；lighting-rendering critique §2/3/7（明度、形体与引擎伪影）。只采用适合该机械载货局部的判据；不要求低保真整车升级，不改灯或相机。

以下八张 `blind_0001.png` 均实际逐张以 original 读取，1920×1200。近景与 normal 分开判断，不用近景代签正常/Main可读性。

| 有效图目录 / 位置 | 实际观察与判断 | 判据 |
|---|---|---|
| before-close90 → current-close90；箱盖约 x51–59%、y40–49% | PASS。旧图盖面右前部白色三角消失，新图为连续深色平面；盖外缘、箱侧、护栏和轮组未见相应新裂缝/穿插 | hard-surface #3/#6；lighting §7 |
| before-close180 → current-close180；箱盖约 x46–58%、y37–46% | PASS。旧图盖面的两块白片消失；反向盖面、外沿和侧面连续，未见箱身缩短造成外壳破口 | hard-surface #3/#6/#11 |
| current-normal-loaded / current-normal-empty / current-normal-low；中央车辆约 x49–52%、y48–52% | PASS，仅粗外形未见异常和载货区域仍存在。空载为低货区，有载有抬高盖/箱体；低画质仍见车辆主体与载区。对象很小，不能由此宣布空/载辨识充分、全部姿态或 Main 货物可读性通过 | hard-surface #12；lighting §2 |
| current-maintenance180；前舱开盖及后部货箱约 x40–60%、y37–61% | PASS，仅该静态姿态。前舱开盖与后部货箱分离，未见新箱体挡住开盖或穿入前舱；货箱盖面保持连续 | hard-surface #11；lighting §3/7 |

没有本切片需要返工的视觉发现，因而没有缺陷严重度或新增修改要求；不能把“未见回归”扩写为未展示方向/动态全部通过。

## 未验边界

动态旋转、移动时的闪烁与全部维护运动 **NOT_RUN / NOT_ASSESSED**：本批是静态图，单帧不能证明所有动态时刻无伪影；当前 GLB 已独立证明这两个顶面不再共面，故不把动态检查缺口误写为该几何修复未成立。

整车高保真、原完整设备审美、全部动画姿态和 Main 载荷可读性 **NOT_ASSESSED**；新 Godot运行、Blender源重开、Main接入、性能、所有者终审 **NOT_RUN**。本轮 PASS 只绑定上述 GLB 与八张已核图片；资产/相关材质或运行输入改变时需重新限定复审。
