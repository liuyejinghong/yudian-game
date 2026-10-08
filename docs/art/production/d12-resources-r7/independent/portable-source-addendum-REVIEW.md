# A04a r7 可移植源身份独立附录

2026-10-08。结论：**有限接受两份新blob的源身份与当前六资源可移植性。** 已独立核对实际文件、完整快照差异、原生证据绑定及导出字节一致性，未发现需要修改源的具体问题。原57张Godot图的限定静态PASS保持原范围；本附录没有新增美术签收。

接受的源为 `art/source/resources/d12-r1/r7-portable/resources-grey-r7.blend`，SHA `5b5eeb3a68fcd3160e40efe47e5af6e816e4d6543e221d3dc523c61b7a2cb200`；builder输入为 `art/source/resources/d12-r1/resource-pair-grey-r5-portable.blend`，SHA `de0fbdcc1af74b3e830c5ae41a62c46720e1993dc30cfd8e91cfacf3a6d1636b`。冻结原r7源 `73b927f43df0c179f80bc08696531719e201f3f2bb1bbb452b69c5eb96e396ce` 与原r5源 `425778ce1fb9acb95d406dd0078e07f6ab7503d77cac386be274b6f1eed34604` 均核实未变。新源并非旧源同一blob，不改写原源身份。

## 实际核验

只读读取 `current-verifier-receipt.json`、`native-proof.json`、`isolated-rebuild-proof.json`、`fresh-reopen-differences.json`，随后核它们引用的脚本、快照、源、GLB、日志、输入和实际输出。独立用标准库重算351个唯一文件的SHA与字节数；作者57项portable哈希索引、98项冻结文件及当前19项输入均匹配，身份记录包含实际隔离文件与原评审绑定。

| 项目 | 依据与结论 |
|---|---|
| 库链接与可编辑节点 | 本地化脚本实际使用make_local/live-user remap，检查remaining linked-ID，再移除Library；没有删除或apply所需modifier。本地化前后两组完整快照精确相等。已绑定的原生fresh-reopen记录与当前source读回均为libraries=0、linked_ids=0；铁矿、铜矿、结构件的Smooth by Angle仍是本地GeometryNodeTree。该项依赖已交付的原生遍历证据，本会话没有另开Blender重新遍历。 |
| 基础与求值数据 | 独立比较before/after/fresh JSON全部字段。基础/求值位置、vertex/corner normals、edge/loop/polygon/triangle数据、mesh attributes、使用中材质与节点图、modifier参数、自定义资源元数据、object transform、scene units均保留。两份本地化前后快照原样相等；fresh-reopen的差异仅为下述Material与session_uid。不是只比包围盒或面数。 |
| 原源/portable导出与冻结payload | 实际读取 `historical-local-project/yudian-r7-portable-export-proof` 的16个GLB：r5两资源及r7六资源，各有original/portable导出。16份均与对应冻结r7 GLB逐字节相等，覆盖全部属性、index、材质、node/scene、变换与extras。原生proof日志内结果JSON与交付proof JSON相同；未仅引用其中的true/PASS。 |
| 隔离原生重建 | 实际隔离目录 `historical-local-project/yudian-r7-portable-rebuild` 内builder、portable r5、工具脚本等输入匹配记录；原linked r5/r7源在此目录均不存在。重建源SHA `c4380e554db0777fcfe944c462b6b1cf3da681acb3ae8e1ffa22bd02deaf171d` 与proof匹配，六个重建GLB与冻结文件逐字节相等。该重建blend与交付5b5e源的blob不同，proof中的部分GN名称为Smooth by Angle.001；不宣称重建blend所有编辑语义逐字段相同。原生闭包日志记录零库/零linked-ID及本地GN，支持本次同工具环境下的重建闭包。 |
| 当前verifier的实际运行绑定 | 当前脚本SHA `4465626e4d93fe90eae7e01bf686aca61db4ddacb8f145455870036036833d59`，stage SHA `588023dab3b25eebbcce564bc468f294157ddcae7d0125a758e0f84e826db5f5`。19项输入在正式树及 `historical-local-project/yudian-r7-portable-current-verify` 中均与记录匹配；source/glb两份日志、RESULT面数、JSON输出、回执哈希一致，退出0由交付回执记录。当前source入口确实检查冻结完整语义快照与零库/GN local，不借用旧检查器PASS。 |
| 输出位置与范围 | receipt的134项render/output路径按其隔离cwd解析，实际134文件均匹配哈希；正式树只交付重命名的current-source/current-glb读回JSON等证据。没有把这些相对路径错误解释成正式树已有134份图片，也没有观看它们作新审美评判。两份读回JSON与实际隔离输出逐字节匹配。 |

六份冻结GLB独立解析面数为铁矿64、铁料188、铜矿48、铜料124、结构件412、线缆460；没有texture/image/animation。当前source/glb读回所报面数与这些实际文件一致。原57张PNG及六GLB身份均保持，详见 [原限定评审](REVIEW.md)。本附录不重新评价铜矿微小硬边或线缆机械平面诊断，也不扩大远距裸模型辨认范围。

## 两类重开差异的判断

**默认Material：可接受删除。** r5 fresh-reopen只有 `/materials/Material` 这一非UID差异，交付differences JSON与独立差异复算一致。删除前users=0、use_fake_user=false；基础与求值mesh材质槽均只绑定grey_shape_review。移除该孤立材料记录后，其余快照没有精确名为Material的引用。它的节点图虽存在，但未参与两资源渲染；实际导出字节相等进一步支持此次删除不改视觉payload。这里接受的是这一个具体孤立材料，不能泛化为忽略所有材料差异。

**session_uid：按实际用途接受七处变化。** r7 fresh-reopen有七处差异，均为六object的object_properties与使用中材质properties中的session_uid；没有网格、modifier、resource metadata、材质节点或变换差异。r5此次未出现UID值差异。已读builder按资源名/resource_id处理、导出资源metadata，未将session_uid当作资源键或持久契约；原生导出和冻结GLB又逐字节相同。只排除这些具体ID属性，不排除自定义元数据或其他数值变化。

## 历史回执与限制

旧 `isolated-stage.json` 的verifier声明 `30fc3e8900347d876034eea128d01e80fa312c235a57d7d49227a9ca28999d67` 与该旧隔离树实际 `65dfdcf226ecdc1c153b39e21b0ec0e8a589bd1ab99cb598b2796d09a634fab3` 不符；实际脚本与归档before-snapshot-check副本相同。该历史记录缺口保留，不能把旧stage当作当前verifier运行凭据。新的current stage/receipt已经独立绑定并实际核对446562版本的输入、日志及输出；本附录采用新回执。旧隔离重建的六GLB字节等价由实际文件直接复核，不依赖旧verifier声明。

原生记录使用Blender 5.2.2 LTS；builder仍需要该原生工具环境与已有Scenario脚本，依赖portable r5源。本次可移植性指六个静态无贴图资源的源数据闭包与所供隔离重建；不证明任意Blender版本、平台或任意资源的通用可移植性。原生记录中仅有空filepath的Render Result/Viewer Node，不构成待打包外部贴图；render output为输出目录，不能当作输入依赖。

本会话实际运行的是只读哈希、快照差异与字节比较；**Blender原生重开、重建、导出和渲染未重复运行（NOT_RUN）**，库闭包与运行退出码属于已绑定且已读取的原生证据。未新看图、未重拍、未改源/GLB/生产文档；只写此独立报告与身份记录。真实Main、连续动态、性能及所有者终审：NOT_RUN；原57图审美范围以原报告为准。
