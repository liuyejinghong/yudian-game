extends SceneTree

var config: Dictionary
var main: Node3D
var camera: Camera3D
var overlay: Node3D
var binder: Node
var output: String
var report := {"scope": "actual Main baseline plus art-only overlay; not default application integration", "frames": {}}
var models: Array[Node3D] = []
var label: Label

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var folder := (get_script().resource_path as String).get_base_dir()
	output = OS.get_environment("YUDIAN_ART_OUTPUT")
	if output.is_empty():
		_fail("set YUDIAN_ART_OUTPUT to a new capture directory")
		return
	if DirAccess.dir_exists_absolute(output):
		_fail("capture directory already exists; preserve earlier evidence")
		return
	DirAccess.make_dir_recursive_absolute(output)
	config = JSON.parse_string(FileAccess.get_file_as_string(folder.path_join("camera.json")))
	main = load("res://scenes/Main.tscn").instantiate()
	root.add_child(main)
	for i in range(180):
		await physics_frame
		if main.get_node_or_null("Robot_Zhulei_1") != null:
			break
	if main.get_node_or_null("Robot_Zhulei_1") == null:
		_fail("Main live terrain actors did not start")
		return
	camera = root.get_camera_3d()
	report["engine"] = Engine.get_version_info().string
	report["display"] = DisplayServer.get_name()
	report["renderer"] = RenderingServer.get_current_rendering_method()
	report["driver"] = RenderingServer.get_current_rendering_driver_name()
	report["window_px"] = [root.size.x, root.size.y]
	report["viewport_px"] = [root.get_visible_rect().size.x, root.get_visible_rect().size.y]
	report["msaa"] = root.msaa_3d
	report["scaling_3d_scale"] = root.scaling_3d_scale
	if not await _capture("actual-main-v0"):
		return
	main.set_process(false)
	main.set_physics_process(false)
	for node in main.get_children():
		if node is CanvasLayer:
			node.visible = false
		if node is Node3D and (str(node.name).begins_with("Facility_") or str(node.name).begins_with("Robot_")):
			node.visible = false
			node.set_process(false)
			node.set_physics_process(false)
	overlay = Node3D.new()
	root.add_child(overlay)
	binder = load("res://scenes/art_preview_r1/preview_r1.gd").new()
	for item in config.calibration_models:
		var path: String = ProjectSettings.globalize_path("res://../" + item.manifest)
		var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
		var entity := Node3D.new()
		entity.name = manifest.id
		overlay.add_child(entity)
		var wrapper: Node3D = load(manifest.preview_scene).instantiate()
		entity.add_child(wrapper)
		var error: String = binder._bind_materials(wrapper, manifest, {})
		if not error.is_empty():
			_fail(error)
			return
		error = wrapper.apply_preview({"state":"idle", "phase":"completed", "phase_t":0.0, "cargo":"empty", "reason":"none", "time_s":0.0})
		if not error.is_empty():
			_fail(error)
			return
		var height := _height(item.xz_m[0], item.xz_m[1])
		if not is_finite(height):
			_fail("native terrain sample missing")
			return
		entity.position = Vector3(item.xz_m[0], height, item.xz_m[1])
		models.append(entity)
	_make_ui()
	for mode in ["main_baseline", "reference_normal", "normal", "overview", "close"]:
		_place(mode)
		label.text = "美术镜头候选 / " + mode + " · 原尺度 · 未接入普通应用"
		if not await _capture(mode):
			return
	if not await _gallery(folder):
		return
	var file := FileAccess.open(output.path_join("capture.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t") + "\n")
	binder.free()
	print("A01_A03_CAPTURE_OK Main baseline, art overlay, surface style samples separately recorded")
	quit()

func _height(x: float, z: float) -> float:
	var query := PhysicsRayQueryParameters3D.create(Vector3(x, 50, z), Vector3(x, -20, z), 1)
	var hit := main.get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return NAN
	return hit.position.y

func _place(mode: String) -> void:
	var spec: Dictionary = config.cameras[mode]
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	camera.keep_aspect = Camera3D.KEEP_HEIGHT
	camera.fov = spec.fov
	camera.near = config.near_m
	camera.far = config.far_m
	camera.look_at_from_position(Vector3(spec.position[0],spec.position[1],spec.position[2]), Vector3(spec.target[0],spec.target[1],spec.target[2]))

func _make_ui() -> void:
	var canvas := CanvasLayer.new()
	root.add_child(canvas)
	for rect in _ui_rects():
		var panel := ColorRect.new()
		panel.position = rect.position
		panel.size = rect.size
		panel.color = Color(0.035,0.047,0.06,0.94)
		canvas.add_child(panel)
	label = Label.new()
	label.position = Vector2(340,14)
	label.add_theme_font_size_override("font_size", 22)
	canvas.add_child(label)
	var note := Label.new()
	note.position = Vector2(18,80)
	note.text = "操作区占位 · UI 1x\n左侧 %d px\n\n模型保持米制原尺度\n三型机器人\n四种代表设施\n\n实际地形上的美术候选\n普通版本接入待验" % config.ui_reservation_px.left
	note.add_theme_font_size_override("font_size", 22)
	canvas.add_child(note)

func _capture(name: String) -> bool:
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	if image.save_png(output.path_join(name + ".png")) != OK:
		_fail("PNG save failed")
		return false
	var frame := {"position": [camera.position.x,camera.position.y,camera.position.z], "fov": camera.fov, "projection": "perspective" if camera.projection == Camera3D.PROJECTION_PERSPECTIVE else "other", "keep_aspect": "keep_height" if camera.keep_aspect == Camera3D.KEEP_HEIGHT else "other", "near_m": camera.near, "far_m": camera.far, "rotation_degrees": [camera.rotation_degrees.x,camera.rotation_degrees.y,camera.rotation_degrees.z], "msaa_3d_raw": root.msaa_3d, "scaling_3d_scale": root.scaling_3d_scale, "models": []}
	for entity in models:
		var bounds := Rect2()
		var first := true
		for node in entity.find_children("*", "MeshInstance3D", true, false):
			if not node.is_visible_in_tree():
				continue
			var box: AABB = node.get_aabb()
			for index in range(8):
				var point: Vector2 = camera.unproject_position(node.global_transform * box.get_endpoint(index))
				if first:
					bounds = Rect2(point,Vector2.ZERO)
					first = false
				else:
					bounds = bounds.expand(point)
		frame.models.append({"id": str(entity.name), "position_m": [entity.position.x, entity.position.y, entity.position.z], "scale": [entity.scale.x,entity.scale.y,entity.scale.z], "projected_aabb_px": [bounds.position.x,bounds.position.y,bounds.size.x,bounds.size.y], "ui_overlap": _ui_rects().any(func(rect): return bounds.intersects(rect))})
	report.frames[name] = frame
	return true

func _fail(message: String) -> void:
	printerr("A01_CAPTURE_FAIL " + message)
	quit(1)

func _gallery(folder: String) -> bool:
	var markers: Script = load(folder.path_join("markers.gd"))
	var specs := [[Vector2(12,-10),2.0,"legal"], [Vector2(18,-3),2.0,"illegal"], [Vector2(18,-9),2.0,"committed"], [Vector2(10,-4),1.15,"selected"], [Vector2(14,-3),1.15,"hover"]]
	var measured := []
	for spec in specs:
		var marker: MeshInstance3D = markers.build(spec[0],spec[1],spec[2],_height)
		if marker == null:
			_fail("A03 marker build failed")
			return false
		overlay.add_child(marker)
		var vertices: PackedVector3Array = marker.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		var max_error := 0.0
		var min_gap := INF
		var max_gap := -INF
		for point in vertices:
			max_error = maxf(max_error,absf(point.y - _height(point.x,point.z) - 0.025))
		for i in range(0,vertices.size(),3):
			var center := (vertices[i]+vertices[i+1]+vertices[i+2])/3
			var gap := center.y-_height(center.x,center.z)
			min_gap = minf(min_gap,gap)
			max_gap = maxf(max_gap,gap)
		measured.append({"kind":spec[2],"vertices":vertices.size(),"vertex_height_error_m":max_error,"triangle_center_gap_m":[min_gap,max_gap]})
	report["marker_native_height_checks"] = measured
	_place("normal")
	label.text = "标记候选 · 实际坡地 · 实线 / 虚线加叉 / 双圆环（非实时权限）"
	if not await _capture("markers-normal"):
		return false
	_place("overview")
	root.msaa_3d = Viewport.MSAA_DISABLED
	root.scaling_3d_scale = 0.75
	label.text = "标记候选 · 总览低画质 · 关闭 MSAA，内部渲染比例 0.75"
	if not await _capture("markers-overview-low"):
		return false
	root.msaa_3d = Viewport.MSAA_4X
	root.scaling_3d_scale = 1.0
	_place("normal")
	var terrain: MeshInstance3D
	for node in main.find_children("*", "MeshInstance3D", true, false):
		if node.mesh is ArrayMesh and node.get_parent().get_children().any(func(child): return child is StaticBody3D):
			terrain = node
			break
	if terrain == null:
		_fail("native terrain render mesh missing")
		return false
	var material := ShaderMaterial.new()
	material.shader = load(folder.path_join("surface.gdshader"))
	material.set_shader_parameter("region_center",Vector2(12,-10))
	material.set_shader_parameter("region_radius",2.0)
	terrain.material_override = material
	for stage in range(3):
		material.set_shader_parameter("stage",stage)
		label.text = "表面候选 %d · 同一原始几何上的材质小样 · 非作业进度" % stage
		if not await _capture("surface-style-%d" % stage):
			return false
	report["surface_sample"] = {"terrain_source":"actual Main v0 mesh, geometry unchanged", "stage_source":"explicit style input 0/1/2 only; no actual D1 task fixture supplied", "region_center_m":[12,-10],"region_radius_m":2}
	return true

func _ui_rects() -> Array[Rect2]:
	var width: float = config.window_px[0]
	var height: float = config.window_px[1]
	var ui: Dictionary = config.ui_reservation_px
	return [Rect2(0,0,ui.left,height),Rect2(ui.left,0,width-ui.left,ui.top),Rect2(ui.left,height-ui.bottom,width-ui.left,ui.bottom)]
