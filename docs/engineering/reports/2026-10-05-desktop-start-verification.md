# 本机双击试玩入口 · 2026-10-05

APP-START限定接受。本机协调目录「余电.app」双击直接进入整平场景；实际包保留在主仓库忽略的export目录，原始Yudian引擎/PCK与工程CLI保留。复用build_macos.py，新选项--playable只加入arm64原生入口，定位同包引擎，不依赖Finder工作目录或外部SDK设置。无游戏逻辑/模板原件修改、新依赖下载、系统安装或商业发行。

- 构建工具最终源码3e64e532a3474b2329e52575756c1a79bdac47db；导出使用Godot4.7.2mono/SDK10.0.401，包内net8.0自包含runtime8.0.31。manifest如实标dirty：Godot生成未提交.cs.uid，且后续说明文件正在编辑；生产C#/场景/配置相对84371c3没有改动。生成metadata保留，不混入本票diff。
- 两次导出exit0；最终签名codesign --verify --deep --strict与启动器arm64核验通过，签名后启动器/plist/引擎/PCK四hash均与manifest相符。第一包签名前记录launcher hash导致签名后不符，已改签名后计算并新目录重建；原包和失败json保留，不抹去首轮。
- 最终包从普通路径移动到中文含空格目录并改名「余电.app」，签名仍有效；项目根入口为指向保留目录的symbolic link，原有入口不存在才创建，未覆盖旧包。
- Cua从Finder双击两个GUI包，无终端参数：首包真实到场→完成100%/v1；最终移动包41%施工→完成100%/权威/绑定/物理v1，12贴地。两轮真实关窗、inventory isRunning=false。GUI exit code未采集，不冒称exit0；截图是会话内联观察，无导出PNG。首轮Cua管道中断，重新观察后重试一次，游戏未退出。
- 首包及最终包各跑真正Main15步原生检查exit0/12贴地；DOTNET_ROOT未设置、cwd为scratch，最终包实载CLR8.0.31，身份如下。未在无SDK隔离设备或其他Mac验证，不扩签稳定性能、存档或公证发行。

`MAIN_GROUND_IDENTITY build_sha256=ce6283140081ee1f03a2e8723666b48ff0ece0e1e921664cd1048fb00a74ff9b loaded_mvid=bc8fa224-229b-4f75-8a0c-022537b3a838 CLR=8.0.31 display=headless`

[最终15步](../evidence/2026-10-05-desktop-start/packaged-final-level.log) / [最终构建manifest](../evidence/2026-10-05-desktop-start/build-manifest.json) / [签名与四hash](../evidence/2026-10-05-desktop-start/r2-hash-and-move.json) / [GUI观察](../evidence/2026-10-05-desktop-start/gui-observations.json) / [来源hash](../evidence/2026-10-05-desktop-start/hashes.json)。原始完整构建日志留本机scratch；公开构建摘录明确标excerpt，文本替换个人路径并去行尾空白。

独立architect复核入口、四hash与边界未发现实质阻碍。[PR35](https://github.com/liuyejinghong/yudian-game/pull/35)已合并，head `8a2c986dbc74dc8503bba792293174a3a7257aab`、merge `115f1c7ef320a5c463cc572dbaa104e8335e9ba8`，主工作树干净快进同步；源码/模板与已验本地包一致，文档变更不再扩大重复验证。本机已保留实际应用和根入口，无遗留测试进程；入口是该次本地构建，不自动跟随源码更新。
