extends SceneTree

var output: String
var camera: Camera3D
var plane: MeshInstance3D
var material: ShaderMaterial
var checks := 0
var failures := 0
var samples := {}

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	output = OS.get_environment("YUDIAN_TERRAIN_SURFACE_CHECK")
	if output.is_empty() or DirAccess.dir_exists_absolute(output):
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output)
	root.size = Vector2i(640,640)
	var world := Node3D.new()
	root.add_child(world)
	camera = Camera3D.new()
	world.add_child(camera)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 8.0
	camera.current = true
	plane = MeshInstance3D.new()
	plane.mesh = PlaneMesh.new()
	plane.mesh.size = Vector2(6,6)
	world.add_child(plane)
	var shader := Shader.new()
	var home: String = get_script().resource_path.get_base_dir().get_base_dir().get_base_dir().get_base_dir()
	shader.code = FileAccess.get_file_as_string(home.path_join("art/d1/surface.gdshader")).replace("render_mode cull_back;","render_mode cull_back, unshaded;")
	material = ShaderMaterial.new()
	material.shader = shader
	plane.material_override = material
	var flat := await _frame("flat",Vector3(-8,8,8))
	plane.rotation.z = PI/3.0
	var slope_a := await _frame("slope-a",Vector3(-8,8,8))
	var slope_b := await _frame("slope-b",Vector3(-8,8,-8))
	_check(_at(slope_a,Vector3.ZERO).distance_to(_at(flat,Vector3.ZERO)) > .03,"slope shading differs at same world coordinate")
	_check(_at(slope_a,Vector3.ZERO).distance_to(_at(slope_b,Vector3.ZERO)) < .015,"world slope color is camera invariant")
	plane.rotation = Vector3.ZERO
	var mask := Image.create(65,65,false,Image.FORMAT_R8)
	mask.fill(Color.BLACK)
	mask.set_pixel(32,32,Color.WHITE)
	material.set_shader_parameter("committed_mask",ImageTexture.create_from_image(mask))
	material.set_shader_parameter("use_committed_mask",true)
	var edge := await _frame("mask-boundary",Vector3(-8,8,8))
	var inside := _at(edge,Vector3(.3,0,0))
	var outside := _at(edge,Vector3(.7,0,0))
	_check(inside.distance_to(outside) > .07,"mask edge distinguishes committed and untouched side")
	material.set_shader_parameter("stage",1)
	var working := await _frame("working-boundary",Vector3(-8,8,8))
	_check(inside.distance_to(_at(working,Vector3(.3,0,0))) < .015,"work cannot recolor committed side")
	_check(outside.distance_to(_at(working,Vector3(.7,0,0))) > .07,"work changes only untouched side inside circle")
	root.scaling_3d_scale = .75
	var low := await _frame("low-boundary",Vector3(-8,8,8))
	_check(_at(low,Vector3(.3,0,0)).distance_to(_at(low,Vector3(.7,0,0))) > .07,"low resolution retains mask boundary")
	var file := FileAccess.open(output.path_join("surface-check.json"),FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks":checks,"failures":failures,"scope":"synthetic unshaded slope and boundary checks; no gameplay evidence","engine":Engine.get_version_info().string,"driver":RenderingServer.get_current_rendering_driver_name(),"samples":samples},"\t")+"\n")
	print("TERRAIN_SURFACE_CHECK %d/%d" % [checks-failures,checks])
	quit(1 if failures else 0)

func _frame(label: String, position: Vector3) -> Image:
	camera.look_at_from_position(position,Vector3.ZERO)
	await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	image.save_png(output.path_join(label+".png"))
	var color := _at(image,Vector3.ZERO)
	samples[label] = [color.x,color.y,color.z]
	return image

func _at(image: Image, point: Vector3) -> Vector3:
	var pixel := camera.unproject_position(point).round()
	var color := image.get_pixelv(Vector2i(pixel))
	return Vector3(color.r,color.g,color.b)

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		printerr("TERRAIN_SURFACE_FAIL "+label)
