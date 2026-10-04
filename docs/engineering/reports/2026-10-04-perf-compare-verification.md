# PERF-01-COMPARE · 限定工具验收

ACCEPTED：只接受只读多轮证据比较器；独立review及最终集成复验通过，按所有者授权独立合并，不等待LIFE包。冻结基线1ba4aa6；原交付ebfb877仅tools/perf_compare/三个文件，未修改公共合同/Recorder/measure_baseline/真实证据，依赖仅Python标准库与既有recompute。原门禁27项与三轮报告已读取，不重复用它替代修复后源码验证。

独立审查先复现：requested/applied嵌套漏检、并发输出覆盖及悬空链接被替换、summary顶层数组导致traceback/私路径。GLM修复e1df119后，主控复现新增小数半径误拒绝；按实际FixtureConfig浮点类型修正440b686，并保留完整对象身份。review再复现解析器长度限制异常漏接，3926518接住ValueError/csv.Error；输出保留JSON Unicode转义，孤立代理字符不使UTF8写入崩溃。各轮提交/失败输入保留，未扩配置值域。主控将README“原子写”改为准确的“独占创建”，无源码变化。

主控集成源码cf55502实际50项unittest通过，9项独立复验通过：原漏检/坏顶层、真实三轮、输入SHA不变、已有输出、悬空链接及最终写入入口同步的两个真实进程（恰0+2）。三轮同一身份组，max_ms仍1053.338 / 73.745 / 77.351；不同身份分组与完整配置由单元金样覆盖。最终独立review另实跑3项解析/编码回归，确认原问题关闭、无新增实质缺陷；不重复全量门禁。

[主控检查](../evidence/2026-10-04-evidence-tools/perf-integration-checks.json) / [三轮报告](../evidence/2026-10-04-evidence-tools/perf-golden.json) / [50项日志](../evidence/2026-10-04-evidence-tools/perf-integration-unittest.log) / [独立解析复核](../evidence/2026-10-04-evidence-tools/perf-parser-review.log) / [交付与源码SHA](../evidence/2026-10-04-evidence-tools/perf-receipt.json) / [原失败](../evidence/2026-10-04-evidence-tools/perf-review-before.json) / [原覆盖竞态](../evidence/2026-10-04-evidence-tools/perf-overwrite-before.json)。公开日志脱敏，本机原门禁与三轮返工日志保留。

Bridge原生模型回执GLM-5.3-Flash确认，observed_model未提供；工具上下文用量不等于计费token，费用未知。工人报告本包无需子代理而未使用；授权已保留，不冒称调用。Bridge根目录changed-files包含其他会话/主控变动，归属以实际git diff核定。初次手动workflow不伪造Bridge派单。

父票PERF-01继续DRAFT：实机受控采样、原因裁决与稳定性能未完成；同身份不代表温度/前后台/电源受控。本轮未跑引擎/GUI、不触及美术；保留全部启动尖刺，不作跨组性能提升或达标结论。
