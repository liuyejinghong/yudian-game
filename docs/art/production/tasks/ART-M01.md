# ART-M01 · 共享材质试样 r1

状态：READY，仅准备可派单票，尚未执行。拟执行：GLM-Material；接口/接受：Codex。依赖输入：[首批美术需求r1](../requirements/first-assets-r1.md)中的六行材质表；已安装Godot资源格式可用。无需等待机器人建模或T3合同。

## 固定输入与文件归属

代码基线`9a896010f4bb7934f97fb6ecd366b1aa44cef2a1`；派单前主控将本票与需求的已提交版本一并作为只读输入，记录实际分支/base与Bridge session/task，再设IN_PROGRESS。独立分支`worker/art-m01-<批次>`，不得沿用工具预检工作树接着领取任务。

本机入口：`$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot`；`PROJECT_ROOT`指协调会话目录，不是Git工作树目录，派单消息给出实际绝对路径。只读版本命令为`"$PROJECT_ROOT/tools-bin/Godot.app/Contents/MacOS/Godot" --version`，主控已实测`4.7.2.stable.mono.official.ed1daf0bf`。加载检查也用此入口，以独立临时工程执行`--headless --path <临时工程目录> --script <临时检查脚本>`；工程/脚本由本票为六个资源建立，不安装新工具、不启动游戏Main、不把测试场景提交到仓库。

唯一可写目录：`prototype/assets/materials/art-r1/`。新增六个`StandardMaterial3D`类型`.tres`，文件名分别为`body_light.tres`、`frame_dark.tres`、`rubber.tres`、`accent_warm.tres`、`solar_face.tres`、`soil_mars.tres`，以及`README.md`。不得改Main、project.godot、桥接脚本、旧资源、I00、公共清单/TODO或其他模型；不生成GLB/贴图、不安装依赖、不联网下载、不推送、不合并。

六个角色、颜色目标、粗糙度和金属度按冻结表；不透明、无发光、无外部纹理、无自定义shader。README逐项记录实际序列化值、颜色口径、来源（本项目原创参数）、Godot版本与引用方法。写明上层预览如何按稳定角色将材质用于MeshInstance3D；不要在本票接入游戏或擅自改变glTF导入器。

## 验收与交付

1. 工人报告六个资源各自的加载结果、类型、实际参数及外部依赖。加载检查可在工作树外临时场景完成，不留下生成缓存/临时测试文件的提交；Godot命令若失败保留诊断，不用手工文件存在代替加载通过。
2. 主控独立用已安装Godot加载全部资源，核对六个角色、参数、不透明/无发光/无纹理设置，以及缺失路径明确加载失败。资源被接受仅说明可用与一致，视觉比较仍等预览场景，标NOT_RUN。
3. 一次仅提交这个目录；提供commit、文件列表、真实检查命令/结果/未运行项。五行报告说明做了什么、验证什么、限制什么、用量是否可见与提交。不得自己写ACCEPTED或领取下一票。

若颜色序列化/资源加载与目标不一致，先给具体诊断；同一接口两轮失败停下，由主控缩票或修正输入。无需创建新材质框架、自动生成器、通用校验平台或游戏UI。
