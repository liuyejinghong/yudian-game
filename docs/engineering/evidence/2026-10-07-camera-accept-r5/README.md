# r5解锁后的实际镜头与Godot启动环境

2026-10-07。运行源880d04d、PCK8d3faf88…bbb439、DLL35f475ad…f7a14、MVIDbf9eddae…c1c0，Metal/自包含CLR8.0.31。完整限定见[复验](../../reports/2026-10-07-camera-verification.md)。

`gui-first.log`与`gui-reopen.log`是同包真实鼠标，两个进程exit0；暂停73.783s/取消阶段保存后关闭，重开读取和镜头继续可用。`gui/*.png`来自CUA实际窗口，选用暂停缩放/旋转/平移、边界返回、确认取消、保存及读取后画面；这些都是隔离新档，不是玩家原档。小幅滚动变化不明显，较大上/下滚动可见；边框拖动未缩小，真实1280×800、物理触控板及持续键盘手感未签。

`app-entry.json`记录根入口r5、r3备用及正常原schema2只读取不保存/原档字节不变。原玩家档和该次画面均留私下，不上传。

`hostfxr-red.log`保留不含SDK环境的编辑器缺库/退出134，`hostfxr-green.log`及`hostfxr-wrapper-green.log`为显式路径/最小脚本加载editor退出0；`godot-editor-sdk-found.png`是正确入口实际打开。退出后状态查询曾重新唤起原Godot，再现弹窗；已关闭并改只查listApps，不把该次默认重启说成正确入口回归。

公开文本仅脱敏`<PROJECT_ROOT>`、`<DOTNET_ROOT>`、`<USER_HOME>`、`<USER_LIBRARY>`、`<TMP>`等路径，原件私下保留。SHA256针对本目录公开副本；前批21份证据未改。候选包及原入口不删除，不上传存档JSON。

启动修复可从仓库根复核（已安装工具，脚本不安装依赖或修改全局环境）：

```sh
env -u DOTNET_ROOT -u DOTNET_ROOT_ARM64 PATH=/usr/bin:/bin:/usr/sbin:/sbin \
  tools/run_godot.sh --headless --path '<PROJECT>' --editor --quit --log-file '<NEW_LOG>'
```
