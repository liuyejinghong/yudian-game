# 火星首样 r2 证据

**历史版本：** 本目录固定r2输入及230342f时的检查结果；F01后来升revision3，新数据见[维护r3](../art-solar-maintenance-r3-2026-10-05.md)。旧verify_archive核当前输入时会拒绝已变化F01；复核r2须使用对应230342f源，不覆盖旧索引。


执行结果与限制见[本轮交付](../art-mars-models-r2-2026-10-05.md)。[索引](capture-index.json)记录实际输入、PNG与脱敏前后哈希；[Bridge](bridge.json)记录真实派单取消和主控归属。PNG共33张，31张单件、2张同场。

本轮最终检查：`python3 docs/art/production/evidence/mars-r2-receipts/checks/verify_archive.py`，实际PASS。仓库`python3 tools/verify_documents.py`与`git diff --check`也实际PASS；文档检查本身不证明模型/图形运行。

`checks/check_facility_clearance.py`可从仓库任意位置执行，会重生成两个设施源并核字节一致，输出有限采样净空JSON；不运行Godot。三源生成器自检保留在对应源目录。

Godot实际检查源、私有project.godot与gallery保存在checks；使用仓库内**忽略目录**搭建独立项目：复制checks内gd/tscn/project及independent、inputs的scenes/assets；将当前三个GLB放到对应assets私有目录，并复制inputs/manifests。创建reports目录，实际导入后按资产将对应manifest复制为项目根manifest.json，再运行`--headless --script res://<资产>-actual.gd`。图形采集使用`PreviewR1.tscn`既有`--manifest/--state/--camera/--yaw/--cargo/--time/--capture-dir`参数；保持manifest及源文件原哈希，不改Main。机器路径占位符不是可直接执行的命令。

normal与close采集参数在各PNG同名JSON；gallery的presentation只是展示镜头。没有游戏集成、工程可靠性、性能、完整盲比或所有者新版接受记录。
