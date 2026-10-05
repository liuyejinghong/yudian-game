# 美术目录公开发布副本 · 2026-10-05

所有者授权将当前美术文件夹上传并合并main。本副本从main f5ad71e逐文件导入制作分支8a9f13b相对共同基线的1471个美术变更；不导入根级AGENTS／PLAN／TODO，不覆盖Main、工程配置、共享材质或其他技术文件。素材仍REVIEW；环境／资源缺件见[覆盖盘点](../../requirements/asset-coverage-2026-10-05.md)，正式游戏接入与所有者视觉验收仍未完成。

## 公开副本与原件

原始制作树和真实回执留在本地。本公开副本清除了45份文本与13份Blender源中保存的机器路径；Blender内仅将home字符串换成等长占位符，未改变网格／动画字节。历史m01-board脚本输出改用用户数据目录。清单见[原件／公开件SHA](public-files.json)。

**历史回执中的hash、提交、时间和测试结论属于原始制作时的文件。路径清理后，历史capture_hashes／hashes及历史截图JSON中的manifest hash不能用来核验清理后文档或脚本的字节。** 公布的清理文件用本页清单核验；PNG、运行GLB及生成器字节未改。现役11份manifest的source.hash／source.files已更新为公开Blender源SHA，并重跑交付检查，不把旧hash冒充当前hash。

## 本次真实检查

- 13份Blender源真实重开／再导出：11份现役源见[重开比较](../lowfi-batch-r1/source-reopen.json)，另2份较早试样见[重开比较](older-source-reopen.json)。结构与拓扑一致，最大浮点差1.7881393e-7，小于1e-6；不要求再导出GLB含时间戳的字节完全一致。
- `check_delivery.py`通过：11源、21clip、637已归档姿态、110非法输入、12缺源案例、108已归档净空与284＋3截图身份一致。后五类本次是已归档证据复核，不是再次运行所有状态／渲染采集。
- 独立Godot无缓存[导入](import.log)成功，无ERROR；独立预览[自测](selftest.log)228/228，无SCRIPT ERROR。自测故意传坏JSON，Godot输出一条Parse JSON ERROR并由manifest_neg_bad_json断言接受；旧原始自测也有此行，不能写“全部日志无ERROR”。
- 文档验证通过；新增JSON可解析，公开文件无机器路径／常见token格式；变更范围检查通过。`diff --check`对非日志文件通过；历史日志的原始尾空格／末尾空行保留，不为格式化改写原始诊断。

本次未重新生成概念图／离线渲染／Godot截图；正式Main接入、全场性能、碰撞／导航／保存及最终视觉验收NOT_RUN。合并只是共享素材与制作资料。
