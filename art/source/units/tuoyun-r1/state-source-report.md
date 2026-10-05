# ART-U01-STATE-A · 源动画与 GLB 交付报告

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 3与本轮实测见[mars-r2-report](mars-r2-report.md)。

**历史源交件记录；当前主控r9资产与独立检查见[state-report](state-report.md)。**

日期：2026-10-04。执行：GLM STATE-A 工人（本票缩减版：仅源动画与同步 GLB；wrapper/manifest
等属 STATE-B 另派）。状态：**SUBMITTED**，主控独立核验/接受在工人之外；不能自签 ACCEPTED。
基线 commit 2051d7f（GEO 已接受），原生成源/几何保留并在此基础上最小修改。

## 交付物（仅两目录）

- `art/source/units/tuoyun-r1/`：generate_tuoyun_r1.py（+动画/局部改动）、tuoyun-r1.gltf+.bin
  （可编辑源，bin 仅随 .gltf）、tuoyun-r1.glb（自包含，JSON buffer 无 uri）、geometry-facts.json、
  geometry-report.md（追加 STATE-A r5 节）、本报告。
- `prototype/assets/units/tuoyun-r1/tuoyun-r1.glb`：与源 GLB 字节相同
  （sha256 `506c02ae21baff90cdfb446b7cb19a4a05e4456c65e4c36dcb02f4bbdd775922`）。

## 七态在源中的命名与实现

| 态 | 源实现 | 真实 clip / duration / loop |
|---|---|---|
| idle | 源闭合静姿（无 clip） | 无（manifest 层 clip=null, duration 0，由 STATE-B 填写） |
| move | `move`：四轮绕各自局部X，1s；keyframes t 0/.25/.5/.75/1 = 0/90/180/270/360° | move / 1.0 / 设计循环 |
| work | `work`：RearLock 暖横杆绕局部X +25°，t1 定格 | work / 1.0 / 非循环 |
| charge | `charge`：ChargeCap 绕局部Z +60°（顶铰链 0.48,0.58,-0.35）向+X 开 | charge / 1.0 / 非循环 |
| disabled | `disabled`：HoodLid 绕局部X -35° | disabled / 1.0 / 非循环 |
| towed | **复用 `disabled` clip** + TowRod（源内真实 0.35m 前杆沿 Socket_TowFront -Z） | disabled（复用）/ 1.0 / 非循环 |
| maintenance | `maintenance`：HoodLid 绕局部X +70° | maintenance / 1.0 / 非循环 |

关键实现事实（防"extras 假声明"）：

- clip 是 glTF animations 真通道（LINEAR 四元数轨），不是 extras 注记；Godot 实测
  `AnimationPlayer.get_animation_list() == ["charge","disabled","maintenance","move","work"]`。
- move 首尾四元数不同（q(360°)=(0,0,0,-1) 双覆盖），LINEAR 插值才能整圈真实转动；生成器自检
  含逐档"旋转测试向量"核对与相邻档 dot>0.7 短弧断言。**GLB/glTF 核心无 loop 标志**：Godot 导入
  loop_mode=0（none）是引擎默认而非设计声明，move 的 1s 循环由 STATE-B 播放端开启（canonical
  manifest `states.move.loop=true` 层面表达）。t.25/.75 恰为 90°/270°，帧间可辨（星形毂+环形胎）。
- Socket_TowRear 烘 Y180°（实测 fwd=(0,0,1)）、Socket_Charge 烘 Y-90°（实测 fwd=(1,0,0)）；
  Cargo/TowFront identity（fwd=(0,0,-1)），四 socket up 均 +Y、位置不变（audit 逐项 PASS）。
- TowRod extras.visible=false：**Godot 4.7.2 导入器不采用该 extras，实测 visible=true**；
  glTF 2.0 核心无节点可见性，默认隐藏是 STATE-B wrapper 的复位职责（如实记录，不假称源已隐藏）。
- 有限运动穿插（离散端点，非连续证明）：work 25° 横杆 vs 导向柱/后横梁/后护栏 gap 0.01/0.0188/0.0171；
  charge 60° 盖铰链角与充电罩原位接触 0/-0.0；GEO 106 角盖采样（含后壁）保留且重跑通过。
  局部几何调整（横杆 0.035 高、充电盖 0.015 厚内侧面=铰链平面、导向柱入 Body、货箱外沿 0.64/0.70/0.38）
  详 geometry-report.md r5 节。

## 实际命令与结果（证据目录 art-preview-evidence/u01-state-a-worker-20261004/）

```
python3 generate_tuoyun_r1.py        # 自检（SAT/绕向/冻结尺寸/动画结构）→ OK，两次重跑 diff 空
python3 run_import_and_audit.py      # subprocess，env DOTNET_ROOT=<user-home>/.dotnet
  Godot --headless --editor --import --quit --path tmp-project   # timeout=35s → 实际 exit 0，无超时
  Godot --headless --path tmp-project --script res://audit.gd   # timeout=20s → 实际 exit 0，无超时
```

- 空工程 tmp-project 只放单份 GLB + project.godot + audit.gd（GDScript-only，未编译游戏、未跑 GUI）。
  导入真实发生：`.godot/imported/tuoyun-r1.glb-*.scn` 生成，104 步场景导入，无 glTF/SCRIPT 错误。
  启动时有一行 mono 胶合层 `ERROR: .NET Sdk not found. The required version is '10.0.12'.`
  （本机 ~/.dotnet 为 10.0.401；工程无 C#，该行与 glTF 导入无关）——已如实保留在 import.log，
  未以 exit 0 单独判过。
- audit.gd（SceneTree 首帧运行）实测并打印：clip 名单/长度（5×1.0）/loop_mode=0、每 clip rotation3d
  轨（导入器给 7 轨=全动画节点集）、move t.25 每轮 =q_x90、t.75 =q_x270 且两时刻姿态不同、
  work/charge/disabled/maintenance t1 =25°/60°/-35°/+70° 四元数、四 socket fwd/up/pos、
  TowRod 存在（visible=true）、全部 seek 后 wrapper 级根 identity、四轮位置不变。**AUDIT_PASS 45/45，
  exit 0**（audit.log）。
- 过程事实：曾用 `--headless --import --path .`（无 `--editor`/`--quit`）两次失败（no main scene，
  由主控停止原 turn）；本片按新指示改用空工程 `--editor --import --quit` 一次通过，未复跑旧命令。
  audit 初版两处脚本错误（`_init` 时机实例化子树为空、GDScript lambda 按值捕获致递归失效）已在
  tmp-project/dump.gd 取证节点树后修正，最终 45/45；错误与修正过程不掩盖。

## NOT_RUN（STATE-B 或主控范围，未做不假称）

- preview wrapper（preview.tscn/preview.gd、apply_preview、六键校验/复位/绝对时间 seek/非法保持）
  与七态往返/重复输入不叠加检查：**NOT_RUN（STATE-B）**。
- canonical manifest（bounds.active 实测包络、material_bindings 真实路径、states 七键）：NOT_RUN
  （STATE-B）；本片未写 art/manifests/。
- 材质 surface 接线（六 M01 共享资源加载/每 surface 恰一绑定）：NOT_RUN（STATE-B）；
  本片 GLB 仍为 4 占位角色材质、0 纹理。
- 实际窗口/Metal 渲染截图与视觉矩阵、正常镜头/近景、所有者视觉确认：NOT_RUN（STATE-B 后主控）。
- move 循环在播放端的实际循环表现、towed 杆可见性切换在包装层的复位表现：NOT_RUN（STATE-B）。
- 连续扫掠（非 1° 离散采样）的数学证明：NOT_RUN（沿用 GEO 口径：离散采样+端点 SAT）。
- 游戏接入/性能/模拟规则：本票之外。

## 来源与可复现

原创 Python 标准库生成器（无第三方/无下载/无生成式服务）；`animations` 由同一生成器写入，
源 .gltf/.bin/.glb 与 prototype GLB 同批导出、两次重跑字节级一致
（source-hashes.txt）；Godot `4.7.2.stable.mono.official.ed1daf0bf` headless 实测。


## 主控r6 · 当前源技术接受（2026-10-04）

2b2abd6原STATE-A提交已实际读取（1181秒，上下文used161065非计费；工人估40分钟不采用）。主控局部修轮胎144个逆向橡胶三角（端环96/内孔48），补按实体面方向独立判定；顶点/车体尺度未改。恢复后轮vs后侧伸板的正确轮位断言，原错误写成前轮。真实glTF move命名`move-loop`，按[Godot导入命名惯例](https://godotengine.org/article/importing-3d-assets-blender-gamedevtv/)并实际在4.7.2单GLB新工程导入核为`move` loop_mode=1，非循环四clip=0。没有只用loop_intent假声明。

主控bounded空工程导入/网格/clip读取三步exit0、无ERROR/SCRIPT ERROR，12 mesh/56 surface/1524 triangles/4 socket。实际5clip均1s、move quarter姿态不同/根固定；另t1四动作basis、实际货箱.64×.70×.38/socket位置方向通过。源重跑byte一致；轮面192三角物理朝向全部通过。固定close Metal原PNG实际看轮胎侧环闭合、没有旧扇瓣缺面。记录见 docs/art/production/evidence/production-r1-receipts/u01-state-a-accepted-r6/ 。

仅源/局部动作技术接受。wrapper/七态复位/非法保持/最终manifest与active包络、完整normal视觉/所有者效果/游戏/性能仍NOT_RUN，不能用static-only临时包装代签七态。原r1/r3/r5与工人45项报告为历史，当前hash如下。

| 当前文件 | SHA256 |
|---|---|
|generate_tuoyun_r1.py|`61f9e895a0aac0ee0fef4bea4b9c9c7f1aa5a64a9d9ee6ecb579fad244a92f38`|
|tuoyun-r1.gltf|`179a98f5f2c1a425f8729e1eaa3cef09dc2d97749d85fc9839c4baf5f90562f3`|
|tuoyun-r1.bin|`fc70ad7e3ec404abbdb2945aaf0e2fcedc15f95c62f59969870011f928821c90`|
|tuoyun-r1.glb|`d28c38b02ca298e427dc8562ecdcdd0d4785018b3aa6dc54e7136c523a18dac5`|
|geometry-facts.json|`b4e81faf07bfa8484a295fec3a66494f4666c9cc61ddb19d43f1917be8274086`|
