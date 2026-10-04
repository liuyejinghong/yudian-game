# ART-TOOLS-01 · 只读制作工具预检

批次 art-brief-r1；base `9a896010f4bb7934f97fb6ecd366b1aa44cef2a1`；分支 `worker/art-tools-20261004`。目标：查清现有工具与可编辑源路径，不安装、不制作资产。

执行GLM-Art-A；验收Codex。只可写 `docs/art/production/evidence/art-tools-2026-10-04.json` 和同名 `.md`，报告不超过40行；只提交这两文件。最小资料为本票和 `art/manifests/art_i00.json`，无需再全文读产品路线。禁止修改源码、资产、TODO、项目、依赖、旧证据，不push、不调其他agent。

只读检查：Python3与标准库json/struct；项目现有Godot（project工具目录）精确版本；dotnet可用版本；Blender在PATH及常见Applications路径是否存在。仅`--version`或文件存在检查，不启动GUI、导入工程或下载。检查当前I00源gltf/bin/生成器/GLB/manifest存在且GLB hash匹配，不重新生成或宣称它是生产模型。

JSON字段：`checked_at_utc`、`base_sha`、`tools`（每项available/version/location_hint/check_status；location_hint用project-relative/$HOME或basename，禁止个人绝对路径）、`calibration_sources_present`、`calibration_glb_hash_matches`、`production_pipeline_verified`（本票不验证，固定false）、`limitations`。缺失工具如实available=false；无法检查写NOT_RUN，不把unknown写不存在。不读取凭证/系统序列号。

验收：两个文件JSON可读/字段全、有真实探测方法/版本/缺口且无私人路径；校准hash独立匹配，commit范围仅两文件。检查完成可以ACCEPTED，但不等于所有生产工具就绪。无Blender时记录程序化glTF可能路径，由主控结合既有I00证据决定，不擅自断言或安装。

中断只保留检测报告，命令失败不静默改成成功。接口或权限不明停该检查并报告；不自动重试外部进程。交本地commit与5行报告，无模型/源生成。主控接受前不写ACCEPTED。

## 2026-10-04 接受记录

实际ZCode会话`sess_f8bf50cf1f`原生确认GLM-5.3-Flash；Bridge未提供observed_model，模型身份仅有原生确认。首任务`task_7cdf0293b3`用时281秒、提交`dd49b34`；主控发现Godot所在协调根目录与生成器tools路径误判，限定返工`task_e8e48e03e2`用时72秒，追加修正提交`000fcbb4e56df5f985e80fa2c72d6543084712f5`。Bridge最后usage.used=31512，是工具读数，不当作计费token或费用；实际费用未知。

主控独立运行版本检查，核对两个文件的JSON/范围、五个校准文件存在、GLB与manifest hash，以及生成器hash；接受修正后的最终快照。原始和返工提交保留在工人分支，不把第一版误判当当前事实。主控另将报告中的“未启动”明确为“只运行--version，未启动GUI/导入工程”，初测时间不冒称返工或独立复核时间。

[报告](../evidence/art-tools-2026-10-04.md)及[JSON](../evidence/art-tools-2026-10-04.json)：仅预检ACCEPTED。Godot/Python/.NET可用；Blender在检查路径未找到；生成器与GLB存在且hash独立匹配。没有新资产、导入/导出、生成复验或视觉结果，本票production_pipeline_verified=false。会话已结束，不自动领取下一票。
