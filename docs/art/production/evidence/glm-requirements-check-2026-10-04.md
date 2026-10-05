# GLM 冻结文档机械清单核对 2026-10-04

执行者：GLM-5.3-Flash（会话内 `/model GLM-5.3-Flash` 已确认）。性质：冻结文档机械清单核对，不承担美术设计与最终视觉验收；不生产资产/代码/工具，不联网/安装/推送。本文是本会话唯一新增文件；所有需求/任务/TODO/PLAN 只读，未改动。

## 读取基线（真实 git 事实）

| 项 | 值 |
|---|---|
| HEAD（读取时） | `adabb1b64b0b376ba31b5ad58ed1c3e66452bb44`（adabb1b docs(art): complete first-sample requirements and bounded production tickets） |
| merge-base HEAD ↔ origin/main | `3999d302b4066d92ec7582eb8a62a400d9b91d9d` |
| origin/main | `3999d302b4066d92ec7582eb8a62a400d9b91d9d` |
| 工作树 | 读取前 clean；派单起点 `3999d30`（dispatch-r1.md L23）与实测 base 一致 |
| 分支 | `art/requirements-r1-20261004` |

已读文件：`AGENTS.md`；`docs/art/production/requirements/{first-assets-r1,preview-r1,visual-acceptance-r1}.md`；`docs/art/production/tasks/{dispatch-r1,ART-M01,ART-PREVIEW-01,ART-U01,ART-F01,ART-F02}.md`；`docs/art/production/todo.md`。

## 已跑命令与结果

`python3 tools/verify_documents.py` → `PASS: required documentation, current local links, six archived source hashes, A01-A24 and B01-B08. Game/native/model/performance/playtest results: NOT_RUN.` 退出码 0。该结果只是文档完整性检查，不替代主控验收，不证明任何视觉/生产结果。

## 每票清单核对（8 项：目标/输入/独占目录/禁写/交付格式/正常与异常例/验收/依赖）

行号对应读取时 HEAD `adabb1b` 的文件内容。

### dispatch-r1.md（派单索引票）

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有（索引形式） | L7–17 切片表含工作结果列；L19 串行/并行与停止规则 |
| 输入 | 有 | L3（三份需求+对应任务页）；L7「输入就绪/解除条件」列 |
| 独占目录 | 有（间接） | L19 分目录并行/单一写者；L23 派单记录要求「唯一写者和目录」；逐票目录由各票承载 |
| 禁写 | 有 | L29 共同禁写清单（根PLAN/TODO、AGENTS、Main、project.godot/csproj/fixtures、I00/桥/注册表、docs/engineering/、archive、公共材质（M01除外）等） |
| 交付格式 | 有 | L31–33（GEO/STATE 交付物、geometry-report.md、证据新 run 目录、commit 归属） |
| 正常与异常例 | 缺（本票无） | 索引票属性；正常/异常例由各任务票及 preview-r1.md L56 承载。机械记录，不判冲突 |
| 验收 | 有 | L25（五行结果；主控读 diff 与关键证据独立复核）；L32（同接口两轮失败停止） |
| 依赖 | 有 | L7–17 解除条件列；L19「没有资源或真实预览依赖时，即使文本完整也不把生产片写READY」 |

### ART-M01.md

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有（无显式「目标」小节） | 目标由交付定义承载：L11（六个 `.tres` + `README.md`）、L13（六角色参数实现与核对口径）。机械记录 |
| 输入 | 有 | L3（六行材质表；已安装 Godot）；L7（基线 `9a89601`、起点 `3999d30`）；L9（Godot 入口与实测版本 `4.7.2.stable.mono.official`） |
| 独占目录 | 有 | L11 唯一可写 `prototype/assets/materials/art-r1/` |
| 禁写 | 有 | L11（不得改 Main/project.godot/桥/I00/公共清单/其他模型；不生成 GLB/贴图、不装依赖、不联网、不推送、不合并） |
| 交付格式 | 有 | L11（六 `.tres` + README）；L19（commit、文件列表、真实检查命令/结果/未运行项、五行报告） |
| 正常与异常例 | 有 | 正常 L17（六资源加载/类型/实际参数）；异常 L18（缺一路径或错类型保留诊断、不生成替代材质） |
| 验收 | 有 | L17–19（工人报告 + 主控独立加载核参；不得自己写 ACCEPTED）；L21（两轮失败停下） |
| 依赖 | 有 | L3（无需等待机器人建模或 T3 合同）；dispatch-r1 L9（READY 条件） |

### ART-PREVIEW-01.md

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有 | A 切片 L9；B 切片 L17 |
| 输入 | 有 | L3（preview-r1 + visual-acceptance-r1）；A L9（实际接受的 M01 六资源 + 当前需求 commit）；B L17（A 已接受 commit 与 M01） |
| 独占目录 | 有 | L5（`prototype/scenes/art_preview_r1/`、`tools/art-preview-r1/`）；A 完成释放写权 L9 |
| 禁写 | 有 | L5（M01 目录只读；工程/fixture/I00/他人场景禁写，指向 dispatch-r1 L29 共同约束） |
| 交付格式 | 有 | A L9（PreviewR1 入口、加载/验证 .gd、test-only probe.tscn 及 manifest、检查脚本/README）；B L17（局部 .gd/配置、采集命令与 README） |
| 正常与异常例 | 有 | A L11（正常/异常逐条）；B L19（正常/异常逐条，含撞名拒绝、不覆盖） |
| 验收 | 有 | A L13（headless 签技术接口、图形待 B）；B L21（真实 GUI 验收；图形未跑写 NOT_RUN，不解除下游依赖） |
| 依赖 | 有 | L3（BLOCKED：真实 M01 未制作/接受）；dispatch-r1 L10–11（A←M01 接受；B←A 独立技术接受） |

### ART-U01.md

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有 | GEO L9（前低头/开放后台/四轮/独立货箱、四个局部socket）；STATE L19（七态全提供及各态部位） |
| 输入 | 有 | L3（三份需求+派单约束）；GEO L11（前置+接受的六资源与预览commit）；STATE L21（GEO 接受 commit 且目录释放） |
| 独占目录 | 有 | L5（`art/source/units/tuoyun-r1/`、`prototype/assets/units/tuoyun-r1/`、`art/manifests/tuoyun-r1.json`；GEO/STATE 串行独占） |
| 禁写 | 有 | L5（不写其他模型/公共道具材质/桥/Main/project/fixture/根TODO/PLAN/工程合同，详见共同约束 dispatch-r1 L29） |
| 交付格式 | 有 | GEO L11（源/生成器、真实 GLB、geometry-report.md 全清单）；STATE L21（preview.tscn/.gd、最终 canonical manifest、state-report.md、新 run 证据） |
| 正常与异常例 | 有 | GEO L13（正常/异常案例）；STATE L23（正常/异常案例，含未知第八态拒绝） |
| 验收 | 有 | GEO L15（GLM 仅交 SUBMITTED、主控接受后释放写权）；STATE L25（技术/主控可读性/所有者视觉三结果；未看不写通过） |
| 依赖 | 有 | L3（BLOCKED：M01 与 PREVIEW 独立接受尚未齐备）；GEO 前置 L11；STATE 前置 L21；dispatch-r1 L12–13 |

### ART-F01.md

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有 | GEO L9（中央脊/四脚/两侧各两折叠翼/束带/检修箱；packed/installed/completed 静态端点；PowerOut/Service 两 socket）；STATE L19（四建设阶段和独立七态） |
| 输入 | 有 | L3；GEO L11；STATE L21（同 U01 结构） |
| 独占目录 | 有 | L5（`art/source/facilities/solar-r1/`、`prototype/assets/facilities/solar-r1/`、`art/manifests/solar-r1.json`；串行独占） |
| 禁写 | 有 | L5（同上，指向共同约束） |
| 交付格式 | 有 | GEO L11；STATE L21（同 U01 结构） |
| 正常与异常例 | 有 | GEO L13；STATE L23（同 U01 模板） |
| 验收 | 有 | GEO L15；STATE L25（同 U01 结构） |
| 依赖 | 有 | L3（BLOCKED：U01 最终导入/比例复核通过 + M01/PREVIEW 接受尚未齐备）；dispatch-r1 L14–15 |

### ART-F02.md

| 项 | 判定 | 行号与依据 |
|---|---|---|
| 目标 | 有 | GEO L9（3.6×4.8×2.4 候选；三级体量；Input/Output/PowerIn/Service 四 socket）；STATE L19（四表现；标签 offline 只映射 disabled、输入 offline 拒绝） |
| 输入 | 有 | L3；GEO L11；STATE L21（同结构） |
| 独占目录 | 有 | L5（`art/source/facilities/processor-r1/`、`prototype/assets/facilities/processor-r1/`、`art/manifests/processor-r1.json`；串行独占） |
| 禁写 | 有 | L5（同上，指向共同约束） |
| 交付格式 | 有 | GEO L11；STATE L21（同结构） |
| 正常与异常例 | 有 | GEO L13；STATE L23（同模板） |
| 验收 | 有 | GEO L15；STATE L25（同结构） |
| 依赖 | 有 | L3（BLOCKED：同 F01 前置）；dispatch-r1 L16–17 |

## 指定交叉核对项

### 六材质

| 来源 | 位置 | 内容 |
|---|---|---|
| first-assets-r1.md | L23–30 | 六角色表：body_light #D8DCD6 / frame_dark #38454B / rubber #24292B / accent_warm #D77B3D / solar_face #203B52 / soil_mars #98684D |
| ART-M01.md | L11 | 六文件名逐一对应同六角色 |
| preview-r1.md | L17、L30 | 试板顺序六角色与稳定六角色字典、`res://assets/materials/art-r1/<role>.tres` 引用路径 |

判定：**一致**。三处角色名逐一相同；路径约定 `res://assets/materials/art-r1/` 与 M01 票唯一可写目录 `prototype/assets/materials/art-r1/` 对应，无冲突。

### U01 四 socket 与七态

| 来源 | 位置 | 内容 |
|---|---|---|
| first-assets-r1.md | L52–57 | Socket_Cargo(0,0.48,0.28)、Socket_TowFront(0,0.24,-0.80)、Socket_TowRear(0,0.24,0.80)、Socket_Charge(0.48,0.55,-0.35)，各含 forward/up |
| first-assets-r1.md | L61–69 | 七态表：idle/move/work/charge/disabled/towed/maintenance，七行全列 |
| ART-U01.md | L9 / L19 | GEO「四个局部socket」；STATE「七态全提供」 |
| preview-r1.md | L38 | 状态仅七键，大小写精确 |

判定：**一致**。数量（4 socket、7 态）与名称三处吻合；preview-r1 L34 manifest `states` 七键与之一致。

### F01 四 phase 与七态分离

| 来源 | 位置 | 内容 |
|---|---|---|
| first-assets-r1.md | L81–88 | 四阶段 packed/installed/deploying/completed；L88「运行态与phase分别输入，不拼成第八状态」；deploying 4 秒由外部 phase_t 寻址，「动画结束不改变phase」 |
| preview-r1.md | L40 | phase 仅四项；phase_t∈[0,1] 仅对 deploying 有意义，其余必须 0；不靠动画播完改 phase |
| ART-F01.md | L19 | 「四建设阶段和独立七态」「状态不重写phase」 |
| visual-acceptance-r1.md | L20–21 | deploying t=0/.5/1；失败情形含「phase因动画结束自动改」 |

判定：**一致**。四 phase 名称、phase_t 约束、phase/七态两维分离在四处表述相同。

### F02 offline 映射

| 来源 | 位置 | 内容 |
|---|---|---|
| first-assets-r1.md | L110 | 展示标签 offline 映射 disabled，「绝不传入第八状态」 |
| preview-r1.md | L38 | 「offline在F02展示标签中映射disabled，输入offline必须拒绝」 |
| ART-F02.md | L19 | 「标签offline只映射disabled，输入offline拒绝」 |
| visual-acceptance-r1.md | L22 | 失败情形含「offline被当第八状态」 |

判定：**一致**。四处均为「标签映射 disabled + 输入拒绝」，无一处把 offline 当第八状态。

### READY/BLOCKED 与生产授权不混

| 票/文档 | 状态声明 | 一致性 |
|---|---|---|
| dispatch-r1.md L9 / ART-M01.md L3 / todo.md L49 | ART-M01 = READY | 一致 |
| dispatch-r1.md L10–17 / ART-PREVIEW-01 L3 / ART-U01 L3 / ART-F01 L3 / ART-F02 L3 / todo.md L44–48、L59 | 其余切片与票全部 BLOCKED | 一致 |
| dispatch-r1.md L3、L19、L23；todo.md L9 | 未来票非已派出；文本完整不写 READY；READY 不会自动启动；启动由主控记录 | 一致 |

判定：**一致，未混**。无任何票自称 IN_PROGRESS/ACCEPTED 生产状态；todo L19 的 ART-I00=ACCEPTED 与 L57 ART-TOOLS-01=ACCEPTED 属既往技术校准/预检，非本轮首批生产票，与 dispatch 状态表无冲突。

## 其他客观一致项（抽查）

1. 相机：normal (18.3848,28,18.3848)→(0,0,0) FOV60、close (3,3,4)→(0,0.4,0) FOV50、1920×1200、KEEP_HEIGHT（first-assets-r1 L15 ↔ preview-r1 L11）。
2. Godot 4.7.2：preview-r1 L9（候选）↔ ART-M01 L9（实测 `4.7.2.stable.mono.official`）↔ todo.md L57。
3. 时刻锚点：U01 move t=.25/.75、work t=1（first-assets-r1 L64–65 ↔ visual-acceptance L19）；F02 work 2 秒循环 t=.5/1.5（L109 ↔ visual-acceptance L22）；F01 deploying 4 秒 t=0/.5/1（L85 ↔ preview L40 ↔ visual-acceptance L20）。
4. socket 计数：F01 两个（ART-F01 L9 ↔ first-assets-r1 L90）；F02 四个（ART-F02 L9 ↔ first-assets-r1 L104）。
5. 独占目录互不重叠：tuoyun-r1 / solar-r1 / processor-r1 三组源/资产/manifest 路径互斥，与 M01 材质目录、preview 两目录互斥；dispatch L19「F01/F02分目录可并行」成立。
6. GEO/STATE 分工一致：三票 GEO 均「不需 preview wrapper 或最终 manifest，动画留 STATE，报告标尚未制作，不伪装N/A」（各 L11），STATE 均交 wrapper/manifest/state-report（各 L21）；与 dispatch L19 一致。
7. preset 键与 reason 枚举：preview-r1 L38、L42（none/return_charge/no_power/mechanical/both，仅预览注记非模拟 enum）与 first-assets-r1 L64、L67（move 回充、disabled 原因外部图标）一致；ART-U01 L19「两个故障原因只读注记」相符。
8. F02 货箱为固定私有示意（first-assets-r1 L102 ↔ preview L41「不能误称loaded」↔ ART-F02 L9「物件不生成资源」）一致。
9. manifest 路径约定 `art/manifests/<sample>.json`（preview L28）与三票独占 manifest 文件名一致。

## 客观缺项与瑕疵（仅记录，不修改）

1. dispatch-r1.md 本票无独立「正常与异常例」章节：索引票属性，用例由各任务票与 preview-r1.md L56 承载。机械缺项，不判冲突。
2. ART-M01.md 无显式「目标」小节：目标由交付定义（L11、L13）承载。机械记录。
3. U01/F01/F02 三票 GEO「正常案例」句为逐字复制模板：「货物/翼片/压头各有独立节点」（各票 L13）。该清单未按资产裁剪：U01 无翼片/压头，F01 无货物/压头（实为束带/翼片），F02 无翼片。各票「目标」行的实际件枚举正确，模板句不改变交付物定义，也不构成授权/规则冲突；机械瑕疵（复制清单未本地化）。
4. 视觉与生产事实：全部为 NOT_RUN（verify_documents 输出、first-assets-r1 L135、visual-acceptance L3、todo L19–23 相互一致）。本核对未运行任何 Godot/渲染，未产生任何视觉证据，不写 ACCEPTED。

## 会话期间主控并发改动记录

本核对读取的文件内容以 HEAD `adabb1b` 为基准。核对进行中，工作树出现主控（Codex）对 7 个已跟踪文件的并发改动：`evidence/art-requirements-2026-10-04.md`、`requirements/first-assets-r1.md`、`requirements/preview-r1.md`、`requirements/visual-acceptance-r1.md`、`tasks/dispatch-r1.md`、`tasks/ART-F01.md`、`tasks/ART-F02.md`。按授权保留未暂存、不入本核对提交。

对 requirements/tasks 的改动逐项复核：均为同行替换（requirements/tasks 行数不变，本报告引用行号仍有效）。交叉结论复核后不变：六材质、U01 四 socket 七态、F01 四 phase 与七态分离（preview-r1 新增四阶段×七态 `phase_state_modes` 表为强化而非冲突）、F02 offline 映射、READY/BLOCKED 与生产授权不混均维持。`first-assets-r1` F01 段中央服务脊上沿 Y0.6→Y0.85 为本报告未引用数值，不影响任何结论；dispatch L19 新增「U01 单件通过即可解除设施前置、三件关系图齐套后主控补验」不改变各切片 BLOCKED 状态。本报告其余内容不因并发改动重写。

## 结论边界

本报告是机械清单核对记录：逐票 8 项齐备度、指定交叉项一致性、以及上述机械缺项/瑕疵。不构成：主控验收、美术设计裁决、生产授权、或任何视觉接受结论。主控改动与后续派单决策权在 Codex 主控与所有者。
