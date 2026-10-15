extends Node3D
## 教学 MVP 远侧露头地标：把已通过内部视觉验收的火星露头样板一次性接入真实 Main，
## 并以纯视觉荒地延伸衔接原 60x60 地形的边界外画面。详见
## docs/art/production/tutorial-mars-r1/consumer-receipt.md（含 v1 返工记录）。
##
## 边界：
## - 纯装饰：只有 MeshInstance3D 视觉，无碰撞体、无每帧逻辑、不碰权威地形/施工/存档。
## - 固定单实例：本节点在 Main.tscn 中静态存在一次；Main 的 rebuild/reset/教学存档
##   不会重新实例化或复制它（无任何代码路径会 AddChild 第二份）。
## - 基准保护：benchmark 模式不加载（判断与 scripts/Configuration/BenchmarkOptions.cs 一致：
##   env YUDIAN_BENCHMARK=1、user --benchmark/--yudian-benchmark/--duration、
##   engine --yudian-benchmark/--duration）。基准基线因此不含本资产。
## - 大件只在非基准运行时按需加载：GLB 不是本场景的 ext_resource，场景加载不触发它。
## - 不调用任何 C#/Main 成员：世界模式判断只用 OS 环境变量与命令行，无跨语言耦合。

const GLB_PATH := "res://assets/environment/tutorial-mars-r1/mars-outcrop-hifi-r2.glb"

# 边界外荒地延伸：覆盖默认机位可见、原 60x60 权威地形之外的落点，避免露头脚底悬空 cutoff。
# 四元组 = (x_min, z_min, x_max, z_max)，是**世界坐标**区间（米），一律在玩家可玩区域
# x/z ∈ [-30, 30] 之外、不覆盖任何可玩/开挖高度、无碰撞体。
# 本节点位于 Main.tscn 的实例变换下（当前为纯平移），构建时用当前全局变换的逆把世界目标
# 显式转成局部；父级旋转/缩放变化时延伸面仍保持世界轴对齐且与这些区间一致。
const APRON_QUADS := [
	Vector4(-140.0, -140.0, 40.0, -30.0),   # 南侧远带
	Vector4(-140.0, -30.0, -30.0, 40.0),    # 西侧带
	Vector4(30.0, -30.0, 50.0, 10.0),       # 东南近角带
]

func _ready() -> void:
	if _is_benchmark_mode():
		return
	_build_apron()
	var packed := load(GLB_PATH) as PackedScene
	if packed == null:
		push_error("TutorialMarsOutcrop: 无法加载 " + GLB_PATH + "（若为首次接入，需先完成一次 Godot 资源导入）")
		return
	var visual := packed.instantiate() as Node3D
	if visual == null:
		push_error("TutorialMarsOutcrop: " + GLB_PATH + " 导入根不是 Node3D")
		return
	visual.name = "MarsOutcropHifiR2"
	add_child(visual)
	print("MAIN_MARS_LANDMARK_READY " + GLB_PATH)

## 单份 plain surface，参数与 TerrainRegionView 的既有地面材质一致（非新 shader）：
## StandardMaterial3D albedo (0.63, 0.32, 0.19)、roughness 0.95、背面剔除。
func _build_apron() -> void:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.63, 0.32, 0.19)
	material.roughness = 0.95
	material.cull_mode = BaseMaterial3D.CULL_BACK
	var to_local := global_transform.affine_inverse()
	for index in APRON_QUADS.size():
		var quad: Vector4 = APRON_QUADS[index]
		var apron := MeshInstance3D.new()
		apron.name = "OutsideGroundApron%d" % index
		var plane := PlaneMesh.new()
		plane.size = Vector2(quad.z - quad.x, quad.w - quad.y)
		apron.mesh = plane
		apron.material_override = material
		var center_world := Vector3((quad.x + quad.z) * 0.5, 0.0, (quad.y + quad.w) * 0.5)
		apron.transform = to_local * Transform3D(Basis.IDENTITY, center_world)
		add_child(apron)

## 与 BenchmarkOptions.Parse 一致的最小判断：只识别会让 Main 进入基准模式的旗标/环境开关。
func _is_benchmark_mode() -> bool:
	if OS.get_environment("YUDIAN_BENCHMARK") == "1":
		return true
	for arg in OS.get_cmdline_user_args():
		if arg == "--benchmark" or arg == "--yudian-benchmark" or arg == "--duration":
			return true
	for arg in OS.get_cmdline_args():
		if arg == "--yudian-benchmark" or arg == "--duration":
			return true
	return false
