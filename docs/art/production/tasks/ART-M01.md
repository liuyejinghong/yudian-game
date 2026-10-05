# ART-M01 · 共享材质试样 r1

状态：ACCEPTED（六资源加载/核参、主控白昼试板通过；所有者最终配色NOT_RUN）。实际制作：原生GLM-Material，经Bridge交付1b91467；主控独立接受见[生产记录](../evidence/art-production-r1-2026-10-04.md)。依赖输入：[首批美术需求r1](../requirements/first-assets-r1.md)中的六行材质表；已安装Godot资源格式可用。无需等待机器人建模或T3合同。

目标：制作六个可编辑共享材质资源并核对试样参数与引用方法；本票不制作模型，不签最终视觉配色。

## 固定输入与文件归属

历史初票基线`9a89601`；本轮需求起点`3999d30`。派单遵循[执行顺序/共同约束](dispatch-r1.md)，主控将本票与需求的实际已提交版本一并作为只读输入，记录实际分支/base与Bridge session/task，再设IN_PROGRESS。独立分支`worker/art-m01-<批次>`，不得沿用工具预检工作树接着领取任务。

本机入口：`$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot`；`PROJECT_ROOT`指协调会话目录，不是Git工作树目录，派单消息给出实际绝对路径。只读版本命令为`"$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --version`，主控已实测`4.7.2.stable.mono.official.ed1daf0bf`。加载检查也用此入口，以独立临时工程执行`--headless --path <临时工程目录> --script <临时检查脚本>`；设DOTNET_ROOT到已核实SDK目录并将其加入PATH，给临时检查指定可写log路径；启动失败保留诊断，不以退出码0独签通过，检查脚本必须输出自身成功标记且无SCRIPT ERROR。工程/脚本由本票为六个资源建立，不安装新工具、不启动游戏Main、不把测试场景提交到仓库。

唯一可写目录：`prototype/assets/materials/art-r1/`。新增六个`StandardMaterial3D`类型`.tres`，文件名分别为`body_light.tres`、`frame_dark.tres`、`rubber.tres`、`accent_warm.tres`、`solar_face.tres`、`soil_mars.tres`，以及`README.md`。不得改Main、project.godot、桥接脚本、旧资源、I00、公共清单/TODO或其他模型；不生成GLB/贴图、不安装依赖、不联网下载、不推送、不合并。

六个角色、颜色目标、粗糙度和金属度按[首批样件候选表](../requirements/first-assets-r1.md)；这只是本次实现输入冻结，不是所有者已视觉认可的最终配色。不透明、无发光、无外部纹理、无自定义shader。README逐项记录实际序列化值、颜色口径、来源（本项目原创参数）、Godot版本与引用方法。sRGB颜色写成RGB字节/255的Color、alpha=1（不手工预转linear）；六.tres的albedo_color和表逐一按浮点误差1e-6核对。上层按[预览接线合同](../requirements/preview-r1.md)的node/surface/role表引用共享.tres，set_surface_override_material逐面绑定，不set整件material_override；本票只说明，不实现模型接入或改glTF导入器。

## 验收与交付

1. 工人报告六个资源各自的加载结果、类型、实际参数及外部依赖。加载检查可在工作树外临时场景完成，不留下生成缓存/临时测试文件的提交；Godot命令若失败保留诊断，不用手工文件存在代替加载通过。
2. 主控独立用已安装Godot加载全部资源，核对六个角色、参数、不透明/无发光/无纹理设置，以及缺失路径明确加载失败。正常六资源分别加载并核类型/颜色/粗糙度/金属度；异常缺一路径或错类型须保留具体诊断，不生成替代材质。资源被接受仅说明可用与一致，视觉比较仍等预览场景，标NOT_RUN。
3. 一次仅提交这个目录；提供commit、文件列表、真实检查命令/结果/未运行项。五行报告说明做了什么、验证什么、限制什么、用量是否可见与提交。不得自己写ACCEPTED或领取下一票。

若颜色序列化/资源加载与目标不一致，先给具体诊断；同一接口两轮失败停下，由主控缩票或修正输入。无需创建新材质框架、自动生成器、通用校验平台或游戏UI。
