extends SceneTree

var output: String
var main: Node3D
var camera: Camera3D
var terrain: MeshInstance3D
var material: ShaderMaterial
var baseline: Shader
var revised: Shader
var rock_root: Node3D
var record := {"scope":"actual Main with read-only shader overrides; optional engineering save loaded through existing API; rocks are art-only placement examples, no game registration", "frames":{},"rocks":[]}
var failed := false
var home: String

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	output = OS.get_environment("YUDIAN_TERRAIN_LOOK_OUTPUT")
	if output.is_empty() or DirAccess.dir_exists_absolute(output):
		printerr("set YUDIAN_TERRAIN_LOOK_OUTPUT to a new directory")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output)
	home = get_script().resource_path.get_base_dir().get_base_dir().get_base_dir().get_base_dir()
	baseline = load(home.path_join("docs/art/production/terrain-look-r1/evidence/before-source/surface.gdshader"))
	revised = load(home.path_join("art/d1/surface.gdshader"))
	main = load("res://scenes/Main.tscn").instantiate()
	root.add_child(main)
	for i in range(180):
		await physics_frame
		if main.get_node_or_null("Robot_Zhulei_1") != null:
			break
	if main.get_node_or_null("Robot_Zhulei_1") == null:
		printerr("Main not ready")
		quit(1)
		return
	if OS.get_environment("YUDIAN_TERRAIN_LOOK_LOAD") == "1":
		main.call("QueuePlayerAction","load")
		for i in range(12):
			await physics_frame
	for i in range(4):
		await process_frame
	await RenderingServer.frame_post_draw
	for mesh in main.find_children("*","MeshInstance3D",true,false):
		if mesh.material_override is ShaderMaterial and mesh.material_override.shader.resource_path.get_file() == "surface.gdshader":
			terrain = mesh
	if terrain == null:
		printerr("actual terrain shader binding not found")
		quit(1)
		return
	material = terrain.material_override.duplicate()
	terrain.material_override = material
	var controller := main.get_node("PlayerController")
	controller.set_process(false)
	controller.set_process_input(false)
	controller.set_process_unhandled_input(false)
	main.set_process(false)
	main.set_physics_process(false)
	for robot in main.get_children():
		if str(robot.name).begins_with("Robot_"):
			robot.set_process(false)
			robot.set_physics_process(false)
	camera = root.get_camera_3d()
	camera.look_at_from_position(Vector3(25.12,28.8,26.3),Vector3(4,0,-2.5))
	camera.fov = 50.0
	var initial_pose := camera.transform
	var mesh_hash := _hash(var_to_bytes(terrain.mesh.surface_get_arrays(0)))
	record.engine = Engine.get_version_info().string
	record.driver = RenderingServer.get_current_rendering_driver_name()
	record.viewport_px = [root.size.x,root.size.y]
	record.terrain_arrays_sha256 = mesh_hash
	record.shader_before_sha256 = FileAccess.get_sha256(baseline.resource_path)
	record.shader_after_sha256 = FileAccess.get_sha256(revised.resource_path)
	record.stage = material.get_shader_parameter("stage")
	record.mask_enabled = material.get_shader_parameter("use_committed_mask")
	var mask: Texture2D = material.get_shader_parameter("committed_mask")
	record.mask_sha256 = _hash(mask.get_image().get_data())
	await _pair("normal")
	var focus := Vector3(12,0,-10)
	camera.look_at_from_position(focus+Vector3(7,8,10),focus)
	await _pair("close")
	camera.transform = initial_pose
	root.msaa_3d = Viewport.MSAA_DISABLED
	root.scaling_3d_scale = 0.75
	await _pair("low")
	root.scaling_3d_scale = 1.0
	if OS.get_environment("YUDIAN_TERRAIN_LOOK_ROCKS") == "1":
		rock_root = Node3D.new()
		main.add_child(rock_root)
		await _rock("rock-cluster",Vector2(19,4),0.25)
		await _rock("rock-outcrop",Vector2(13,-5),-0.6)
		await _capture("rocks-normal",revised)
		camera.look_at_from_position(Vector3(23,10,12),Vector3(15,0,-2))
		await _capture("rocks-close",revised)
	record.terrain_unchanged = mesh_hash == _hash(var_to_bytes(terrain.mesh.surface_get_arrays(0)))
	record.mask_unchanged = record.mask_sha256 == _hash(mask.get_image().get_data())
	if failed or not record.terrain_unchanged or not record.mask_unchanged:
		printerr("read-only terrain/mask identity changed")
		quit(1)
		return
	var file := FileAccess.open(output.path_join("capture.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(record,"\t")+"\n")
	print("TERRAIN_LOOK_CAPTURE_OK frames=%d terrain_and_mask_unchanged=true" % record.frames.size())
	quit()

func _pair(label: String) -> void:
	await _capture(label+"-before",baseline)
	await _capture(label+"-after",revised)

func _capture(label: String, shader: Shader) -> void:
	material.shader = shader
	await process_frame
	await RenderingServer.frame_post_draw
	if root.get_texture().get_image().save_png(output.path_join(label+".png")) != OK:
		failed = true
		printerr("PNG save failed: "+label)
		return
	var box := {"position":[camera.position.x,camera.position.y,camera.position.z],"rotation_degrees":[camera.rotation_degrees.x,camera.rotation_degrees.y,camera.rotation_degrees.z],"fov":camera.fov,"scale":root.scaling_3d_scale,"msaa":root.msaa_3d,"shader_sha256":FileAccess.get_sha256(shader.resource_path)}
	record.frames[label] = box

func _rock(id: String, center: Vector2, yaw: float) -> void:
	var path := home.path_join("art/environment/terrain-look-r1/rocks/"+id+".glb")
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	if document.append_from_file(path,state) != OK:
		failed = true
		printerr("rock GLB load failed: "+path)
		quit(1)
		return
	var instance: Node3D = document.generate_scene(state)
	rock_root.add_child(instance)
	instance.rotation.y = yaw
	var ground: float = main.call("SamplePlayerGround",center.x,center.y)
	if not is_finite(ground):
		failed = true
		printerr("rock ground height unavailable")
		quit(1)
		return
	instance.position = Vector3(center.x,ground,center.y)
	var base_vertices: Array[Vector3] = []
	for mesh in instance.find_children("*","MeshInstance3D",true,false):
		for surface in range(mesh.mesh.get_surface_count()):
			for vertex in mesh.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]:
				var point: Vector3 = mesh.global_transform * vertex
				if absf(point.y-ground) < .001:
					base_vertices.append(point)
	var offset := INF
	for point in base_vertices:
		offset = minf(offset,float(main.call("SamplePlayerGround",point.x,point.z))-point.y)
	if not is_finite(offset) or base_vertices.is_empty():
		failed = true
		printerr("rock base sample unavailable")
		quit(1)
		return
	instance.position.y += offset - .02
	var gaps: Array[float] = []
	for point in base_vertices:
		gaps.append(point.y+offset-.02-float(main.call("SamplePlayerGround",point.x,point.z)))
	record.rocks.append({"id":id,"sha256":FileAccess.get_sha256(path),"position":[center.x,instance.position.y,center.y],"yaw":yaw,"ground_at_origin":ground,"base_samples":base_vertices.size(),"base_gap_max_m":gaps.max(),"base_gap_min_m":gaps.min(),"scope":"art-only placement example; no collider, no resource identity"})

func _hash(bytes: PackedByteArray) -> String:
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(bytes)
	return context.finish().hex_encode()
