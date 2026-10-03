# 文档导入与检查记录

日期：2026-10-03。执行环境：GitHub Actions Ubuntu；只处理文档，不运行游戏。

- 原始产品 v0.1／v0.2／v0.3：与交付附件的字节数和 SHA-256 一致。
- 原始美术 v0.1／v0.2 与旧外部审查提示词：与交付附件的字节数和 SHA-256 一致。
- 当前产品主文档：完整保留 v0.3 正文，不是摘要；最新 v0.4 补充由 README 和决定登记明确优先。
- 当前内部 Markdown 文件链接、必要文档、历史六份源文件校验：通过。
- 原有 A01–A24、新增 B01–B08 编号：完整，共 32 个需求场景，不是已跑游戏测试。
- 文档验证工具自测试：7 项通过。

工作流：[run 37129584775](https://github.com/liuyejinghong/yudian-game/actions/runs/37129584775)。
输入提交：`2572b3c794ab74edebb864ac5344a05b1dff17d9`。实际写回提交由本报告所在 Git 历史确定。

复查命令：`python3 tools/verify_documents.py` 与 `python3 -m unittest discover -s tools -p 'test_verify_documents.py'`。

Godot／.NET 原生构建、机器人模拟、真实模型、地形导航、MetalFX 等接入、低配性能和真人试玩：**NOT_RUN**。
未修改旧 MUD 仓库、部署或数据库；未更改新库可见性，未添加开源许可证。一次性导入工作流完成后移除，保留脚本与哈希供审计，不作为未来游戏 CI。
