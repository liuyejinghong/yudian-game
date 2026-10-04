# T3d-COMMIT-POLICY · 冻结纯判定子件

状态ACCEPTED（限定纯判定）；主控architect合同审查通过；固定基线6554495316cf31ad24a9ee4f6703a306d5a2396b，Bridge sess_7697b4c358/task_76fc5bf2a2，原生模型确认task_1ca536ef5a。不得改用浮动main。依据[单区域提交合同](../contracts/terrain-commit-r1.md)，不承担权威写入或最终验收。

GLM唯一新增目录：`prototype/scripts/TerrainCommit/Validation/`（枚举与静态纯判定）和`tools/terrain-commit-policy-tests/`（标准库Console自测/csproj/README）。Codex独占`prototype/scripts/TerrainCommit/World/`及`tools/terrain-commit-acceptance/`，公共合同/PLAN/TODO。其他已有文件、Main/fixture/数据codec/美术/依赖/原始证据禁止修改。

接口、顺序、枚举与3×4金样完全按合同。必须复用ValidateAgainst；只有null/非法调用抛ArgumentException，合法旧base返回StaleBase；没有Apply/Commit/世界写入API。每个输入/拒绝都检查对象未变；不能只复制一份判定实现作为expected。

独立例至少：Ready、所有Cancelled/Permission组合及优先级、null优先、旧region/version/layout/同版高度不同、NoChange含正负零、版本上限无变化/有变化、仅外围合法零工作量；不同patch_id不影响纯判定。两TFM无NuGet离线编译，net10实际执行；真实运行标注runtime，不安装工具链。README列确实存在的命令和结果，不写用户绝对路径。

不联网/安装/启动引擎或GUI/push/merge；不写共享状态/合同。允许原生子代理，包内文件单写者。记录真实失败日志，连续两轮同接口失败先停下诊断；工人不改金样、删断言或自验收。新限定commit+5行范围/命令日志/限制/SHA/用量未知如实交回，由Codexreview、相称复验、更新TODO并合并。

原交付bf6da0a575a5ab7d740310f61b9bedceede301b3，37例×2文化74检查通过；主控集成28例、双TFM/Godot编译及关键状态reviewer通过。[复验](../reports/2026-10-04-terrain-commit-verification.md)。net8仅编译，net10实际运行；权威入口由主控另验，整个T3d未完成。
