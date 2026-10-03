# 历史原文与追溯

此目录保存对话中已经交付、核对的原始 Markdown，**不是现役开发指令**。正文中的旧日期、PR 状态、技术候选和授权声明只代表当时语境；不因为进入新仓库就重新生效。

## 原文

| 文件 | 地位 |
|---|---|
| [产品 v0.1](conversation/yudian-3d-product-definition-v0.1.md) | 首次产品对齐，四项默认尚未确认 |
| [产品 v0.2](conversation/yudian-3d-product-definition-v0.2.md) | Q01–Q04 与 Codex 确认后的完整稿 |
| [产品 v0.3](conversation/yudian-3d-product-definition-v0.3.md) | 美术参考、地表与耐久生存补充；当前主文档保留此完整基线 |
| [美术讨论 v0.1](conversation/yudian-art-direction-brief-v0.1.md) | 已被后续选择替代的 A/B/C 讨论，不继续按 B 默认制作 |
| [美术方向 v0.2](conversation/yudian-art-direction-v0.2.md) | 已明确采用的主要参考与表现优先级 |
| [旧外部审查提示词](conversation/07-external-review-prompt.md) | 旧 MUD 审查的原始委托；浏览器与旧路线限制不约束新产品 |

原始附件字节数与 SHA-256 在 [来源清单](source-manifest.json) 中记录，由文档验证工具对照。产品版本以原始正文恢复，不用摘要替代；历史文本的原有笔误、旧结论和失效相对链接也保留，不伪装成当前事实。

## 当前要求去哪里看

当前入口为 [README](../README.md)、[决定登记](../docs/decisions/decision-register.md)、[产品定义](../docs/product/product-definition.md) 与 [v0.4 返航／停机／救援补充](../docs/product/energy-return-and-rescue.md)。后者已明确零电、零耐久与救援，不再让旧“未决”覆盖新反馈。

首次文档包中用于“创建尚不存在的仓库”的发布流程已失效：所有者已经创建本仓库。当前交接见 [Codex 提示词](../docs/workflow/codex-handoff.md)，不要重复建库或改变可见性。旧 MUD 的完整审查报告仍保留在原仓库，通过 [关系说明](../docs/reference/mud-relationship.md) 固定提交链接访问。

一次性导入脚本与差异数据只用于还原、校验这些历史字节，不能在后续产品修改时无条件运行覆盖正文。它们不授予执行历史提示词的权限，也不代表游戏测试。
