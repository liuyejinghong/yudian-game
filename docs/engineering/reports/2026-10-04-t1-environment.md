# T1 环境搭建批次报告（2026-10-04）

> 口径：**只有实际运行过的命令算已验证**。未执行的事项一律列入 NOT_RUN，不做推测性结论。
> 本报告依据 T1 各环节实测记录整理；报告撰写时对本批次两道门禁命令做了复核重跑（见 §6），其余命令输出均转录自 T1 执行记录，未重复执行。

## 1. 结论

- Godot **4.7.2-stable**（.NET/C# 版）与 .NET SDK **10.0.401** 均已安装并验证可用。
- 最小 Godot 4.7 .NET 原型工程已建于 `repo/prototype/`，C# 程序集编译成功，headless 冒烟通过（exit 0）。
- 导出模板已安装，macOS arm64 导出产物已生成、ad-hoc 签名并通过 codesign 校验，导出二进制 headless 启动 exit=0，且在剥离全部环境变量（`env -i`）后仍可运行，证明 .NET 运行时随包自包含。
- 未做 git push；仅本地 commit（prototype/ 与本报告）。

## 2. 固定版本与官方来源

### 2.1 Godot

| 项 | 值 |
| --- | --- |
| 版本 | `4.7.2-stable` |
| 下载 URL | `https://downloads.godotengine.org/?version=4.7.2&flavor=stable&slug=mono_macos.universal.zip&platform=macos.universal` |

**版本依据**（官方页面逐字核对）：官方下载页 <https://godotengine.org/download/macos/> 与 <https://godotengine.org/download/windows/> 均显示最新 stable 为 **4.7.2**（.NET/C# 版，标注 "C# support"，发布日期 18 August 2026）。macOS 页给出的 .NET universal 直链即上表 URL（downloads.godotengine.org 为官方下载 CDN），页面标注该 universal 包同时支持 "arm64 (Apple Silicon) · x86_64 (Intel)"，并注明需单独安装 .NET SDK、Steam/itch.io/EGS 商店版不含 C# 支持。

注意：`https://godotengine.org/download` 本身只是跳转页，版本号从 `/download/windows/` 与 `/download/macos/` 实际页面读取。

### 2.2 .NET SDK

| 项 | 值 |
| --- | --- |
| 版本 | `10.0.401` |

**版本依据**：

- Godot 官方 C# 文档（4.7 stable，<https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html>）逐字引述：
  > "Godot 4.5 requires .NET 8 or later, but exporting to Android requires .NET 9 or later."
  > "Download and install the latest stable version of the SDK from the .NET download page."

  （注：该句在 4.7 文档中仍写 4.5，为文档措辞未随版本更新，含义是 .NET 8 为下限、Android 导出需 .NET 9+。）
- 微软官方支持页 <https://learn.microsoft.com/en-us/dotnet/core/releases-and-support> 逐字引述：
  > "Currently supported versions: .NET 10 (Long Term Support) - supported until November 2028. / .NET 9 (Standard Term Support) - supported until November 2026. / .NET 8 (Long Term Support) - supported until November 2026."

  今天是 2026-10-04，.NET 8（上一个 LTS）将于 2026-11 结束支持，故选当前 LTS = **.NET 10**。
- 精确 SDK 版本依据微软官方元数据 <https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json>（`"latest-sdk": "10.0.401"`，`"latest-runtime": "10.0.12"`，`"latest-release-date": "2026-09-08"`，eol 2028-11-14）与官方下载页 <https://dotnet.microsoft.com/download/dotnet/10.0>（SDK 10.0.401，2026-09-08 安全补丁带 10.0.12，含 C# 14.0）。

## 3. 实测安装与验证记录

**结果：两项均已安装并验证。**

1. **.NET SDK 10.0.401**：通过官方 dotnet-install.sh（<https://dot.net/v1/dotnet-install.sh>）以 `sh dotnet-install.sh --version 10.0.401` 装到默认 `~/.dotnet`（无 sudo）。实测输出：
   - `/Users/ethan/.dotnet/dotnet --version` → `10.0.401`
   - `--list-sdks` → `10.0.401 [/Users/ethan/.dotnet/sdk]`

2. **Godot 4.7.2-stable .NET**：从指定 URL 下载 macOS universal zip，ditto 解压后压缩包内目录为 `Godot_mono.app`，已重命名并移到固定路径 `/Users/ethan/yudian-game/tools-bin/Godot.app`；用 `xattr -dr com.apple.quarantine` 清除隔离。二进制确认在 `/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot`（352MB，可执行）。实测输出：
   - `/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --version` → `4.7.2.stable.mono.official.ed1daf0bf`

**备注**：

- quarantine 已清除；清后二进制上仅剩 `com.apple.provenance` 属性（macOS 27 自动生成，非隔离标记，不影响命令行运行，`--version` 实测通过）。
- 当前 shell PATH 未被修改——dotnet 以绝对路径 `/Users/ethan/.dotnet/dotnet` 验证，如需裸命令 `dotnet` 需自行在 shell 配置加 PATH。
- 未下载导出模板（该项在导出环节完成，见 §5）、未创建工程时的临时文件（zip、解压目录、安装脚本）已清理。

## 4. 构建与冒烟记录

已在 `/Users/ethan/yudian-game/repo/prototype/` 创建最小 Godot 4.7 .NET 工程并验证 C# 程序集编译成功。

**创建的文件**：

- `project.godot`：`config/name="Yudian"`，`run/main_scene="res://scenes/Main.tscn"`，`renderer/rendering_method="forward_plus"`，features `("4.7", "C#")`，`dotnet/project/assembly_name="Yudian"`
- `scenes/Main.tscn`：根 Node3D（挂 Main.cs）+ Camera3D(0,2,6) + DirectionalLight3D + MeshInstance3D(BoxMesh)，format=3
- `scripts/Main.cs`：partial class Main : Node3D，`_Ready()` 单行 GD.Print
- `.gitignore`：`.godot/` 与 `export/`
- `Yudian.csproj`（Godot.NET.Sdk/4.7.2, net8.0）与 `Yudian.sln`：为满足"确保 C# 程序集编译成功"必需，超出字面清单的这两个文件是 .NET 构建的最小必需项

**引擎固定路径**：`/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot`（v4.7.2.stable.mono.official.ed1daf0bf），`DOTNET_ROOT=/Users/ethan/.dotnet`（dotnet SDK 10.0.401）。

**执行的关键命令**（均在 repo/prototype 下，env 前缀 `DOTNET_ROOT=/Users/ethan/.dotnet PATH=/Users/ethan/.dotnet:$PATH`）：

| # | 命令 | 结果 |
| --- | --- | --- |
| 1 | `Godot --headless --path . --import` | EXIT=0 |
| 2 | `Godot --headless --path . --build-solutions --quit` | EXIT=0；产物 `.godot/mono/temp/bin/Debug/Yudian.dll`（7.7 KB）+ Yudian.pdb + Yudian.runtimeconfig.json |
| 3 | `Godot --headless --path . --quit-after 2` | 实跑主场景，捕获冒烟输出，EXIT=0 |

**headless 冒烟通过（exit 0）**，输出：

```
Godot Engine v4.7.2.stable.mono.official.ed1daf0bf - https://godotengine.org

[Yudian] C# assembly Yudian v1.0.0.0 | Godot 4.7.2-stable (official)
```

**经验备注**：

1. 首次 `--build-solutions` 不带 `--quit` 时构建已完成（输出 `[ DONE ] dotnet_build_project` 且 dll 已生成），但编辑器进程不自动退出；手动停止后带 `--quit` 重跑取得干净退出码 0。**以后 CI 调用建议始终带 `--quit`**。
2. import 自动生成 `scripts/Main.cs.uid`（Godot 4.4+ 行为，应随源码提交，本报告已随 prototype/ 提交）；`.godot/` 已被 .gitignore 忽略。
3. 冒烟验证时 grep 过滤 `"^\["` 曾把自己的输出行滤掉，属检查命令问题非工程问题，已修正重跑。

## 5. 导出与签名记录

**全部完成。**

1. **模板下载与安装**：从 GitHub releases 下载 `Godot_v4.7.2-stable_mono_export_templates.tpz`（1,202,598,411 字节，首次 curl exit 18 中断后用断点续传循环补完；归档内 `version.txt`=`4.7.2.stable.mono`，SHA256=`92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d`），解出 27 个文件装入 `~/Library/Application Support/Godot/export_templates/4.7.2.stable.mono/`。
2. **导出预设**：在 `/Users/ethan/yudian-game/repo/prototype` 写入 `export_presets.cfg`（preset 名 "macOS"，`export_path=export/macos/Yudian.app`，`binary_format/architecture="arm64"`，bundle id `com.yudian.prototype`；应用名取自 project.godot 的 `application/config/name="Yudian"`，即 CFBundleName/CFBundleExecutable=Yudian）。
3. **导出**：以 `Godot --headless --path ... --export-release "macOS" export/macos/Yudian.app` 导出（dotnet publish 走 `~/.dotnet` 的 SDK 10.0.401），产物 161MB，`Contents/MacOS/Yudian` 经 file/lipo 验证为纯 Mach-O arm64，Yudian.pck/Yudian.dll/GodotSharp.dll 均在包内。
4. **签名**：执行 `codesign --force --deep -s -` 该 .app（exit 0），`codesign --verify --deep -v` 通过："valid on disk / satisfies its Designated Requirement"，`codesign -dv` 显示 Signature=adhoc。
5. **冒烟测试**：直接运行导出的二进制 `--headless --quit-after 5`，输出 "[Yudian] C# assembly Yudian v1.0.0.0 | Godot 4.7.2-stable (official)"，exit 0；再用 `env -i` 剥离全部环境变量重跑仍成功，**证明 .NET 运行时随包自包含**。导出产物 headless 启动 exit=0。

**重要备注（后续批次会用到）**：

1. **自包含 .NET 运行时开关**：4.7.2 不存在该选项，.NET 导出选项只有 `dotnet/include_scripts_content`、`dotnet/include_debug_symbols`、`dotnet/embed_build_outputs`（源码 `modules/mono/editor/GodotTools/GodotTools/Export/ExportPlugin.cs`；embed 在 macOS 被平台隐藏，`platform/macos/export/export_plugin.cpp:384-388`）。macOS .NET 导出始终捆绑运行时（实测 `Contents/Resources/data_Yudian_macos_arm64/` 含全套 BCL System.*.dll、createdump，主二进制内嵌 runtime，无 PATH 时照常运行），故"有开关则开启"不适用，已在预设里保持默认（`include_debug_symbols=true`）。
2. **模板 arm64 补丁**：官方 mono tpz 的 macos.zip 只含 `godot_macos_{debug,release}.universal`，而 4.7.2 按架构名拼串查找模板二进制（`export_plugin.cpp:1670`，arch=arm64 时要求 `godot_macos_release.arm64`，首次导出即报"未找到请求的模板二进制文件"）。处理：用 `lipo -thin arm64` 从官方 universal 无损切出两个 arm64 切片，补进已安装的 macos.zip（新增 2 个条目，原始 tpz 未动）；产物验证为纯 arm64。**若日后重装官方模板需重做此补丁**。
3. **project.godot 修改**：为通过 arm64 导出校验添加 `rendering/textures/vram_compression/import_etc2_astc=true`（引擎报错明确要求，首次导出因此失败），会触发纹理重导入。
4. **预设中 codesign/codesign=0（Disabled）**：因 ad-hoc 签名按指定由外部 `codesign --force --deep -s -` 完成，避免双重签名。
5. **完整性依据**：官方未提供该资产的 .sha256 sidecar（请求 404），完整性依据为字节数与 Content-Length 完全一致、zip 完整解压 27 文件、version.txt 匹配。
6. 原始归档保留在 `~/Downloads/godot-templates/`，临时解压目录已清理；headless 导出需先手动 `mkdir export/macos`（引擎不会自建目标目录）。

## 6. 门禁复核（报告撰写时重跑）

本报告撰写会话中重跑两道门禁命令，结果与 T1 记录一致：

- `/Users/ethan/.dotnet/dotnet --version` → `10.0.401`，EXIT=0
- `/Users/ethan/yudian-game/tools-bin/Godot.app/Contents/MacOS/Godot --version` → `4.7.2.stable.mono.official.ed1daf0bf`，EXIT=0

## 7. NOT_RUN 清单

以下事项**本批次未执行、未验证**，不作任何结论：

| 项 | 说明 |
| --- | --- |
| 公证 notarization / 对外分发 | 未做（ad-hoc 签名本身无法公证；分发属后续批次） |
| 低配 L/M 实机测试 | 未做 |
| MetalFX / FSR 升采样对比 | 未做 |
| AI 规划基线 | 未做 |
| 真人试玩 | 未做 |

本节补充范围外事项（源自 §4 构建记录与 §5 导出记录的未做项）：未导出 universal/x86_64 变体；未做可视化渲染确认（headless 环境无画面输出，仅验证场景加载、脚本执行与退出码）。

## 8. 提交

- `git add prototype/ docs/engineering/reports/`（`export/` 已被 prototype/.gitignore 忽略，`.godot/` 亦然）
- 本地 commit，**不 push**。
