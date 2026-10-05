# 2026-10-05 · 旧PR验收与集成

PR1/2/3/13/14已分别验收并合并，查询时open队列为0。原分支/提交保留，无强推、删除或生产资产改动。

## 实际合并

| PR | 接受head | merge SHA | 范围 |
|---|---|---|---|
| [#1](https://github.com/liuyejinghong/yudian-game/pull/1) | `01ca6cd8832bde584cc72b64e02426888449b9f5` | `0d7b4b39f2d0f8c69dcef215d74da8b844a61958` | V0历史计划/当前入口 |
| [#2](https://github.com/liuyejinghong/yudian-game/pull/2) | `6d2c23fb4f516355cdc17d42f0375f3227c95c65` | `2801fd83098fc27616921bc08965bf06adcc4811` | 从零研发历史Spec |
| [#3](https://github.com/liuyejinghong/yudian-game/pull/3) | `d4f4294354a494398723a6c51a5d7aa8ebeccc1b` | `997074409bb0fe36ded5b8c1305b9002d8dee715` | 27WebP/12SVG参考归档 |
| [#13](https://github.com/liuyejinghong/yudian-game/pull/13) | `0e5b0a95e30fd0a1f87cc7a9a02bc7a5e996c76d` | `5beb948c0a2304ec4c0f160fd286bbe54b4725f7` | 固定902a662历史评审 |
| [#14](https://github.com/liuyejinghong/yudian-game/pull/14) | `f97d6a417183400ebb572210295096baf9ed5a02` | `8e35483d948f4bd43ac856000209099220df4f20` | 固定902a662历史美术工单/CSV |

旧计划均明确日期与范围，现行TODO/I00/首批需求/生产票优先。PR1唯一workflow冲突保留当前入口；PR2明确空工程起点只是历史；PR13/14原文与CSV保留，已接受QA/I00不重复派单。最新用户授权覆盖旧PR当时“不合并”措辞。

## Standards

独立qa_static_review审查：除历史/现役入口修正外，无新增实质缺陷；参考素材格式/敏感文本核对通过。未重新核验原图裁切、旧实机结果或游戏能力。

## Spec

独立perf_package_review审查：原PR声明交付齐备、无新增阻塞或越界；PR2替代V0旧05/06组织提案，PR3只签参考图/SVG，PR13/14固定旧基线。原会话ZIP/PNG未复验。

两轴剩余发现0；architect前置、重复门禁错误重计划与最终组合复核无剩余阻碍，主控最终验收。

## 实际验证

- 现有verify_documents完整性、本地入口、六份历史来源SHA和A01–A24/B01–B08检查通过；7项工具自测试本批执行通过，工具本体未改。
- 组合新增/修改文档150处本地链接通过。主线快进后全部接受路径与精确head一致；prototype/tools/art/.github组合diff为空，AGENTS只新增Lessons。
- 27WebP完整解码、尺寸和索引Git blob SHA一致；12SVG仅SVG命名空间的svg/title/desc/path，无事件/脚本/外链/url()/DTD/ENTITY；素材字节与原提交一致，旧CSV不改。
- SVG门禁两次遗漏合法title/desc，暂停并经architect独立读实际元素后重计划；修正验证器完整重跑通过，未删除元数据或改素材。旧main既有日志空白不改，本批有效差异检查通过。
- 五张PR的GitHub检查列表均空，不称CI通过；合并依据为实际检查和独立审查。本轮未启动游戏/GUI或重复性能采样；参考归档不签模型生产/视觉/游戏功能。

任务表仍保留PERF受控/归因/稳定预算、完整世界/导航/保存及正式美术范围；PR清理不扩签它们。工程负责游戏测试，合格PR及时集成。
