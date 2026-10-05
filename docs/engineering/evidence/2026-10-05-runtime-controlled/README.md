# 实机证据 · 2026-10-05

LIFE三次真实默认目录运行；`native-actions.json`记录真实关闭自有窗口，`life-audit-report.json`复算产物。限定接受进程/默认输出/关窗/同包重开，不称存档恢复或商业发行完成。

`focus-failed/`保留两次原生焦点操作失败的全部帧和摘要。随后按所有者明确授权，AppleScript只切本次自有Yudian/Finder焦点；`sampled/sequence.json`保留新六轮顺序和间隔，`sampled/focus-conditions.json`保留实际PID计数。后台目标没有持久置前，严格受控比较NOT_ACCEPTED；两组比较只复算原始统计，不判断外部条件有效性。实际监测约0.8秒，采样间隙不签连续焦点，启动帧与尖刺全部计入。

六轮`collector-verification.json`与本机原件逐字一致。比较器首次因采集器缺少顶层`app_binary_sha256`拒绝输入；`verification.json`仅从已实测相等的before/after binary派生该字段，并附原件SHA/来源；CSV/summary未改，比较器未改。每组3轮、1身份组，最终CLI均exit0。身份组不包含焦点/负载条件，不能据同group_id声称受控。

日志/负载快照只替换私有路径，原件及采集器留本机，原件SHA见`method-receipt.json`。原始CSV/summary/exit/轨迹的11组复制、六份适配来源与本机原件核对一致；`final-verification.json`记录最终签名exit0、包SHA不变、无残留Yudian。内存覆盖启动，温度NOT_MEASURED，无profiler归因。

复算命令（在仓库根执行，输出使用尚不存在的新路径）：

```sh
python3 tools/lifecycle_audit/audit.py --manifest docs/engineering/evidence/2026-10-05-runtime-controlled/life-manifest.json --output /tmp/yudian-life-recheck.json
python3 tools/perf_compare/compare.py --runs docs/engineering/evidence/2026-10-05-runtime-controlled/sampled/foreground-{1,2,3} --output /tmp/yudian-front-recheck.json
python3 tools/perf_compare/compare.py --runs docs/engineering/evidence/2026-10-05-runtime-controlled/sampled/background-{1,2,3} --output /tmp/yudian-back-recheck.json
```

复算不需要GUI，也不代替原生动作或焦点条件验收。
