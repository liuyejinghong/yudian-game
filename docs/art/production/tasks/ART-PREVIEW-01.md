# ART-PREVIEW-01 · 独立预览工具

状态：A/B ACCEPTED（工具技术与主控图形），B ed4f856+主控异常清单返修，独立228自测/原8合同/672姿态/Metal采集/原生输入通过；所有者视觉NOT_RUN；真实M01与A主控独立接受见生产记录。GLM工具槽实际执行；主控独立接缝/图形核查。遵循[派单共同约束](dispatch-r1.md)；固定输入为[preview-r1](../requirements/preview-r1.md)和[视觉验收](../requirements/visual-acceptance-r1.md)，不是I00截图或旧Main基准。

独占`prototype/scenes/art_preview_r1/`（PreviewR1.tscn、局部.gd、配置/README及test-only探针），以及`tools/art-preview-r1/`（仅本预览验证/采集脚本）。采用GDScript局部实现，不增加C#公共合同或改项目配置；Viewport级参数设在独立场景里；方向光阴影atlas通过本进程RenderingServer设置，不改共享project.godot。M01目录只读；工程/fixture/I00/他人场景禁写，详见共同约束。工具报告随本票目录，主控维护TODO与验收evidence。

## A切片 · 加载与材质试板

目标：一个明确场景入口，六角色试板；只从manifest加载样件、逐surface接材质、校验preset与完整复位。输入：实际接受的M01六资源与当前需求commit；独占上述两个目录，此片完成释放写权。交PreviewR1入口、加载/验证.gd、自身目录内test-only probe.tscn及manifest、检查脚本/README，不伪装正式机器人，不依赖U01尚不存在的路径。

正常：试板六资源一致；探针一个surface/一socket/静态或局部动作，模拟四phase/七态/两cargo验证接口；同一preset两次、work→disabled→idle与loaded→empty复位且双根不动。异常：空/缺manifest、坏JSON、缺wrapper/GLB/hash不匹配、角色/节点缺失、surface越界/重复/遗漏、未知offline、非法phase/cargo/reason/time、NaN/重复CLI键；退出1且不产生通过图，非法调用保持上一合法姿态。N/A按合同静态复位并记录原因。

验收：主控实际Godot headless导入/运行自测，读取完整诊断；核材质实际属性与每面绑定、根变换不动、同输入幂等。headless结果只签技术接口，图形待B。A接受后提交独立commit，不能自行接B。

## B切片 · 公平比较与采集

目标：按冻结参数实现正常/近景/试板镜头、yaw、GUI切换、evidence/blind图与JSON。输入：A已接受commit与M01；同目录串行独占。交必要局部.gd/配置、采集命令与README，不引入通用编辑器/游戏看板。

正常：实际窗口试板、探针两镜头三方向、状态/phase/cargo切换；Reset恢复默认；同样请求产生同构图/姿态；PNG1920×1200，JSON记录实际后端与hash。异常：空输入、双次相同选择、关闭重开、非法参数、新证据目录撞名、不可写输出；明确错误且不覆盖。blind无答案/图标/socket辅助，evidence完整标注，取指定time而非随机帧。近景截到大设施时遵循局部检查约定，不自动居中缩放。

验收：主控真实GUI试空输入/重复选择/重启，打开实际PNG并检查尺寸/光照/答案隐藏/JSON对应；命令成功且scene参数实录一致，才接受预览。GLM没有真实图形条件可提交工具技术结果并写图形NOT_RUN，不能解除下游“PREVIEW接受”依赖。

未来启动方式（实现后验证，不代表目前存在）：`<Godot> --path <工作树>/prototype --rendering-driver metal res://scenes/art_preview_r1/PreviewR1.tscn -- --material-board`；样件用`--manifest <绝对manifest路径>`替代试板模式；其他参数见preview-r1。工具只生成预览证据，不输出FPS预算或生产资产。
