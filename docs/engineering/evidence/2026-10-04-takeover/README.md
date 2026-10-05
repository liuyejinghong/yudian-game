# 接管证据包

运行源码来源为内部集成7341ea8；公开代码9128523的运行文件与构建脚本逐项SHA一致，仅测试命令注释脱敏。未将包或测量身份改写为文档HEAD。

CSV原始帧与旧summary副本保留；日志移除ANSI、私人绝对路径和行尾空白，保留错误、顺序与字段内容。路径替换为明确占位符，不是可执行命令。SHA256SUMS.json校验本交付包（不含自身）。旧CSV/summary原件hash另在legacy/hashes.json。包/模板hash在各summary和clean-build-manifest.json。

metal-native每轮的summary包含请求/应用/实际身份，memory.txt是time测量原文，verification.json是配套复算与有效外部内存。runs.json记录相同构建配置及未受控桌面条件，不意味着性能稳定。完整解释、命令、NOT_RUN与下一批建议见../../reports/2026-10-04-takeover-verification.md。
