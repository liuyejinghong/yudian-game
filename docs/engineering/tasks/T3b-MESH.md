# T3b-MESH · 纯网格生成子件

状态见产研权威TODO；主控冻结/验收，GLM执行。依赖T3a-DATA已接受及[网格合同r1](../contracts/terrain-mesh-r1.md)。父票T3b/T3c的完整候选集成与更新验证未完成；本票不代表整套T3方案。

## 唯一任务与目录

实现合同中StageMeshBuilder的两个Build重载。只新增`prototype/scripts/TerrainCandidates/StageGrid/`和`tools/terrain-stage-tests/`，不改既有文件。namespace Yudian.Terrain；公共共享类型只读输入在TerrainGeometry，数据在TerrainData。可新增小量同目录辅助代码，不创建框架、其他算法、兼容层、可变缓存或新依赖。

禁改Main/fixture/project.godot/项目csproj、共享输出、已有数据/测试、合同/PLAN/TODO、美术/材质、其他工人的目录；不做碰撞/导航/世界状态/保存/更新/GUI，不下载安装、联网取包、push/merge、不自写ACCEPTED，不领取下一票。提交只含自己的源码、console测试与README，无bin/obj/绝对个人路径。

## 验证与交付

测试工程名Tests.csproj，net8.0;net10.0、ImplicitUsings enable、Nullable disable、UseAppHost false、NuGetAudit false。链接TerrainData/*.cs、TerrainGeometry/*.cs及自己的候选目录，零外部包。用现有$HOME/.dotnet/dotnet；创建工作树外空NuGet源restore，NuGetAudit=false；Release --no-restore编译两个TFM，net10实际run --no-build --no-restore。net8执行NOT_RUN，不擅自安装。输出真实检查数量、非0失败exit，至少合同对应金样、全部几何/索引/确定性、边界、版本/内容错误、最大/最小、不可变、两文化。

完成条件：实现与测试构建/执行通过、干净限定提交；五行报告范围/真实命令结果/限制/用量可见性/commit。模型确认/合同base/任务receipt由主控派单后附上。两轮同接口失败先停并具体报告，不能改合同绕过错误。独立验收与实际合并由主控负责。

## 实际派单

2026-10-04：合同base8c3e1178557c6c0f9e6aa9e34345a482435bed7f；Bridge session sess_f8ea17b9fe，原生模型确认task_13bdf47de7返回GLM-5.3-Flash（observed_model未提供）；实现task_8501addd0a。当前为实际执行，未交付/未验收不标通过。
