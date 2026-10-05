# LIFE-01-AUDIT · 运行产物与退出证据审计器

状态：ACCEPTED（限定离线产物审计器）；冻结技术要求保留，交付/返工/验收见[复验记录](../reports/2026-10-04-lifecycle-audit-verification.md)。

只新增`tools/lifecycle_audit/`内的`audit.py`、标准库unittest、README及少量人工fixture。其他任何文件禁止改动；不改Recorder/Main/measure_baseline/公共文档/美术，不启动引擎/GUI/进程杀停，不安装/联网/push/merge。实际GUI关窗验证由主控完成。

## 输入与输出

`python3 tools/lifecycle_audit/audit.py --manifest MANIFEST.json --output NEW_REPORT.json`

manifest schema_version=1，runs为非空列表。每项包含唯一label、frames_csv、summary_json、exit_record三个路径；相对路径对manifest所在目录解析，不依赖cwd；输出相对路径也对manifest目录解析；可选exit_case为字符串。缺文件保留为缺证，不靠查找近似名字补齐。exit_case有值时exit_record按既有results.json的cases列表精确查case名取exit；无exit_case时exit_record必须含整数exit_code。布尔不当整数、exit_case重复匹配拒绝。工人不要把自己的预期exit作为实际退出证据。

CSV固定四列frame,time_s,frame_ms,fps，完整行序号连续，有限正frame_ms/fps、有限递增time_s；统计调用既有measure_baseline.recompute()（定位仓库导入），不复制统计算法。summary要求schema_version=1、非空run_id。frames/最后time_s、avg_fps、frame_time_ms与独立复算核对：时长1e-5s，avg_fps max(1e-4,abs(expected)*1e-6)，p50/p95/p99/max1e-5ms，over_33ms精确；completed必须exit0、interrupted必须exit130，其他状态或不符明确FAIL。全批非空run_id不得重复；不得凭results.unique_run_ids自报通过。

JSON schema_version=1，overall为PASS/INCOMPLETE/FAIL；runs逐项label、run_id或null、summary_status或null、exit_code或null、verdict、reasons（具体field）、文件SHA256或null、已读完整帧数量。输入路径只保留相对定位，不泄露用户目录。PASS代表产物一致与有相符退出证据，不代表GUI关闭、无覆盖、存档恢复或权限检查已实测。

末行截断只指文件末尾无换行且最后一行不足四列（完整四列无末尾换行仍可有效）；末行四列却类型非法按FAIL，不以截断掩盖。确认末行截断时，仅校验此前完整行，跳过依赖完整CSV的frames/时长/FPS/分位汇总比较；已有独立可证的exit/status矛盾、重复run_id仍FAIL。缺summary/CSV/exit证据、零帧或上述末尾截断CSV为INCOMPLETE，不补造summary、不把硬杀部分CSV当成功；仍检查能读完整部分的格式，摘要与确切退出证据矛盾或重复run_id为FAIL。frame中间断裂、非法数值、未知status、坏JSON/类型是FAIL。FAIL优先于INCOMPLETE；只有全部PASS才overall PASS。CLI/manifest结构无效或输出存在则stderr具体错误、exit2、无报告；有效manifest总能保留审计报告，overall PASS exit0，其余exit1。不覆盖原记录。

## 独立可证完成条件

`python3 -m unittest discover -s tools/lifecycle_audit -p 'test_*.py' -v`；生成清单引用已有`docs/engineering/evidence/2026-10-04-takeover/lifecycle-r2/`的default-1、default-2、interrupted三对CSV/summary和results.json，exit_case对应case字段，三项PASS且run_id互异。清单可在临时目录使用绝对输入路径，从另一cwd运行以证相对规则；输出中不得含用户绝对路径。

人工反例至少：completed/130、interrupted/0、帧数/时长/分位矛盾、重复run_id、缺summary、缺退出记录、截断末行、零帧、NaN、未知状态、坏JSON、覆盖输出；测试原输入SHA不变。硬杀案例只做产物缺证金样，不启动杀停进程。错误必须有具体path/field，不能吞异常成PASS。保留首次失败日志，同一步两次失败停止诊断，不改金样。

工人交一个限定干净commit与五行报告（范围、命令/日志、限制、commit、用量）；无自动领下一票。只接受离线审计工具，LIFE-01真实GUI关窗、发行包重开等仍归主控实机检查。主控review/独立验收/更新TODO并合并。
