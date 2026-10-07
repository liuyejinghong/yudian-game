extends Node3D

const VIEWS := {
	"normal": [Vector3(25.12,28.8,26.3),Vector3(4,0,-2.5),50.0],
	"near": [Vector3(12,4.2,9),Vector3(6,.8,0),50.0],
	"reverse": [Vector3(-1,3.4,-8),Vector3(6,.8,0),50.0],
	"horizon": [Vector3(20,3,22),Vector3(-25,2,-80),62.0],
	"detail": [Vector3(8.5,2.4,3.7),Vector3(6.4,1,0),50.0],
	"ground": [Vector3(12,1.2,7),Vector3(9.2,.15,3.5),50.0]
}
var camera: Camera3D
var note: Label
var low := false
var view := "normal"
var ground: Node3D
var hero: Node3D
var sources: Array[Dictionary] = []
var output := ""
var dragging := false
var hifi := OS.get_environment("YUDIAN_MARS_HIFI") == "1"

func _ready() -> void:
	output = OS.get_environment("YUDIAN_MARS_OUTPUT")
	if not output.is_empty() and DirAccess.dir_exists_absolute(output):
		_fail("capture directory must be new")
		return
	_light()
	ground = _asset("res://hifi/ground-with-patch-hole.glb" if hifi else "res://ground/mars-ground-r1.glb",Vector3.ZERO)
	hero = _asset("res://hifi/mars-outcrop-hifi-r2.glb" if hifi else "res://hero/mars-outcrop-r1.glb",Vector3(6,0,0))
	if ground == null or hero == null:
		return
	if hifi:
		if _asset("res://hifi/mars-ground-patch-hifi-r2.glb",Vector3(6,0,0)) == null:
			return
		DisplayServer.window_set_title("余电 · 火星高保真看样 r2")
	_ground_materials()
	_asset("res://anchors/tuoyun.glb",Vector3(-5,0,5))
	_asset("res://anchors/solar.glb",Vector3(0,0,-6))
	_asset("res://anchors/processor.glb",Vector3(-6,0,-2))
	camera = Camera3D.new()
	camera.near = .05
	camera.far = 650
	add_child(camera)
	camera.current = true
	_ui()
	_set_view("normal")
	if not output.is_empty():
		_capture.call_deferred()

func _asset(path: String, at: Vector3) -> Node3D:
	var scene := load(path) as PackedScene
	if scene == null:
		_fail("asset import failed: "+path)
		return null
	var node := scene.instantiate() as Node3D
	add_child(node)
	node.position = at
	var box := AABB()
	var started := false
	var triangles := 0
	for mesh in node.find_children("*","MeshInstance3D",true,false):
		var points := _mesh_bounds(mesh,node)
		if not started:
			box = points
			started = true
		else:
			box = box.merge(points)
		for surface in range(mesh.mesh.get_surface_count()):
			var arrays: Array = mesh.mesh.surface_get_arrays(surface)
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			triangles += indices.size()/3 if not indices.is_empty() else arrays[Mesh.ARRAY_VERTEX].size()/3
	if not started or box.size.y <= 0 or triangles == 0:
		_fail("empty asset: "+path)
		return null
	sources.append({"file":path.trim_prefix("res://"),"sha256":FileAccess.get_sha256(path),"position_m":[at.x,at.y,at.z],"scale":1,"bounds_min_m":[box.position.x,box.position.y,box.position.z],"bounds_size_m":[box.size.x,box.size.y,box.size.z],"triangles":triangles})
	return node

func _mesh_bounds(mesh: MeshInstance3D, ancestor: Node3D) -> AABB:
	var relative: Transform3D = ancestor.global_transform.affine_inverse()*mesh.global_transform
	return relative * mesh.get_aabb()

func _ground_materials() -> void:
	var soil := ShaderMaterial.new()
	soil.shader = load("res://ground.gdshader")
	if hifi:
		# The baked sand is linear; source_color uniforms receive sRGB values.
		soil.set_shader_parameter("sand",Color(.27,.211,.162).linear_to_srgb())
		soil.set_shader_parameter("fine_sand",Color(.28,.222,.171).linear_to_srgb())
		soil.set_shader_parameter("dark_grain",Color(.25,.193,.145).linear_to_srgb())
	var changed := 0
	for mesh in ground.find_children("*","MeshInstance3D",true,false):
		for surface in range(mesh.mesh.get_surface_count()):
			var original: Material = mesh.mesh.surface_get_material(surface)
			var id := original.resource_name.to_lower() if original != null else str(mesh.name).to_lower()
			if "regolith" in id or "regolith" in str(mesh.name).to_lower():
				mesh.set_surface_override_material(surface,soil)
			else:
				var stone := ShaderMaterial.new()
				stone.shader = load("res://stone.gdshader")
				stone.set_shader_parameter("base",Color("635b51") if "clast" in id else Color("91806e"))
				mesh.set_surface_override_material(surface,stone)
			changed += 1
	if changed == 0:
		_fail("ground has no named material surfaces")

func _light() -> void:
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("84725f")
	sky_material.sky_horizon_color = Color("b4a18a")
	sky_material.ground_bottom_color = Color("635344")
	sky_material.ground_horizon_color = Color("b4a18a")
	sky_material.sun_angle_max = 1.0
	var sky := Sky.new()
	sky.sky_material = sky_material
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("d4cbc0")
	env.ambient_light_energy = .32
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_AGX
	env.tonemap_exposure = 1.0
	env.ssao_enabled = true
	env.ssao_radius = .65
	env.ssao_intensity = .8
	env.fog_enabled = true
	env.fog_light_color = Color("b4a18a")
	env.fog_density = .0015
	var world := WorldEnvironment.new()
	world.name = "WorldEnvironment"
	world.environment = env
	add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-38,-28,0)
	sun.light_color = Color("fffaf4")
	sun.light_energy = 1.2
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 110
	add_child(sun)

func _ui() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	note = Label.new()
	note.position = Vector2(28,24)
	note.add_theme_font_size_override("font_size",19)
	note.add_theme_color_override("font_color",Color("e6dfd3"))
	note.add_theme_color_override("font_shadow_color",Color(0,0,0,.9))
	note.add_theme_constant_override("shadow_offset_x",1)
	note.add_theme_constant_override("shadow_offset_y",1)
	layer.add_child(note)

func _set_view(id: String) -> void:
	view = id
	var pose: Array = VIEWS[id]
	camera.look_at_from_position(pose[0],pose[1],Vector3.UP)
	camera.fov = pose[2]
	_note()

func _note() -> void:
	note.text = ("余电 · 火星高保真看样 r2  |  " if hifi else "余电 · 火星环境看样 r1  |  ")+view+(" / LOW" if low else "")+"\nGale / Stimson 砂岩参考 · 原创场景 · 设备为现役尺度参照\n1 正常   2 近景   3 地平线   4 反向   L 低画质   H 隐藏说明   右键拖动   Esc 退出"
	if hifi:
		note.text += "   5 岩面细节   6 地表细节"

func _quality(value: bool) -> void:
	low = value
	get_viewport().scaling_3d_scale = .65 if low else 1.0
	get_viewport().msaa_3d = Viewport.MSAA_DISABLED if low else Viewport.MSAA_4X
	$WorldEnvironment.environment.ssao_enabled = not low
	_note()

func _unhandled_input(event: InputEvent) -> void:
	if not output.is_empty():
		return
	if event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_1: _set_view("normal")
			KEY_2: _set_view("near")
			KEY_3: _set_view("horizon")
			KEY_4: _set_view("reverse")
			KEY_5:
				if hifi: _set_view("detail")
			KEY_6:
				if hifi: _set_view("ground")
			KEY_L: _quality(not low)
			KEY_H: note.visible = not note.visible
			KEY_ESCAPE: get_tree().quit()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		dragging = event.pressed
	elif event is InputEventMouseMotion and dragging:
		var target: Vector3 = VIEWS[view][1]
		var delta := camera.position-target
		delta = delta.rotated(Vector3.UP,-event.relative.x*.005)
		camera.look_at_from_position(target+delta,target,Vector3.UP)

func _capture() -> void:
	DirAccess.make_dir_recursive_absolute(output)
	var frames := {}
	for i in range(40):
		await get_tree().process_frame
	var ids := ["normal","near","reverse","horizon","normal-low"]
	if hifi:
		ids.append_array(["detail","ground"])
	for id in ids:
		_quality(id == "normal-low")
		_set_view("normal" if low else id)
		for i in range(5):
			await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var path := output.path_join(id+".png")
		if get_viewport().get_texture().get_image().save_png(path) != OK:
			_fail("cannot save frame: "+id)
			return
		frames[id] = {"position_m":[camera.position.x,camera.position.y,camera.position.z],"target_m":[VIEWS[view][1].x,VIEWS[view][1].y,VIEWS[view][1].z],"fov":camera.fov,"render_scale":get_viewport().scaling_3d_scale,"msaa":get_viewport().msaa_3d,"ssao":$WorldEnvironment.environment.ssao_enabled,"mesh_lod_threshold":get_viewport().mesh_lod_threshold,"png_sha256":FileAccess.get_sha256(path)}
	var files := {}
	for path in ["res://lookdev.gd","res://ground.gdshader","res://stone.gdshader","res://project.godot"]:
		files[path.trim_prefix("res://")] = FileAccess.get_sha256(path)
	for row in sources:
		if FileAccess.get_sha256("res://"+row.file) != row.sha256:
			_fail("source changed during capture")
			return
	var record := {"scope":"independent original Mars art lookdev; no Main or simulation", "candidate":"hifi-r2" if hifi else "r1", "engine":Engine.get_version_info().string,"driver":RenderingServer.get_current_rendering_driver_name(),"viewport_px":[get_viewport().size.x,get_viewport().size.y],"assets":sources,"sources":files,"frames":frames,"source_unchanged":true,"owner_visual_acceptance":"NOT_RUN","scene_performance":"NOT_RUN"}
	var file := FileAccess.open(output.path_join("capture.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify(record,"\t")+"\n")
	print("MARS_LOOKDEV_CAPTURE_OK frames=",frames.size()," sources_unchanged=true")
	get_tree().quit()

func _fail(message: String) -> void:
	push_error(message)
	get_tree().quit(1)
