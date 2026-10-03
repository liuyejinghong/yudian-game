# 独立 3D《余电》与旧 MUD 的关系

日期：2026-10-03｜性质：产品谱系与参考边界。

## 1. 新产品不是旧 MUD 的换皮或下一部署分支

新产品面向 macOS 原生单人、目标委托、实时三维、地表改造与机群保障。所有者已创建独立仓库 `liuyejinghong/yudian-game`，本次 GitHub 连接确认有写入权限，仓库当前公开；不改变其可见性。

旧仓库 `liuyejinghong/ai-mud` 仅为历史原型、产品问题和实现参考。新项目不 fork，不引入其 .git 历史、运行代码、依赖、数据库、账号、线上环境或旧 AGENTS 作为现役约束。两个仓库不会组成必须共同运行的系统。

## 2. 哪些经验值得继承

资源守恒、预留与产出分离、任务真实状态、计划权限、幂等效果、保存恢复、候选校验、模型迟到拒绝、问题可追溯。这些是规则与测试语义，可以重新实现和验证，不等于必须继续使用原来数据库与网络架构。

火星题材、机器人分工称呼、缺料前置与维护等内容可以作为候选素材；旧版数值、开局数量、年代与设施清单并不自动成为新产品已确认设定。

尤其保留“能运行不等于想玩”的教训。旧 AI 评审、历史 CI、旧单元测试和收尾报告不能当成新产品功能、画面或模型效果已验证。

## 3. 明确不继承什么

文字形态、网页注册登录、Fastify/PostgreSQL 运行前提、控制租约、先把 M1–M4 全做完、固定旧时间换算、Phaser、32px、Windows 优先、AI 只做后置通讯、无限免费回电或把新手强制受损当验收。

以后复用任何代码、资源或数据，需单独说明来源、许可、迁移范围和测试，不能整仓复制再改项目名。

## 4. 旧仓库读取记录，不冒充本轮现状

v0.3 文档包准备时，通过 GitHub 连接读取 PR #41：Open、未合并；HEAD `7e6ca34e5257f021502d88996cff5df2210604cc`，base `c6bd33e416adfe186fd1cd22013bbfbd22bc7cae`，分支已包含 `closeout-report.md`。这是当时读取记录，不表示本次上传又重新核验了旧 PR 最新状态。

收尾报告自述 `CLOSED_AS_PLAN` 仅表示旧排期交接，不等于游戏好玩、缺陷清零或新游戏实施。报告中的旧测试成绩属于原执行者记录，本次没有重新运行。

本次没有改旧仓库，没有合并／关闭 PR #41，没有归档仓库、停止服务、删除存档、撤销权限或修改旧项目工作树。用户说“仅参考”不等于授权破坏旧资料或生产。

## 5. 固定参考链接

- [旧仓库](https://github.com/liuyejinghong/ai-mud)
- [原游戏性诊断](https://github.com/liuyejinghong/ai-mud/blob/c6bd33e416adfe186fd1cd22013bbfbd22bc7cae/docs/reviews/base-operations/2026-10-01-fun-diagnosis/report.md)
- [原 Jev 协作方案](https://github.com/liuyejinghong/ai-mud/blob/c6bd33e416adfe186fd1cd22013bbfbd22bc7cae/docs/implementation/2026-09-19-base-operations/v0.14.0-jev-cooperation.md)
- [PR #41 独立审查](https://github.com/liuyejinghong/ai-mud/blob/7e6ca34e5257f021502d88996cff5df2210604cc/docs/reviews/base-operations/2026-10-03-external-playability-review/report.md)
- [旧路线收尾报告](https://github.com/liuyejinghong/ai-mud/blob/7e6ca34e5257f021502d88996cff5df2210604cc/docs/implementation/2026-10-03-2d-transition/closeout-report.md)

这些链接是参考，不是新产品排期入口。旧报告保留在旧仓库；新库只存必要说明与用户已交付文档的原样快照。
