extends SceneTree

var main: Node3D
var camera: Camera3D
var objects: Array[Node3D] = []
var ui: Array[Rect2] = []
var output: String
var report := {"scope":"actual engineering Main scene and HUD; camera-only in-memory overrides; no gameplay commands, entity/layout/material changes or source writes", "frames":{}}

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	output = OS.get_environment("YUDIAN_PLAYER_CAMERA_OUTPUT")
	if output.is_empty() or DirAccess.dir_exists_absolute(output):
		printerr("set YUDIAN_PLAYER_CAMERA_OUTPUT to a new directory")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output)
	main = load("res://scenes/Main.tscn").instantiate()
	root.add_child(main)
	for i in range(180):
		await physics_frame
		if main.get_node_or_null("Robot_Zhulei_1") != null:
			break
	var controller := main.get_node_or_null("PlayerController")
	if controller == null or main.get_node_or_null("Robot_Zhulei_1") == null:
		printerr("actual player scene not ready")
		quit(1)
		return
	for i in range(4):
		await process_frame
	await RenderingServer.frame_post_draw
	camera = root.get_camera_3d()
	controller.set_process(false)
	controller.set_process_input(false)
	controller.set_process_unhandled_input(false)
	main.set_process(false)
	main.set_physics_process(false)
	for node in main.get_children():
		if str(node.name).begins_with("Facility_") or str(node.name).begins_with("Robot_Zhulei_"):
			objects.append(node)
	for panel in controller.find_children("*","PanelContainer",true,false):
		ui.append(panel.get_global_rect())
	report["engine"] = Engine.get_version_info().string
	report["driver"] = RenderingServer.get_current_rendering_driver_name()
	report["window_px"] = [root.size.x,root.size.y]
	report["hud_rects_px"] = []
	for rect in ui:
		report.hud_rects_px.append([rect.position.x,rect.position.y,rect.size.x,rect.size.y])
	await _capture("actual-normal")
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(get_script().resource_path.get_base_dir().path_join("camera.json")))
	var target := Vector3(4,0,-2.5)
	var normal: Dictionary = config.cameras.normal
	camera.fov = normal.fov
	camera.look_at_from_position(target + (Vector3(normal.position[0],normal.position[1],normal.position[2])-target)*1.6,target)
	await _capture("normal-distance-1.6")
	var overview: Dictionary = config.cameras.overview
	camera.fov = overview.fov
	camera.look_at_from_position(Vector3(overview.position[0],overview.position[1],overview.position[2]),target)
	await _capture("existing-overview")
	var file := FileAccess.open(output.path_join("player-camera.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t") + "\n")
	print("A01_PLAYER_CAMERA_CAPTURE_OK frames=3 actual facilities=6 zhulei=4")
	quit()

func _capture(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	if image.save_png(output.path_join(name+".png")) != OK:
		quit(1)
		return
	var frame := {"position":[camera.position.x,camera.position.y,camera.position.z],"rotation_degrees":[camera.rotation_degrees.x,camera.rotation_degrees.y,camera.rotation_degrees.z],"fov":camera.fov,"near_m":camera.near,"far_m":camera.far,"objects":[]}
	for entity in objects:
		var box := Rect2()
		var first := true
		for node in entity.find_children("*","MeshInstance3D",true,false):
			if not node.is_visible_in_tree():
				continue
			var aabb: AABB = node.get_aabb()
			for i in range(8):
				var point: Vector2 = camera.unproject_position(node.global_transform*aabb.get_endpoint(i))
				if first:
					box = Rect2(point,Vector2.ZERO)
					first = false
				else:
					box = box.expand(point)
		frame.objects.append({"id":str(entity.name),"position_m":[entity.position.x,entity.position.y,entity.position.z],"projected_bounds_px":[box.position.x,box.position.y,box.size.x,box.size.y],"clipped":box.position.x<0 or box.position.y<0 or box.end.x>root.size.x or box.end.y>root.size.y,"hud_overlap":ui.any(func(rect):return box.intersects(rect))})
	report.frames[name] = frame
