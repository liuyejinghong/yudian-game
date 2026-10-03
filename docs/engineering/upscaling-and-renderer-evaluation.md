# 超分辨率、平台加速与渲染器验证

日期：2026-10-03｜状态：官方资料调研完成；本项目 SDK 接入、Mac 实机、画质和性能均 **NOT_RUN**。

所有者要求：技术选型要考虑 DLSS、XeSS、FSR 与 Apple 同类能力，面向非高配置用户，不能把性能问题留到后期重构。本文件是 [技术验证计划](technical-validation-plan.md) 的专项补充，不宣布引擎最终选定或全功能已经支持。

## 1. 结论

Apple 的对应技术是 **MetalFX**。这些厂商提供可供开发者使用的接口或 SDK，但“公开可下载”“开源”“允许特定分发”“当前引擎已集成”“本机硬件支持”是不同问题。不能统一称为全开源通用插件，也不能据 SDK 存在承诺轻松接入。

**优先级建议**：Mac 首发先验证引擎已提供的 MetalFX 路径及原生渲染回退；FSR 作为跨硬件的对照候选；Windows 阶段按硬件、后端及维护成本评估 DLSS／XeSS。不为了首个版本集齐品牌标志；也不因尚未完成实测就断言必须自己改引擎。

## 2. 本次查到的支持情况

| 技术 | 一手资料支持的事实 | 《余电》的边界 |
|---|---|---|
| MetalFX | Apple 提供空间超分与时间抗锯齿／超分的接口、示例及接入说明 [S1]。Godot 当前 stable RenderingServer API 明列 METALFX_SPATIAL 和 METALFX_TEMPORAL，注明限 Metal 驱动，平台为 macOS／iOS [S2] | Mac 首发的优先验证项。先锁定真实引擎版本、渲染器、系统和设备支持；本项目没有实机结果。不把“使用 Metal”自动当成“启用 MetalFX” |
| FSR | Godot 文档列出 FSR 1.0、FSR 2.2，教程将这两种模式限定在 Forward+ [S3]。AMD 官方 FSR 3 页面提供源代码／MIT 说明，并分开介绍超分与帧生成 [S4] | Godot 内置 FSR2 不等于内置 FSR3、FSR4 或帧生成；AMD 新版本功能也不能反向推定已在引擎可用。具体跨 GPU／图形 API 支持按所用版本核定 |
| DLSS | NVIDIA 提供公开 SDK、Streamline 接入及部分引擎插件；DLSS 超分、帧生成、光线重建是不同功能 [S5][S6] | 与受支持 RTX 硬件及图形路径匹配，非 M3 Pro 首发选项。公开 SDK 不等于全部算法开源；按其 LICENSE 和插件实际兼容性核定 |
| XeSS | Intel 提供 SDK、开发指南与许可文件。当前仓库将 SR／FG／低延迟拆开；SR 文档说明输入和 D3D／Vulkan 路径 [S7][S8] | 非默认 Mac 路径；Windows 后续按所用组件、GPU、驱动与图形 API 实测，不能只按品牌判断硬件范围 |

### 文档交叉核验的重要发现

Godot 的 resolution_scaling 教程主要列出 Bilinear／FSR1／FSR2；但同次读取的 stable RenderingServer API 已列出 MetalFX 两模式。因此**不能从教程未列 MetalFX 推断引擎不支持**。同理，本次 API 表没有 DLSS／XeSS 选项，只能说未确认官方内置路径，不能据此断言社区插件不存在。

stable 文档是移动入口；在验证报告中固定引擎 release、补丁、源码提交、API 和后端。首次接入先检查实际构建暴露的模式和运行结果，不从文档菜单凭空宣称成功。

## 3. 需要分清三类能力

**超分辨率**：用较低分辨率渲染三维场景，再重建目标分辨率；重点缓解 GPU 像素／着色负担。[S1][S3]

**帧生成**：在已有图像之间生成额外显示帧。它可能提高视觉流畅度，但不是多执行了一次机器人决策、导航或世界结算。AMD 也要求单独处理帧生成的交换链、UI 和输入画质等事项。[S4]

**模拟优化**：任务分配、领域规划、导航、地形更新、存档与本地推理的计算。降低三维渲染分辨率不保证解决这些瓶颈；Godot 明确指出 CPU 或其他成本占主导时，降分辨率收益可能有限。[S3]

本项目**先做基础帧率与高质量超分，帧生成后置单独评估**。测试至少记录真实渲染帧、显示帧（如有插帧）、模拟步耗时和实际倍速；不能用插帧数字证明机器人计算更快。

## 4. 为什么不是导入 SDK 就结束

空间超分输入相对简单；时间超分一般需要正确的颜色、运动信息、采样抖动和历史管理，部分路径还需要深度等输入。Apple 与 Intel 指南明确列出了相关输入和重置历史等要求。[S1][S8]

应核查：相机平移／缩放，机器人机械臂和车轮，拖运设备，细线缆／太阳能支架，透明尘粒，夜间灯光，施工形变和新出现的物体。它们可能出现闪烁、拖影、重影或细节丢失，不能只看静态截图。

地形推平／开挖会改变几何和遮挡，不能让时间历史长期留着已不存在的土坡。具体做局部历史处理还是适当重置，按渲染路径确定，不为了消除残影每帧无条件清空所有历史。

屏幕 UI、中文任务看板与鼠标光标保持目标输出分辨率的清晰度；3D 重建与界面合成分开验证，避免整屏文本被一起低分辨率渲染或错误插帧。

引擎已经集成的路径通常是配置和验证问题；没有合适接口、插件未维护或缺少关键渲染输入时，才可能变成引擎级工作。实际难度应在小探针后定，不能提前承诺工期或零重构。

## 5. 项目验证矩阵

同一存档、相机路径、资源密度、显示分辨率和画质比较：原生输出、较低内部三维分辨率、可用的 MetalFX Spatial／Temporal、可用的 FSR；不把不同质量预设的成绩直接对比。

Mac：M3 Pro／36GB 参考机＋至少一台明显较低配置的 Apple Silicon 原生设备。系统版本、供电模式、温度、引擎构建与依赖记录齐全；最低配置尚未承诺。

负载分层：三维独立；三维＋正常模拟；三维＋地形／导航更新；三维＋本地分类／生成；全部系统＋自动保存。同时比较模型 GPU 与其他可用推理配置的竞争，不能把 GPU 节省假定为推理资源必然增加。

指标：CPU／GPU 帧时间、P95／P99 卡顿、真实基础 FPS、内存峰值与压力、功耗／热稳定性、规划时延、模拟追赶、保存卡顿。无工具测到的项明确写 NOT_MEASURED，不用 FPS 推算未测数据。

画质验收：静止与移动镜头都能辨认对象和施工进度；细小机器人、支架、拖运连接、矿坑边缘和尘暴状态可读；中文面板清楚；无持续性几何残影。给出原生与超分的同条件视频／截图，概念图不能替代。

回退验收：模式不可用、初始化失败、改变分辨率或切换窗口后可以安全退回受支持模式；玩家可关闭超分。退出恢复画质选择，不写入游戏规则，不影响存档和资源。

## 6. 对引擎选择的实际影响

Godot 4 .NET＋C# 仍是首选验证候选，当前 MetalFX API 是正面证据，但不是项目通过证据。

最终评审同时看：Mac 原生、地形更新与导航、机群模拟、AI 联合负载、资源工作流、超分可用性、后续 Windows 成本及维护风险。若只能靠长期维护大型私有渲染分叉才能达到已确认目标，应在大量资产生产前重新比较候选；不能因沉没成本继续硬撑。

反过来，若引擎内置 MetalFX／FSR 与合理基础优化已经达标，也没有理由为了同时出现四个厂商开关而迁移引擎。

## 7. 官方来源与范围

以下于 2026-10-03 读取。技术许可为来源记录，不是法律意见；分发具体 SDK／二进制前重新核定许可、版本、系统和硬件要求。

- [S1] Apple，Boost performance with MetalFX Upscaling，WWDC22：空间／时间模式、颜色／运动／深度／抖动、历史与接入最佳实践。https://developer.apple.com/videos/play/wwdc2022/10103/
- [S2] Godot stable，RenderingServer，ViewportScaling3DMode：METALFX_SPATIAL／TEMPORAL 与 Metal 驱动限制。https://docs.godotengine.org/en/stable/classes/class_renderingserver.html
- [S3] Godot stable，Resolution scaling：Bilinear、FSR 1.0／2.2、Forward+ 限制及 CPU 瓶颈警告。https://docs.godotengine.org/en/stable/tutorials/3d/resolution_scaling.html
- [S4] AMD，FSR 3：源码／MIT、帧生成与超分分离、API／交换链／UI 和基础帧率要求。本稿不把该页的具体版本能力推广至全部 FSR 版本。https://gpuopen.com/fidelityfx-super-resolution-3/
- [S5] NVIDIA，DLSS 开发者页面：超分、帧生成、光线重建和 Streamline／引擎接入。https://developer.nvidia.com/rtx/dlss
- [S6] NVIDIA DLSS SDK 与许可证。https://github.com/NVIDIA/DLSS ｜ https://github.com/NVIDIA/DLSS/blob/main/LICENSE.txt
- [S7] Intel XeSS SDK 与许可证。https://github.com/intel/xess ｜ https://github.com/intel/xess/blob/main/LICENSE.txt
- [S8] Intel XeSS-SR 开发指南：输入、运动信息、图形 API、历史与画质验证。https://github.com/intel/xess/blob/main/doc/xess_sr_developer_guide_english.md

## 8. 证据状态

网页／官方 API 读取：已完成。引擎安装、SDK 编译、MetalFX／DLSS／XeSS／FSR 实际接入、Apple Silicon 运行、低配验证、Windows 测试、真人画质签字：**NOT_RUN**。
